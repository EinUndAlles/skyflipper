using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

/// <summary>
/// Bottle of Jyrre intelligence bonus.
/// Coflnet stores as seconds; user input is in hours.
/// </summary>
public sealed class IntelligenceBonusFilter : NumberFilterBase
{
    private readonly AppDbContext _dbContext;

    public IntelligenceBonusFilter(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override string Name => "IntelligenceBonus";
    public override IEnumerable<string> OptionsGet(FilterContext context) => new[] { "0", "37" };

    protected override IQueryable<Auction> ApplyRanges(IQueryable<Auction> query, List<(long Min, long Max)> ranges, FilterContext context)
    {
        var keyId = _dbContext.NBTKeys
            .Where(k => k.KeyName == "bottle_of_jyrre_seconds")
            .Select(k => k.Id)
            .FirstOrDefault();

        if (keyId <= 0)
            return query;

        var min = ranges[0].Min * 3600;
        var max = (ranges[0].Max + 1) * 3600 - 1;

        var auctionIds = _dbContext.NBTLookups
            .Where(n => n.KeyId == keyId && n.ValueNumeric.HasValue
                && n.ValueNumeric.Value >= min && n.ValueNumeric.Value <= max)
            .Select(n => n.Auction!.Uuid);

        return query.Where(a => auctionIds.Contains(a.Uuid));
    }
}
