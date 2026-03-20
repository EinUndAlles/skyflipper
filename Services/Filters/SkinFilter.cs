using SkyFlipperSolo.Data;

namespace SkyFlipperSolo.Services.Filters;

public class SkinFilter : NbtStringFilter
{
    public SkinFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "Skin";
    protected override string PropName => "skin";

    // Exclude pets (they have PetSkin filter)
    public override bool IsApplicable(string tag) => !PetLevelFilter.IsPet(tag);
}
