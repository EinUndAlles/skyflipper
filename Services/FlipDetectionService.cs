using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services;

/// <summary>
/// Background service that detects flip opportunities by comparing
/// current BIN prices against historical median prices.
/// </summary>
public class FlipDetectionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FlipDetectionService> _logger;
    private const int DETECTION_INTERVAL_SECONDS = 60;
    private const double MIN_PROFIT_MARGIN = 0.10; // 10%
    private const int MIN_SALES_THRESHOLD = 5; // Minimum sales to consider
    private const int PRICE_HISTORY_DAYS = 7; // Look back 7 days for median
    private const int MAX_FLIPS_TO_STORE = 200; // Limit stored flips

    public FlipDetectionService(
        IServiceScopeFactory scopeFactory,
        ILogger<FlipDetectionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
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
            .GroupBy(p => p.ItemTag)
            .Select(g => new
            {
                Tag = g.Key,
                MedianPrice = g.Average(p => p.Median),
                Volume = g.Sum(p => p.Volume),
                IsRecent = true
            })
            .ToDictionaryAsync(p => p.Tag, stoppingToken);

        // Step 2: Get daily prices for items without recent hourly data
        var hourlyTags = hourlyPrices.Keys.ToList();
        var dailyPrices = await dbContext.AveragePrices
            .Where(p => p.Granularity == PriceGranularity.Daily &&
                       p.Timestamp > now.AddDays(-PRICE_HISTORY_DAYS) &&
                       p.Volume >= MIN_SALES_THRESHOLD &&
                       !hourlyTags.Contains(p.ItemTag))
            .GroupBy(p => p.ItemTag)
            .Select(g => new
            {
                Tag = g.Key,
                MedianPrice = g.Average(p => p.Median),
                Volume = g.Sum(p => p.Volume),
                IsRecent = false
            })
            .ToDictionaryAsync(p => p.Tag, stoppingToken);

        // Step 3: Merge hourly and daily data (hourly takes priority)
        var allPrices = hourlyPrices
            .Concat(dailyPrices)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        // Step 4: Get active BIN auctions
        var activeAuctions = await dbContext.Auctions
            .Where(a => a.Bin &&
                       a.Status == AuctionStatus.ACTIVE &&
                       a.End > now &&
                       allPrices.Keys.Contains(a.Tag))
            .Select(a => new
            {
                a.Uuid,
                a.Tag,
                a.ItemName,
                a.StartingBid,
                a.HighestBidAmount,
                a.End
            })
            .ToListAsync(stoppingToken);

        var flips = new List<FlipOpportunity>();

        // Step 5: Calculate flips
        foreach (var auction in activeAuctions)
        {
            if (!allPrices.TryGetValue(auction.Tag, out var priceData))
                continue;

            if (priceData.Volume < MIN_SALES_THRESHOLD)
                continue;

            var currentPrice = auction.HighestBidAmount > 0
                ? auction.HighestBidAmount
                : auction.StartingBid;
            var medianPrice = (long)priceData.MedianPrice;

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
                    DataSource = priceData.IsRecent ? "Hourly (24h)" : "Daily (7d)"
                });
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
}
