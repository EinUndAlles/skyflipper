using Microsoft.AspNetCore.SignalR;

namespace SkyFlipperSolo.Hubs;

/// <summary>
/// SignalR hub for real-time flip notifications.
/// Clients can subscribe to receive live flip updates.
/// </summary>
public class FlipHub : Hub
{
    private const string FlipSubscribersGroup = "FlipSubscribers";
    private readonly ILogger<FlipHub> _logger;

    public FlipHub(ILogger<FlipHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Subscribe to flip notifications.
    /// </summary>
    public async Task SubscribeToFlips()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, FlipSubscribersGroup);
        _logger.LogInformation("Client {ConnectionId} subscribed to flips", Context.ConnectionId);
    }

    /// <summary>
    /// Unsubscribe from flip notifications.
    /// </summary>
    public async Task UnsubscribeFromFlips()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, FlipSubscribersGroup);
        _logger.LogInformation("Client {ConnectionId} unsubscribed from flips", Context.ConnectionId);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
/// DTO sent to clients when flips are updated.
/// </summary>
public class FlipNotification
{
    public string AuctionUuid { get; set; } = string.Empty;
    public string ItemTag { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public long CurrentPrice { get; set; }
    public long MedianPrice { get; set; }
    public long EstimatedProfit { get; set; }
    public double ProfitMarginPercent { get; set; }
    public DateTime DetectedAt { get; set; }
    public DateTime AuctionEnd { get; set; }
    public string DataSource { get; set; } = string.Empty;
}
