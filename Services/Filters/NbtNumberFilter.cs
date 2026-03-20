using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public abstract class NbtNumberFilter : NumberFilterBase
{
    private readonly string? _propName;
    protected virtual string PropName => _propName ?? throw new InvalidOperationException("PropName must be overridden or provided via constructor");
    public override string Name => PropName;
    protected readonly AppDbContext _dbContext;

    protected NbtNumberFilter(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    protected NbtNumberFilter(AppDbContext dbContext, string propName)
    {
        _dbContext = dbContext;
        _propName = propName;
    }

    protected override IQueryable<Auction> ApplyRanges(IQueryable<Auction> query, List<(long Min, long Max)> ranges, FilterContext context)
    {
        var keyId = _dbContext.NBTKeys
            .Where(k => k.KeyName == PropName)
            .Select(k => k.Id)
            .FirstOrDefault();

        if (keyId <= 0)
            return query;

        var auctionIds = _dbContext.NBTLookups
            .Where(n => n.KeyId == keyId && n.ValueNumeric.HasValue)
            .Where(n => ranges.Any(r => n.ValueNumeric.HasValue && n.ValueNumeric.Value >= r.Min && n.ValueNumeric.Value <= r.Max))
            .Select(n => n.Auction!.Uuid);

        return query.Where(a => auctionIds.Contains(a.Uuid));
    }
}
