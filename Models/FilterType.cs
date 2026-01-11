namespace SkyFlipperSolo.Models;

/// <summary>
/// Filter types enum matching hypixel-react implementation
/// </summary>
public enum FilterType
{
    EQUAL = 1,
    HIGHER = 2,
    LOWER = 4,
    DATE = 8,
    NUMERICAL = 16,
    RANGE = 32,
    PLAYER = 64,
    SIMPLE = 128,
    BOOLEAN = 256,
    PLAYER_WITH_RANK = 512,
    SHOW_ICON = 1024
}

/// <summary>
/// Checks if a flag is present in a FilterType enum value
/// </summary>
public static class FilterTypeHelper
{
    public static bool HasFlag(FilterType? full, FilterType flag)
    {
        return full.HasValue && (full.Value & flag) == flag;
    }
}
