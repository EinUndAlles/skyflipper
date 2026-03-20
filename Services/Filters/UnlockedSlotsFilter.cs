using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public sealed class UnlockedSlotsFilter : IFilter
{
    private readonly AppDbContext _dbContext;

    public UnlockedSlotsFilter(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string Name => "UnlockedSlots";
    public FilterType FilterType => FilterType.NUMERICAL | FilterType.RANGE;

    public IEnumerable<string> OptionsGet(FilterContext context) => new[] { "0", "5" };

    public IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        var raw = context.Get(Name);
        if (string.IsNullOrEmpty(raw))
            return query;

        var ranges = RangeParser.ParseLongRanges(raw);
        if (ranges.Count == 0)
            return query;

        var keyId = _dbContext.NBTKeys
            .Where(k => k.KeyName == "unlocked_slots")
            .Select(k => k.Id)
            .FirstOrDefault();

        if (keyId <= 0)
            return query;

        var min = ranges[0].Min;
        var max = ranges[0].Max;

        var auctionSlotCounts = _dbContext.NBTLookups
            .Where(l => l.KeyId == keyId)
            .GroupBy(l => l.AuctionId)
            .Select(g => new { AuctionId = g.Key, Count = g.Count() })
            .Where(x => x.Count >= min && x.Count <= max)
            .Select(x => x.AuctionId);

        return query.Where(a => auctionSlotCounts.Contains(a.Id));
    }
}
