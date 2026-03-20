using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public class PerfectGemsCountFilter : NumberFilterBase
{
    protected readonly AppDbContext _dbContext;

    protected virtual string PropertyValueName => "PERFECT";

    public PerfectGemsCountFilter(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override string Name => "PerfectGemsCount";
    public override IEnumerable<string> OptionsGet(FilterContext context) => new[] { "0", "5" };

    protected override IQueryable<Auction> ApplyRanges(IQueryable<Auction> query, List<(long Min, long Max)> ranges, FilterContext context)
    {
        var (min, max) = ranges[0];

        var matchingKeyIds = _dbContext.NBTValues
            .Where(v => v.Value == PropertyValueName)
            .Select(v => v.KeyId)
            .Distinct()
            .ToList();

        if (matchingKeyIds.Count == 0)
            return query.Where(a => min == 0);

        var auctionGemCounts = _dbContext.NBTLookups
            .Where(l => l.KeyId.HasValue && matchingKeyIds.Contains(l.KeyId.Value) && l.ValueId.HasValue)
            .Join(_dbContext.NBTValues,
                l => new { Kid = l.KeyId!.Value, Vid = l.ValueId!.Value },
                v => new { Kid = v.KeyId, Vid = v.Id },
                (l, v) => new { l.AuctionId, v.Value })
            .Where(x => x.Value == PropertyValueName)
            .GroupBy(x => x.AuctionId)
            .Select(g => new { AuctionId = g.Key, Count = g.Count() })
            .Where(x => x.Count >= min && x.Count <= max)
            .Select(x => x.AuctionId);

        return query.Where(a => auctionGemCounts.Contains(a.Id));
    }
}
