using SkyFlipperSolo.Data;

namespace SkyFlipperSolo.Services.Filters;

public sealed class EditionFilter : NbtNumberFilter, IApplicableFilter
{
    public EditionFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "Edition";
    protected override string PropName => "edition";

    public bool IsApplicable(FilterApplicabilityContext context)
    {
        return context.NbtKeys.Contains(PropName);
    }
}
