using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public sealed class CountFilter : NumberFilterBase
{
    public override string Name => "Count";

    protected override IQueryable<Auction> ApplyRanges(IQueryable<Auction> query, List<(long Min, long Max)> ranges, FilterContext context)
    {
        return query.Where(a => ranges.Any(r => a.Count >= r.Min && a.Count <= r.Max));
    }
}
