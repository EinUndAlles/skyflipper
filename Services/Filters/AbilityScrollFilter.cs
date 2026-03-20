using SkyFlipperSolo.Data;

namespace SkyFlipperSolo.Services.Filters;

public sealed class AbilityScrollFilter : NbtStringFilter, IApplicableFilter
{
    public AbilityScrollFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "AbilityScroll";
    protected override string PropName => "ability_scroll";

    public bool IsApplicable(FilterApplicabilityContext context)
    {
        return context.NbtKeys.Contains(PropName);
    }
}
