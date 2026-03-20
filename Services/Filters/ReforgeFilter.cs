using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public sealed class ReforgeFilter : IFilter
{
    public string Name => "Reforge";
    public FilterType FilterType => FilterType.EQUAL;

    public IEnumerable<string> OptionsGet(FilterContext context) => Enum.GetNames(typeof(Reforge));

    public IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        var value = context.Get(Name);
        if (string.IsNullOrEmpty(value) || !Enum.TryParse<Reforge>(value, true, out var reforge))
            return query;

        return query.Where(a => a.Reforge == reforge);
    }
}
