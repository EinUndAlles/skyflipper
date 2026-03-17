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
        
        // Note: Expired auction cleanup is handled by AuctionLifecycleService to avoid deadlocks
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Check Hypixel's auctions_ended API for sold auctions
                await CheckForSoldAuctions(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in sold auction service");
            }
            
            await Task.Delay(_checkInterval, stoppingToken);
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
            _logger.LogDebug("No auctions in auctions_ended response");
            return;
        }

        _logger.LogInformation("📥 Received {Count} ended auctions from Hypixel API", endedData.Auctions.Count);

        // Get the UUIDs of ended auctions (API returns auction_id field)
        var endedUuids = endedData.Auctions
            .Select(a => a.AuctionId?.Replace("-", "")?.ToLower())
            .Where(u => !string.IsNullOrEmpty(u))
            .ToHashSet();

        if (endedUuids.Count == 0)
        {
            _logger.LogWarning("All auction_id fields were null/empty");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Find auctions in our DB that match ended UUIDs
        // Note: endedUuids are already lowercase without dashes, DB stores without dashes
        var endedUuidsList = endedUuids.ToList(); // Convert to list for EF Core compatibility
        var auctionsToMark = await dbContext.Auctions
            .Where(a => a.Status == AuctionStatus.ACTIVE && endedUuidsList.Contains(a.Uuid.ToLower()))
            .ToListAsync(stoppingToken);

        // Also log total active auctions in DB for debugging
        var totalActiveAuctions = await dbContext.Auctions
            .Where(a => a.Status == AuctionStatus.ACTIVE)
            .CountAsync(stoppingToken);

        _logger.LogInformation("🔍 Found {MatchCount} matching auctions in our DB (out of {EndedCount} ended, {TotalActive} active in DB)", 
            auctionsToMark.Count, endedUuids.Count, totalActiveAuctions);

        // Log sample IDs for debugging if no matches
        if (auctionsToMark.Count == 0 && totalActiveAuctions > 0)
        {
            // Check if ended auctions are already processed (SOLD status) or truly missing
            var sampleEndedIds = endedUuids.Take(5).ToList();
            var alreadySold = await dbContext.Auctions
                .Where(a => sampleEndedIds.Contains(a.Uuid.ToLower()) && a.Status == AuctionStatus.SOLD)
                .CountAsync(stoppingToken);
            
            if (alreadySold > 0)
            {
                // This is normal - auctions_ended API returns same data for ~60s
                _logger.LogDebug("Ended auctions already processed (SOLD). Waiting for new data from Hypixel API.");
            }
            else
            {
                // This is unexpected - auctions not in DB at all
                var sampleDbIds = await dbContext.Auctions
                    .Where(a => a.Status == AuctionStatus.ACTIVE)
                    .OrderBy(a => a.Id)
                    .Take(3)
                    .Select(a => a.Uuid)
                    .ToListAsync(stoppingToken);
                
                _logger.LogWarning("⚠️ No matches! Sample ended IDs: [{EndedIds}]", string.Join(", ", sampleEndedIds.Take(3)));
                _logger.LogWarning("⚠️ No matches! Sample DB IDs: [{DbIds}]", string.Join(", ", sampleDbIds));
            }
        }

        if (auctionsToMark.Count > 0)
        {
            // Build a dictionary for quick lookup of ended info.
            // Hypixel includes a unix ms timestamp; using it avoids skewing our price history windows.
            var endedById = endedData.Auctions
                .Where(a => !string.IsNullOrEmpty(a.AuctionId))
                .ToDictionary(
                    a => a.AuctionId!.Replace("-", "").ToLowerInvariant(),
                    a => a);

            foreach (var auction in auctionsToMark)
            {
                auction.Status = AuctionStatus.SOLD;

                if (endedById.TryGetValue(auction.Uuid.ToLowerInvariant(), out var ended))
                {
                    auction.SoldPrice = ended.Price;
                    try
                    {
                        auction.SoldAt = DateTimeOffset.FromUnixTimeMilliseconds(ended.Timestamp).UtcDateTime;
                    }
                    catch
                    {
                        auction.SoldAt = DateTime.UtcNow;
                    }
                }
                else
                {
                    auction.SoldAt = DateTime.UtcNow;
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
    // Hypixel API uses "auction_id" not "uuid"
    [System.Text.Json.Serialization.JsonPropertyName("auction_id")]
    public string? AuctionId { get; set; }
    
    public string? Seller { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("seller_profile")]
    public string? SellerProfile { get; set; }
    
    public string? Buyer { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("buyer_profile")]
    public string? BuyerProfile { get; set; }
    
    public long Price { get; set; }
    
    // API uses "bin" not "BuyItemNow"
    public bool Bin { get; set; }
    
    public long Timestamp { get; set; }
    
    // item_bytes contains the NBT data for the item
    [System.Text.Json.Serialization.JsonPropertyName("item_bytes")]
    public string? ItemBytes { get; set; }
}
