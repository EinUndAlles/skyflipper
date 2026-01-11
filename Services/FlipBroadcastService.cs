using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Hubs;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services;

/// <summary>
/// Background service that broadcasts flip opportunities to connected SignalR clients.
/// Polls the database every 5 seconds and notifies subscribers of updates.
/// </summary>
public class FlipBroadcastService : BackgroundService
{
    private const string FlipSubscribersGroup = "FlipSubscribers";
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<FlipHub> _hubContext;
    private readonly ILogger<FlipBroadcastService> _logger;
    private readonly HashSet<string> _previouslyBroadcastedUuids = new();
    private readonly HashSet<string> _activeBroadcastedUuids = new(); // Track active flips to detect sold ones
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

    public FlipBroadcastService(
        IServiceProvider serviceProvider,
        IHubContext<FlipHub> hubContext,
        ILogger<FlipBroadcastService> logger)
    {
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FlipBroadcastService started - polling every {Interval} seconds", _pollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await BroadcastFlipsAsync(stoppingToken);
                await CheckForSoldAuctionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting flips");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    private async Task BroadcastFlipsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Get active flip opportunities (not expired)
        var flips = await dbContext.FlipOpportunities
            .Where(f => f.AuctionEnd > DateTime.UtcNow)
            .OrderByDescending(f => f.EstimatedProfit)
            .Take(50) // Limit to top 50 flips
            .ToListAsync(cancellationToken);

        if (flips.Count == 0)
        {
            return;
        }

        // Convert to notifications
        var notifications = flips.Select(f => new FlipNotification
        {
            AuctionUuid = f.AuctionUuid,
            ItemTag = f.ItemTag,
            ItemName = f.ItemName,
            CurrentPrice = f.CurrentPrice,
            MedianPrice = f.MedianPrice,
            EstimatedProfit = f.EstimatedProfit,
            ProfitMarginPercent = f.ProfitMarginPercent,
            DetectedAt = f.DetectedAt,
            AuctionEnd = f.AuctionEnd,
            DataSource = f.DataSource
        }).ToList();

        // Broadcast all flips to subscribers
        await _hubContext.Clients.Group(FlipSubscribersGroup)
            .SendAsync("FlipsUpdated", notifications, cancellationToken);

        // Find and broadcast new flips
        var currentUuids = flips.Select(f => f.AuctionUuid).ToHashSet();
        var newFlips = notifications.Where(n => !_previouslyBroadcastedUuids.Contains(n.AuctionUuid)).ToList();

        if (newFlips.Count > 0)
        {
            _logger.LogDebug("Broadcasting {Count} new flips", newFlips.Count);
            foreach (var newFlip in newFlips)
            {
                await _hubContext.Clients.Group(FlipSubscribersGroup)
                    .SendAsync("NewFlip", newFlip, cancellationToken);
            }
        }

        // Update tracking set (keep only current flips to prevent memory bloat)
        _previouslyBroadcastedUuids.Clear();
        foreach (var uuid in currentUuids)
        {
            _previouslyBroadcastedUuids.Add(uuid);
            _activeBroadcastedUuids.Add(uuid);
        }
    }

    /// <summary>
    /// Check if any previously broadcasted flips are now sold and notify clients.
    /// </summary>
    private async Task CheckForSoldAuctionsAsync(CancellationToken cancellationToken)
    {
        if (_activeBroadcastedUuids.Count == 0)
            return;

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Check auction statuses for our tracked flips
        var uuidsToCheck = _activeBroadcastedUuids.ToList();
        var soldOrExpiredAuctions = await dbContext.Auctions
            .Where(a => uuidsToCheck.Contains(a.Uuid) && 
                       (a.Status != AuctionStatus.ACTIVE || a.End < DateTime.UtcNow))
            .Select(a => new { a.Uuid, a.Status })
            .ToListAsync(cancellationToken);

        foreach (var auction in soldOrExpiredAuctions)
        {
            // Notify clients this auction is no longer available
            await _hubContext.Clients.Group(FlipSubscribersGroup)
                .SendAsync("AuctionSold", auction.Uuid, cancellationToken);
            
            // Remove from tracking
            _activeBroadcastedUuids.Remove(auction.Uuid);
            
            _logger.LogDebug("Auction {Uuid} is no longer active (status: {Status})", 
                auction.Uuid, auction.Status);
        }
    }
}
