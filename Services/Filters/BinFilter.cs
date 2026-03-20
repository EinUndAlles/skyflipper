using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public sealed class BinFilter : IFilter
{
    public string Name => "Bin";
    public FilterType FilterType => FilterType.BOOLEAN;

    public IEnumerable<string> OptionsGet(FilterContext context) => new[] { "true", "false" };

    public IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        var value = context.Get(Name);
        if (string.IsNullOrEmpty(value) || !bool.TryParse(value, out var binOnly))
            return query;

        return query.Where(a => a.Bin == binOnly);
    }
}
