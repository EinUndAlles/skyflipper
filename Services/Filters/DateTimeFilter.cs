using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public abstract class DateTimeFilter : IFilter, IApplicableFilter
{
    public abstract string Name { get; }
    public virtual FilterType FilterType => FilterType.DATE;
    public virtual bool IsApplicable(string tag) => true;

    public virtual IEnumerable<string> OptionsGet(FilterContext context) => Array.Empty<string>();

    public IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        var raw = context.Get(Name);
        if (string.IsNullOrEmpty(raw))
            return query;

        if (!long.TryParse(raw, out var unix))
            return query;

        var timestamp = DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
        return ApplyComparison(query, timestamp);
    }

    protected abstract IQueryable<Auction> ApplyComparison(IQueryable<Auction> query, DateTime timestamp);

    public virtual bool IsApplicable(FilterApplicabilityContext context) => true;
}
