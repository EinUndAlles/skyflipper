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
    private readonly TimeSpan _aggregationInterval = TimeSpan.FromMinutes(30); // Check every 30min

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
        _logger.LogInformation("PriceAggregationService starting (hourly + daily mode)...");

        // Wait for initial data collection
        await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Aggregate last hour
                await AggregateHourlyPrices(stoppingToken);

                // Once per day at midnight UTC, aggregate daily and cleanup
                if (DateTime.UtcNow.Hour == 0)
                {
                    await AggregateDailyPrices(stoppingToken);
                    await CleanupOldHourlyData(stoppingToken);
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
            .ToListAsync(stoppingToken);

        // Apply UID deduplication: for items with the same ItemUid, only take the earliest sale
        // Note: ItemUid column may not exist in database yet - handle gracefully
        var soldAuctions = allAuctions
            .GroupBy(a => a.ItemUid ?? a.Uuid) // Fallback to auction UUID if no item UID
            .Select(g => g.OrderBy(a => a.SoldAt ?? a.End).First()) // Take earliest sale
            .ToList();

        if (soldAuctions.Count == 0)
        {
            _logger.LogDebug("No sold auctions for hour {Hour}", previousHour);
            return;
        }

        // Group by cache key and calculate statistics
        // CRITICAL: Subtract gem value from each sale to get "base item" price
        // This matches reference project: FlippingEngine.cs line 340
        var aggregatesData = new List<(string CacheKey, List<double> Prices)>();
        
        foreach (var group in soldAuctions
            .Select(a => new
            {
                Auction = a,
                CacheKey = _cacheKeyService.GeneratePriceCacheKey(a)
            })
            .GroupBy(x => x.CacheKey))
        {
            var prices = new List<double>();
            foreach (var item in group)
            {
                var salePrice = (double)item.Auction.SoldPrice!.Value;
                // Subtract gem value to get base item value (matching reference project)
                var (gemValue, _) = await _componentValueService.GetGemstoneValueOnly(item.Auction);
                var baseValue = salePrice - gemValue;
                // Don't allow negative base values
                prices.Add(Math.Max(baseValue, salePrice * 0.1));
            }
            aggregatesData.Add((group.Key, prices));
        }

        var aggregates = aggregatesData
            .Select(x => new { CacheKey = x.CacheKey, Prices = x.Prices })
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
                TotalVolume = g.Sum(p => p.Volume)
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
                Volume = item.TotalVolume
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
    /// Deletes hourly price data older than 7 days.
    /// Runs once per day at midnight UTC.
    /// </summary>
    private async Task CleanupOldHourlyData(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoff = DateTime.UtcNow.AddDays(-7);
        var oldHourly = await dbContext.AveragePrices
            .Where(p => p.Granularity == PriceGranularity.Hourly && p.Timestamp < cutoff)
            .ToListAsync(stoppingToken);

        if (oldHourly.Count > 0)
        {
            dbContext.AveragePrices.RemoveRange(oldHourly);
            await dbContext.SaveChangesAsync(stoppingToken);

            _logger.LogInformation("🗑️ Cleaned up {Count} old hourly records (older than {Cutoff})",
                oldHourly.Count, cutoff);
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
            // Use reflection to get CacheKey and Prices properties
            var cacheKeyProp = item.GetType().GetProperty("CacheKey");
            var pricesProp = item.GetType().GetProperty("Prices");

            if (cacheKeyProp == null || pricesProp == null) continue;

            var cacheKey = cacheKeyProp.GetValue(item) as string;
            var prices = pricesProp.GetValue(item) as List<double>;

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
                Volume = sorted.Count
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
}
