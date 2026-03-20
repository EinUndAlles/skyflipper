namespace SkyFlipperSolo.Models;

/// <summary>
/// Filter types enum matching Coflnet SkyFilter
/// </summary>
[Flags]
public enum FilterType
{
    EQUAL = 1,
    HIGHER = 2,
    LOWER = 4,
    DATE = 8,
    NUMERICAL = 16,
    RANGE = 32,
    TEXT = 64,
    SIMPLE = 128,
    BOOLEAN = 256,
    PLAYER_WITH_RANK = 512,
    AppliedItem = 1024
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
