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

    private const int BatchSize = 200;
    private const int DelayBetweenBatchesMs = 50;
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
        var hypixelBatch = new List<HypixelAuction>(BatchSize);
        var totalSaved = 0;
        var totalSkipped = 0;
        var totalRetries = 0;

        try
        {
            await foreach (var hypixelAuction in _auctionChannel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    var auction = _nbtParser.ParseAuction(hypixelAuction);
                    batch.Add(auction);
                    hypixelBatch.Add(hypixelAuction);

                    if (batch.Count >= BatchSize)
                    {
                        var (saved, skipped, retries) = await FlushBatchWithRetry(batch, hypixelBatch, stoppingToken);
                        totalSaved += saved;
                        totalSkipped += skipped;
                        totalRetries += retries;
                        batch.Clear();
                        hypixelBatch.Clear();

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

        if (batch.Count > 0)
        {
            await FlushBatchWithRetry(batch, hypixelBatch, CancellationToken.None);
        }
    }

    private async Task<(int saved, int skipped, int retries)> FlushBatchWithRetry(
        List<Auction> batch,
        List<HypixelAuction> hypixelBatch,
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
                    delay *= 2;
                    retryCount++;
                }

                var (saved, skipped) = await FlushBatch(batch, hypixelBatch, stoppingToken);
                return (saved, skipped, retryCount);
            }
            catch (Exception ex) when (ex is not OperationCanceledException && attempt < MaxRetries)
            {
                _logger.LogWarning(ex, "Failed to save batch (attempt {Attempt}/{Max}), will retry...",
                    attempt + 1, MaxRetries);
            }
            catch (Exception ex) when (ex is not OperationCanceledException && attempt == MaxRetries)
            {
                _logger.LogError(ex, "All retries exhausted for batch of {Count}. Attempting batch split...",
                    batch.Count);

                if (batch.Count > 1)
                {
                    return await SplitAndRetry(batch, hypixelBatch, stoppingToken, retryCount);
                }

                _logger.LogError("Cannot split single-item batch. Auction lost: {Uuid}",
                    batch.FirstOrDefault()?.Uuid);
                return (0, 0, retryCount);
            }
        }

        return (0, 0, retryCount);
    }

    private async Task<(int saved, int skipped, int retries)> SplitAndRetry(
        List<Auction> batch,
        List<HypixelAuction> hypixelBatch,
        CancellationToken stoppingToken,
        int currentRetries)
    {
        var midpoint = batch.Count / 2;
        var batch1 = batch.Take(midpoint).ToList();
        var batch2 = batch.Skip(midpoint).ToList();
        var hBatch1 = hypixelBatch.Take(midpoint).ToList();
        var hBatch2 = hypixelBatch.Skip(midpoint).ToList();

        _logger.LogInformation("Splitting batch of {Total} into {Size1} + {Size2}",
            batch.Count, batch1.Count, batch2.Count);

        var (saved1, skipped1, retries1) = await FlushBatchWithRetry(batch1, hBatch1, stoppingToken);
        var (saved2, skipped2, retries2) = await FlushBatchWithRetry(batch2, hBatch2, stoppingToken);

        return (saved1 + saved2, skipped1 + skipped2, currentRetries + retries1 + retries2);
    }

    private async Task<(int saved, int skipped)> FlushBatch(
        List<Auction> batch,
        List<HypixelAuction> hypixelBatch,
        CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var itemDetailsService = scope.ServiceProvider.GetRequiredService<ItemDetailsService>();

        var incoming = batch
            .Zip(hypixelBatch, (auction, hypixel) => new IncomingAuction(auction, hypixel))
            .ToList();

        var uuids = incoming.Select(x => x.Auction.Uuid).ToList();
        var existingAuctions = await dbContext.Auctions
            .Where(a => uuids.Contains(a.Uuid))
            .Include(a => a.Bids)
            .ToListAsync(stoppingToken);

        var existingByUuid = existingAuctions.ToDictionary(a => a.Uuid, StringComparer.Ordinal);
        var refreshedExisting = 0;

        foreach (var entry in incoming.Where(x => existingByUuid.ContainsKey(x.Auction.Uuid)))
        {
            var existing = existingByUuid[entry.Auction.Uuid];
            if (existing.Status != AuctionStatus.ACTIVE)
                continue;

            var changed = ApplyAuctionRefresh(existing, entry.Auction);
            var newBids = BuildMissingBidRecords(existing, entry.Hypixel);
            if (newBids.Count > 0)
            {
                dbContext.BidRecords.AddRange(newBids);
                changed = true;
            }

            if (changed)
                refreshedExisting++;
        }

        if (refreshedExisting > 0)
        {
            await dbContext.SaveChangesAsync(stoppingToken);
        }

        var newEntries = incoming
            .Where(x => !existingByUuid.ContainsKey(x.Auction.Uuid))
            .ToList();

        if (newEntries.Count == 0)
        {
            if (refreshedExisting > 0)
            {
                _logger.LogInformation("Refreshed {UpdatedCount} existing auctions (no new auctions in batch)", refreshedExisting);
            }

            return (0, existingAuctions.Count);
        }

        var newAuctions = newEntries.Select(x => x.Auction).ToList();
        foreach (var auction in newAuctions)
        {
            if (string.IsNullOrEmpty(auction.RawNbtBytes))
                continue;

            var extraTag = _nbtParser.GetExtraTagFromBytes(auction.RawNbtBytes);
            if (extraTag == null)
                continue;

            var nbtData = _nbtParser.CreateNbtData(extraTag);
            if (nbtData != null)
                auction.NbtData = nbtData;
        }

        dbContext.Auctions.AddRange(newAuctions);
        await dbContext.SaveChangesAsync(stoppingToken);

        var bidBatch = new List<BidRecord>();
        foreach (var entry in newEntries)
        {
            if (entry.Hypixel.Bids == null || entry.Hypixel.Bids.Count == 0)
                continue;

            foreach (var hypixelBid in entry.Hypixel.Bids)
            {
                bidBatch.Add(new BidRecord
                {
                    AuctionId = entry.Auction.Id,
                    BidderId = hypixelBid.Bidder.Replace("-", ""),
                    Amount = hypixelBid.Amount,
                    Timestamp = hypixelBid.Timestamp
                });
            }
        }

        if (bidBatch.Count > 0)
        {
            dbContext.BidRecords.AddRange(bidBatch);
            await dbContext.SaveChangesAsync(stoppingToken);
        }

        var lookupBatch = new List<NBTLookup>();
        foreach (var auction in newAuctions)
        {
            if (string.IsNullOrEmpty(auction.RawNbtBytes))
                continue;

            var extraTag = _nbtParser.GetExtraTagFromBytes(auction.RawNbtBytes);
            if (extraTag != null)
            {
                var lookups = await _nbtParser.CreateLookupAsync(extraTag, auction.Id);
                lookupBatch.AddRange(lookups);
            }
        }

        if (lookupBatch.Count > 0)
        {
            dbContext.NBTLookups.AddRange(lookupBatch);
            await dbContext.SaveChangesAsync(stoppingToken);
        }

        foreach (var auction in newAuctions)
        {
            try
            {
                await itemDetailsService.GetOrCreateItemDetails(
                    auction.Tag,
                    auction.ItemName,
                    auction.Tier,
                    auction.Category,
                    null
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update ItemDetails for {Tag}", auction.Tag);
            }
        }

        _logger.LogInformation("Saved {NewCount} new auctions with {NbtCount} NBT data, {LookupCount} lookups, {BidCount} bids; refreshed {UpdatedCount} existing auctions",
            newAuctions.Count,
            newAuctions.Count(a => a.NbtData != null),
            lookupBatch.Count,
            bidBatch.Count,
            refreshedExisting);

        return (newAuctions.Count, existingAuctions.Count);
    }

    private static bool ApplyAuctionRefresh(Auction existing, Auction incoming)
    {
        var changed = false;

        changed |= UpdateIfDifferent(existing.ItemName, incoming.ItemName, value => existing.ItemName = value);
        changed |= UpdateIfDifferent(existing.StartingBid, incoming.StartingBid, value => existing.StartingBid = value);
        changed |= UpdateIfDifferent(existing.HighestBidAmount, incoming.HighestBidAmount, value => existing.HighestBidAmount = value);
        changed |= UpdateIfDifferent(existing.Bin, incoming.Bin, value => existing.Bin = value);
        changed |= UpdateIfDifferent(existing.Start, incoming.Start, value => existing.Start = value);
        changed |= UpdateIfDifferent(existing.End, incoming.End, value => existing.End = value);
        changed |= UpdateIfDifferent(existing.AuctioneerId, incoming.AuctioneerId, value => existing.AuctioneerId = value);
        changed |= UpdateIfDifferent(existing.Tag, incoming.Tag, value => existing.Tag = value);
        changed |= UpdateIfDifferent(existing.Count, incoming.Count, value => existing.Count = value);
        changed |= UpdateIfDifferent(existing.Tier, incoming.Tier, value => existing.Tier = value);
        changed |= UpdateIfDifferent(existing.Category, incoming.Category, value => existing.Category = value);
        changed |= UpdateIfDifferent(existing.Reforge, incoming.Reforge, value => existing.Reforge = value);
        changed |= UpdateIfDifferent(existing.AnvilUses, incoming.AnvilUses, value => existing.AnvilUses = value);
        changed |= UpdateIfDifferent(existing.ItemCreatedAt, incoming.ItemCreatedAt, value => existing.ItemCreatedAt = value);
        changed |= UpdateIfDifferent(existing.ItemUid, incoming.ItemUid, value => existing.ItemUid = value);
        changed |= UpdateIfDifferent(existing.Texture, incoming.Texture, value => existing.Texture = value);
        changed |= UpdateIfDifferent(existing.FlatenedNBTJson, incoming.FlatenedNBTJson, value => existing.FlatenedNBTJson = value);

        if (changed)
        {
            existing.FetchedAt = DateTime.UtcNow;
        }

        return changed;
    }

    private static bool UpdateIfDifferent<T>(T currentValue, T newValue, Action<T> apply)
    {
        if (EqualityComparer<T>.Default.Equals(currentValue, newValue))
            return false;

        apply(newValue);
        return true;
    }

    private static List<BidRecord> BuildMissingBidRecords(Auction auction, HypixelAuction hypixelAuction)
    {
        if (hypixelAuction.Bids == null || hypixelAuction.Bids.Count == 0)
            return new List<BidRecord>();

        var existingBidKeys = auction.Bids
            .Select(b => $"{b.BidderId}|{b.Amount}|{b.Timestamp.Ticks}")
            .ToHashSet(StringComparer.Ordinal);

        var result = new List<BidRecord>();
        foreach (var hypixelBid in hypixelAuction.Bids)
        {
            var bidderId = hypixelBid.Bidder.Replace("-", "");
            var bidKey = $"{bidderId}|{hypixelBid.Amount}|{hypixelBid.Timestamp.Ticks}";
            if (!existingBidKeys.Add(bidKey))
                continue;

            result.Add(new BidRecord
            {
                AuctionId = auction.Id,
                BidderId = bidderId,
                Amount = hypixelBid.Amount,
                Timestamp = hypixelBid.Timestamp
            });
        }

        return result;
    }

    private sealed record IncomingAuction(Auction Auction, HypixelAuction Hypixel);
}
