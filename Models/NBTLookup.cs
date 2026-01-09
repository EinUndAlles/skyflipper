using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SkyFlipperSolo.Models;

/// <summary>
/// Stores important NBT key-value pairs for fast querying/filtering.
/// For example: dungeon_item_level=5, skin="DRAGON_NEON", heldItem="PET_ITEM_TIER_BOOST"
/// Based on Coflnet's NBTLookup table approach.
/// </summary>
public class NBTLookup
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the auction.
    /// </summary>
    public int AuctionId { get; set; }

    /// <summary>
    /// The NBT key (e.g., "dungeon_item_level", "heldItem", "skin")
    /// </summary>
    [MaxLength(50)]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Numeric value for numbers (stars, levels, counts, etc.)
    /// Null if the value is a string.
    /// </summary>
    public long? ValueNumeric { get; set; }

    /// <summary>
    /// String value for text (skins, items, scrolls, etc.)
    /// Null if the value is numeric.
    /// </summary>
    [MaxLength(100)]
    public string? ValueString { get; set; }

    /// <summary>
    /// Navigation property to auction.
    /// </summary>
    [ForeignKey("AuctionId")]
    public Auction? Auction { get; set; }
}
