using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public sealed class StartingBidFilter : NumberFilterBase
{
    public override string Name => "StartingBid";

    protected override IQueryable<Auction> ApplyRanges(IQueryable<Auction> query, List<(long Min, long Max)> ranges, FilterContext context)
    {
        return query.Where(a => ranges.Any(r => a.StartingBid >= r.Min && a.StartingBid <= r.Max));
    }
}
