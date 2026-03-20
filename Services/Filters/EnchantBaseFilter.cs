using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

/// <summary>
/// Per-enchant type filter. Queries Enchantments table directly for a specific enchant type and level range.
/// Used both for canonical names and aliases (e.g. "pristine", "ultimate_duplex").
/// </summary>
public sealed class EnchantBaseFilter : NumberFilterBase
{
    private readonly AppDbContext _dbContext;
    private readonly EnchantmentType _enchant;
    private readonly string _filterName;

    public EnchantBaseFilter(AppDbContext dbContext, EnchantmentType enchant, string? name = null)
    {
        _dbContext = dbContext;
        _enchant = enchant;
        _filterName = name ?? enchant.ToString();
    }

    public override string Name => _filterName;
    public override IEnumerable<string> OptionsGet(FilterContext context) => new[] { "0", "10" };

    protected override IQueryable<Auction> ApplyRanges(IQueryable<Auction> query, List<(long Min, long Max)> ranges, FilterContext context)
    {
        var min = (int)ranges[0].Min;
        var max = (int)ranges[0].Max;

        if (min == 0 && max == 0)
            return query.Where(a => !a.Enchantments.Any(e => e.Type == _enchant));

        var auctionIds = _dbContext.Enchantments
            .Where(e => e.Type == _enchant && e.Level >= min && e.Level <= max)
            .Select(e => e.AuctionId);

        if (min == 0)
            return query; // 0 means "any or none", no filter

        return query.Where(a => auctionIds.Contains(a.Id));
    }
}
