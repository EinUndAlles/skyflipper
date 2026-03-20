using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public sealed class HexColorListFilter : ColorFilter
{
    public HexColorListFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "HexColorList";
    public override FilterType FilterType => FilterType.RANGE;
}
