using SkyFlipperSolo.Data;

namespace SkyFlipperSolo.Services.Filters;

public sealed class DyeItemFilter : NbtStringFilter, IApplicableFilter
{
    public DyeItemFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "DyeItem";
    protected override string PropName => "dye_item";

    public bool IsApplicable(FilterApplicabilityContext context)
    {
        return context.NbtKeys.Contains(PropName);
    }
}
