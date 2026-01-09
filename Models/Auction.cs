using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SkyFlipperSolo.Models;

/// <summary>
/// Represents an auction from the Hypixel Skyblock API.
/// Based on Coflnet.Sky.Core.SaveAuction
/// </summary>
public class Auction
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// The unique auction UUID from Hypixel (32 chars, no dashes).
    /// </summary>
    [MaxLength(32)]
    public string Uuid { get; set; } = string.Empty;

    /// <summary>
    /// Numeric ID derived from the UUID for faster lookups.
    /// </summary>
    public long UId { get; set; }

    /// <summary>
    /// The internal item tag (e.g., "HYPERION", "PET_DRAGON").
    /// </summary>
    [MaxLength(60)]
    public string Tag { get; set; } = string.Empty;

    /// <summary>
    /// The display name of the item.
    /// </summary>
    [MaxLength(120)]
    public string ItemName { get; set; } = string.Empty;

    /// <summary>
    /// Number of items in this auction (usually 1, can be 64 for stacks).
    /// </summary>
    public int Count { get; set; } = 1;

    /// <summary>
    /// The starting bid price in coins.
    /// </summary>
    public long StartingBid { get; set; }

    /// <summary>
    /// The highest bid amount (or BIN price if sold).
    /// </summary>
    public long HighestBidAmount { get; set; }

    /// <summary>
    /// Whether this is a Buy It Now auction.
    /// </summary>
    public bool Bin { get; set; }

    /// <summary>
    /// When the auction started.
    /// </summary>
    public DateTime Start { get; set; }

    /// <summary>
    /// When the auction ends (or ended).
    /// </summary>
    public DateTime End { get; set; }

    /// <summary>
    /// The UUID of the player who created the auction.
    /// </summary>
    [MaxLength(32)]
    public string? AuctioneerId { get; set; }

    /// <summary>
    /// The tier/rarity of the item.
    /// </summary>
    public Tier Tier { get; set; } = Tier.UNKNOWN;

    /// <summary>
    /// The category of the item.
    /// </summary>
    public Category Category { get; set; } = Category.UNKNOWN;

    /// <summary>
    /// The reforge applied to the item.
    /// </summary>
    public Reforge Reforge { get; set; } = Reforge.None;

    /// <summary>
    /// Number of anvil uses on this item.
    /// </summary>
    public short AnvilUses { get; set; }

    /// <summary>
    /// When the item was originally created.
    /// </summary>
    public DateTime ItemCreatedAt { get; set; }

    /// <summary>
    /// Enchantments on the item.
    /// </summary>
    public List<Enchantment> Enchantments { get; set; } = new();

    /// <summary>
    /// Flattened NBT data stored as JSON string for flexible querying.
    /// Contains stars, gems, hot potato books, etc.
    /// </summary>
    [Column(TypeName = "text")]
    public string? FlatenedNBTJson { get; set; }

    /// <summary>
    /// Foreign key to NbtData table (stores full compressed NBT).
    /// </summary>
    public int? NbtDataId { get; set; }

    /// <summary>
    /// Navigation property to full NBT data.
    /// </summary>
    [ForeignKey("NbtDataId")]
    public NbtData? NbtData { get; set; }

    /// <summary>
    /// Important NBT key-value pairs extracted for fast filtering.
    /// </summary>
    public List<NBTLookup> NBTLookups { get; set; } = new();

    /// <summary>
    /// When this auction was first fetched from the API.
    /// </summary>
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The status of the auction (Active, Sold, or Expired).
    /// </summary>
    public AuctionStatus Status { get; set; } = AuctionStatus.ACTIVE;

    /// <summary>
    /// The final price at which this auction sold (from auctions_ended API).
    /// Only populated for SOLD auctions. Different from HighestBidAmount for regular auctions.
    /// </summary>
    public long? SoldPrice { get; set; }

    /// <summary>
    /// The Base64 encoded texture value for the skull (if applicable), or the skin URL.
    /// </summary>
    [MaxLength(500)]
    public string? Texture { get; set; }

    /// <summary>
    /// Bids placed on this auction.
    /// </summary>
    public List<Bid> Bids { get; set; } = new();

    /// <summary>
    /// Temporary storage for raw NBT bytes (not saved to database).
    /// Used by FlipperService to create NbtData and NBTLookups after auction is saved.
    /// </summary>
    [NotMapped]
    public string? RawNbtBytes { get; set; }
}
