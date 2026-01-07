using System.Text.Json.Serialization;

namespace SkyFlipperSolo.Models;

/// <summary>
/// Response from the Hypixel Auctions API.
/// </summary>
public class AuctionPageResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("totalPages")]
    public int TotalPages { get; set; }

    [JsonPropertyName("totalAuctions")]
    public int TotalAuctions { get; set; }

    [JsonPropertyName("lastUpdated")]
    public long LastUpdated { get; set; }

    [JsonPropertyName("auctions")]
    public List<HypixelAuction> Auctions { get; set; } = new();

    /// <summary>
    /// Parse LastUpdated to DateTime.
    /// </summary>
    public DateTime LastUpdatedDateTime => DateTimeOffset.FromUnixTimeMilliseconds(LastUpdated).UtcDateTime;
}

/// <summary>
/// A single auction from the Hypixel API response.
/// </summary>
public class HypixelAuction
{
    [JsonPropertyName("uuid")]
    public string Uuid { get; set; } = string.Empty;

    [JsonPropertyName("auctioneer")]
    public string Auctioneer { get; set; } = string.Empty;

    [JsonPropertyName("profile_id")]
    public string? ProfileId { get; set; }

    [JsonPropertyName("start")]
    public long StartMs { get; set; }

    [JsonPropertyName("end")]
    public long EndMs { get; set; }

    [JsonPropertyName("item_name")]
    public string ItemName { get; set; } = string.Empty;

    [JsonPropertyName("item_lore")]
    public string? ItemLore { get; set; }

    [JsonPropertyName("extra")]
    public string? Extra { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("tier")]
    public string? Tier { get; set; }

    [JsonPropertyName("starting_bid")]
    public long StartingBid { get; set; }

    [JsonPropertyName("item_bytes")]
    public string ItemBytes { get; set; } = string.Empty;

    [JsonPropertyName("claimed")]
    public bool Claimed { get; set; }

    [JsonPropertyName("highest_bid_amount")]
    public long HighestBidAmount { get; set; }

    [JsonPropertyName("bids")]
    public List<HypixelBid>? Bids { get; set; }

    [JsonPropertyName("bin")]
    public bool Bin { get; set; }

    /// <summary>
    /// Parse StartMs to DateTime.
    /// </summary>
    public DateTime Start => DateTimeOffset.FromUnixTimeMilliseconds(StartMs).UtcDateTime;

    /// <summary>
    /// Parse EndMs to DateTime.
    /// </summary>
    public DateTime End => DateTimeOffset.FromUnixTimeMilliseconds(EndMs).UtcDateTime;
}

/// <summary>
/// A bid on an auction.
/// </summary>
public class HypixelBid
{
    [JsonPropertyName("auction_id")]
    public string AuctionId { get; set; } = string.Empty;

    [JsonPropertyName("bidder")]
    public string Bidder { get; set; } = string.Empty;

    [JsonPropertyName("profile_id")]
    public string? ProfileId { get; set; }

    [JsonPropertyName("amount")]
    public long Amount { get; set; }

    [JsonPropertyName("timestamp")]
    public long TimestampMs { get; set; }

    public DateTime Timestamp => DateTimeOffset.FromUnixTimeMilliseconds(TimestampMs).UtcDateTime;
}
