using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services;

/// <summary>
/// Background service that detects flip opportunities by comparing
/// current BIN prices against historical median prices.
/// Uses NBT-aware cache keys and hit count decay for accurate detection.
/// </summary>
public class FlipDetectionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FlipDetectionService> _logger;
    private readonly CacheKeyService _cacheKeyService;
    private const int DETECTION_INTERVAL_SECONDS = 60;
    private const double MIN_PROFIT_MARGIN = 0.10; // 10%
    private const int MIN_SALES_THRESHOLD = 5; // Minimum sales to consider
    private const int PRICE_HISTORY_DAYS = 7; // Look back 7 days for median
    private const int MAX_FLIPS_TO_STORE = 200; // Limit stored flips
    private const double HIT_COUNT_DECAY_FACTOR = 1.05; // 5% decay per hit
    private const int MAX_HIT_COUNT = 20; // Cap decay at 20 hits

    public FlipDetectionService(
        IServiceScopeFactory scopeFactory,
        ILogger<FlipDetectionService> logger,
        CacheKeyService cacheKeyService)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _cacheKeyService = cacheKeyService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FlipDetectionService starting (threshold: {Threshold}%)...", 
            MIN_PROFIT_MARGIN * 100);

        // Wait for initial data collection
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

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

        // Step 1: Get hourly prices from last 24 hours (more accurate for active items)
        var hourlyPrices = await dbContext.AveragePrices
            .Where(p => p.Granularity == PriceGranularity.Hourly &&
                       p.Timestamp > now.AddHours(-24) &&
                       p.Volume >= MIN_SALES_THRESHOLD)
            .GroupBy(p => p.CacheKey)
            .Select(g => new
            {
                CacheKey = g.Key,
                Medians = g.OrderByDescending(p => p.Timestamp).Select(p => p.Median).ToList(),
                Volume = g.Sum(p => p.Volume),
                IsRecent = true
            })
            .ToDictionaryAsync(p => p.CacheKey, stoppingToken);

        // Step 2: Get daily prices for items without recent hourly data
        var hourlyCacheKeys = hourlyPrices.Keys.ToList();
        var dailyPrices = await dbContext.AveragePrices
            .Where(p => p.Granularity == PriceGranularity.Daily &&
                       p.Timestamp > now.AddDays(-PRICE_HISTORY_DAYS) &&
                       p.Volume >= MIN_SALES_THRESHOLD &&
                       !hourlyCacheKeys.Contains(p.CacheKey))
            .GroupBy(p => p.CacheKey)
            .Select(g => new
            {
                CacheKey = g.Key,
                Medians = g.OrderByDescending(p => p.Timestamp).Select(p => p.Median).ToList(),
                Volume = g.Sum(p => p.Volume),
                IsRecent = false
            })
            .ToDictionaryAsync(p => p.CacheKey, stoppingToken);

        // Step 3: Merge hourly and daily data (hourly takes priority)
        var allPrices = new Dictionary<string, (double SafeMedian, int Volume, bool IsRecent)>();

        // Process hourly prices with weighted median algorithm
        foreach (var kvp in hourlyPrices)
        {
            var safeMedian = CalculateWeightedMedian(kvp.Value.Medians);
            allPrices[kvp.Key] = (safeMedian, kvp.Value.Volume, kvp.Value.IsRecent);
        }

        // Process daily prices for items not in hourly
        foreach (var kvp in dailyPrices)
        {
            if (!allPrices.ContainsKey(kvp.Key))
            {
                var safeMedian = CalculateWeightedMedian(kvp.Value.Medians);
                allPrices[kvp.Key] = (safeMedian, kvp.Value.Volume, kvp.Value.IsRecent);
            }
        }

        // Step 4: Get active BIN auctions with full data for cache key generation
        var activeAuctions = await dbContext.Auctions
            .Where(a => a.Bin &&
                       a.Status == AuctionStatus.ACTIVE &&
                       a.End > now)
            .Include(a => a.Enchantments)
            .Include(a => a.NBTLookups)
                .ThenInclude(nbt => nbt.NBTKey)
            .ToListAsync(stoppingToken);

        // Filter auctions that have price data and generate cache keys
        var auctionsWithPrices = activeAuctions
            .Select(a => new
            {
                Auction = a,
                CacheKey = _cacheKeyService.GeneratePriceCacheKey(a)
            })
            .Where(x => allPrices.ContainsKey(x.CacheKey))
            .ToList();

        var flips = new List<FlipOpportunity>();

        // Step 5: Calculate flips with hit count decay
        foreach (var auctionData in auctionsWithPrices)
        {
            var auction = auctionData.Auction;
            var cacheKey = auctionData.CacheKey;

            if (!allPrices.TryGetValue(cacheKey, out var priceData))
                continue;

            if (priceData.Volume < MIN_SALES_THRESHOLD)
                continue;

            var currentPrice = auction.HighestBidAmount > 0
                ? auction.HighestBidAmount
                : auction.StartingBid;
            var baseMedianPrice = priceData.SafeMedian;

            // Apply hit count decay
            var decayedMedianPrice = await ApplyHitCountDecay(dbContext, cacheKey, baseMedianPrice);
            var medianPrice = (long)decayedMedianPrice;

            // Skip low-value items
            if (medianPrice < 100000) continue;

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
                    DataSource = priceData.IsRecent ? "Hourly (24h)" : $"Daily (7d) - Weighted Median + Decay"
                });

                // Track this as a hit
                await RecordFlipHit(dbContext, cacheKey);
            }
        }

        // Sort and limit results
        flips = flips
            .OrderByDescending(f => f.ProfitMarginPercent)
            .Take(MAX_FLIPS_TO_STORE)
            .ToList();

        // Clear old flips and insert new ones
        await dbContext.FlipOpportunities.ExecuteDeleteAsync(stoppingToken);

        if (flips.Count > 0)
        {
            dbContext.FlipOpportunities.AddRange(flips);
            await dbContext.SaveChangesAsync(stoppingToken);

            var topFlip = flips.First();
            var hourlyCount = flips.Count(f => f.DataSource.StartsWith("Hourly"));
            _logger.LogInformation("🔥 Detected {Count} flips ({Hourly} hourly, {Daily} daily)! Top: {Item} (-{Margin:F1}%, profit: {Profit:N0})",
                flips.Count,
                hourlyCount,
                flips.Count - hourlyCount,
                topFlip.ItemName,
                topFlip.ProfitMarginPercent,
                topFlip.EstimatedProfit);
        }
        else
        {
            _logger.LogDebug("No flips detected in this cycle");
        }
    }

    /// <summary>
    /// Calculates weighted median using Coflnet's algorithm:
    /// MIN(long-term median, short-term median of last 3 sales)
    /// This prevents price manipulation and crash vulnerability.
    /// </summary>
    private double CalculateWeightedMedian(List<double> medians)
    {
        if (medians == null || medians.Count == 0)
            return 0;

        // Sort medians chronologically (most recent first)
        medians.Sort((a, b) => b.CompareTo(a)); // Descending order

        // Long-term median: all available data
        var longTermMedian = CalculateMedian(medians);

        // Short-term median: last 3 sales (most recent)
        var shortTermData = medians.Take(3).ToList();
        var shortTermMedian = CalculateMedian(shortTermData);

        // Use the more conservative (lower) median to avoid manipulation
        return Math.Min(longTermMedian, shortTermMedian);
    }

    /// <summary>
    /// Calculates the median of a list of doubles.
    /// </summary>
    private double CalculateMedian(List<double> values)
    {
        if (values.Count == 0) return 0;

        var sorted = values.OrderBy(v => v).ToList();
        int mid = sorted.Count / 2;

        if (sorted.Count % 2 == 0)
            return (sorted[mid - 1] + sorted[mid]) / 2.0;
        else
            return sorted[mid];
    }

    /// <summary>
    /// Applies hit count decay to the median price.
    /// Items flagged as flips repeatedly get reduced target prices.
    /// </summary>
    private async Task<double> ApplyHitCountDecay(AppDbContext dbContext, string cacheKey, double baseMedianPrice)
    {
        var hitCountRecord = await dbContext.FlipHitCounts
            .FirstOrDefaultAsync(h => h.CacheKey == cacheKey);

        if (hitCountRecord == null || hitCountRecord.HitCount == 0)
            return baseMedianPrice;

        // Apply decay: targetPrice = basePrice * (1.05 ^ hitCount)
        // Cap at MAX_HIT_COUNT to prevent excessive decay
        var effectiveHitCount = Math.Min(hitCountRecord.HitCount, MAX_HIT_COUNT);
        var decayFactor = Math.Pow(HIT_COUNT_DECAY_FACTOR, effectiveHitCount);

        var decayedPrice = baseMedianPrice * decayFactor;

        _logger.LogDebug("Applied {DecayFactor:F2}x decay to {CacheKey} (hit count: {Hits})",
            decayFactor, cacheKey, hitCountRecord.HitCount);

        return decayedPrice;
    }

    /// <summary>
    /// Records that a cache key was flagged as a flip (increments hit count).
    /// </summary>
    private async Task RecordFlipHit(AppDbContext dbContext, string cacheKey)
    {
        var hitCountRecord = await dbContext.FlipHitCounts
            .FirstOrDefaultAsync(h => h.CacheKey == cacheKey);

        if (hitCountRecord == null)
        {
            hitCountRecord = new FlipHitCount
            {
                CacheKey = cacheKey,
                HitCount = 1,
                LastHitAt = DateTime.UtcNow
            };
            dbContext.FlipHitCounts.Add(hitCountRecord);
        }
        else
        {
            hitCountRecord.HitCount++;
            hitCountRecord.LastHitAt = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync();
    }
}
