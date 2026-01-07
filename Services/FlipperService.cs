using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services;

/// <summary>
/// Background service that processes auctions and saves to database.
/// Uses throttled batch processing to avoid overwhelming PostgreSQL.
/// </summary>
public class FlipperService : BackgroundService
{
    private readonly Channel<HypixelAuction> _auctionChannel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NbtParserService _nbtParser;
    private readonly ILogger<FlipperService> _logger;

    private const int BatchSize = 50;
    private const int DelayBetweenBatchesMs = 100; // Rate limit

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
        _logger.LogInformation("FlipperService starting (throttled mode)...");

        var batch = new List<Auction>(BatchSize);
        var totalSaved = 0;
        var totalSkipped = 0;

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
                        var (saved, skipped) = await FlushBatch(batch, stoppingToken);
                        totalSaved += saved;
                        totalSkipped += skipped;
                        batch.Clear();

                        // Rate limit - give the DB and web server breathing room
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
            _logger.LogInformation("FlipperService stopping... Saved {Saved} auctions, skipped {Skipped} duplicates", 
                totalSaved, totalSkipped);
        }

        // Flush remaining
        if (batch.Count > 0)
        {
            await FlushBatch(batch, CancellationToken.None);
        }
    }

    private async Task<(int saved, int skipped)> FlushBatch(List<Auction> batch, CancellationToken stoppingToken)
    {
        try
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
                // Don't save enchantments for now to simplify - they cause FK issues
                foreach (var auction in newAuctions)
                {
                    auction.Enchantments = new List<Enchantment>();
                }

                dbContext.Auctions.AddRange(newAuctions);
                await dbContext.SaveChangesAsync(stoppingToken);
                
                _logger.LogInformation("✅ Saved {Count} auctions (skipped {Skipped} existing)", 
                    newAuctions.Count, skipped);
            }

            return (newAuctions.Count, skipped);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to save batch of {Count} auctions", batch.Count);
            return (0, 0);
        }
    }
}
