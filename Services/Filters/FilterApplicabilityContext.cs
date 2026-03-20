using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public sealed class FilterApplicabilityContext
{
    public string Tag { get; }
    public Category? Category { get; }
    public bool HasEnchantments { get; }
    public HashSet<string> NbtKeys { get; }

    public FilterApplicabilityContext(string tag, Category? category, bool hasEnchantments, HashSet<string> nbtKeys)
    {
        Tag = tag;
        Category = category;
        HasEnchantments = hasEnchantments;
        NbtKeys = nbtKeys;
    }
}
