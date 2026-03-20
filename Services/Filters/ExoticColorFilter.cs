using SkyFlipperSolo.Data;

namespace SkyFlipperSolo.Services.Filters;

public sealed class ExoticColorFilter : ColorFilter
{
    public static readonly HashSet<string> FairyColors = new(StringComparer.OrdinalIgnoreCase)
    {
        "330066", "4C0099", "660033", "660066", "6600CC", "7F00FF", "99004C", "990099", "9933FF", "B266FF",
        "CC0066", "CC00CC", "CC99FF", "E5CCFF", "FF007F", "FF00FF", "FF3399", "FF33FF", "FF66B2", "FF66FF",
        "FF99CC", "FF99FF", "FFCCE5", "FFCCFF"
    };

    public static readonly HashSet<string> CrystalColors = new(StringComparer.OrdinalIgnoreCase)
    {
        "1F0030", "46085E", "54146E", "5D1C78", "63237D", "6A2C82", "7E4196", "8E51A6", "9C64B3",
        "A875BD", "B88BC9", "C6A3D4", "D9C1E3", "E5D1ED", "EFE1F5", "FCF3FF"
    };

    public ExoticColorFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "ExoticColor";

    public override IEnumerable<string> OptionsGet(FilterContext context)
    {
        var combined = FairyColors.Concat(CrystalColors).ToList();
        return new[]
        {
            $"Fairy:{string.Join(',', FairyColors)}",
            $"Crystal:{string.Join(',', CrystalColors)}",
            $"Fairy+Crystal:{string.Join(',', combined)}"
        };
    }
}
