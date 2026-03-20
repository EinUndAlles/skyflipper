using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public class ColorFilter : NbtStringFilter
{
    public ColorFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "Color";
    protected override string PropName => "color";

    public override IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        var value = context.Get(Name);
        if (string.IsNullOrEmpty(value))
            return query;

        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (value.Contains(':'))
        {
            values.Add(value.Split(':').Last());
        }
        else
        {
            foreach (var part in value.Split(',', ' '))
            {
                var cleaned = Regex.Replace(part, "[^0-9A-Fa-f]", string.Empty);
                if (!string.IsNullOrEmpty(cleaned))
                    values.Add(cleaned.ToUpperInvariant());
            }
        }

        var keyId = _dbContext.NBTKeys
            .Where(k => k.KeyName == "color")
            .Select(k => k.Id)
            .FirstOrDefault();

        if (keyId <= 0)
            return query;

        var valueIds = _dbContext.NBTValues
            .Where(v => v.KeyId == keyId && values.Contains(v.Value))
            .Select(v => v.Id)
            .ToList();

        if (valueIds.Count == 0)
            return query;

        return query.Where(a => a.NBTLookups.Any(n => n.KeyId == keyId && n.ValueId.HasValue && valueIds.Contains(n.ValueId.Value)));
    }
}
