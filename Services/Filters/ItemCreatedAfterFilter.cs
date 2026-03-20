namespace SkyFlipperSolo.Services.Filters;

public sealed class ItemCreatedAfterFilter : DateTimeFilter
{
    public override string Name => "ItemCreatedAfter";

    protected override IQueryable<Models.Auction> ApplyComparison(IQueryable<Models.Auction> query, DateTime timestamp)
    {
        return query.Where(a => a.ItemCreatedAt > timestamp);
    }
}
