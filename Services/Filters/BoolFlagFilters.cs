using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

/// <summary>
/// Base class for boolean NBT presence filters.
/// Checks if an NBT key is present (yes/no).
/// </summary>
public abstract class BoolNbtKeyFilter : IFilter
{
    protected abstract string PropName { get; }
    protected readonly AppDbContext _dbContext;

    protected BoolNbtKeyFilter(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public virtual string Name => PropName;
    public virtual FilterType FilterType => FilterType.EQUAL;
    public virtual IEnumerable<string> OptionsGet(FilterContext context) => new[] { "yes", "no" };

    public virtual IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        var raw = context.Get(Name);
        if (string.IsNullOrEmpty(raw))
            return query;

        var shouldBePresent = raw.Equals("true", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("1", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("any", StringComparison.OrdinalIgnoreCase);

        var keyId = _dbContext.NBTKeys
            .Where(k => k.KeyName == PropName)
            .Select(k => k.Id)
            .FirstOrDefault();

        if (keyId <= 0)
            return shouldBePresent ? query.Where(a => false) : query;

        return shouldBePresent
            ? query.Where(a => a.NBTLookups.Any(n => n.KeyId == keyId))
            : query.Where(a => !a.NBTLookups.Any(n => n.KeyId == keyId));
    }
}

public sealed class IsShinyFilter : BoolNbtKeyFilter
{
    public IsShinyFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "IsShiny";
    protected override string PropName => "is_shiny";
}

public sealed class ArtOfPeaceFilter : BoolNbtKeyFilter
{
    public ArtOfPeaceFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "ArtOfPeace";
    protected override string PropName => "artOfPeaceApplied";
}

public sealed class WoodSingularityFilter : BoolNbtKeyFilter
{
    public WoodSingularityFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "WoodSingularity";
    protected override string PropName => "wood_singularity_count";
}

/// <summary>
/// Model filter — exact match on "model" NBT key (Abicase models).
/// </summary>
public sealed class ModelFilter : NbtStringFilter
{
    public ModelFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "Model";
    protected override string PropName => "model";
}

/// <summary>
/// Sold filter — auctions that have ended with at least one bid or BIN sale.
/// </summary>
public sealed class SoldFilter : IFilter
{
    public string Name => "Sold";
    public FilterType FilterType => FilterType.BOOLEAN;

    public IEnumerable<string> OptionsGet(FilterContext context) => new[] { "true", "false" };

    public IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        var raw = context.Get(Name);
        if (string.IsNullOrEmpty(raw))
            return query;

        var shouldBeSold = raw.Equals("true", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("1", StringComparison.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;
        return shouldBeSold
            ? query.Where(a => a.End < now && a.HighestBidAmount > 0)
            : query.Where(a => a.End >= now || a.HighestBidAmount == 0);
    }
}

/// <summary>
/// Clean filter — items with no extra NBT keys (no modifications applied).
/// Checks that no non-standard NBT keys and no enchantments are present.
/// </summary>
public sealed class CleanFilter : IFilter
{
    private static readonly HashSet<string> OkKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "uid", "exp", "uuid", "spawnedFor", "bossId", "active",
        "winning_bid", "is_shiny",
        "type", "tier", "hideInfo", "candyUsed", "hideRightClick",
        "cc", "color",
        "count", "reforge", "abr", "name",
        // attribute keys are considered "clean"
        "lifeline", "breeze", "speed", "experience", "mana_pool",
        "life_regeneration", "blazing_resistance", "arachno_resistance",
        "undead_resistance", "blazing_fortune", "fishing_experience",
        "double_hook", "infection", "trophy_hunter", "fisherman", "hunter",
        "fishing_speed", "life_recovery", "ignition", "combo", "attack_speed",
        "midas_touch", "mana_regeneration", "veteran", "mending", "ender_resistance",
        "dominance", "ender", "mana_steal", "blazing", "elite", "arachno", "undead",
        "warrior", "deadeye", "fortitude", "magic_find"
    };

    private readonly Data.AppDbContext _dbContext;

    public CleanFilter(Data.AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string Name => "Clean";
    public FilterType FilterType => FilterType.BOOLEAN;

    public IEnumerable<string> OptionsGet(FilterContext context) => new[] { "yes" };

    public IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        var raw = context.Get(Name);
        if (string.IsNullOrEmpty(raw))
            return query;

        var wantClean = raw.Equals("true", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("1", StringComparison.OrdinalIgnoreCase);

        var okKeyIds = _dbContext.NBTKeys
            .Where(k => OkKeys.Contains(k.KeyName))
            .Select(k => k.Id)
            .ToHashSet();

        if (okKeyIds.Count == 0)
            return wantClean ? query : query;

        return wantClean
            ? query.Where(a => !a.Enchantments.Any()
                && !a.NBTLookups.Any(n => n.KeyId.HasValue && !okKeyIds.Contains(n.KeyId.Value)))
            : query.Where(a => a.Enchantments.Any()
                || a.NBTLookups.Any(n => n.KeyId.HasValue && !okKeyIds.Contains(n.KeyId.Value)));
    }
}
