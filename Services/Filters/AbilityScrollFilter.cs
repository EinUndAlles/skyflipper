using SkyFlipperSolo.Data;

namespace SkyFlipperSolo.Services.Filters;

public sealed class AbilityScrollFilter : NbtStringFilter
{
    public AbilityScrollFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "AbilityScroll";
    protected override string PropName => "ability_scroll";
}
