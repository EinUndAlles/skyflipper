using System.Text.RegularExpressions;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public sealed class PetLevelFilter : NumberFilterBase, IApplicableFilter
{
    private static readonly Regex NameRegex = new(@"Lvl (\d{1,3})", RegexOptions.Compiled);
    private readonly AppDbContext _dbContext;

    public PetLevelFilter(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override string Name => "PetLevel";
    public override bool IsApplicable(string tag) => IsPet(tag);
    public override IEnumerable<string> OptionsGet(FilterContext context) => new[] { "1", "200" };

    public static bool IsPet(string tag) => tag.StartsWith("PET", StringComparison.OrdinalIgnoreCase)
        && !tag.StartsWith("PET_ITEM", StringComparison.OrdinalIgnoreCase)
        && !tag.StartsWith("PET_SKIN", StringComparison.OrdinalIgnoreCase);

    protected override IQueryable<Auction> ApplyRanges(IQueryable<Auction> query, List<(long Min, long Max)> ranges, FilterContext context)
    {
        // Regex + int.Parse on IQueryable is not SQL-translatable.
        // Materialize a small projection and filter in-memory, then apply back by IDs.
        var candidates = query
            .Where(a => a.Tag.StartsWith("PET"))
            .Select(a => new { a.Id, a.ItemName })
            .ToList();

        var matchingIds = candidates
            .Where(a => !string.IsNullOrEmpty(a.ItemName))
            .Where(a =>
            {
                var match = NameRegex.Match(a.ItemName);
                if (!match.Success)
                    return false;

                if (!int.TryParse(match.Groups[1].Value, out var level))
                    return false;

                return ranges.Any(r => level >= r.Min && level <= r.Max);
            })
            .Select(a => a.Id)
            .ToList();

        if (matchingIds.Count == 0)
            return query.Where(a => false);

        return query.Where(a => matchingIds.Contains(a.Id));
    }

    public bool IsApplicable(FilterApplicabilityContext context)
    {
        return context.Tag.StartsWith("PET", StringComparison.OrdinalIgnoreCase);
    }
}
