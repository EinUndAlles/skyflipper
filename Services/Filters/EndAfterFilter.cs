using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public sealed class EndAfterFilter : DateTimeFilter
{
    public override string Name => "EndAfter";
    public override FilterType FilterType => FilterType.HIGHER | FilterType.DATE;

    protected override IQueryable<Models.Auction> ApplyComparison(IQueryable<Models.Auction> query, DateTime timestamp)
    {
        return query.Where(a => a.End > timestamp);
    }
}
