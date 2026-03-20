using SkyFlipperSolo.Data;

namespace SkyFlipperSolo.Services.Filters;

public sealed class DyeItemFilter : NbtStringFilter
{
    public DyeItemFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "DyeItem";
    protected override string PropName => "dye_item";
}
