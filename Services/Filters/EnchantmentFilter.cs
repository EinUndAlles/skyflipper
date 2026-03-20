using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public sealed class EnchantmentFilter : IFilter
{
    public string Name => "Enchantment";
    public FilterType FilterType => FilterType.EQUAL;

    public IEnumerable<string> OptionsGet(FilterContext context) => Enum.GetNames(typeof(EnchantmentType)).OrderBy(n => n);

    public IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        var value = context.Get(Name);
        if (string.IsNullOrEmpty(value))
            return query;

        if (!Enum.TryParse<EnchantmentType>(value, true, out var enchantType))
            return query;

        if (enchantType == EnchantmentType.unknown)
            return query.Where(a => a.Enchantments == null || a.Enchantments.Count == 0);

        if (context.Filters.ContainsKey("EnchantLvl") || context.Filters.ContainsKey("EnchantLvlFilter"))
            return query;

        return query.Where(a => a.Enchantments.Any(e => e.Type == enchantType));
    }
}
