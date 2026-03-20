using SkyFlipperSolo.Data;

namespace SkyFlipperSolo.Services.Filters;

public sealed class HotPotatoCountFilter : NbtNumberFilter, IApplicableFilter
{
    public HotPotatoCountFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "HotPotatoCount";
    protected override string PropName => "hot_potato_count";

    public bool IsApplicable(FilterApplicabilityContext context)
    {
        return context.NbtKeys.Contains(PropName);
    }
}
