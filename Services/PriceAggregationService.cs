using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services;

/// <summary>
/// Background service that aggregates price statistics from sold auctions.
/// Supports both hourly (7-day retention) and daily (indefinite) aggregation.
/// </summary>
public class PriceAggregationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PriceAggregationService> _logger;
    private readonly CacheKeyService _cacheKeyService;
    private readonly ComponentValueService _componentValueService;
    private readonly TimeSpan _aggregationInterval = TimeSpan.FromMinutes(5); // Check every 5 min for 15-min aggregation
    
    // High-volume items get 15-minute aggregation for faster price updates
    private const int HIGH_VOLUME_THRESHOLD = 10; // Items with 10+ sales per hour qualify

    /// <summary>
    /// Date when gemstone/unlocked slots were introduced to Skyblock.
    /// Items with unlocked_slots NBT should only compare to items created after this date.
    /// Reference: FlippingEngine.cs line 117: private static readonly DateTime UnlockedIntroduction = new DateTime(2021, 9, 4);
    /// </summary>
    private static readonly DateTime GemstoneIntroductionDate = new DateTime(2021, 9, 4, 0, 0, 0, DateTimeKind.Utc);

    public PriceAggregationService(
        IServiceScopeFactory scopeFactory,
        ILogger<PriceAggregationService> logger,
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
        _logger.LogInformation("PriceAggregationService starting (15-min + hourly + daily mode)...");

        // Wait for initial data collection
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 15-minute aggregation runs every cycle (for high-volume items)
                await AggregateFifteenMinutePrices(stoppingToken);
                
                // Hourly aggregation - check if we need to aggregate the previous hour
                var now = DateTime.UtcNow;
                if (now.Minute < 10) // Within first 10 min of hour, aggregate previous hour
                {
                    await AggregateHourlyPrices(stoppingToken);
                }

                // Once per day at midnight UTC, aggregate daily and cleanup
                if (now.Hour == 0 && now.Minute < 10)
                {
                    await AggregateDailyPrices(stoppingToken);
                    await CleanupOldData(stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error aggregating prices");
            }

            await Task.Delay(_aggregationInterval, stoppingToken);
        }

        _logger.LogInformation("PriceAggregationService stopped");
    }

    /// <summary>
    /// Aggregates sold auctions from the previous 15 minutes into price records.
    /// Only tracks high-volume items (those with sufficient sales per hour).
    /// </summary>
    private async Task AggregateFifteenMinutePrices(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;
        // Round to 15-minute boundaries (0, 15, 30, 45)
        var minuteSlot = (now.Minute / 15) * 15;
        var currentSlot = new DateTime(now.Year, now.Month, now.Day, now.Hour, minuteSlot, 0, DateTimeKind.Utc);
        var previousSlot = currentSlot.AddMinutes(-15);

        // Check if we've already aggregated this slot
        var existing = await dbContext.AveragePrices
            .AnyAsync(p => p.Timestamp == previousSlot && p.Granularity == PriceGranularity.FifteenMinute, stoppingToken);

        if (existing)
        {
            return; // Already aggregated
        }

        // Get sold auctions from the previous 15 minutes
        var allAuctions = await dbContext.Auctions
            .Where(a => a.Status == AuctionStatus.SOLD &&
                       a.SoldPrice.HasValue &&
                       a.End >= previousSlot &&
                       a.End < currentSlot)
            .Include(a => a.Enchantments)
            .Include(a => a.NBTLookups)
                .ThenInclude(nbt => nbt.NBTKey)
            .Include(a => a.Bids) // For buyer deduplication
            .ToListAsync(stoppingToken);

        if (allAuctions.Count == 0)
        {
            return;
        }

        // Apply anti-manipulation and gemstone date filter
        var soldAuctions = ApplyAntiMarketManipulation(allAuctions);
        soldAuctions = ApplyUnlockedSlotsDateFilter(soldAuctions);

        // Group by cache key
        var aggregatesData = new List<(string CacheKey, List<double> Prices, int BinCount)>();
        
        foreach (var group in soldAuctions
            .Select(a => new
            {
                Auction = a,
                CacheKey = _cacheKeyService.GeneratePriceCacheKey(a)
            })
            .GroupBy(x => x.CacheKey))
        {
            var prices = new List<double>();
            var binCount = 0;
            foreach (var item in group)
            {
                var salePrice = (double)item.Auction.SoldPrice!.Value;
                var (gemValue, _) = await _componentValueService.GetGemstoneValueOnly(item.Auction);
                var baseValue = salePrice - gemValue;
                prices.Add(Math.Max(baseValue, salePrice * 0.1));
                
                if (item.Auction.Bin)
                    binCount++;
            }
            
            // Only include items that would qualify as high-volume
            // (at least 2 sales in 15 min = ~8+ sales/hour)
            if (prices.Count >= 2)
            {
                aggregatesData.Add((group.Key, prices, binCount));
            }
        }

        if (aggregatesData.Count == 0)
        {
            return;
        }

        var aggregates = aggregatesData
            .Select(x => new { CacheKey = x.CacheKey, Prices = x.Prices, BinCount = x.BinCount })
            .ToList();

        await SavePriceAggregates(dbContext, aggregates, previousSlot, 
            PriceGranularity.FifteenMinute, stoppingToken);

        _logger.LogDebug("⚡ Aggregated 15-min prices for {Time}: {Count} high-volume items",
            previousSlot.ToString("HH:mm"), aggregates.Count);
    }

    /// <summary>
    /// Aggregates sold auctions from the previous hour into hourly price records.
    /// </summary>
    private async Task AggregateHourlyPrices(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;
        var currentHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
        var previousHour = currentHour.AddHours(-1);

        _logger.LogDebug("Aggregating hourly prices for {Hour}", previousHour);

        // Check if we've already aggregated this hour
        var existing = await dbContext.AveragePrices
            .AnyAsync(p => p.Timestamp == previousHour && p.Granularity == PriceGranularity.Hourly, stoppingToken);

        if (existing)
        {
            _logger.LogDebug("Hourly aggregation already done for {Hour}", previousHour);
            return;
        }

        // Get sold auctions from the previous hour with full data for cache key generation
        var allAuctions = await dbContext.Auctions
            .Where(a => a.Status == AuctionStatus.SOLD &&
                       a.SoldPrice.HasValue &&
                       a.End >= previousHour &&
                       a.End < currentHour)
            .Include(a => a.Enchantments)
            .Include(a => a.NBTLookups)
                .ThenInclude(nbt => nbt.NBTKey)
            .Include(a => a.Bids) // For buyer deduplication
            .ToListAsync(stoppingToken);

        // Apply anti-manipulation deduplication per reference:
        // 1. Dedupe by seller (take lowest price per seller to prevent artificial inflation)
        // 2. Dedupe by buyer (take first per buyer to prevent wash trading)
        // 3. Dedupe by item UID (same item sold multiple times)
        // Reference: FlippingEngine.cs ApplyAntiMarketManipulation() lines 550-586
        var soldAuctions = ApplyAntiMarketManipulation(allAuctions);
        
        // Apply unlocked slots date filter - reference FlippingEngine.cs lines 748-757
        soldAuctions = ApplyUnlockedSlotsDateFilter(soldAuctions);

        if (soldAuctions.Count == 0)
        {
            _logger.LogDebug("No sold auctions for hour {Hour}", previousHour);
            return;
        }

        // Group by cache key and calculate statistics
        // CRITICAL: Subtract gem value from each sale to get "base item" price
        // This matches reference project: FlippingEngine.cs line 340
        var aggregatesData = new List<(string CacheKey, List<double> Prices, int BinCount)>();
        
        foreach (var group in soldAuctions
            .Select(a => new
            {
                Auction = a,
                CacheKey = _cacheKeyService.GeneratePriceCacheKey(a)
            })
            .GroupBy(x => x.CacheKey))
        {
            var prices = new List<double>();
            var binCount = 0;
            foreach (var item in group)
            {
                var salePrice = (double)item.Auction.SoldPrice!.Value;
                // Subtract gem value to get base item value (matching reference project)
                var (gemValue, _) = await _componentValueService.GetGemstoneValueOnly(item.Auction);
                var baseValue = salePrice - gemValue;
                // Don't allow negative base values
                prices.Add(Math.Max(baseValue, salePrice * 0.1));
                
                // Track BIN count for ratio check
                if (item.Auction.Bin)
                    binCount++;
            }
            aggregatesData.Add((group.Key, prices, binCount));
        }

        var aggregates = aggregatesData
            .Select(x => new { CacheKey = x.CacheKey, Prices = x.Prices, BinCount = x.BinCount })
            .ToList();

        await SavePriceAggregates(dbContext, aggregates, previousHour, 
            PriceGranularity.Hourly, stoppingToken);

        _logger.LogInformation("⏰ Aggregated hourly prices for {Hour}: {Count} items, {Sales} sales",
            previousHour, aggregates.Count, soldAuctions.Count);
    }

    /// <summary>
    /// Aggregates yesterday's hourly data into daily price records.
    /// Runs once per day at midnight UTC.
    /// </summary>
    private async Task AggregateDailyPrices(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var yesterday = DateTime.UtcNow.Date.AddDays(-1);
        var today = DateTime.UtcNow.Date;

        _logger.LogInformation("Aggregating daily prices for {Date}", yesterday);

        // Get all hourly aggregates from yesterday
        var hourlyAggregates = await dbContext.AveragePrices
            .Where(p => p.Granularity == PriceGranularity.Hourly &&
                       p.Timestamp >= yesterday &&
                       p.Timestamp < today)
            .GroupBy(p => p.CacheKey)
            .Select(g => new
            {
                CacheKey = g.Key,
                Min = g.Min(p => p.Min),
                Max = g.Max(p => p.Max),
                Medians = g.Select(p => p.Median).ToList(),
                TotalVolume = g.Sum(p => p.Volume),
                TotalBinCount = g.Sum(p => p.BinCount)
            })
            .ToListAsync(stoppingToken);

        if (hourlyAggregates.Count == 0)
        {
            _logger.LogInformation("No hourly data to aggregate for {Date}", yesterday);
            return;
        }

        foreach (var item in hourlyAggregates)
        {
            if (item.TotalVolume == 0) continue;

            var dailyAggregate = new AveragePrice
            {
                ItemTag = "", // Will be set when we parse the cache key, but not critical for daily aggregates
                CacheKey = item.CacheKey,
                Timestamp = yesterday,
                Granularity = PriceGranularity.Daily,
                Min = item.Min,
                Max = item.Max,
                Avg = item.Medians.Average(),
                Median = CalculateMedian(item.Medians.OrderBy(m => m)),
                Volume = item.TotalVolume,
                BinCount = item.TotalBinCount
            };

            var existing = await dbContext.AveragePrices
                .FirstOrDefaultAsync(p => p.CacheKey == item.CacheKey &&
                                         p.Timestamp == yesterday &&
                                         p.Granularity == PriceGranularity.Daily,
                    stoppingToken);

            if (existing != null)
            {
                existing.Min = dailyAggregate.Min;
                existing.Max = dailyAggregate.Max;
                existing.Avg = dailyAggregate.Avg;
                existing.Median = dailyAggregate.Median;
                existing.Volume = dailyAggregate.Volume;
                existing.BinCount = dailyAggregate.BinCount;
            }
            else
            {
                dbContext.AveragePrices.Add(dailyAggregate);
            }
        }

        await dbContext.SaveChangesAsync(stoppingToken);
        _logger.LogInformation("📅 Created {Count} daily aggregates for {Date}",
            hourlyAggregates.Count, yesterday);
    }

    /// <summary>
    /// Deletes old price data:
    /// - 15-minute data older than 2 hours
    /// - Hourly data older than 7 days
    /// Runs once per day at midnight UTC.
    /// </summary>
    private async Task CleanupOldData(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Clean 15-minute data older than 2 hours
        var fifteenMinCutoff = DateTime.UtcNow.AddHours(-2);
        var oldFifteenMin = await dbContext.AveragePrices
            .Where(p => p.Granularity == PriceGranularity.FifteenMinute && p.Timestamp < fifteenMinCutoff)
            .ExecuteDeleteAsync(stoppingToken);

        if (oldFifteenMin > 0)
        {
            _logger.LogInformation("🗑️ Cleaned up {Count} old 15-min records", oldFifteenMin);
        }

        // Clean hourly data older than 7 days
        var hourlyCutoff = DateTime.UtcNow.AddDays(-7);
        var oldHourly = await dbContext.AveragePrices
            .Where(p => p.Granularity == PriceGranularity.Hourly && p.Timestamp < hourlyCutoff)
            .ExecuteDeleteAsync(stoppingToken);

        if (oldHourly > 0)
        {
            _logger.LogInformation("🗑️ Cleaned up {Count} old hourly records (older than {Cutoff})",
                oldHourly, hourlyCutoff);
        }
    }

    /// <summary>
    /// Saves price aggregates to database with upsert logic.
    /// </summary>
    private async Task SavePriceAggregates<T>(
        AppDbContext dbContext,
        List<T> aggregates,
        DateTime timestamp,
        PriceGranularity granularity,
        CancellationToken stoppingToken) where T : class
    {
        foreach (var item in aggregates)
        {
            // Use reflection to get CacheKey, Prices, and BinCount properties
            var cacheKeyProp = item.GetType().GetProperty("CacheKey");
            var pricesProp = item.GetType().GetProperty("Prices");
            var binCountProp = item.GetType().GetProperty("BinCount");

            if (cacheKeyProp == null || pricesProp == null) continue;

            var cacheKey = cacheKeyProp.GetValue(item) as string;
            var prices = pricesProp.GetValue(item) as List<double>;
            var binCount = binCountProp?.GetValue(item) as int? ?? 0;

            if (string.IsNullOrEmpty(cacheKey) || prices == null || prices.Count == 0) continue;

            var sorted = prices.OrderBy(p => p).ToList();

            var avgPrice = new AveragePrice
            {
                ItemTag = "", // CacheKey contains the tag info, but keep for backward compatibility
                CacheKey = cacheKey,
                Timestamp = timestamp,
                Granularity = granularity,
                Min = sorted.First(),
                Max = sorted.Last(),
                Avg = sorted.Average(),
                Median = CalculateMedian(sorted),
                Volume = sorted.Count,
                BinCount = binCount
            };

            var existing = await dbContext.AveragePrices
                .FirstOrDefaultAsync(p => p.CacheKey == cacheKey &&
                                         p.Timestamp == timestamp &&
                                         p.Granularity == granularity,
                    stoppingToken);

            if (existing != null)
            {
                existing.Min = avgPrice.Min;
                existing.Max = avgPrice.Max;
                existing.Avg = avgPrice.Avg;
                existing.Median = avgPrice.Median;
                existing.Volume = avgPrice.Volume;
                existing.BinCount = avgPrice.BinCount;
            }
            else
            {
                dbContext.AveragePrices.Add(avgPrice);
            }
        }

        await dbContext.SaveChangesAsync(stoppingToken);
    }

    private double CalculateMedian(IEnumerable<double> prices)
    {
        var sorted = prices.OrderBy(p => p).ToList();
        if (sorted.Count == 0) return 0;

        int mid = sorted.Count / 2;
        if (sorted.Count % 2 == 0)
            return (sorted[mid - 1] + sorted[mid]) / 2.0;
        else
            return sorted[mid];
    }

    /// <summary>
    /// Applies anti-market manipulation deduplication to auction list.
    /// 
    /// Reference: FlippingEngine.cs ApplyAntiMarketManipulation() lines 550-586
    /// 
    /// 1. Dedupe by seller - take lowest price per seller (prevents artificial price inflation)
    /// 2. Dedupe by buyer - take first per buyer (prevents wash trading)
    /// 3. Dedupe by item UID - each physical item counted once
    /// 4. Detect back-forth trading between same user pairs (for UID-less items)
    /// </summary>
    private static List<Auction> ApplyAntiMarketManipulation(List<Auction> auctions)
    {
        if (auctions.Count <= 1)
            return auctions;

        var counter = 1;
        
        // 1. Dedupe by seller (AuctioneerId) - take the LOWEST price per seller
        // Reference line 554-555: .GroupBy(a => a.SellerId).Select(a => a.OrderBy(s => s.HighestBidAmount).First())
        var dedupedBySeller = auctions
            .GroupBy(a => a.AuctioneerId ?? $"unknown_{counter++}")
            .Select(g => g.OrderBy(a => a.SoldPrice ?? a.StartingBid).First())
            .ToList();

        // 2. Dedupe by buyer (highest bidder) - take first per buyer
        // Reference line 556-557: .GroupBy(a => a.Bids.OrderByDescending(b => b.Amount).First().Bidder).Select(a => a.First())
        counter = 1;
        var dedupedByBuyer = dedupedBySeller
            .GroupBy(a => GetWinningBidder(a) ?? $"unknown_buyer_{counter++}")
            .Select(g => g.First())
            .ToList();

        // 3. Dedupe by item UID - each unique item counted once
        // Reference line 558-559: .GroupBy(a => a.FlatenedNBT.TryGetValue("uid", out string uid) ? uid : counter++.ToString())
        counter = 1;
        var dedupedByUid = dedupedByBuyer
            .GroupBy(a => a.ItemUid ?? $"no_uid_{counter++}")
            .Select(g => g.OrderBy(a => a.SoldAt ?? a.End).First())
            .ToList();

        // 4. Detect back-forth trading for UID-less items
        // Reference lines 561-576: Check for same buyer-seller pairs appearing multiple times
        if (counter > 2) // We had items without UID
        {
            dedupedByUid = FilterBackForthTrading(dedupedByUid);
        }
        
        return dedupedByUid;
    }

    /// <summary>
    /// Gets the winning bidder (highest bidder) from an auction's bids.
    /// </summary>
    private static string? GetWinningBidder(Auction auction)
    {
        if (auction.Bids == null || auction.Bids.Count == 0)
            return null;

        return auction.Bids
            .OrderByDescending(b => b.Amount)
            .FirstOrDefault()?.BidderId;
    }

    /// <summary>
    /// Filters out back-and-forth trading between the same user pairs.
    /// Reference: FlippingEngine.cs lines 564-576
    /// 
    /// For items without UID, checks if the same buyer-seller pair appears multiple times,
    /// which indicates wash trading to manipulate prices.
    /// </summary>
    private static List<Auction> FilterBackForthTrading(List<Auction> auctions)
    {
        // Create normalized buyer-seller pairs (sorted so order doesn't matter)
        var tradePairs = auctions
            .Select(a =>
            {
                var buyer = GetWinningBidder(a) ?? "";
                var seller = a.AuctioneerId ?? "";
                // Normalize order for consistent comparison
                return string.Compare(buyer, seller, StringComparison.Ordinal) < 0 
                    ? (buyer, seller) 
                    : (seller, buyer);
            })
            .GroupBy(pair => pair)
            .Where(g => g.Count() > 1) // Pairs that appear more than once
            .Select(g => g.Key)
            .ToHashSet();

        if (tradePairs.Count == 0)
            return auctions;

        // Exclude all auctions from suspicious trading pairs
        return auctions
            .Where(a =>
            {
                var buyer = GetWinningBidder(a) ?? "";
                var seller = a.AuctioneerId ?? "";
                var pair = string.Compare(buyer, seller, StringComparison.Ordinal) < 0 
                    ? (buyer, seller) 
                    : (seller, buyer);
                return !tradePairs.Contains(pair);
            })
            .ToList();
    }

    /// <summary>
    /// Checks if an auction has gemstone/unlocked_slots NBT that requires date filtering.
    /// Reference: FlippingEngine.cs lines 748-757
    /// 
    /// Items with unlocked_slots should only compare to items created after the gemstone
    /// introduction date (2021-09-04) to avoid comparing to older items without this feature.
    /// </summary>
    private static bool HasUnlockedSlotsNbt(Auction auction)
    {
        // Check FlatenedNBTJson first
        if (!string.IsNullOrEmpty(auction.FlatenedNBTJson))
        {
            try
            {
                var flatNbt = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(auction.FlatenedNBTJson);
                if (flatNbt != null && flatNbt.ContainsKey("unlocked_slots"))
                    return true;
            }
            catch { }
        }

        // Fallback to NBTLookups
        return auction.NBTLookups?.Any(n => 
            n.NBTKey?.KeyName?.Equals("unlocked_slots", StringComparison.OrdinalIgnoreCase) == true) == true;
    }

    /// <summary>
    /// Filters auctions based on gemstone introduction date.
    /// 
    /// Reference: FlippingEngine.cs lines 748-757:
    /// if (canHaveGemstones || flatNbt.ContainsKey("unlocked_slots"))
    /// {
    ///     select = select.Where(a => a.ItemCreatedAt > UnlockedIntroduction);
    /// }
    /// 
    /// This ensures that items with unlocked gemstone slots are only compared to
    /// other items created after gemstones were introduced to the game.
    /// </summary>
    private static List<Auction> ApplyUnlockedSlotsDateFilter(List<Auction> auctions)
    {
        return auctions.Where(a =>
        {
            // If this auction has unlocked_slots NBT, it should only be included
            // if the item was created after the gemstone introduction date
            if (HasUnlockedSlotsNbt(a))
            {
                return a.ItemCreatedAt > GemstoneIntroductionDate;
            }
            // Auctions without unlocked_slots are always included
            return true;
        }).ToList();
    }
}
