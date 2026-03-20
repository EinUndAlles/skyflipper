using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public sealed class WinningBidFilter : NbtNumberFilter
{
    public WinningBidFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "WinningBid";
    protected override string PropName => "winning_bid";

    protected override IQueryable<Auction> ApplyRanges(IQueryable<Auction> query, List<(long Min, long Max)> ranges, FilterContext context)
    {
        var keyId = _dbContext.NBTKeys
            .Where(k => k.KeyName == "winning_bid")
            .Select(k => k.Id)
            .FirstOrDefault();

        var extraKeyId = _dbContext.NBTKeys
            .Where(k => k.KeyName == "additional_coins")
            .Select(k => k.Id)
            .FirstOrDefault();

        if (keyId <= 0)
            return query;

        var auctionIds = _dbContext.NBTLookups
            .Where(n => (n.KeyId == keyId || n.KeyId == extraKeyId) && n.ValueNumeric.HasValue)
            .GroupBy(n => n.Auction!.Uuid)
            .Select(g => new { Uuid = g.Key, Sum = g.Sum(x => x.ValueNumeric ?? 0) })
            .Where(g => ranges.Any(r => g.Sum >= r.Min && g.Sum <= r.Max))
            .Select(g => g.Uuid);

        return query.Where(a => auctionIds.Contains(a.Uuid));
    }
}
