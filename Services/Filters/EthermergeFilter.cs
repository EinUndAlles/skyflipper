using SkyFlipperSolo.Data;

namespace SkyFlipperSolo.Services.Filters;

public sealed class EthermergeFilter : BoolNbtFilter, IApplicableFilter
{
    public EthermergeFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "Ethermerge";
    protected override string PropName => "ethermerge";

    public bool IsApplicable(FilterApplicabilityContext context)
    {
        return context.NbtKeys.Contains(PropName);
    }
}
