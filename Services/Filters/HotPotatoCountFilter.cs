using SkyFlipperSolo.Data;

namespace SkyFlipperSolo.Services.Filters;

public sealed class HotPotatoCountFilter : NbtNumberFilter
{
    public HotPotatoCountFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "HotPotatoCount";
    protected override string PropName => "hot_potato_count";
}
