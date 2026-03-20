using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public sealed class UnlockedSlotsMatchFilter : NbtStringFilter
{
    public UnlockedSlotsMatchFilter(Data.AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "UnlockedSlotsMatch";
    protected override string PropName => "unlocked_slots";
}
