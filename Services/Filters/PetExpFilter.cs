using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public sealed class PetExpFilter : NbtNumberFilter
{
    public PetExpFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "PetExp";
    protected override string PropName => "exp";

    public override IEnumerable<string> OptionsGet(FilterContext context) => new[] { "0", (long)int.MaxValue * 4L + string.Empty };
}
