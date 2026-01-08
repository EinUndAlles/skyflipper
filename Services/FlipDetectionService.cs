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

        // Get price history from last 7 days
        var cutoffDate = DateTime.UtcNow.Date.AddDays(-PRICE_HISTORY_DAYS);
        var priceHistory = await dbContext.PriceHistory
            .Where(p => p.Date >= cutoffDate && p.TotalSales >= MIN_SALES_THRESHOLD)
            .ToListAsync(stoppingToken);

        // Calculate median of medians for each item tag
        var priceMap = priceHistory
            .GroupBy(p => p.ItemTag)
            .ToDictionary(
                g => g.Key,
                g => new 
                { 
                    MedianPrice = (long)g.Average(p => p.MedianPrice), // Average of daily medians
                    TotalSales = g.Sum(p => p.TotalSales)
                });

        // Get active BIN auctions
        var now = DateTime.UtcNow;
        var activeAuctions = await dbContext.Auctions
            .Where(a => a.Bin && 
                       a.Status == AuctionStatus.ACTIVE && 
                       a.End > now)
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

        foreach (var auction in activeAuctions)
        {
            if (!priceMap.TryGetValue(auction.Tag, out var priceData))
                continue;

            // Only consider items with enough sales data
            if (priceData.TotalSales < MIN_SALES_THRESHOLD)
                continue;

            var currentPrice = auction.HighestBidAmount > 0 
                ? auction.HighestBidAmount 
                : auction.StartingBid;
            var medianPrice = priceData.MedianPrice;

            // Skip if median price is too low (likely junk items)
            if (medianPrice < 100000) // 100k coins minimum
                continue;

            // Calculate profit margin
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
                    AuctionEnd = auction.End
                });
            }
        }

        // Sort by profit margin and take top results
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
            _logger.LogInformation("🔥 Detected {Count} flips! Top: {Item} (-{Margin:F1}%, profit: {Profit:N0})",
                flips.Count,
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
