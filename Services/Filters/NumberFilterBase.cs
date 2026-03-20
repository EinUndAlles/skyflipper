using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public abstract class NumberFilterBase : IFilter
{
    public abstract string Name { get; }
    public virtual FilterType FilterType => FilterType.NUMERICAL | FilterType.RANGE;
    public virtual IEnumerable<string> OptionsGet(FilterContext context) => new[] { "0", "50000000000" };

    public IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        var raw = context.Get(Name);
        var ranges = RangeParser.ParseLongRanges(raw);
        if (ranges.Count == 0)
            return query;

        return ApplyRanges(query, ranges, context);
    }

    protected abstract IQueryable<Auction> ApplyRanges(IQueryable<Auction> query, List<(long Min, long Max)> ranges, FilterContext context);
}
