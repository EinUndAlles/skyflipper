using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services;

/// <summary>
/// Comprehensive auction lifecycle management service.
/// Handles all auction state transitions and cleanup.
/// Based on Coflnet.Sky.Indexer pattern.
/// </summary>
public class AuctionLifecycleService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuctionLifecycleService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5); // Check every 5 minutes

    public AuctionLifecycleService(
        IServiceScopeFactory scopeFactory,
        ILogger<AuctionLifecycleService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AuctionLifecycleService starting - comprehensive lifecycle management...");

        // Initial cleanup on startup
        await PerformLifecycleCleanup(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformLifecycleCleanup(stoppingToken);
                await CleanupOldEndedAuctions(stoppingToken);
                await CleanupOldFlipHitCounts(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in auction lifecycle management");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("AuctionLifecycleService stopped");
    }

    /// <summary>
    /// Comprehensive lifecycle cleanup - ensures all auctions are in correct states.
    /// </summary>
    private async Task PerformLifecycleCleanup(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;

        // 1. Mark auctions that have ended but are still ACTIVE as EXPIRED
        var expiredAuctions = await dbContext.Auctions
            .Where(a => a.Status == AuctionStatus.ACTIVE && a.End < now)
            .ToListAsync(stoppingToken);

        if (expiredAuctions.Count > 0)
        {
            foreach (var auction in expiredAuctions)
            {
                auction.Status = AuctionStatus.EXPIRED;
            }

            await dbContext.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("✅ Marked {Count} auctions as EXPIRED (ended before {Now})",
                expiredAuctions.Count, now);
        }

        // 2. Check for auctions that should be SOLD but weren't caught by auctions_ended API
        // This handles edge cases where auctions_ended API missed some auctions
        var potentiallySoldAuctions = await dbContext.Auctions
            .Where(a => a.Status == AuctionStatus.ACTIVE &&
                       a.End < now.AddHours(-1) && // Ended more than 1 hour ago
                       a.Bin == true) // Only BIN auctions can be reliably checked
            .Take(100) // Limit to avoid overwhelming
            .ToListAsync(stoppingToken);

        if (potentiallySoldAuctions.Count > 0)
        {
            var auctionsToCheck = new List<Auction>();

            foreach (var auction in potentiallySoldAuctions)
            {
                // Check if there are any newer auctions with higher bids (indicating this one sold)
                var hasHigherBid = await dbContext.Auctions
                    .AnyAsync(a => a.Tag == auction.Tag &&
                                  a.Status == AuctionStatus.SOLD &&
                                  a.HighestBidAmount > auction.HighestBidAmount &&
                                  a.SoldAt > auction.End,
                           stoppingToken);

                if (hasHigherBid)
                {
                    auctionsToCheck.Add(auction);
                }
            }

            if (auctionsToCheck.Count > 0)
            {
                foreach (var auction in auctionsToCheck)
                {
                    auction.Status = AuctionStatus.EXPIRED; // Conservative: mark as expired, not sold
                }

                await dbContext.SaveChangesAsync(stoppingToken);
                _logger.LogInformation("✅ Conservatively marked {Count} potentially sold BIN auctions as EXPIRED",
                    auctionsToCheck.Count);
            }
        }

        // 3. Validate auction data integrity
        var integrityIssues = await ValidateAuctionIntegrity(dbContext, stoppingToken);
        if (integrityIssues > 0)
        {
            _logger.LogWarning("⚠️ Fixed {Count} auction data integrity issues", integrityIssues);
        }
    }

    /// <summary>
    /// Clean up very old ended auctions to prevent database bloat.
    /// </summary>
    private async Task CleanupOldEndedAuctions(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoffDate = DateTime.UtcNow.AddDays(-30); // Keep 30 days of history

        // Only clean up auctions that have been SOLD or EXPIRED for more than 30 days
        var oldAuctions = await dbContext.Auctions
            .Where(a => (a.Status == AuctionStatus.SOLD || a.Status == AuctionStatus.EXPIRED) &&
                       a.End < cutoffDate)
            .Take(1000) // Limit deletion batch size
            .ToListAsync(stoppingToken);

        if (oldAuctions.Count > 0)
        {
            // Remove associated data first (due to foreign key constraints)
            var auctionIds = oldAuctions.Select(a => a.Id).ToList();

            // Remove enchantments
            var enchantments = await dbContext.Enchantments
                .Where(e => auctionIds.Contains(e.AuctionId))
                .ToListAsync(stoppingToken);
            dbContext.Enchantments.RemoveRange(enchantments);

            // Remove NBT lookups
            var nbtLookups = await dbContext.NBTLookups
                .Where(n => auctionIds.Contains(n.AuctionId))
                .ToListAsync(stoppingToken);
            dbContext.NBTLookups.RemoveRange(nbtLookups);

            // Remove bids
            var bids = await dbContext.BidRecords
                .Where(b => auctionIds.Contains(b.AuctionId))
                .ToListAsync(stoppingToken);
            dbContext.BidRecords.RemoveRange(bids);

            // Remove auctions
            dbContext.Auctions.RemoveRange(oldAuctions);

            await dbContext.SaveChangesAsync(stoppingToken);

            _logger.LogInformation("🗑️ Cleaned up {Count} old auctions (ended before {Cutoff})",
                oldAuctions.Count, cutoffDate);
        }
    }

    /// <summary>
    /// Validate and fix auction data integrity issues.
    /// </summary>
    private async Task<int> ValidateAuctionIntegrity(AppDbContext dbContext, CancellationToken stoppingToken)
    {
        var fixes = 0;

        // Fix auctions with invalid status transitions
        var invalidSoldAuctions = await dbContext.Auctions
            .Where(a => a.Status == AuctionStatus.SOLD && !a.SoldPrice.HasValue && !a.SoldAt.HasValue)
            .ToListAsync(stoppingToken);

        if (invalidSoldAuctions.Count > 0)
        {
            foreach (var auction in invalidSoldAuctions)
            {
                // SOLD auctions must have SoldPrice and SoldAt
                auction.Status = AuctionStatus.EXPIRED; // Conservative fix
                fixes++;
            }
            await dbContext.SaveChangesAsync(stoppingToken);
        }

        // Fix auctions with future end dates that are marked as EXPIRED
        var invalidExpiredAuctions = await dbContext.Auctions
            .Where(a => a.Status == AuctionStatus.EXPIRED && a.End > DateTime.UtcNow)
            .ToListAsync(stoppingToken);

        if (invalidExpiredAuctions.Count > 0)
        {
            foreach (var auction in invalidExpiredAuctions)
            {
                auction.Status = AuctionStatus.ACTIVE; // Reactivate if end date is in future
                fixes++;
            }
            await dbContext.SaveChangesAsync(stoppingToken);
        }

// Fix auctions with negative prices (not zero - zero is valid for auctions with no bids)
        var invalidPriceAuctions = await dbContext.Auctions
            .Where(a => a.StartingBid < 0 || a.HighestBidAmount < 0)
            .ToListAsync(stoppingToken);

        if (invalidPriceAuctions.Count > 0)
        {
            foreach (var auction in invalidPriceAuctions)
            {
                if (auction.StartingBid < 0)
                    auction.StartingBid = 1; // Minimum valid price

                if (auction.HighestBidAmount < 0)
                    auction.HighestBidAmount = 0; // Reset to no bids

                fixes++;
            }
            await dbContext.SaveChangesAsync(stoppingToken);
        }

        return fixes;
    }

    /// <summary>
    /// Clean up old flip hit counts to prevent database bloat.
    /// Reset hit counts for items that haven't been flagged recently.
    /// </summary>
    private async Task CleanupOldFlipHitCounts(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoffDate = DateTime.UtcNow.AddDays(-7); // Reset after 7 days of no hits

        // Find hit counts that haven't been updated recently
        var oldHitCounts = await dbContext.FlipHitCounts
            .Where(h => h.LastHitAt < cutoffDate)
            .ToListAsync(stoppingToken);

        if (oldHitCounts.Count > 0)
        {
            // Reset hit counts rather than delete (preserve learning)
            foreach (var hitCount in oldHitCounts)
            {
                hitCount.HitCount = Math.Max(0, hitCount.HitCount - 1); // Gradually decay
                if (hitCount.HitCount == 0)
                {
                    dbContext.FlipHitCounts.Remove(hitCount);
                }
            }

            await dbContext.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("🧹 Cleaned up {Count} old flip hit counts", oldHitCounts.Count);
        }
    }
}