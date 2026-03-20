using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public sealed class PetExpFilter : NbtNumberFilter, IApplicableFilter
{
    public PetExpFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "PetExp";
    protected override string PropName => "pet_exp";
    public override IEnumerable<string> OptionsGet(FilterContext context) => new[] { "0", ((long)int.MaxValue * 4).ToString() };

    public bool IsApplicable(FilterApplicabilityContext context)
    {
        return context.Tag.StartsWith("PET", StringComparison.OrdinalIgnoreCase);
    }
}
