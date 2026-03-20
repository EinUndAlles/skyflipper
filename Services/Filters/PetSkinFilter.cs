using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public sealed class PetSkinFilter : NbtStringFilter
{
    public PetSkinFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "PetSkin";
    protected override string PropName => "skin";
    public override FilterType FilterType => base.FilterType | FilterType.AppliedItem;

    public override IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        var value = context.Get(Name);
        if (string.IsNullOrEmpty(value))
            return query.Where(a => a.Tag.StartsWith("PET_"));

        if (value.Equals("Any", StringComparison.OrdinalIgnoreCase))
        {
            var keyId = _dbContext.NBTKeys
                .Where(k => k.KeyName == "skin")
                .Select(k => k.Id)
                .FirstOrDefault();
            if (keyId <= 0)
                return query;
            return query.Where(a => a.Tag.StartsWith("PET_") && a.NBTLookups.Any(n => n.KeyId == keyId));
        }

        if (value.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            var keyId = _dbContext.NBTKeys
                .Where(k => k.KeyName == "skin")
                .Select(k => k.Id)
                .FirstOrDefault();
            if (keyId <= 0)
                return query;
            return query.Where(a => a.Tag.StartsWith("PET_") && !a.NBTLookups.Any(n => n.KeyId == keyId));
        }

        var keyIdMatch = _dbContext.NBTKeys
            .Where(k => k.KeyName == "skin")
            .Select(k => k.Id)
            .FirstOrDefault();

        if (keyIdMatch <= 0)
            return query;

        var targetValue = value.StartsWith("PET_SKIN_", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"PET_SKIN_{value}";

        var valueId = _dbContext.NBTValues
            .Where(v => v.KeyId == keyIdMatch && v.Value == targetValue)
            .Select(v => v.Id)
            .FirstOrDefault();

        if (valueId == 0)
            return query;

        return query.Where(a => a.Tag.StartsWith("PET_") && a.NBTLookups.Any(n => n.KeyId == keyIdMatch && n.ValueId == valueId));
    }
}
