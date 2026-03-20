using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

public sealed class HasAttributeFilter : IFilter
{
    private static readonly HashSet<string> AttributeKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "lifeline", "breeze", "speed", "experience", "mana_pool",
        "life_regeneration", "blazing_resistance", "arachno_resistance",
        "undead_resistance", "blazing_fortune", "fishing_experience",
        "double_hook", "infection", "trophy_hunter", "fisherman", "hunter",
        "fishing_speed", "life_recovery", "ignition", "combo", "attack_speed",
        "midas_touch", "mana_regeneration", "veteran", "mending", "ender_resistance",
        "dominance", "ender", "mana_steal", "blazing", "elite", "arachno", "undead",
        "warrior", "deadeye", "fortitude", "magic_find"
    };

    private readonly AppDbContext _dbContext;

    public HasAttributeFilter(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string Name => "HasAttribute";
    public FilterType FilterType => FilterType.BOOLEAN;

    public IEnumerable<string> OptionsGet(FilterContext context) => new[] { "true", "false" };

    public IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        var raw = context.Get(Name);
        if (string.IsNullOrEmpty(raw))
            return query;

        var shouldBePresent = raw.Equals("true", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("1", StringComparison.OrdinalIgnoreCase);

        var keyIds = _dbContext.NBTKeys
            .Where(k => AttributeKeys.Contains(k.KeyName))
            .Select(k => k.Id)
            .ToList();

        if (keyIds.Count == 0)
            return shouldBePresent ? query.Where(a => false) : query;

        return shouldBePresent
            ? query.Where(a => a.NBTLookups.Any(n => n.KeyId.HasValue && keyIds.Contains(n.KeyId.Value)))
            : query.Where(a => !a.NBTLookups.Any(n => n.KeyId.HasValue && keyIds.Contains(n.KeyId.Value)));
    }
}
