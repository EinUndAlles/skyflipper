using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services;

/// <summary>
/// Background service that processes auctions and saves to database.
/// Features: Large batch processing (200), retry logic with exponential backoff, batch splitting on failures.
/// </summary>
public class FlipperService : BackgroundService
{
    private readonly Channel<HypixelAuction> _auctionChannel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NbtParserService _nbtParser;
    private readonly ILogger<FlipperService> _logger;

    private const int BatchSize = 200; // Increased from 50 for better throughput
    private const int DelayBetweenBatchesMs = 50; // Reduced delay with larger batches
    private const int MaxRetries = 3;
    private const int InitialRetryDelayMs = 1000;

    public FlipperService(
        Channel<HypixelAuction> auctionChannel,
        IServiceScopeFactory scopeFactory,
        NbtParserService nbtParser,
        ILogger<FlipperService> logger,
        IConfiguration configuration)
    {
        _auctionChannel = auctionChannel;
        _scopeFactory = scopeFactory;
        _nbtParser = nbtParser;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FlipperService starting (optimized: batch=200, retry=enabled)...");

        var batch = new List<Auction>(BatchSize);
        var totalSaved = 0;
        var totalSkipped = 0;
        var totalRetries = 0;

        try
        {
            await foreach (var hypixelAuction in _auctionChannel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    // Parse the auction
                    var auction = _nbtParser.ParseAuction(hypixelAuction);
                    batch.Add(auction);

                    // Flush when batch is full
                    if (batch.Count >= BatchSize)
                    {
                        var (saved, skipped, retries) = await FlushBatchWithRetry(batch, stoppingToken);
                        totalSaved += saved;
                        totalSkipped += skipped;
                        totalRetries += retries;
                        batch.Clear();

                        // Rate limit - allow DB breathing room
                        await Task.Delay(DelayBetweenBatchesMs, stoppingToken);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogDebug(ex, "Error parsing auction");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("FlipperService stopping... Saved {Saved} auctions, skipped {Skipped}, retries {Retries}", 
                totalSaved, totalSkipped, totalRetries);
        }

        // Flush remaining
        if (batch.Count > 0)
        {
            await FlushBatchWithRetry(batch, CancellationToken.None);
        }
    }

    /// <summary>
    /// Flush batch with exponential backoff retry logic.
    /// If a full batch fails, splits it in half and retries each half separately.
    /// </summary>
    private async Task<(int saved, int skipped, int retries)> FlushBatchWithRetry(
        List<Auction> batch, 
        CancellationToken stoppingToken)
    {
        var retryCount = 0;
        var delay = InitialRetryDelayMs;

        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    _logger.LogWarning("Retry attempt {Attempt}/{Max} for batch of {Count} auctions", 
                        attempt, MaxRetries, batch.Count);
                    await Task.Delay(delay, stoppingToken);
                    delay *= 2; // Exponential backoff
                    retryCount++;
                }

                var (saved, skipped) = await FlushBatch(batch, stoppingToken);
                return (saved, skipped, retryCount);
            }
            catch (Exception ex) when (ex is not OperationCanceledException && attempt < MaxRetries)
            {
                _logger.LogWarning(ex, "Failed to save batch (attempt {Attempt}/{Max}), will retry...", 
                    attempt + 1, MaxRetries);
            }
            catch (Exception ex) when (ex is not OperationCanceledException && attempt == MaxRetries)
            {
                // Final retry failed - try batch splitting
                _logger.LogError(ex, "All retries exhausted for batch of {Count}. Attempting batch split...", 
                    batch.Count);
                
                if (batch.Count > 1)
                {
                    return await SplitAndRetry(batch, stoppingToken, retryCount);
                }
                else
                {
                    _logger.LogError("Cannot split single-item batch. Auction lost: {Uuid}", 
                        batch.FirstOrDefault()?.Uuid);
                    return (0, 0, retryCount);
                }
            }
        }

        return (0, 0, retryCount);
    }

    /// <summary>
    /// Splits batch in half and attempts to save each half separately.
    /// Recursively splits if necessary.
    /// </summary>
    private async Task<(int saved, int skipped, int retries)> SplitAndRetry(
        List<Auction> batch,
        CancellationToken stoppingToken,
        int currentRetries)
    {
        var midpoint = batch.Count / 2;
        var batch1 = batch.Take(midpoint).ToList();
        var batch2 = batch.Skip(midpoint).ToList();

        _logger.LogInformation("Splitting batch of {Total} into {Size1} + {Size2}", 
            batch.Count, batch1.Count, batch2.Count);

        var (saved1, skipped1, retries1) = await FlushBatchWithRetry(batch1, stoppingToken);
        var (saved2, skipped2, retries2) = await FlushBatchWithRetry(batch2, stoppingToken);

        return (saved1 + saved2, skipped1 + skipped2, currentRetries + retries1 + retries2);
    }

    /// <summary>
    /// Core batch save logic with NBT data and lookups.
    /// </summary>
    private async Task<(int saved, int skipped)> FlushBatch(List<Auction> batch, CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Get existing UUIDs in one query
        var uuids = batch.Select(a => a.Uuid).ToList();
        var existingUuids = (await dbContext.Auctions
            .Where(a => uuids.Contains(a.Uuid))
            .Select(a => a.Uuid)
            .AsNoTracking()
            .ToListAsync(stoppingToken))
            .ToHashSet();

        // Only add new auctions
        var newAuctions = batch.Where(a => !existingUuids.Contains(a.Uuid)).ToList();
        var skipped = batch.Count - newAuctions.Count;

        if (newAuctions.Count > 0)
        {
            // Step 1: Create NbtData for each auction (before first save)
            foreach (var auction in newAuctions)
            {
                if (string.IsNullOrEmpty(auction.RawNbtBytes)) continue;

                var extraTag = _nbtParser.GetExtraTagFromBytes(auction.RawNbtBytes);
                if (extraTag != null)
                {
                    var nbtData = _nbtParser.CreateNbtData(extraTag);
                    if (nbtData != null)
                    {
                        auction.NbtData = nbtData;
                    }
                }
            }

            // Step 2: Save auctions with NbtData
            dbContext.Auctions.AddRange(newAuctions);
            await dbContext.SaveChangesAsync(stoppingToken);

            // Step 3: Create NBTLookups (requires auction IDs from database)
            var lookupBatch = new List<NBTLookup>();
            foreach (var auction in newAuctions)
            {
                if (string.IsNullOrEmpty(auction.RawNbtBytes)) continue;

                var extraTag = _nbtParser.GetExtraTagFromBytes(auction.RawNbtBytes);
                if (extraTag != null)
                {
                    var lookups = _nbtParser.CreateLookup(extraTag, auction.Id);
                    lookupBatch.AddRange(lookups);
                }
            }

            // Step 4: Save all NBTLookups in batch
            if (lookupBatch.Count > 0)
            {
                dbContext.NBTLookups.AddRange(lookupBatch);
                await dbContext.SaveChangesAsync(stoppingToken);
                
                _logger.LogInformation("✅ Saved {Count} auctions with {NbtCount} NBT data, {LookupCount} lookups (skipped {Skipped})", 
                    newAuctions.Count, 
                    newAuctions.Count(a => a.NbtData != null),
                    lookupBatch.Count,
                    skipped);
            }
            else
            {
                _logger.LogInformation("✅ Saved {Count} auctions (skipped {Skipped} existing)", 
                    newAuctions.Count, skipped);
            }
        }

        return (newAuctions.Count, skipped);
    }
}
