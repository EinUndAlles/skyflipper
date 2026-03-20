using SkyFlipperSolo.Data;

namespace SkyFlipperSolo.Services.Filters;

public sealed class RecombobulatedFilter : BoolNbtFilter, IApplicableFilter
{
    public RecombobulatedFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "Recombobulated";
    protected override string PropName => "rarity_upgrades";

    public bool IsApplicable(FilterApplicabilityContext context)
    {
        return context.NbtKeys.Contains(PropName);
    }
}
