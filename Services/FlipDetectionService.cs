using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services;

/// <summary>
/// Background service that detects flip opportunities by comparing
/// current BIN prices against historical median prices.
/// Uses NBT-aware cache keys and hit count decay for accurate detection.
/// 
/// Performance optimizations:
/// - Pre-loads all hit counts into memory at start of each detection cycle
/// - Batches all hit count updates at the end of detection
/// - Uses in-memory caching for price lookups
/// </summary>
public class FlipDetectionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FlipDetectionService> _logger;
    private readonly CacheKeyService _cacheKeyService;
    private readonly ComponentValueService _componentValueService;

    private const int DETECTION_INTERVAL_SECONDS = 30; // Check every 30 seconds
    private const double MIN_PROFIT_MARGIN = 0.05; // 5% minimum profit
    private const int MIN_SALES_THRESHOLD = 7; // Reference project requires > 7 references
    private const int PRICE_HISTORY_DAYS = 7; // Look back 7 days for median
    private const int MAX_FLIPS_TO_STORE = 200; // Limit stored flips
    private const double HIT_COUNT_DECAY_FACTOR = 1.05; // 5% decay per hit
    private const int MAX_HIT_COUNT = 20; // Cap decay at 20 hits
    private const long MIN_ITEM_VALUE = 10000; // 10k minimum value
    private static readonly TimeSpan HIT_COUNT_TTL = TimeSpan.FromHours(2); // Reference uses 2 hour TTL

    public FlipDetectionService(
        IServiceScopeFactory scopeFactory,
        ILogger<FlipDetectionService> logger,
        CacheKeyService cacheKeyService,
        ComponentValueService componentValueService)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _cacheKeyService = cacheKeyService;
        _componentValueService = componentValueService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FlipDetectionService starting (threshold: {Threshold}%)...", 
            MIN_PROFIT_MARGIN * 100);

        // Wait for initial data collection
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

            await Task.Delay(TimeSpan.FromSeconds(DETECTION_INTERVAL_SECONDS), stoppingToken);
        }

        _logger.LogInformation("FlipDetectionService stopped");
    }

    private async Task DetectFlips(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;

        // OPTIMIZATION: Pre-load all hit counts into memory dictionary
        // Apply TTL: only include entries that were hit within the last 2 hours (reference behavior)
        var hitCountCutoff = now - HIT_COUNT_TTL;
        var hitCountsDict = await dbContext.FlipHitCounts
            .AsNoTracking()
            .Where(h => h.LastHitAt > hitCountCutoff)  // TTL filter
            .ToDictionaryAsync(h => h.CacheKey, h => h.HitCount, stoppingToken);

        // Step 1: Get 15-minute prices (last 2 hours) for high-volume items - most responsive
        var fifteenMinPrices = await dbContext.AveragePrices
            .Where(p => p.Granularity == PriceGranularity.FifteenMinute &&
                       p.Timestamp > now.AddHours(-2))
            .GroupBy(p => p.CacheKey)
            .Select(g => new
            {
                CacheKey = g.Key,
                Medians = g.OrderByDescending(p => p.Timestamp).Select(p => p.Median).ToList(),
                Volume = g.Sum(p => p.Volume),
                BinCount = g.Sum(p => p.BinCount),
                DataSource = "15min (2h)"
            })
            .ToDictionaryAsync(p => p.CacheKey, stoppingToken);

        // Step 2: Get hourly prices from last 24 hours
        var fifteenMinCacheKeys = fifteenMinPrices.Keys.ToList();
        var hourlyPrices = await dbContext.AveragePrices
            .Where(p => p.Granularity == PriceGranularity.Hourly &&
                       p.Timestamp > now.AddHours(-24) &&
                       p.Volume >= MIN_SALES_THRESHOLD &&
                       !fifteenMinCacheKeys.Contains(p.CacheKey))
            .GroupBy(p => p.CacheKey)
            .Select(g => new
            {
                CacheKey = g.Key,
                Medians = g.OrderByDescending(p => p.Timestamp).Select(p => p.Median).ToList(),
                Volume = g.Sum(p => p.Volume),
                BinCount = g.Sum(p => p.BinCount),
                DataSource = "Hourly (24h)"
            })
            .ToDictionaryAsync(p => p.CacheKey, stoppingToken);

        // Step 3: Get daily prices for cache keys not in hourly or 15-min
        var existingCacheKeys = fifteenMinCacheKeys.Concat(hourlyPrices.Keys).ToList();
        var dailyPrices = await dbContext.AveragePrices
            .Where(p => p.Granularity == PriceGranularity.Daily &&
                       p.Timestamp > now.AddDays(-PRICE_HISTORY_DAYS) &&
                       p.Volume >= MIN_SALES_THRESHOLD &&
                       !existingCacheKeys.Contains(p.CacheKey))
            .GroupBy(p => p.CacheKey)
            .Select(g => new
            {
                CacheKey = g.Key,
                Medians = g.OrderByDescending(p => p.Timestamp).Select(p => p.Median).ToList(),
                Volume = g.Sum(p => p.Volume),
                BinCount = g.Sum(p => p.BinCount),
                DataSource = "Daily (7d)"
            })
            .ToDictionaryAsync(p => p.CacheKey, stoppingToken);

        // Step 4: Merge prices (15-min > hourly > daily)
        var allPrices = new Dictionary<string, (double SafeMedian, int Volume, int BinCount, string DataSource)>();

        foreach (var kvp in fifteenMinPrices)
        {
            var safeMedian = CalculateWeightedMedian(kvp.Value.Medians);
            allPrices[kvp.Key] = (safeMedian, kvp.Value.Volume, kvp.Value.BinCount, kvp.Value.DataSource);
        }

        foreach (var kvp in hourlyPrices)
        {
            if (!allPrices.ContainsKey(kvp.Key))
            {
                var safeMedian = CalculateWeightedMedian(kvp.Value.Medians);
                allPrices[kvp.Key] = (safeMedian, kvp.Value.Volume, kvp.Value.BinCount, kvp.Value.DataSource);
            }
        }

        foreach (var kvp in dailyPrices)
        {
            if (!allPrices.ContainsKey(kvp.Key))
            {
                var safeMedian = CalculateWeightedMedian(kvp.Value.Medians);
                allPrices[kvp.Key] = (safeMedian, kvp.Value.Volume, kvp.Value.BinCount, kvp.Value.DataSource);
            }
        }

        // Step 4: Get active BIN auctions
        var activeAuctions = await dbContext.Auctions
            .Where(a => a.Bin &&
                       a.Status == AuctionStatus.ACTIVE &&
                       a.End > now)
            .Include(a => a.Enchantments)
            .Include(a => a.NBTLookups)
                .ThenInclude(nbt => nbt.NBTKey)
            .ToListAsync(stoppingToken);

        var flips = new List<FlipOpportunity>();
        
        // OPTIMIZATION: Track hit updates in memory, batch write at end
        var hitUpdates = new Dictionary<string, int>();

        // Step 5: Calculate flips using NBT-based matching
        // Reference: FlippingEngine.cs - match by exact cache key, add gem value back
        // Stored medians have gem value SUBTRACTED (PriceAggregationService line 129-131)
        // So we add gem value back to get effective median
        foreach (var auction in activeAuctions)
        {
            var cacheKey = _cacheKeyService.GeneratePriceCacheKey(auction);
            
            // Only proceed if we have exact cache key match with sufficient volume
            if (!allPrices.TryGetValue(cacheKey, out var priceData) || priceData.Volume < MIN_SALES_THRESHOLD)
            {
                continue; // No pricing data - skip
            }

            // Base median from stored prices (gems already subtracted)
            var baseMedian = priceData.SafeMedian;
            var dataSource = priceData.DataSource;
            
            // Reference: FlippingEngine.cs lines 273-278
            // If more than 2/3 of sales were non-BIN auctions, halve the median
            // This prevents manipulation via fake auction sales
            if (priceData.Volume > priceData.BinCount * 2)
            {
                baseMedian /= 2;
            }
            
            // Add gem value back to get effective target price
            // Reference: FlippingEngine.cs line 282, 340
            var (gemValue, gemBreakdown) = await _componentValueService.GetGemstoneValue(auction);
            var effectiveMedian = baseMedian + gemValue;
            
            var breakdown = gemValue > 0 
                ? $"Base: {baseMedian/1_000_000.0:F1}m + Gems: {gemBreakdown}"
                : "";

            var currentPrice = auction.HighestBidAmount > 0
                ? auction.HighestBidAmount
                : auction.StartingBid;
            
            // OPTIMIZATION: Use in-memory hit count lookup instead of DB call
            var decayedMedianPrice = ApplyHitCountDecayFromDict(hitCountsDict, cacheKey, effectiveMedian);
            
            // Reference project (lines 284-287): Low-value items get 10% extra margin requirement
            if (decayedMedianPrice < 1_000_000)
            {
                decayedMedianPrice *= 0.9;
            }
            
            // Reference project (lines 311-315): Stack count multiplier
            // Items with count > 1 are harder to sell at full price
            if (auction.Count > 1)
            {
                decayedMedianPrice *= 0.9 * auction.Count;
            }
            
            var medianPrice = (long)decayedMedianPrice;

            // Skip low-value items
            if (medianPrice < MIN_ITEM_VALUE) continue;

            var profitMargin = (double)(medianPrice - currentPrice) / medianPrice;

            if (profitMargin >= MIN_PROFIT_MARGIN)
            {
                flips.Add(new FlipOpportunity
                {
                    AuctionUuid = auction.Uuid,
                    ItemTag = auction.Tag,
                    ItemName = auction.ItemName,
                    CurrentPrice = currentPrice,
                    MedianPrice = medianPrice,
                    EstimatedProfit = medianPrice - currentPrice,
                    ProfitMarginPercent = profitMargin * 100,
                    AuctionEnd = auction.End,
                    DataSource = dataSource,
                    ValueBreakdown = breakdown
                });

                // OPTIMIZATION: Track hit in memory, batch write later
                if (!hitUpdates.ContainsKey(cacheKey))
                    hitUpdates[cacheKey] = 0;
                hitUpdates[cacheKey]++;
            }
        }

        // Sort by profit margin and limit
        flips = flips
            .OrderByDescending(f => f.ProfitMarginPercent)
            .Take(MAX_FLIPS_TO_STORE)
            .ToList();

        // Save flips (replace old ones)
        await dbContext.FlipOpportunities.ExecuteDeleteAsync(stoppingToken);

        if (flips.Count > 0)
        {
            dbContext.FlipOpportunities.AddRange(flips);
            await dbContext.SaveChangesAsync(stoppingToken);

            var topFlip = flips.First();
            _logger.LogInformation("🔥 Detected {Count} flips! Top: {Item} (-{Margin:F1}%, profit: {Profit:N0})",
                flips.Count,
                topFlip.ItemName,
                topFlip.ProfitMarginPercent,
                topFlip.EstimatedProfit);
        }

        // OPTIMIZATION: Batch update hit counts at the end
        if (hitUpdates.Count > 0)
        {
            await BatchUpdateHitCounts(dbContext, hitUpdates, stoppingToken);
        }

        // Periodically cleanup expired hit counts (every ~10 cycles)
        if (Random.Shared.Next(10) == 0)
        {
            await CleanupExpiredHitCounts(dbContext, stoppingToken);
        }
    }

    private double CalculateWeightedMedian(List<double> medians)
    {
        if (medians == null || medians.Count == 0) return 0;
        medians.Sort((a, b) => b.CompareTo(a));
        var longTermMedian = CalculateMedian(medians);
        var shortTermData = medians.Take(3).ToList();
        var shortTermMedian = CalculateMedian(shortTermData);
        return Math.Min(longTermMedian, shortTermMedian);
    }

    private double CalculateMedian(List<double> values)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(v => v).ToList();
        int mid = sorted.Count / 2;
        return sorted.Count % 2 != 0 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }

    /// <summary>
    /// Apply hit count decay using pre-loaded dictionary (no DB call).
    /// </summary>
    private double ApplyHitCountDecayFromDict(Dictionary<string, int> hitCountsDict, string cacheKey, double baseMedianPrice)
    {
        if (!hitCountsDict.TryGetValue(cacheKey, out var hitCount) || hitCount == 0)
            return baseMedianPrice;
        
        var effectiveHitCount = Math.Min(hitCount, MAX_HIT_COUNT);
        var decayFactor = Math.Pow(HIT_COUNT_DECAY_FACTOR, effectiveHitCount);
        return baseMedianPrice / decayFactor;
    }

    /// <summary>
    /// Batch update all hit counts in a single transaction.
    /// </summary>
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
        {
            dbContext.FlipHitCounts.AddRange(newRecords);
        }

        await dbContext.SaveChangesAsync(stoppingToken);
    }

    /// <summary>
    /// Cleans up hit count entries older than TTL.
    /// Reference uses Redis TTL (2 hours), we simulate with periodic cleanup.
    /// </summary>
    private async Task CleanupExpiredHitCounts(AppDbContext dbContext, CancellationToken stoppingToken)
    {
        var cutoff = DateTime.UtcNow - HIT_COUNT_TTL;
        var expiredCount = await dbContext.FlipHitCounts
            .Where(h => h.LastHitAt < cutoff)
            .ExecuteDeleteAsync(stoppingToken);
        
        if (expiredCount > 0)
        {
            _logger.LogDebug("🧹 Cleaned up {Count} expired hit count entries", expiredCount);
        }
    }
}
