using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services;

/// <summary>
/// Background service that aggregates daily price statistics from sold auctions.
/// Runs every hour to populate ItemPriceHistory table.
/// </summary>
public class PriceAggregationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PriceAggregationService> _logger;
    private const int AGGREGATION_INTERVAL_HOURS = 1;

    public PriceAggregationService(
        IServiceScopeFactory scopeFactory,
        ILogger<PriceAggregationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PriceAggregationService starting...");

        // Wait a bit before starting to let the system collect some data
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await AggregatePrices(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error aggregating prices");
            }

            await Task.Delay(TimeSpan.FromHours(AGGREGATION_INTERVAL_HOURS), stoppingToken);
        }

        _logger.LogInformation("PriceAggregationService stopped");
    }

    private async Task AggregatePrices(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var today = DateTime.UtcNow.Date;
        var yesterday = today.AddDays(-1);

        // Check if we've already aggregated for today
        var existingToday = await dbContext.PriceHistory
            .AnyAsync(p => p.Date == today, stoppingToken);

        if (existingToday)
        {
            _logger.LogDebug("Price aggregation already done for today");
            return;
        }

        // Get all sold auctions from the last 24 hours
        var cutoffTime = DateTime.UtcNow.AddHours(-24);
        var soldAuctions = await dbContext.Auctions
            .Where(a => a.Status == AuctionStatus.SOLD && 
                       a.SoldPrice != null &&
                       a.End >= cutoffTime)
            .Select(a => new { a.Tag, a.SoldPrice, a.Bin })
            .ToListAsync(stoppingToken);

        if (soldAuctions.Count == 0)
        {
            _logger.LogInformation("No sold auctions in last 24h, skipping aggregation");
            return;
        }

        // Group by tag and calculate statistics
        var priceHistory = soldAuctions
            .GroupBy(a => a.Tag)
            .Select(g => new ItemPriceHistory
            {
                ItemTag = g.Key,
                Date = today,
                MedianPrice = CalculateMedian(g.Select(a => a.SoldPrice!.Value)),
                AveragePrice = (long)g.Average(a => a.SoldPrice!.Value),
                LowestBIN = g.Where(a => a.Bin).Any() 
                    ? g.Where(a => a.Bin).Min(a => a.SoldPrice!.Value)
                    : g.Min(a => a.SoldPrice!.Value),
                TotalSales = g.Count()
            })
            .ToList();

        // Save to database
        foreach (var history in priceHistory)
        {
            // Use upsert to handle duplicates
            var existing = await dbContext.PriceHistory
                .FirstOrDefaultAsync(p => p.ItemTag == history.ItemTag && p.Date == today, stoppingToken);

            if (existing != null)
            {
                existing.MedianPrice = history.MedianPrice;
                existing.AveragePrice = history.AveragePrice;
                existing.LowestBIN = history.LowestBIN;
                existing.TotalSales = history.TotalSales;
            }
            else
            {
                dbContext.PriceHistory.Add(history);
            }
        }

        await dbContext.SaveChangesAsync(stoppingToken);
        _logger.LogInformation("📊 Aggregated prices for {Count} items (total {Sales} sales)", 
            priceHistory.Count, soldAuctions.Count);
    }

    private long CalculateMedian(IEnumerable<long> prices)
    {
        var sorted = prices.OrderBy(p => p).ToList();
        if (sorted.Count == 0) return 0;

        int mid = sorted.Count / 2;
        if (sorted.Count % 2 == 0)
            return (sorted[mid - 1] + sorted[mid]) / 2;
        else
            return sorted[mid];
    }
}
