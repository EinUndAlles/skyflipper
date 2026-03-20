using System.Text.RegularExpressions;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public sealed class PetLevelFilter : NumberFilterBase
{
    private static readonly Regex NameRegex = new(@"Lvl (\d{1,3})", RegexOptions.Compiled);
    private readonly AppDbContext _dbContext;

    public PetLevelFilter(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override string Name => "PetLevel";
    public override IEnumerable<string> OptionsGet(FilterContext context) => new[] { "1", "200" };

    protected override IQueryable<Auction> ApplyRanges(IQueryable<Auction> query, List<(long Min, long Max)> ranges, FilterContext context)
    {
        var rarity = context.Get("Rarity");
        if (string.IsNullOrEmpty(rarity) && ranges.Any(r => r.Min != r.Max))
            return query;

        var (min, max) = ranges[0];
        return query.Where(a => a.Tag.StartsWith("PET_") &&
                                NameRegex.IsMatch(a.ItemName) &&
                                int.Parse(NameRegex.Match(a.ItemName).Groups[1].Value) >= min &&
                                int.Parse(NameRegex.Match(a.ItemName).Groups[1].Value) <= max);
    }
}
