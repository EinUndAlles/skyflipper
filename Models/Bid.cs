using System.ComponentModel.DataAnnotations;

namespace SkyFlipperSolo.Models;

/// <summary>
/// Represents a bid on an auction (database record).
/// Tracks bid history for price discovery and sniper behavior analysis.
/// Based on Coflnet.Sky.Commands.MC.Bid pattern.
/// </summary>
public class BidRecord
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to auction this bid belongs to.
    /// </summary>
    public int AuctionId { get; set; }
    public Auction Auction { get; set; } = null!;

    /// <summary>
    /// UUID of bidder (without hyphens).
    /// </summary>
    [MaxLength(32)]
    [Required]
    public string BidderId { get; set; } = string.Empty;

    /// <summary>
    /// Bid amount in coins.
    /// </summary>
    public long Amount { get; set; }

    /// <summary>
    /// When bid was placed.
    /// </summary>
    public DateTime Timestamp { get; set; }
}
