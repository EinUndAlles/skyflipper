using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public sealed class StarsFilter : IFilter
{
    private readonly AppDbContext _dbContext;

    public StarsFilter(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string Name => "Stars";
    public FilterType FilterType => FilterType.NUMERICAL | FilterType.RANGE;

    public IEnumerable<string> OptionsGet(FilterContext context) => new[] { "0", "1", "2", "3", "4", "5" };

    public IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        var value = context.Get(Name) ?? string.Empty;
        var (min, max, hasRange) = ParseRange(value);

        var starKeyId = _dbContext.NBTKeys
            .Where(k => k.KeyName == "upgrade_level" || k.KeyName == "dungeon_item_level")
            .Select(k => k.Id)
            .FirstOrDefault();

        if (starKeyId <= 0)
            return query;

        var starLookups = _dbContext.NBTLookups
            .Where(nbt => nbt.KeyId == starKeyId);

        if (hasRange)
        {
            if (min.HasValue)
                starLookups = starLookups.Where(nbt => nbt.ValueNumeric.HasValue && nbt.ValueNumeric.Value >= min.Value);
            if (max.HasValue)
                starLookups = starLookups.Where(nbt => nbt.ValueNumeric.HasValue && nbt.ValueNumeric.Value <= max.Value);
        }
        else if (min.HasValue)
        {
            starLookups = starLookups.Where(nbt => nbt.ValueNumeric.HasValue && nbt.ValueNumeric.Value == min.Value);
        }

        var ids = starLookups.Select(n => n.Auction!.Uuid);
        return query.Where(a => ids.Contains(a.Uuid));
    }

    private static (long? min, long? max, bool hasRange) ParseRange(string value)
    {
        var parts = value.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && long.TryParse(parts[0], out var min) && long.TryParse(parts[1], out var max))
            return (min, max, true);

        if (long.TryParse(value, out var exact))
            return (exact, exact, false);

        return (null, null, false);
    }
}
