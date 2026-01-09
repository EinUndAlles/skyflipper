using System.ComponentModel.DataAnnotations;

namespace SkyFlipperSolo.Models;

/// <summary>
/// Stores aggregated price statistics for flip detection with hourly and daily granularity.
/// Replaces ItemPriceHistory with enhanced time-based tracking.
/// Based on Coflnet's AveragePrice table approach.
/// </summary>
public class AveragePrice
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// The item tag (e.g., "HYPERION", "PET_DRAGON")
    /// </summary>
    [MaxLength(60)]
    public string ItemTag { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp of this aggregate (for hourly: hour start time, for daily: date at midnight)
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Granularity of this aggregate (Hourly or Daily)
    /// </summary>
    public PriceGranularity Granularity { get; set; }

    /// <summary>
    /// Minimum sale price in this period
    /// </summary>
    public double Min { get; set; }

    /// <summary>
    /// Maximum sale price in this period
    /// </summary>
    public double Max { get; set; }

    /// <summary>
    /// Average sale price in this period
    /// </summary>
    public double Avg { get; set; }

    /// <summary>
    /// Median sale price in this period (primary metric for flip detection)
    /// </summary>
    public double Median { get; set; }

    /// <summary>
    /// Number of sales in this period
    /// </summary>
    public int Volume { get; set; }
}
