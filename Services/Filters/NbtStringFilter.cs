using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public abstract class NbtStringFilter : IFilter
{
    protected abstract string PropName { get; }
    protected virtual bool AllowAnyNone => true;
    protected readonly AppDbContext _dbContext;

    protected NbtStringFilter(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public virtual string Name => PropName;
    public virtual FilterType FilterType => FilterType.EQUAL;

    public virtual IEnumerable<string> OptionsGet(FilterContext context)
    {
        var keyId = _dbContext.NBTKeys
            .Where(k => k.KeyName == PropName)
            .Select(k => k.Id)
            .FirstOrDefault();

        IEnumerable<string> values = Array.Empty<string>();
        if (keyId > 0)
        {
            values = _dbContext.NBTValues
                .Where(v => v.KeyId == keyId)
                .Select(v => v.Value)
                .Distinct()
                .OrderBy(v => v)
                .ToList();
        }

        if (!AllowAnyNone)
            return values;

        return values.Prepend("Any").Append("None");
    }

    public virtual IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        var value = context.Get(Name);
        if (string.IsNullOrEmpty(value))
            return query;

        var keyId = _dbContext.NBTKeys
            .Where(k => k.KeyName == PropName)
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
