using SkyFlipperSolo.Data;

namespace SkyFlipperSolo.Services.Filters;

public sealed class FarmingForDummiesFilter : NbtNumberFilter, IApplicableFilter
{
    public FarmingForDummiesFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "FarmingForDummies";
    protected override string PropName => "farming_for_dummies_count";

    public bool IsApplicable(FilterApplicabilityContext context)
    {
        return context.NbtKeys.Contains(PropName);
    }
}
