using System;
using System.Collections.Generic;
using SkyFlipperSolo.Services;

namespace SkyFlipperSolo.Models;

/// <summary>
/// Response model for auction detail API with formatted properties.
/// </summary>
public class AuctionDetailResponse
{
    public int Id { get; set; }
    public string Uuid { get; set; } = string.Empty;
    public long? UId { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public int Count { get; set; }
    public long StartingBid { get; set; }
    public long HighestBidAmount { get; set; }
    public bool Bin { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string? AuctioneerId { get; set; }
    public Tier Tier { get; set; }
    public Category? Category { get; set; }
    public Reforge? Reforge { get; set; }
    public int AnvilUses { get; set; }
    public DateTime? ItemCreatedAt { get; set; }
    public DateTime FetchedAt { get; set; }
    public AuctionStatus Status { get; set; }
    public long? SoldPrice { get; set; }
    public DateTime? SoldAt { get; set; }
    public string? Texture { get; set; }
    public string? ItemUid { get; set; }

    // Formatted properties for display
    public List<ItemProperty>? Properties { get; set; }

    // Raw data for advanced users
    public List<Enchantment>? Enchantments { get; set; }
    public object[]? NbtLookups { get; set; }
    public List<BidRecord>? Bids { get; set; }
}