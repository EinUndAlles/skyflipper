using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public sealed class EnchantLvlFilter : NumberFilterBase
{
    public override string Name => "EnchantLvl";

    public override IEnumerable<string> OptionsGet(FilterContext context) => new[] { "1", "10" };

    protected override IQueryable<Auction> ApplyRanges(IQueryable<Auction> query, List<(long Min, long Max)> ranges, FilterContext context)
    {
        var enchantValue = GetEnchantValue(context);
        if (!enchantValue.HasValue)
            return query;

        var (min, max) = ranges[0];
        return query.Where(a => a.Enchantments.Any(e => e.Type == enchantValue.Value && e.Level >= min && e.Level <= max));
    }

    private static EnchantmentType? GetEnchantValue(FilterContext context)
    {
        var raw = context.Get("Enchantment") ?? context.Get("Enchant") ?? context.Get("EnchantType");
        if (string.IsNullOrEmpty(raw))
            return null;
        if (!Enum.TryParse<EnchantmentType>(raw, true, out var enchantType))
            return null;
        return enchantType;
    }
}
