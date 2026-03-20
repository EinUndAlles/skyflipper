using SkyFlipperSolo.Data;

namespace SkyFlipperSolo.Services.Filters;

public sealed class FlawlessGemsCountFilter : PerfectGemsCountFilter
{
    public FlawlessGemsCountFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "FlawlessGemsCount";
    protected override string PropertyValueName => "FLAWLESS";
}
