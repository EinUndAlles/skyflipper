using SkyFlipperSolo.Data;

namespace SkyFlipperSolo.Services.Filters;

public sealed class RecombobulatedFilter : BoolNbtFilter
{
    public RecombobulatedFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "Recombobulated";
    protected override string PropName => "rarity_upgrades";
}
