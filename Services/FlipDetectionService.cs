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
    private readonly ComponentValueService _componentValueService;

    private const int DETECTION_INTERVAL_SECONDS = 30; // Check every 30 seconds
    private const double MIN_PROFIT_MARGIN = 0.05; // 5% minimum profit
    private const int MIN_SALES_THRESHOLD = 7; // Reference project requires > 7 references
    private const int PRICE_HISTORY_DAYS = 7; // Look back 7 days for median
    private const int MAX_FLIPS_TO_STORE = 200; // Limit stored flips
    private const double HIT_COUNT_DECAY_FACTOR = 1.05; // 5% decay per hit
    private const int MAX_HIT_COUNT = 20; // Cap decay at 20 hits
    private const long MIN_ITEM_VALUE = 10000; // 10k minimum value

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

        // Step 1: Get hourly prices from last 24 hours
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

        // Step 2: Get daily prices for cache keys not in hourly
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

        // Step 3: Merge prices (hourly takes precedence)
        var allPrices = new Dictionary<string, (double SafeMedian, int Volume, bool IsRecent)>();

        foreach (var kvp in hourlyPrices)
        {
            var safeMedian = CalculateWeightedMedian(kvp.Value.Medians);
            allPrices[kvp.Key] = (safeMedian, kvp.Value.Volume, kvp.Value.IsRecent);
        }

        foreach (var kvp in dailyPrices)
        {
            if (!allPrices.ContainsKey(kvp.Key))
            {
                var safeMedian = CalculateWeightedMedian(kvp.Value.Medians);
                allPrices[kvp.Key] = (safeMedian, kvp.Value.Volume, kvp.Value.IsRecent);
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
            var dataSource = priceData.IsRecent ? "Hourly (24h)" : "Daily (7d)";
            
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
            
            // Apply hit count decay
            // Reference: FlippingEngine.cs - reduces expected price based on how often this item type appears
            var decayedMedianPrice = await ApplyHitCountDecay(dbContext, cacheKey, effectiveMedian);
            
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

                // Track this as a hit for decay calculation
                await RecordFlipHit(dbContext, cacheKey);
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

    private async Task<double> ApplyHitCountDecay(AppDbContext dbContext, string cacheKey, double baseMedianPrice)
    {
        var hitCountRecord = await dbContext.FlipHitCounts.FirstOrDefaultAsync(h => h.CacheKey == cacheKey);
        if (hitCountRecord == null || hitCountRecord.HitCount == 0) return baseMedianPrice;
        
        var effectiveHitCount = Math.Min(hitCountRecord.HitCount, MAX_HIT_COUNT);
        var decayFactor = Math.Pow(HIT_COUNT_DECAY_FACTOR, effectiveHitCount);
        return baseMedianPrice / decayFactor;
    }

    private async Task RecordFlipHit(AppDbContext dbContext, string cacheKey)
    {
        var hitCountRecord = await dbContext.FlipHitCounts.FirstOrDefaultAsync(h => h.CacheKey == cacheKey);
        if (hitCountRecord == null)
        {
            hitCountRecord = new FlipHitCount { CacheKey = cacheKey, HitCount = 1, LastHitAt = DateTime.UtcNow };
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
