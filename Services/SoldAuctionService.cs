using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;
using System.Text.Json;

namespace SkyFlipperSolo.Services;

/// <summary>
/// Service that tracks sold/ended auctions using Hypixel's auctions_ended API.
/// </summary>
public class SoldAuctionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SoldAuctionService> _logger;
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30); // Check every 30s

    public SoldAuctionService(
        IServiceScopeFactory scopeFactory,
        ILogger<SoldAuctionService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("HypixelApi");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SoldAuctionService starting...");
        
        // Initial cleanup: mark all past-end-date auctions as sold
        await CleanupOldAuctions(stoppingToken);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Check Hypixel's auctions_ended API for sold auctions
                await CheckForSoldAuctions(stoppingToken);
                
                // Also cleanup any auctions that have passed their end date
                await CleanupOldAuctions(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in sold auction service");
            }
            
            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task CleanupOldAuctions(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var now = DateTime.UtcNow;
            var oldAuctions = await dbContext.Auctions
                .Where(a => a.End < now && a.Status == AuctionStatus.ACTIVE)
                .ToListAsync(stoppingToken);

            if (oldAuctions.Count > 0)
            {
                foreach (var auction in oldAuctions)
                {
                    auction.Status = AuctionStatus.EXPIRED;
                }
                
                await dbContext.SaveChangesAsync(stoppingToken);
                _logger.LogInformation("Marked {Count} old ended auctions as EXPIRED", oldAuctions.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cleanup");
        }
    }

    private async Task CheckForSoldAuctions(CancellationToken stoppingToken)
    {
        // Call Hypixel's auctions_ended API
        var response = await _httpClient.GetAsync("auctions_ended", stoppingToken);
        
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to fetch auctions_ended: {Status}", response.StatusCode);
            return;
        }

        var content = await response.Content.ReadAsStringAsync(stoppingToken);
        var endedData = JsonSerializer.Deserialize<AuctionsEndedResponse>(content, new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        });

        if (endedData?.Auctions == null || endedData.Auctions.Count == 0)
        {
            return;
        }

        // Get the UUIDs of ended auctions
        var endedUuids = endedData.Auctions
            .Select(a => a.Uuid?.Replace("-", "")?.ToLower())
            .Where(u => !string.IsNullOrEmpty(u))
            .ToHashSet();

        if (endedUuids.Count == 0) return;

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Find auctions in our DB that match ended UUIDs
        var auctionsToMark = await dbContext.Auctions
            .Where(a => endedUuids.Contains(a.Uuid.ToLower()) && a.Status == AuctionStatus.ACTIVE)
            .ToListAsync(stoppingToken);

        if (auctionsToMark.Count > 0)
        {
            // Build a dictionary for quick lookup of sold prices
            var soldPrices = endedData.Auctions
                .Where(a => a.Uuid != null)
                .ToDictionary(
                    a => a.Uuid!.Replace("-", "").ToLower(),
                    a => a.Price);

            foreach (var auction in auctionsToMark)
            {
                auction.Status = AuctionStatus.SOLD;
                
                // Set the sold price from the API data
                if (soldPrices.TryGetValue(auction.Uuid.ToLower(), out var soldPrice))
                {
                    auction.SoldPrice = soldPrice;
                }
            }
            
            await dbContext.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("✅ Marked {Count} auctions as SOLD with prices", auctionsToMark.Count);
        }
    }
}

/// <summary>
/// Response model for Hypixel's auctions_ended API
/// </summary>
public class AuctionsEndedResponse
{
    public bool Success { get; set; }
    public long LastUpdated { get; set; }
    public List<EndedAuction> Auctions { get; set; } = new();
}

public class EndedAuction
{
    public string? Uuid { get; set; }
    public string? Seller { get; set; }
    public string? ProfileId { get; set; }
    public string? Buyer { get; set; }
    public long Price { get; set; }
    public bool BuyItemNow { get; set; }
    public long Timestamp { get; set; }
}
