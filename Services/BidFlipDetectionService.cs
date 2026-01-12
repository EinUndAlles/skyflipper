using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Hubs;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services;

/// <summary>
/// Background service that detects flip opportunities on non-BIN auctions
/// that are ending soon (30 seconds to 2 minutes from now).
/// 
/// Reference: FlippingEngine.cs QueckActiveAuctionsForFlips() lines 121-156
/// 
/// Key differences from BIN flip detection:
/// 1. Targets non-BIN auctions ending in 30s-2min window
/// 2. Expected purchase price = (HighestBid == 0 ? StartingBid : HighestBid * 1.1)
///    The 1.1 multiplier accounts for expected bid competition
/// 3. Lower profit thresholds since timing is more critical
/// </summary>
public class BidFlipDetectionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BidFlipDetectionService> _logger;
    private readonly CacheKeyService _cacheKeyService;
    private readonly ComponentValueService _componentValueService;
    private readonly IHubContext<FlipHub> _hubContext;
    
    private const string FlipSubscribersGroup = "FlipSubscribers";

    // Check every 15 seconds since bid auctions are time-sensitive
    private const int DETECTION_INTERVAL_SECONDS = 15;
    
    // Window for auctions ending soon (30 seconds to 2 minutes)
    private static readonly TimeSpan MIN_TIME_TO_END = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MAX_TIME_TO_END = TimeSpan.FromMinutes(2);
    
    // Minimum profit margin (lower than BIN since timing matters more)
    private const double MIN_PROFIT_MARGIN = 0.08; // 8% minimum profit
    
    // Reference requires > 7 reference auctions
    private const int MIN_SALES_THRESHOLD = 7;
    
    // Expected bid competition multiplier
    private const double BID_COMPETITION_FACTOR = 1.1;
    
    // Minimum value to consider
    private const long MIN_ITEM_VALUE = 50000; // 50k minimum (higher than BIN due to time investment)
    
    // Hit count decay settings (same as BIN flip detection)
    private const double HIT_COUNT_DECAY_FACTOR = 1.05;
    private const int MAX_HIT_COUNT = 20;
    private static readonly TimeSpan HIT_COUNT_TTL = TimeSpan.FromHours(2);

    public BidFlipDetectionService(
        IServiceScopeFactory scopeFactory,
        ILogger<BidFlipDetectionService> logger,
        CacheKeyService cacheKeyService,
        ComponentValueService componentValueService,
        IHubContext<FlipHub> hubContext)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _cacheKeyService = cacheKeyService;
        _componentValueService = componentValueService;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BidFlipDetectionService starting (checking auctions ending in {Min}s-{Max}min)...",
            MIN_TIME_TO_END.TotalSeconds, MAX_TIME_TO_END.TotalMinutes);

        // Wait for initial data collection
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DetectBidFlips(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error detecting bid flips");
            }

            await Task.Delay(TimeSpan.FromSeconds(DETECTION_INTERVAL_SECONDS), stoppingToken);
        }

        _logger.LogInformation("BidFlipDetectionService stopped");
    }

    private async Task DetectBidFlips(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;

        // Time window for auctions ending soon
        var minEndTime = now.Add(MIN_TIME_TO_END);
        var maxEndTime = now.Add(MAX_TIME_TO_END);

        // Pre-load hit counts for decay calculation
        var hitCountCutoff = now - HIT_COUNT_TTL;
        var hitCountsDict = await dbContext.FlipHitCounts
            .AsNoTracking()
            .Where(h => h.LastHitAt > hitCountCutoff)
            .ToDictionaryAsync(h => h.CacheKey, h => h.HitCount, stoppingToken);

        // Get non-BIN auctions ending in the target window
        // Reference: FlippingEngine.cs line 129
        var endingAuctions = await dbContext.Auctions
            .Where(a => !a.Bin &&
                       a.Status == AuctionStatus.ACTIVE &&
                       a.End > minEndTime &&
                       a.End < maxEndTime)
            .Include(a => a.Enchantments)
            .Include(a => a.NBTLookups)
                .ThenInclude(nbt => nbt.NBTKey)
            .ToListAsync(stoppingToken);

        if (endingAuctions.Count == 0)
        {
            return;
        }

        // Load price data (same tiered approach as BIN detection)
        var allPrices = await LoadPriceData(dbContext, now, stoppingToken);

        var bidFlips = new List<FlipOpportunity>();
        var hitUpdates = new Dictionary<string, int>();

        foreach (var auction in endingAuctions)
        {
            // Reference: FlippingEngine.cs lines 249-250
            // Blacklist certain items
            if (auction.ItemName == "null" || 
                auction.Tag == "ATTRIBUTE_SHARD" || 
                auction.Tag.Contains(":"))
            {
                continue;
            }

            // Skip Master Crypt Sols items
            if (CacheKeyService.HasMasterCryptSols(auction))
            {
                continue;
            }

            var cacheKey = _cacheKeyService.GeneratePriceCacheKey(auction);

            // Need sufficient price data
            if (!allPrices.TryGetValue(cacheKey, out var priceData) || 
                priceData.Volume < MIN_SALES_THRESHOLD)
            {
                continue;
            }

            // Reference: FlippingEngine.cs line 252
            // Expected purchase price for bid auction:
            // If no bids yet, use starting bid
            // Otherwise, expect to pay ~10% more than current highest bid due to competition
            var expectedPurchasePrice = auction.HighestBidAmount == 0
                ? auction.StartingBid
                : (long)(auction.HighestBidAmount * BID_COMPETITION_FACTOR);

            // Normalize by item count
            var pricePerItem = expectedPurchasePrice / auction.Count;

            // Reference: line 253 - Skip low-value items
            if (pricePerItem < 3000)
            {
                continue;
            }

            // Base median from stored prices
            var baseMedian = priceData.SafeMedian;

            // Reference: FlippingEngine.cs lines 273-278
            // If more than 2/3 of sales were non-BIN, halve the median (anti-manipulation)
            if (priceData.Volume > priceData.BinCount * 2)
            {
                baseMedian /= 2;
            }

            // Add gem value back
            var (gemValue, gemBreakdown) = await _componentValueService.GetGemstoneValue(auction);
            var effectiveMedian = baseMedian + gemValue;

            // Apply hit count decay
            var decayedMedian = ApplyHitCountDecay(hitCountsDict, cacheKey, effectiveMedian);

            // Reference: line 284-287 - Low-value items get 10% extra margin requirement
            if (decayedMedian < 1_000_000)
            {
                decayedMedian *= 0.9;
            }

            // Reference: line 283 - The 0.9 factor for "recommendedBuyUnder"
            var targetBuyPrice = decayedMedian * 0.9;

            // Stack count adjustment
            if (auction.Count > 1)
            {
                targetBuyPrice *= 0.9 * auction.Count;
            }

            var medianPrice = (long)decayedMedian;

            // Skip if below minimum value threshold
            if (medianPrice < MIN_ITEM_VALUE)
            {
                continue;
            }

            // Calculate profit margin
            // Reference: line 289 - Check if price > recommendedBuyUnder
            var profitMargin = (targetBuyPrice - expectedPurchasePrice) / targetBuyPrice;

            if (profitMargin >= MIN_PROFIT_MARGIN)
            {
                var timeRemaining = auction.End - now;
                var breakdown = gemValue > 0
                    ? $"Base: {baseMedian / 1_000_000.0:F1}m + Gems: {gemBreakdown}"
                    : "";

                bidFlips.Add(new FlipOpportunity
                {
                    AuctionUuid = auction.Uuid,
                    ItemTag = auction.Tag,
                    ItemName = auction.ItemName,
                    CurrentPrice = expectedPurchasePrice,
                    MedianPrice = medianPrice,
                    EstimatedProfit = medianPrice - expectedPurchasePrice,
                    ProfitMarginPercent = profitMargin * 100,
                    AuctionEnd = auction.End,
                    DataSource = $"Bid ({timeRemaining.TotalSeconds:F0}s left)",
                    ValueBreakdown = breakdown
                });

                // Track hit for decay
                if (!hitUpdates.ContainsKey(cacheKey))
                    hitUpdates[cacheKey] = 0;
                hitUpdates[cacheKey]++;

                _logger.LogInformation("🎯 Bid flip: {Item} - Expected: {Price:N0}, Median: {Median:N0}, Profit: {Profit:F1}%, Ends in: {Time:F0}s",
                    auction.ItemName,
                    expectedPurchasePrice,
                    medianPrice,
                    profitMargin * 100,
                    timeRemaining.TotalSeconds);
            }
        }

        // Broadcast bid flips immediately (they're time-sensitive!)
        foreach (var flip in bidFlips.OrderByDescending(f => f.ProfitMarginPercent))
        {
            var notification = new FlipNotification
            {
                AuctionUuid = flip.AuctionUuid,
                ItemTag = flip.ItemTag,
                ItemName = flip.ItemName,
                CurrentPrice = flip.CurrentPrice,
                MedianPrice = flip.MedianPrice,
                EstimatedProfit = flip.EstimatedProfit,
                ProfitMarginPercent = flip.ProfitMarginPercent,
                DetectedAt = flip.DetectedAt,
                AuctionEnd = flip.AuctionEnd,
                DataSource = flip.DataSource
            };
            await _hubContext.Clients.Group(FlipSubscribersGroup)
                .SendAsync("NewBidFlip", notification, stoppingToken);
        }

        // Batch update hit counts
        if (hitUpdates.Count > 0)
        {
            await BatchUpdateHitCounts(dbContext, hitUpdates, stoppingToken);
        }

        if (bidFlips.Count > 0)
        {
            _logger.LogInformation("🔔 Found {Count} bid flip opportunities", bidFlips.Count);
        }
    }

    private async Task<Dictionary<string, (double SafeMedian, int Volume, int BinCount, string DataSource)>> LoadPriceData(
        AppDbContext dbContext, DateTime now, CancellationToken stoppingToken)
    {
        var result = new Dictionary<string, (double SafeMedian, int Volume, int BinCount, string DataSource)>();

        // 15-minute prices (highest priority for bid flips - most current)
        var fifteenMinPrices = await dbContext.AveragePrices
            .Where(p => p.Granularity == PriceGranularity.FifteenMinute &&
                       p.Timestamp > now.AddHours(-2))
            .GroupBy(p => p.CacheKey)
            .Select(g => new
            {
                CacheKey = g.Key,
                Medians = g.OrderByDescending(p => p.Timestamp).Select(p => p.Median).ToList(),
                Volume = g.Sum(p => p.Volume),
                BinCount = g.Sum(p => p.BinCount)
            })
            .ToListAsync(stoppingToken);

        foreach (var item in fifteenMinPrices)
        {
            var safeMedian = CalculateWeightedMedian(item.Medians);
            result[item.CacheKey] = (safeMedian, item.Volume, item.BinCount, "15min");
        }

        // Hourly prices
        var existingKeys = result.Keys.ToHashSet();
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
                BinCount = g.Sum(p => p.BinCount)
            })
            .ToListAsync(stoppingToken);

        foreach (var item in hourlyPrices.Where(p => !existingKeys.Contains(p.CacheKey)))
        {
            var safeMedian = CalculateWeightedMedian(item.Medians);
            result[item.CacheKey] = (safeMedian, item.Volume, item.BinCount, "Hourly");
        }

        // Daily prices
        existingKeys = result.Keys.ToHashSet();
        var dailyPrices = await dbContext.AveragePrices
            .Where(p => p.Granularity == PriceGranularity.Daily &&
                       p.Timestamp > now.AddDays(-7) &&
                       p.Volume >= MIN_SALES_THRESHOLD)
            .GroupBy(p => p.CacheKey)
            .Select(g => new
            {
                CacheKey = g.Key,
                Medians = g.OrderByDescending(p => p.Timestamp).Select(p => p.Median).ToList(),
                Volume = g.Sum(p => p.Volume),
                BinCount = g.Sum(p => p.BinCount)
            })
            .ToListAsync(stoppingToken);

        foreach (var item in dailyPrices.Where(p => !existingKeys.Contains(p.CacheKey)))
        {
            var safeMedian = CalculateWeightedMedian(item.Medians);
            result[item.CacheKey] = (safeMedian, item.Volume, item.BinCount, "Daily");
        }

        return result;
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

    private double ApplyHitCountDecay(Dictionary<string, int> hitCountsDict, string cacheKey, double baseMedian)
    {
        if (!hitCountsDict.TryGetValue(cacheKey, out var hitCount) || hitCount == 0)
            return baseMedian;

        var effectiveHitCount = Math.Min(hitCount, MAX_HIT_COUNT);
        var decayFactor = Math.Pow(HIT_COUNT_DECAY_FACTOR, effectiveHitCount);
        return baseMedian / decayFactor;
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
        {
            dbContext.FlipHitCounts.AddRange(newRecords);
        }

        await dbContext.SaveChangesAsync(stoppingToken);
    }
}
