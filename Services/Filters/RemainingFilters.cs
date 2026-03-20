using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

// ── Simple NBT number filters ──

public sealed class CandyFilter : NbtNumberFilter
{
    public CandyFilter(AppDbContext dbContext) : base(dbContext, "candyUsed") { }
}

public sealed class JalapenoBookFilter : NbtNumberFilter
{
    public JalapenoBookFilter(AppDbContext dbContext) : base(dbContext, "jalapeno_count") { }
}

public sealed class BassWeightFilter : NbtNumberFilter
{
    public BassWeightFilter(AppDbContext dbContext) : base(dbContext, "bass_weight") { }
}

public sealed class ItemTierFilter : NbtNumberFilter
{
    public ItemTierFilter(AppDbContext dbContext) : base(dbContext, "item_tier") { }
}

public sealed class PowderCoatingFilter : NbtNumberFilter
{
    public PowderCoatingFilter(AppDbContext dbContext) : base(dbContext, "powder_coating") { }
}

public sealed class GrowthStagesFilter : NbtNumberFilter
{
    public GrowthStagesFilter(AppDbContext dbContext) : base(dbContext, "growth_stage") { }
}

public sealed class UIdFilter : NbtNumberFilter
{
    public UIdFilter(AppDbContext dbContext) : base(dbContext, "uid") { }
}

// ── Simple NBT string filters ──

public sealed class CrabHatColorFilter : NbtStringFilter
{
    public CrabHatColorFilter(AppDbContext dbContext) : base(dbContext) { }
    public override string Name => "CrabHatColor";
    protected override string PropName => "party_hat_color";
}

public sealed class TalismanEnrichmentFilter : NbtStringFilter
{
    public TalismanEnrichmentFilter(AppDbContext dbContext) : base(dbContext) { }
    public override string Name => "TalismanEnrichment";
    protected override string PropName => "talisman_enrichment";
}

public sealed class DungeonSkillReqFilter : NbtStringFilter
{
    public DungeonSkillReqFilter(AppDbContext dbContext) : base(dbContext) { }
    public override string Name => "DungeonSkillReq";
    protected override string PropName => "dungeon_skill_requirement";
}

// ── Rod part filters (NBT string) ──

public sealed class RodHookFilter : NbtStringFilter
{
    public RodHookFilter(AppDbContext dbContext) : base(dbContext) { }
    public override string Name => "RodHook";
    protected override string PropName => "hook";
}

public sealed class RodLineFilter : NbtStringFilter
{
    public RodLineFilter(AppDbContext dbContext) : base(dbContext) { }
    public override string Name => "RodLine";
    protected override string PropName => "line";
}

public sealed class RodSinkerFilter : NbtStringFilter
{
    public RodSinkerFilter(AppDbContext dbContext) : base(dbContext) { }
    public override string Name => "RodSinker";
    protected override string PropName => "sinker";
}

// ── Logging / tool filters (NBT number) ──

public sealed class LogsCutFilter : NbtNumberFilter
{
    public LogsCutFilter(AppDbContext dbContext) : base(dbContext, "logs_cut") { }
}

public sealed class AbsorbLogsFilter : NbtNumberFilter
{
    public AbsorbLogsFilter(AppDbContext dbContext) : base(dbContext, "absorb_logs") { }
}

public sealed class AxeBoostersFilter : NbtNumberFilter
{
    public AxeBoostersFilter(AppDbContext dbContext) : base(dbContext, "axe_boosters_count") { }
}

public sealed class PlarvoidBookFilter : NbtNumberFilter
{
    public PlarvoidBookFilter(AppDbContext dbContext) : base(dbContext, "plarvoid_book_count") { }
}

// ── Item tag / ID filters ──

public sealed class ItemIdFilter : IFilter
{
    public string Name => "ItemId";
    public FilterType FilterType => FilterType.TEXT;

    public IEnumerable<string> OptionsGet(FilterContext context) => Array.Empty<string>();

    public IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        var value = context.Get(Name);
        if (string.IsNullOrWhiteSpace(value))
            return query;
        return query.Where(a => a.ItemUid == value);
    }
}

public sealed class ItemTagFilter : IFilter
{
    public string Name => "ItemTag";
    public FilterType FilterType => FilterType.EQUAL;

    public IEnumerable<string> OptionsGet(FilterContext context) => Array.Empty<string>();

    public IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        var value = context.Get(Name);
        if (string.IsNullOrWhiteSpace(value))
            return query;
        return query.Where(a => a.Tag == value);
    }
}

/// <summary>
/// Pass-through filter (matches everything). Used by coflnet clients as a no-op.
/// </summary>
public sealed class EverythingFilter : IFilter
{
    public string Name => "Everything";
    public FilterType FilterType => FilterType.SIMPLE;

    public IEnumerable<string> OptionsGet(FilterContext context) => new[] { "true" };

    public IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
        => query;
}

/// <summary>
/// Text search on item name.
/// </summary>
public sealed class ItemNameContainsFilter : IFilter
{
    public string Name => "ItemNameContains";
    public FilterType FilterType => FilterType.TEXT;

    public IEnumerable<string> OptionsGet(FilterContext context) => Array.Empty<string>();

    public IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        var value = context.Get(Name);
        if (string.IsNullOrWhiteSpace(value))
            return query;
        var pattern = $"%{value}%";
        return query.Where(a => EF.Functions.Like(a.ItemName, pattern));
    }
}

