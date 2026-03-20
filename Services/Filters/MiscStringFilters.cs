using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services.Filters;

/// <summary>
/// Filters by seller UUID or auctioneer ID.
/// Simple text match on AuctioneerId field.
/// </summary>
public sealed class SellerFilter : IFilter
{
    public string Name => "Seller";
    public FilterType FilterType => FilterType.TEXT;

    public IEnumerable<string> OptionsGet(FilterContext context) => Array.Empty<string>();

    public IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        var value = context.Get(Name);
        if (string.IsNullOrWhiteSpace(value))
            return query;

        // Direct UUID match (32 hex chars)
        if (value.Length == 32)
            return query.Where(a => a.AuctioneerId == value);

        // Partial name match via captured_player NBT
        var pattern = $"%{value.Trim()}%";
        return query.Where(a => EF.Functions.Like(a.AuctioneerId ?? "", pattern));
    }
}

/// <summary>
/// Cake owner filter — matches NBT key "cake_owner".
/// Extends CapturedPlayerFilter pattern for NBT-based player matching.
/// </summary>
public sealed class CakeOwnerFilter : IFilter
{
    private readonly AppDbContext _dbContext;

    public CakeOwnerFilter(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string Name => "CakeOwner";
    public FilterType FilterType => FilterType.EQUAL | FilterType.TEXT;

    public IEnumerable<string> OptionsGet(FilterContext context) => Array.Empty<string>();

    public IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        var value = context.Get(Name);
        if (string.IsNullOrWhiteSpace(value))
            return query;

        var keyId = _dbContext.NBTKeys
            .Where(k => k.KeyName == "cake_owner")
            .Select(k => k.Id)
            .FirstOrDefault();

        if (keyId <= 0)
            return query;

        var pattern = $"%{value.Trim()}%";
        var valueIds = _dbContext.NBTValues
            .Where(v => v.KeyId == keyId && EF.Functions.Like(v.Value, pattern))
            .Select(v => v.Id)
            .ToList();

        if (valueIds.Count == 0)
            return query.Where(a => false);

        return query.Where(a => a.NBTLookups.Any(n => n.KeyId == keyId && n.ValueId.HasValue && valueIds.Contains(n.ValueId.Value)));
    }
}

/// <summary>
/// New Year's Cake year filter — number range on "new_years_cake" NBT key.
/// </summary>
public sealed class CakeYearFilter : NbtNumberFilter
{
    private static readonly int CurrentYear = (int)((DateTime.Now - new DateTime(2019, 6, 13)).TotalDays
        / (TimeSpan.FromDays(5) + TimeSpan.FromHours(4)).TotalDays + 1);

    public CakeYearFilter(AppDbContext dbContext) : base(dbContext, "new_years_cakes") { }

    public override IEnumerable<string> OptionsGet(FilterContext context) => new[] { "1", CurrentYear.ToString() };
}

/// <summary>
/// Party Hat year filter — number range on "party_hat_year" NBT key.
/// </summary>
public sealed class PartyHatYearFilter : NbtNumberFilter
{
    public PartyHatYearFilter(AppDbContext dbContext) : base(dbContext, "party_hat_year") { }

    public override IEnumerable<string> OptionsGet(FilterContext context) => new[] { "1", "100" };
}

/// <summary>
/// Party Hat color filter — string equality on "party_hat_color" NBT key.
/// </summary>
public sealed class PartyHatColorFilter : NbtStringFilter
{
    public PartyHatColorFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "PartyHatColor";
    protected override string PropName => "party_hat_color";
}

/// <summary>
/// Party Hat emoji filter — string equality on "party_hat_emoji" NBT key.
/// </summary>
public sealed class PartyHatEmojiFilter : NbtStringFilter
{
    public PartyHatEmojiFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "PartyHatEmoji";
    protected override string PropName => "party_hat_emoji";
}

/// <summary>
/// Fairy exotic color filter — filters colors to only fairy palette.
/// </summary>
public sealed class FairyColorFilter : ColorFilter
{
    public FairyColorFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "FairyColor";

    public override IEnumerable<string> OptionsGet(FilterContext context)
    {
        return new[] { $"Fairy:{string.Join(',', ExoticColorFilter.FairyColors)}" };
    }
}

/// <summary>
/// Crystal exotic color filter — filters colors to only crystal palette.
/// </summary>
public sealed class CrystalColorFilter : ColorFilter
{
    public CrystalColorFilter(AppDbContext dbContext) : base(dbContext) { }

    public override string Name => "CrystalColor";

    public override IEnumerable<string> OptionsGet(FilterContext context)
    {
        return new[] { $"Crystal:{string.Join(',', ExoticColorFilter.CrystalColors)}" };
    }
}
