using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace SkyFlipperSolo.Models;

/// <summary>
/// Represents a detected flip opportunity.
/// </summary>
public class Flip
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// The auction that is underpriced.
    /// </summary>
    public int AuctionId { get; set; }

    [ForeignKey("AuctionId")]
    [JsonIgnore] // Prevent circular reference during serialization
    public Auction? Auction { get; set; }

    /// <summary>
    /// The current price of the auction.
    /// </summary>
    public long CurrentPrice { get; set; }

    /// <summary>
    /// The calculated median price based on similar auctions.
    /// </summary>
    public long MedianPrice { get; set; }

    /// <summary>
    /// The recommended sell price (90% of median).
    /// </summary>
    public long TargetPrice { get; set; }

    /// <summary>
    /// The potential profit (TargetPrice - CurrentPrice).
    /// </summary>
    public long Profit { get; set; }

    /// <summary>
    /// The percentage profit margin.
    /// </summary>
    public double ProfitPercent { get; set; }

    /// <summary>
    /// Number of reference auctions used for comparison.
    /// </summary>
    public int ReferenceCount { get; set; }

    /// <summary>
    /// When this flip was detected.
    /// </summary>
    public DateTime FoundAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether a notification was sent for this flip.
    /// </summary>
    public bool NotificationSent { get; set; }
}
