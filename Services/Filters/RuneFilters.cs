using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

/// <summary>
/// Base class for rune level filters. Checks both RUNE_X and X key variants.
/// Rune levels are numeric 0-3.
/// </summary>
public abstract class RuneFilter : NumberFilterBase
{
    private readonly string _propName;
    private readonly string _shortPropName;
    protected readonly AppDbContext _dbContext;

    protected RuneFilter(AppDbContext dbContext, string propName)
    {
        _dbContext = dbContext;
        _propName = propName;
        _shortPropName = propName.StartsWith("RUNE_") ? propName["RUNE_".Length..] : propName;
    }

    public override string Name => _propName;
    public override IEnumerable<string> OptionsGet(FilterContext context) => new[] { "0", "3" };

    protected override IQueryable<Auction> ApplyRanges(IQueryable<Auction> query, List<(long Min, long Max)> ranges, FilterContext context)
    {
        var keyId1 = _dbContext.NBTKeys
            .Where(k => k.KeyName == _propName)
            .Select(k => k.Id)
            .FirstOrDefault();

        var keyId2 = _dbContext.NBTKeys
            .Where(k => k.KeyName == _shortPropName)
            .Select(k => k.Id)
            .FirstOrDefault();

        if (keyId1 <= 0 && keyId2 <= 0)
            return query;

        var min = ranges[0].Min;
        var max = ranges[0].Max;

        var auctionIds = _dbContext.NBTLookups
            .Where(n => (n.KeyId == keyId1 || n.KeyId == keyId2) && n.ValueNumeric.HasValue
                && n.ValueNumeric.Value >= min && n.ValueNumeric.Value <= max)
            .Select(n => n.Auction!.Uuid);

        return query.Where(a => auctionIds.Contains(a.Uuid));
    }
}

public sealed class MusicRuneFilter : RuneFilter
{
    public MusicRuneFilter(AppDbContext dbContext) : base(dbContext, "RUNE_MUSIC") { }
}

public sealed class EnchantRuneFilter : RuneFilter
{
    public EnchantRuneFilter(AppDbContext dbContext) : base(dbContext, "RUNE_ENCHANT") { }
}

public sealed class TidalRuneFilter : RuneFilter
{
    public TidalRuneFilter(AppDbContext dbContext) : base(dbContext, "RUNE_TIDAL") { }
}

public sealed class EndRuneFilter : RuneFilter
{
    public EndRuneFilter(AppDbContext dbContext) : base(dbContext, "RUNE_DRAGON") { }
}
