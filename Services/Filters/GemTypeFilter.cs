using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public sealed class GemTypeFilter : IFilter
{
    private readonly AppDbContext _dbContext;
    private readonly string _slotPrefix;
    private readonly string _propName;

    public GemTypeFilter(AppDbContext dbContext, string slotPrefix)
    {
        _dbContext = dbContext;
        _slotPrefix = slotPrefix;
        _propName = $"{slotPrefix}_gem";
    }

    public string Name => $"{_slotPrefix.ToLowerInvariant().Replace("_", "")}GemType";
    public FilterType FilterType => FilterType.EQUAL;

    public IEnumerable<string> OptionsGet(FilterContext context)
    {
        var keyId = _dbContext.NBTKeys
            .Where(k => k.KeyName == _propName)
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
            .Where(k => k.KeyName == _propName)
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
