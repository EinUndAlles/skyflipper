using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public abstract class BoolNbtFilter : IFilter
{
    protected abstract string PropName { get; }
    private readonly AppDbContext _dbContext;

    protected BoolNbtFilter(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public virtual string Name => PropName;
    public virtual FilterType FilterType => FilterType.EQUAL;

    public virtual IEnumerable<string> OptionsGet(FilterContext context) => new[] { "yes", "no" };

    public virtual IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        var raw = context.Get(Name);
        if (string.IsNullOrEmpty(raw))
            return query;

        var shouldBePresent = raw.Equals("true", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("1", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("any", StringComparison.OrdinalIgnoreCase);

        var keyId = _dbContext.NBTKeys
            .Where(k => k.KeyName == PropName)
            .Select(k => k.Id)
            .FirstOrDefault();

        if (keyId <= 0)
            return query;

        return shouldBePresent
            ? query.Where(a => a.NBTLookups.Any(n => n.KeyId == keyId))
            : query.Where(a => !a.NBTLookups.Any(n => n.KeyId == keyId));
    }
}