/// <summary>
/// Jyrre max — items with bottle_of_jyrre_seconds >= threshold.
/// Coflnet uses HIGHER type; we implement as number range with min only.
/// </summary>
public sealed class JyrreMaxFilter : NbtNumberFilter
{
    public JyrreMaxFilter(AppDbContext dbContext) : base(dbContext, "bottle_of_jyrre_seconds") { }

    public override string Name => "JyrreMax";
    public override IEnumerable<string> OptionsGet(FilterContext context) => new[] { "132600" };
}

// ── Computed / special filters ──

/// <summary>
/// Second enchantment filter — same logic as EnchantmentFilter but with different name.
/// Coflnet registers this separately so clients can filter on "second" enchant slot.
/// </summary>
public sealed class SecondEnchantmentFilter : IFilter
{
    public string Name => "SecondEnchantment";
    public FilterType FilterType => FilterType.EQUAL;

    public IEnumerable<string> OptionsGet(FilterContext context) => Enum.GetNames(typeof(EnchantmentType)).OrderBy(n => n);

    public IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        var value = context.Get(Name);
        if (string.IsNullOrEmpty(value))
            return query;

        if (!Enum.TryParse<EnchantmentType>(value, true, out var enchantType))
            return query;

        if (enchantType == EnchantmentType.unknown)
            return query;

        return query.Where(a => a.Enchantments.Any(e => e.Type == enchantType));
    }
}

/// <summary>
/// Second enchant level filter — same logic as EnchantLvlFilter but reads "SecondEnchantment" key.
/// </summary>
public sealed class SecondEnchantLvlFilter : NumberFilterBase
{
    private readonly AppDbContext _dbContext;

    public SecondEnchantLvlFilter(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override string Name => "SecondEnchantLvl";
    public override IEnumerable<string> OptionsGet(FilterContext context) => new[] { "1", "10" };

    protected override IQueryable<Auction> ApplyRanges(IQueryable<Auction> query, List<(long Min, long Max)> ranges, FilterContext context)
    {
        var raw = context.Get("SecondEnchantment");
        if (string.IsNullOrEmpty(raw) || !Enum.TryParse<EnchantmentType>(raw, true, out var enchantType))
            return query;

        var (min, max) = ranges[0];
        return query.Where(a => a.Enchantments.Any(e => e.Type == enchantType && e.Level >= min && e.Level <= max));
    }
}

/// <summary>
/// No other valuable enchants — items that only have the specified enchant (no other valuable ones).
/// Coflnet checks that no "valuable" enchant other than the target exists.
/// </summary>
public sealed class NoOtherValuableEnchantsFilter : IFilter
{
    private static readonly HashSet<EnchantmentType> ValuableEnchants = new()
    {
        EnchantmentType.ultimate_chimera, EnchantmentType.ultimate_combo,
        EnchantmentType.ultimate_duplex, EnchantmentType.ultimate_fatal_tempo,
        EnchantmentType.ultimate_legion, EnchantmentType.ultimate_reiterate,
        EnchantmentType.ultimate_rend, EnchantmentType.ultimate_soul_eater,
        EnchantmentType.ultimate_swarm, EnchantmentType.ultimate_wise,
        EnchantmentType.ultimate_wisdom, EnchantmentType.pristine,
        EnchantmentType.dragon_hunter, EnchantmentType.overload,
        EnchantmentType.soul_eater, EnchantmentType.critical,
        EnchantmentType.giant_killer, EnchantmentType.sharpness,
        EnchantmentType.syphon, EnchantmentType.life_steal,
        EnchantmentType.execute
    };

    public string Name => "NoOtherValuableEnchants";
    public FilterType FilterType => FilterType.BOOLEAN;

    public IEnumerable<string> OptionsGet(FilterContext context) => new[] { "true", "false" };

    public IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        var raw = context.Get(Name);
        if (string.IsNullOrEmpty(raw))
            return query;

        var wantNoOther = raw.Equals("true", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("yes", StringComparison.OrdinalIgnoreCase);

        if (!wantNoOther)
            return query;

        // Items with at most 1 valuable enchant
        return query.Where(a => a.Enchantments.Count(e => ValuableEnchants.Contains(e.Type)) <= 1);
    }
}

/// <summary>
/// Price per level — computed filter (not stored in NBT).
/// Coflnet uses this for cost analysis; we provide a placeholder.
/// </summary>
public sealed class PricePerLevelFilter : IFilter
{
    public string Name => "PricePerLevel";
    public FilterType FilterType => FilterType.NUMERICAL | FilterType.RANGE;

    public IEnumerable<string> OptionsGet(FilterContext context) => new[] { "0", "100000000" };

    public IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
        => query; // Pass-through — computed filter not applicable to DB queries
}

/// <summary>
/// Price per unit — computed filter for stack pricing.
/// </summary>
public sealed class PricePerUnitFilter : IFilter
{
    public string Name => "PricePerUnit";
    public FilterType FilterType => FilterType.NUMERICAL | FilterType.RANGE;

    public IEnumerable<string> OptionsGet(FilterContext context) => new[] { "0", "100000000" };

    public IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
        => query; // Pass-through — computed filter
}

/// <summary>
/// Cost per exp plus base — computed filter.
/// </summary>
public sealed class CostPerExpPlusBaseFilter : IFilter
{
    public string Name => "CostPerExpPlusBase";
    public FilterType FilterType => FilterType.NUMERICAL | FilterType.RANGE;

    public IEnumerable<string> OptionsGet(FilterContext context) => new[] { "0", "100000000" };

    public IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
        => query; // Pass-through — computed filter
}
