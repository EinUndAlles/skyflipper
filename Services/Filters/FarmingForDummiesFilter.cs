using SkyFlipperSolo.Data;

namespace SkyFlipperSolo.Services.Filters;

public sealed class FarmingForDummiesFilter : NbtNumberFilter
{
    public FarmingForDummiesFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "FarmingForDummies";
    protected override string PropName => "farming_for_dummies_count";
}
