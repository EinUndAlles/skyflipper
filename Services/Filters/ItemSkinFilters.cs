using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

/// <summary>
/// Base class for item-specific skin filters.
/// Inherits skin matching from SkinFilter (NbtStringFilter) and adds tag filtering.
/// </summary>
public abstract class ItemSkinFilter : SkinFilter
{
    protected abstract string TagPrefix { get; }
    protected abstract string TagSuffix { get; }

    protected ItemSkinFilter(AppDbContext dbContext) : base(dbContext) { }

    public override IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        query = ApplyTagFilter(query);
        return base.Apply(query, context);
    }

    public override IEnumerable<string> OptionsGet(FilterContext context)
    {
        return base.OptionsGet(context);
    }

    private IQueryable<Auction> ApplyTagFilter(IQueryable<Auction> query)
    {
        if (!string.IsNullOrEmpty(TagSuffix))
            return query.Where(a => a.Tag.EndsWith(TagSuffix));
        if (!string.IsNullOrEmpty(TagPrefix))
            return query.Where(a => a.Tag.StartsWith(TagPrefix));
        return query;
    }
}

public sealed class DragonArmorSkinFilter : ItemSkinFilter
{
    protected override string TagSuffix => "_DRAGON_HELMET";
    protected override string TagPrefix => string.Empty;

    public DragonArmorSkinFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "DragonArmorSkin";
}

public sealed class ReaperMaskSkinFilter : ItemSkinFilter
{
    protected override string TagSuffix => string.Empty;
    protected override string TagPrefix => string.Empty;

    public ReaperMaskSkinFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "ReaperMaskSkin";

    public override IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        query = query.Where(a => a.Tag == "REAPER_MASK");
        return base.Apply(query, context);
    }
}

public sealed class SnowSuiteSkinFilter : ItemSkinFilter
{
    protected override string TagSuffix => string.Empty;
    protected override string TagPrefix => string.Empty;

    public SnowSuiteSkinFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "SnowSuiteSkin";

    public override IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        query = query.Where(a => a.Tag == "SNOW_SUIT_HELMET");
        return base.Apply(query, context);
    }
}

public sealed class TarantulaHelmetSkinFilter : ItemSkinFilter
{
    protected override string TagSuffix => string.Empty;
    protected override string TagPrefix => string.Empty;

    public TarantulaHelmetSkinFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "TarantulaHelmetSkin";

    public override IEnumerable<string> OptionsGet(FilterContext context)
        => new[] { "Any", "TARANTULA_BLACK_WIDOW", "None" };

    public override IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        query = query.Where(a => a.Tag == "TARANTULA_HELMET");
        return base.Apply(query, context);
    }
}

public sealed class FrozenBlazeSkinFilter : ItemSkinFilter
{
    protected override string TagSuffix => string.Empty;
    protected override string TagPrefix => string.Empty;

    public FrozenBlazeSkinFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "FrozenBlazeSkin";

    public override IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        query = query.Where(a => a.Tag == "FROZEN_BLAZE_HELMET");
        return base.Apply(query, context);
    }
}

public sealed class PerfectHelmetSkinFilter : ItemSkinFilter
{
    protected override string TagPrefix => "PERFECT_HELMET";
    protected override string TagSuffix => string.Empty;

    public PerfectHelmetSkinFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "PerfectHelmetSkin";
}

public sealed class DiversMaskSkinFilter : ItemSkinFilter
{
    protected override string TagSuffix => string.Empty;
    protected override string TagPrefix => string.Empty;

    public DiversMaskSkinFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "DiversMaskSkin";

    public override IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        query = query.Where(a => a.Tag == "DIVER_HELMET");
        return base.Apply(query, context);
    }
}

public sealed class ShadowAssassinSkinFilter : ItemSkinFilter
{
    protected override string TagSuffix => string.Empty;
    protected override string TagPrefix => string.Empty;

    public ShadowAssassinSkinFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "ShadowAssassinSkin";

    public override IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        query = query.Where(a => a.Tag == "SHADOW_ASSASSIN_HELMET");
        return base.Apply(query, context);
    }
}
