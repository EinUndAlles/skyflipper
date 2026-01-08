using System.ComponentModel.DataAnnotations;

namespace SkyFlipperSolo.Models;

/// <summary>
/// Stores aggregated daily price statistics for flip detection.
/// Populated by PriceAggregationService.
/// </summary>
public class ItemPriceHistory
{
    [Key]
    public int Id { get; set; }
    
    [MaxLength(60)]
    public string ItemTag { get; set; } = string.Empty;
    
    public DateTime Date { get; set; }
    
    /// <summary>
    /// Median sale price for this item on this date.
    /// Primary metric for flip detection.
    /// </summary>
    public long MedianPrice { get; set; }
    
    /// <summary>
    /// Average sale price for this item on this date.
    /// </summary>
    public long AveragePrice { get; set; }
    
    /// <summary>
    /// Lowest BIN price seen on this date.
    /// </summary>
    public long LowestBIN { get; set; }
    
    /// <summary>
    /// Total number of sales for this item on this date.
    /// </summary>
    public int TotalSales { get; set; }
}
