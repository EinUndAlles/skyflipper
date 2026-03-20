using SkyFlipperSolo.Data;

namespace SkyFlipperSolo.Services.Filters;

public sealed class ArtOfTheWarFilter : NbtNumberFilter, IApplicableFilter
{
    public ArtOfTheWarFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "ArtOfTheWar";
    protected override string PropName => "art_of_war_count";

    public bool IsApplicable(FilterApplicabilityContext context)
    {
        return context.NbtKeys.Contains(PropName);
    }
}
