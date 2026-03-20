using SkyFlipperSolo.Data;

namespace SkyFlipperSolo.Services.Filters;

public sealed class EditionFilter : NbtNumberFilter
{
    public EditionFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "Edition";
    protected override string PropName => "edition";
}
