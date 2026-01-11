using System.ComponentModel.DataAnnotations;

namespace SkyFlipperSolo.Models;

/// <summary>
/// Tracks how many times each item (by cache key) has been flagged as a flip.
/// Used for hit count decay to prevent "bait" auctions from repeatedly appearing.
/// </summary>
public class FlipHitCount
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// The cache key representing the item type.
    /// </summary>
    [MaxLength(200)]
    public string CacheKey { get; set; } = string.Empty;

    /// <summary>
    /// How many times this item has been flagged as a flip.
    /// </summary>
    public int HitCount { get; set; }

    /// <summary>
    /// When this was last flagged as a flip.
    /// </summary>
    public DateTime LastHitAt { get; set; }

    /// <summary>
    /// When this record was first created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}