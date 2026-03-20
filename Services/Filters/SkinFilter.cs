using SkyFlipperSolo.Data;

namespace SkyFlipperSolo.Services.Filters;

public class SkinFilter : NbtStringFilter
{
    public SkinFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "Skin";
    protected override string PropName => "skin";
}
