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
    /// Uses ExecuteUpdateAsync for bulk updates where possible.
    /// </summary>
    private async Task PerformLifecycleCleanup(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;

        // 1. OPTIMIZATION: Bulk update expired auctions using ExecuteUpdateAsync
        var expiredCount = await dbContext.Auctions
            .Where(a => a.Status == AuctionStatus.ACTIVE && a.End < now)
            .ExecuteUpdateAsync(
                s => s.SetProperty(a => a.Status, AuctionStatus.EXPIRED),
                stoppingToken);

        if (expiredCount > 0)
        {
            _logger.LogInformation("✅ Marked {Count} auctions as EXPIRED (ended before {Now})",
                expiredCount, now);
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
    /// Uses ExecuteDeleteAsync for efficient bulk deletion.
    /// </summary>
    private async Task CleanupOldEndedAuctions(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoffDate = DateTime.UtcNow.AddDays(-30); // Keep 30 days of history

        // Get IDs of auctions to delete (limit to prevent memory issues)
        var auctionIdsToDelete = await dbContext.Auctions
            .Where(a => (a.Status == AuctionStatus.SOLD || a.Status == AuctionStatus.EXPIRED) &&
                       a.End < cutoffDate)
            .Take(1000)
            .Select(a => a.Id)
            .ToListAsync(stoppingToken);

        if (auctionIdsToDelete.Count == 0) return;

        // OPTIMIZATION: Use ExecuteDeleteAsync for bulk deletion (EF Core 7+)
        // This generates efficient DELETE statements without loading entities
        
        // Delete related data first (due to foreign key constraints)
        var enchantmentsDeleted = await dbContext.Enchantments
            .Where(e => auctionIdsToDelete.Contains(e.AuctionId))
            .ExecuteDeleteAsync(stoppingToken);

        var lookupsDeleted = await dbContext.NBTLookups
            .Where(n => auctionIdsToDelete.Contains(n.AuctionId))
            .ExecuteDeleteAsync(stoppingToken);

        var bidsDeleted = await dbContext.BidRecords
            .Where(b => auctionIdsToDelete.Contains(b.AuctionId))
            .ExecuteDeleteAsync(stoppingToken);

        // Delete auctions
        var auctionsDeleted = await dbContext.Auctions
            .Where(a => auctionIdsToDelete.Contains(a.Id))
            .ExecuteDeleteAsync(stoppingToken);

        _logger.LogInformation("🗑️ Cleaned up {Count} old auctions with {Enchants} enchants, {Lookups} lookups, {Bids} bids (ended before {Cutoff})",
            auctionsDeleted, enchantmentsDeleted, lookupsDeleted, bidsDeleted, cutoffDate);
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
    /// Uses ExecuteDeleteAsync for efficient bulk operations.
    /// </summary>
    private async Task CleanupOldFlipHitCounts(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoffDate = DateTime.UtcNow.AddDays(-7); // Reset after 7 days of no hits

        // OPTIMIZATION: Use ExecuteUpdateAsync to decrement hit counts in bulk
        var decremented = await dbContext.FlipHitCounts
            .Where(h => h.LastHitAt < cutoffDate && h.HitCount > 0)
            .ExecuteUpdateAsync(
                s => s.SetProperty(h => h.HitCount, h => h.HitCount - 1),
                stoppingToken);

        // Delete records with zero hit count
        var deleted = await dbContext.FlipHitCounts
            .Where(h => h.HitCount <= 0)
            .ExecuteDeleteAsync(stoppingToken);

        if (decremented > 0 || deleted > 0)
        {
            _logger.LogInformation("🧹 Cleaned up flip hit counts: {Decremented} decremented, {Deleted} deleted",
                decremented, deleted);
        }
    }
}