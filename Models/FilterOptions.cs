namespace SkyFlipperSolo.Models;

/// <summary>
/// Options for a single filter
/// </summary>
public class FilterOptions
{
    /// <summary>
    /// Filter name (e.g., "Stars", "Enchantment")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Filter type (ENUM, NUMERICAL, RANGE, etc.)
    /// </summary>
    public FilterType Type { get; set; }

    /// <summary>
    /// Filter type as string for compatibility.
    /// </summary>
    public string LongType { get; set; } = string.Empty;

    /// <summary>
    /// Available options/values for this filter
    /// </summary>
    public string[] Options { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Optional description shown as tooltip/help text
    /// </summary>
    public string? Description { get; set; }
}
