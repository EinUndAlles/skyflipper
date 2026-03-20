using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public sealed class EndBeforeFilter : DateTimeFilter
{
    public override string Name => "EndBefore";
    public override FilterType FilterType => FilterType.LOWER | FilterType.DATE;

    protected override IQueryable<Models.Auction> ApplyComparison(IQueryable<Models.Auction> query, DateTime timestamp)
    {
        return query.Where(a => a.End < timestamp);
    }
}
