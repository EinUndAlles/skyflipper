using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public sealed class HighestBidFilter : NumberFilterBase
{
    public override string Name => "HighestBid";

    protected override IQueryable<Auction> ApplyRanges(IQueryable<Auction> query, List<(long Min, long Max)> ranges, FilterContext context)
    {
        return query.Where(a => ranges.Any(r => a.HighestBidAmount >= r.Min && a.HighestBidAmount <= r.Max));
    }
}
