using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

/// <summary>
/// Per-attribute level filter for Crimson Isle/Kuudra attributes.
/// Range 0-10, 0 means not present.
/// </summary>
public sealed class AttributeLevelFilter : NbtNumberFilter
{
    private readonly string? _filterName;

    public AttributeLevelFilter(AppDbContext dbContext, string attributeName, string? filterName = null)
        : base(dbContext, attributeName)
    {
        _filterName = filterName;
    }

    public override string Name => _filterName ?? base.Name;
    public override IEnumerable<string> OptionsGet(FilterContext context) => new[] { "0", "10" };
}
