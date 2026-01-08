using System.ComponentModel.DataAnnotations;

namespace SkyFlipperSolo.Models;

/// <summary>
/// Represents a detected flip opportunity.
/// Cached by FlipDetectionService for the /api/flips endpoint.
/// </summary>
public class FlipOpportunity
{
    [Key]
    public int Id { get; set; }
    
    [MaxLength(32)]
    public string AuctionUuid { get; set; } = string.Empty;
    
    [MaxLength(60)]
    public string ItemTag { get; set; } = string.Empty;
    
    public string ItemName { get; set; } = string.Empty;
    
    public long CurrentPrice { get; set; }
    
    public long MedianPrice { get; set; }
    
    /// <summary>
    /// Expected profit if bought and resold at median.
    /// </summary>
    public long EstimatedProfit { get; set; }
    
    /// <summary>
    /// Profit margin as a percentage (0-100).
    /// </summary>
    public double ProfitMarginPercent { get; set; }
    
    /// <summary>
    /// When this flip was detected.
    /// </summary>
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When the auction ends.
    /// </summary>
    public DateTime AuctionEnd { get; set; }
}
