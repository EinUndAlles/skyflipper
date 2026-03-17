using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services;

/// <summary>
/// Detects BIN flips using Coflnet-style reference auction selection and weighted median logic.
/// </summary>
public class FlipDetectionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FlipDetectionService> _logger;
    private readonly ReferenceAuctionService _referenceAuctionService;
    private readonly CacheKeyService _cacheKeyService;

    private const int DetectionIntervalSeconds = 30;
    private const double HitCountDecayFactor = 1.05;
    private const int MaxHitCount = 20;
    private const int MaxFlipsToStore = 200;
    private static readonly TimeSpan HitCountTtl = TimeSpan.FromHours(2);

    public FlipDetectionService(
        IServiceScopeFactory scopeFactory,
        ILogger<FlipDetectionService> logger,
        ReferenceAuctionService referenceAuctionService,
        CacheKeyService cacheKeyService)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _referenceAuctionService = referenceAuctionService;
        _cacheKeyService = cacheKeyService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FlipDetectionService starting (reference-auction mode)...");
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DetectFlips(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error detecting flips");
            }

            await Task.Delay(TimeSpan.FromSeconds(DetectionIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("FlipDetectionService stopped");
    }

    private async Task DetectFlips(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;

        var hitCountCutoff = now - HitCountTtl;
        var hitCounts = await dbContext.FlipHitCounts
            .AsNoTracking()
            .Where(h => h.LastHitAt > hitCountCutoff)
            .ToDictionaryAsync(h => h.CacheKey, h => h.HitCount, stoppingToken);

        var activeAuctions = await dbContext.Auctions
            .Where(a => a.Bin &&
                        a.Status == AuctionStatus.ACTIVE &&
                        a.End > now)
            .Include(a => a.Enchantments)
            .Include(a => a.Bids)
            .Include(a => a.NBTLookups)
                .ThenInclude(n => n.NBTKey)
            .Include(a => a.NBTLookups)
                .ThenInclude(n => n.NBTValue)
            .ToListAsync(stoppingToken);

        var flips = new List<FlipOpportunity>();
        var hitUpdates = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var auction in activeAuctions)
        {
            var probeKey = _cacheKeyService.GeneratePriceCacheKey(auction);
            var currentHitCount = hitCounts.TryGetValue(probeKey, out var existing)
                ? Math.Min(existing, MaxHitCount)
                : 0;

            var valuation = await _referenceAuctionService.EvaluateBinAuctionAsync(auction, currentHitCount, stoppingToken);
            if (valuation == null)
                continue;

            var currentPrice = auction.HighestBidAmount > 0 ? auction.HighestBidAmount : auction.StartingBid;

            flips.Add(new FlipOpportunity
            {
                AuctionUuid = auction.Uuid,
                ItemTag = auction.Tag,
                ItemName = auction.ItemName,
                CurrentPrice = currentPrice,
                MedianPrice = valuation.TargetPrice,
                EstimatedProfit = valuation.EstimatedProfit,
                ProfitMarginPercent = valuation.ProfitMarginPercent,
                AuctionEnd = auction.End,
                DataSource = valuation.DataSource,
                ValueBreakdown = valuation.ValueBreakdown
            });

            if (!hitUpdates.ContainsKey(valuation.CacheKey))
                hitUpdates[valuation.CacheKey] = 0;
            hitUpdates[valuation.CacheKey]++;
        }

        flips = flips
            .OrderByDescending(f => f.ProfitMarginPercent)
            .Take(MaxFlipsToStore)
            .ToList();

        await dbContext.FlipOpportunities.ExecuteDeleteAsync(stoppingToken);

        if (flips.Count > 0)
        {
            dbContext.FlipOpportunities.AddRange(flips);
            await dbContext.SaveChangesAsync(stoppingToken);

            var topFlip = flips[0];
            _logger.LogInformation("Detected {Count} BIN flips. Top: {Item} ({Margin:F1}% / {Profit:N0})",
                flips.Count,
                topFlip.ItemName,
                topFlip.ProfitMarginPercent,
                topFlip.EstimatedProfit);
        }

        if (hitUpdates.Count > 0)
            await BatchUpdateHitCounts(dbContext, hitUpdates, stoppingToken);

        if (Random.Shared.Next(10) == 0)
            await CleanupExpiredHitCounts(dbContext, stoppingToken);
    }

    private async Task BatchUpdateHitCounts(AppDbContext dbContext, Dictionary<string, int> hitUpdates, CancellationToken stoppingToken)
    {
        var cacheKeys = hitUpdates.Keys.ToList();
        var existingRecords = await dbContext.FlipHitCounts
            .Where(h => cacheKeys.Contains(h.CacheKey))
            .ToDictionaryAsync(h => h.CacheKey, stoppingToken);

        var now = DateTime.UtcNow;
        var newRecords = new List<FlipHitCount>();

        foreach (var (cacheKey, incrementBy) in hitUpdates)
        {
            if (existingRecords.TryGetValue(cacheKey, out var existing))
            {
                existing.HitCount += incrementBy;
                existing.LastHitAt = now;
            }
            else
            {
                newRecords.Add(new FlipHitCount
                {
                    CacheKey = cacheKey,
                    HitCount = incrementBy,
                    LastHitAt = now
                });
            }
        }

        if (newRecords.Count > 0)
            dbContext.FlipHitCounts.AddRange(newRecords);

        await dbContext.SaveChangesAsync(stoppingToken);
    }

    private async Task CleanupExpiredHitCounts(AppDbContext dbContext, CancellationToken stoppingToken)
    {
        var cutoff = DateTime.UtcNow - HitCountTtl;
        await dbContext.FlipHitCounts
            .Where(h => h.LastHitAt < cutoff)
            .ExecuteDeleteAsync(stoppingToken);
    }
}
