using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public sealed class RarityFilter : IFilter
{
    public string Name => "Rarity";
    public FilterType FilterType => FilterType.EQUAL;

    public IEnumerable<string> OptionsGet(FilterContext context) => Enum.GetNames(typeof(Tier));

    public IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        var value = context.Get(Name);
        if (string.IsNullOrEmpty(value) || !Enum.TryParse<Tier>(value, true, out var tier))
            return query;

        return query.Where(a => a.Tier == tier);
    }
}
