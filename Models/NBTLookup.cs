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
    /// Foreign key to the auction this lookup belongs to.
    /// </summary>
    public int AuctionId { get; set; }
    public Auction Auction { get; set; } = null!;

    /// <summary>
    /// The NBT key ID (normalized). Replaces Key for storage efficiency.
    /// E.g., KeyId=42 refers to "dungeon_item_level" in NBTKeys table.
    /// </summary>
    public short? KeyId { get; set; }
    
    /// <summary>
    /// Navigation property to NBTKey.
    /// </summary>
    public NBTKey? NBTKey { get; set; }

    /// <summary>
    /// DEPRECATED: The NBT key name (e.g., "dungeon_item_level").
    /// Keeping temporarily for migration. Will be removed after migrating to KeyId.
    /// </summary>
    [MaxLength(50)]
    public string? Key { get; set; }

    /// <summary>
    /// Numeric value if the NBT value is a number.
    /// </summary>
    public double? ValueNumeric { get; set; }

    /// <summary>
    /// String value if the NBT value is a string (e.g., "RUBY" for gem type).
    /// </summary>
    [MaxLength(100)]
    public string? ValueString { get; set; }
}
