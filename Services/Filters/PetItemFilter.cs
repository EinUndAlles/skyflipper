using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public sealed class PetItemFilter : NbtStringFilter, IApplicableFilter
{
    public PetItemFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "PetItem";
    protected override string PropName => "pet_held_item";
    public override FilterType FilterType => base.FilterType | FilterType.AppliedItem;
    public override bool IsApplicable(string tag) => PetLevelFilter.IsPet(tag);

    public override IEnumerable<string> OptionsGet(FilterContext context)
    {
        return base.OptionsGet(context)
            .Append("NOT_TIER_BOOST");
    }

    public override IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        var value = context.Get(Name);
        if (string.IsNullOrEmpty(value))
            return query;

        if (value.Equals("NOT_TIER_BOOST", StringComparison.OrdinalIgnoreCase))
        {
            var keyId = _dbContext.NBTKeys
                .Where(k => k.KeyName == "pet_held_item")
                .Select(k => k.Id)
                .FirstOrDefault();

            if (keyId <= 0)
                return query;

            var tierBoostId = _dbContext.NBTValues
                .Where(v => v.KeyId == keyId && v.Value == "PET_ITEM_TIER_BOOST")
                .Select(v => v.Id)
                .FirstOrDefault();

            if (tierBoostId == 0)
                return query;

            return query.Where(a => a.Tag.StartsWith("PET") && !a.NBTLookups.Any(n => n.KeyId == keyId && n.ValueId == tierBoostId));
        }

        return base.Apply(query, context);
    }

    public bool IsApplicable(FilterApplicabilityContext context)
    {
        return context.Tag.StartsWith("PET", StringComparison.OrdinalIgnoreCase);
    }
}
