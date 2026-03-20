using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public sealed class GemFilter : IFilter
{
    private readonly AppDbContext _dbContext;
    private readonly string _slotName;

    public GemFilter(AppDbContext dbContext, string slotName)
    {
        _dbContext = dbContext;
        _slotName = slotName;
    }

    public string Name => $"{_slotName.ToLowerInvariant().Replace("_", "")}Gem";
    public FilterType FilterType => FilterType.EQUAL;

    public IEnumerable<string> OptionsGet(FilterContext context)
    {
        var keyId = _dbContext.NBTKeys
            .Where(k => k.KeyName == _slotName)
            .Select(k => k.Id)
            .FirstOrDefault();

        if (keyId <= 0)
            return new[] { "None" };

        var values = _dbContext.NBTValues
            .Where(v => v.KeyId == keyId)
            .Select(v => v.Value)
            .Distinct()
            .OrderBy(v => v)
            .ToList();

        return values.Prepend("Any").Append("None");
    }

    public IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        var value = context.Get(Name);
        if (string.IsNullOrEmpty(value))
            return query;

        var keyId = _dbContext.NBTKeys
            .Where(k => k.KeyName == _slotName)
            .Select(k => k.Id)
            .FirstOrDefault();

        if (keyId <= 0)
            return query;

        if (value.Equals("Any", StringComparison.OrdinalIgnoreCase))
            return query.Where(a => a.NBTLookups.Any(n => n.KeyId == keyId));

        if (value.Equals("None", StringComparison.OrdinalIgnoreCase))
            return query.Where(a => !a.NBTLookups.Any(n => n.KeyId == keyId));

        var valueId = _dbContext.NBTValues
            .Where(v => v.KeyId == keyId && v.Value == value)
            .Select(v => v.Id)
            .FirstOrDefault();

        if (valueId == 0)
            return query;

        return query.Where(a => a.NBTLookups.Any(n => n.KeyId == keyId && n.ValueId == valueId));
    }
}
