using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;
using SkyFlipperSolo.Services;
using SkyFlipperSolo.Services.Filters;

namespace SkyFlipperSolo.Tests;

[TestFixture]
public class FilterUnitTests
{
    private ServiceProvider _provider = null!;
    private FilterRegistry _registry = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase("filter-tests"));
        services.AddMemoryCache();
        services.AddDistributedMemoryCache();
        services.AddSingleton<NbtLookupResolver>();
        services.AddSingleton<CacheKeyService>();
        services.AddSingleton<FilterRegistry>();
        services.AddSingleton<FilterEngine>();
        services.AddLogging();

        // Register all filter types as scoped
        services.AddScoped<StarsFilter>();
        services.AddScoped<RarityFilter>();
        services.AddScoped<ReforgeFilter>();
        services.AddScoped<BinFilter>();
        services.AddScoped<StartingBidFilter>();
        services.AddScoped<HighestBidFilter>();
        services.AddScoped<CountFilter>();
        services.AddScoped<EnchantmentFilter>();
        services.AddScoped<EnchantLvlFilter>();
        services.AddScoped<HotPotatoCountFilter>();
        services.AddScoped<ArtOfTheWarFilter>();
        services.AddScoped<FarmingForDummiesFilter>();
        services.AddScoped<RecombobulatedFilter>();
        services.AddScoped<EthermergeFilter>();
        services.AddScoped<AbilityScrollFilter>();
        services.AddScoped<SkinFilter>();
        services.AddScoped<WinningBidFilter>();
        services.AddScoped<EditionFilter>();
        services.AddScoped<CapturedPlayerFilter>();
        services.AddScoped<EndBeforeFilter>();
        services.AddScoped<EndAfterFilter>();
        services.AddScoped<ItemCreatedBeforeFilter>();
        services.AddScoped<ItemCreatedAfterFilter>();
        services.AddScoped<PetLevelFilter>();
        services.AddScoped<PetItemFilter>();
        services.AddScoped<PetSkinFilter>();
        services.AddScoped<PetExpFilter>();
        services.AddScoped<ColorFilter>();
        services.AddScoped<HexColorListFilter>();
        services.AddScoped<ExoticColorFilter>();
        services.AddScoped<DyeItemFilter>();
        services.AddScoped<UnlockedSlotsFilter>();
        services.AddScoped<UnlockedSlotsMatchFilter>();
        services.AddScoped<HasAttributeFilter>();
        services.AddScoped<PerfectGemsCountFilter>();
        services.AddScoped<FlawlessGemsCountFilter>();
        services.AddScoped<ZombieKillsFilter>();
        services.AddScoped<SpiderKillsFilter>();
        services.AddScoped<EmanKillsFilter>();
        services.AddScoped<ExpertiseKillsFilter>();
        services.AddScoped<RaiderKillsFilter>();
        services.AddScoped<SwordKillsFilter>();
        services.AddScoped<BloodGodKillsFilter>();
        services.AddScoped<BlazeKillsFilter>();
        services.AddScoped<YogsKilledFilter>();
        services.AddScoped<BlazeConsumerFilter>();
        services.AddScoped<RunicKillsFilter>();
        services.AddScoped<HandlesFoundFilter>();
        services.AddScoped<BaseStatBoostFilter>();
        services.AddScoped<ManaDisintegratorFilter>();
        services.AddScoped<FarmedCultivatingFilter>();
        services.AddScoped<MinedCropsFilter>();
        services.AddScoped<BlocksBrokenFilter>();
        services.AddScoped<ThunderChargeFilter>();
        services.AddScoped<CollectedCoinsFilter>();
        services.AddScoped<ChimeraFoundFilter>();
        services.AddScoped<PickonimbusDurabilityFilter>();
        services.AddScoped<IntelligenceEarnedFilter>();
        services.AddScoped<RaffleWinCountFilter>();
        services.AddScoped<RaffleYearCountFilter>();
        services.AddScoped<IntelligenceBonusFilter>();
        services.AddScoped<MusicRuneFilter>();
        services.AddScoped<EnchantRuneFilter>();
        services.AddScoped<TidalRuneFilter>();
        services.AddScoped<EndRuneFilter>();
        services.AddScoped<DragonArmorSkinFilter>();
        services.AddScoped<ReaperMaskSkinFilter>();
        services.AddScoped<SnowSuiteSkinFilter>();
        services.AddScoped<TarantulaHelmetSkinFilter>();
        services.AddScoped<FrozenBlazeSkinFilter>();
        services.AddScoped<PerfectHelmetSkinFilter>();
        services.AddScoped<DiversMaskSkinFilter>();
        services.AddScoped<ShadowAssassinSkinFilter>();
        services.AddScoped<IsShinyFilter>();
        services.AddScoped<ArtOfPeaceFilter>();
        services.AddScoped<WoodSingularityFilter>();
        services.AddScoped<ModelFilter>();
        services.AddScoped<SoldFilter>();
        services.AddScoped<CleanFilter>();
        services.AddScoped<DrillPartEngineFilter>();
        services.AddScoped<DrillPartFuelTankFilter>();
        services.AddScoped<DrillPartUpgradeModuleFilter>();
        services.AddScoped<PowerAbilityScrollFilter>();
        services.AddScoped<TunedTransmissionFilter>();
        services.AddScoped<SellerFilter>();
        services.AddScoped<CakeOwnerFilter>();
        services.AddScoped<CakeYearFilter>();
        services.AddScoped<PartyHatYearFilter>();
        services.AddScoped<PartyHatColorFilter>();
        services.AddScoped<PartyHatEmojiFilter>();
        services.AddScoped<FairyColorFilter>();
        services.AddScoped<CrystalColorFilter>();
        services.AddScoped<CandyFilter>();
        services.AddScoped<JalapenoBookFilter>();
        services.AddScoped<BassWeightFilter>();
        services.AddScoped<ItemTierFilter>();
        services.AddScoped<PowderCoatingFilter>();
        services.AddScoped<GrowthStagesFilter>();
        services.AddScoped<UIdFilter>();
        services.AddScoped<CrabHatColorFilter>();
        services.AddScoped<TalismanEnrichmentFilter>();
        services.AddScoped<DungeonSkillReqFilter>();
        services.AddScoped<RodHookFilter>();
        services.AddScoped<RodLineFilter>();
        services.AddScoped<RodSinkerFilter>();
        services.AddScoped<LogsCutFilter>();
        services.AddScoped<AbsorbLogsFilter>();
        services.AddScoped<AxeBoostersFilter>();
        services.AddScoped<PlarvoidBookFilter>();
        services.AddScoped<ItemIdFilter>();
        services.AddScoped<ItemTagFilter>();
        services.AddScoped<EverythingFilter>();
        services.AddScoped<ItemNameContainsFilter>();
        services.AddScoped<JyrreMaxFilter>();
        services.AddScoped<SecondEnchantmentFilter>();
        services.AddScoped<SecondEnchantLvlFilter>();
        services.AddScoped<NoOtherValuableEnchantsFilter>();
        services.AddScoped<PricePerLevelFilter>();
        services.AddScoped<PricePerUnitFilter>();
        services.AddScoped<CostPerExpPlusBaseFilter>();

        _provider = services.BuildServiceProvider();
        FilterBootstrapper.RegisterCoreFilters(_provider);
        _registry = _provider.GetRequiredService<FilterRegistry>();
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        _provider?.Dispose();
    }

    // ── Bootstrapping ──

    [Test]
    public void AllFilters_RegisterWithoutError()
    {
        Assert.That(_registry, Is.Not.Null);
        Assert.That(_registry.Filters, Is.Not.Empty);
    }

    [Test]
    public void FilterCount_MatchesExpectedMinimum()
    {
        // 345+ filter registrations expected (static + dynamic)
        var count = _registry.Filters.Count();
        Assert.That(count, Is.GreaterThanOrEqualTo(300),
            $"Expected >= 300 filters, got {count}");
    }

    // ── Filter names are unique ──

    [Test]
    public void AllFilterNames_AreUnique()
    {
        var names = _registry.Filters.Select(f => f.Name).ToList();
        var duplicates = names.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.That(duplicates, Is.Empty,
            $"Duplicate filter names: {string.Join(", ", duplicates)}");
    }

    // ── FilterContext ──

    [Test]
    public void FilterContext_Get_ReturnsValue()
    {
        var ctx = new FilterContext(new Dictionary<string, string> { { "Stars", "5" } });
        Assert.That(ctx.Get("Stars"), Is.EqualTo("5"));
    }

    [Test]
    public void FilterContext_Get_MissingKey_ReturnsNull()
    {
        var ctx = new FilterContext(new Dictionary<string, string>());
        Assert.That(ctx.Get("Missing"), Is.Null);
    }

    [Test]
    public void FilterContext_Get_CaseSensitive()
    {
        var ctx = new FilterContext(new Dictionary<string, string> { { "stars", "3" } });
        Assert.That(ctx.Get("Stars"), Is.Null, "FilterContext is case-sensitive by default");
        Assert.That(ctx.Get("stars"), Is.EqualTo("3"));
    }

    // ── Key filter types exist ──

    [TestCase("Stars")]
    [TestCase("Rarity")]
    [TestCase("Reforge")]
    [TestCase("Bin")]
    [TestCase("StartingBid")]
    [TestCase("HighestBid")]
    [TestCase("Count")]
    [TestCase("Enchantment")]
    [TestCase("EnchantLvl")]
    [TestCase("HotPotatoCount")]
    [TestCase("PetLevel")]
    [TestCase("PetItem")]
    [TestCase("PetSkin")]
    [TestCase("PetExp")]
    [TestCase("Color")]
    [TestCase("DyeItem")]
    [TestCase("UnlockedSlots")]
    [TestCase("HasAttribute")]
    [TestCase("PerfectGemsCount")]
    [TestCase("FlawlessGemsCount")]
    [TestCase("IsShiny")]
    [TestCase("ArtOfPeace")]
    [TestCase("Sold")]
    [TestCase("Clean")]
    [TestCase("Model")]
    [TestCase("DrillPartEngine")]
    [TestCase("Everything")]
    [TestCase("ItemNameContains")]
    [TestCase("SecondEnchantment")]
    public void NamedFilter_ExistsInRegistry(string name)
    {
        var filter = _registry.Filters.FirstOrDefault(f => f.Name == name);
        Assert.That(filter, Is.Not.Null, $"Filter '{name}' not found in registry");
    }

    // ── FilterType flags ──

    [Test]
    public void BinFilter_HasBooleanType()
    {
        var filter = _registry.Filters.First(f => f.Name == "Bin");
        Assert.That(filter.FilterType & FilterType.BOOLEAN, Is.Not.EqualTo(0));
    }

    [Test]
    public void StarsFilter_HasNumericalType()
    {
        var filter = _registry.Filters.First(f => f.Name == "Stars");
        Assert.That(filter.FilterType & FilterType.NUMERICAL, Is.Not.EqualTo(0));
    }

    [Test]
    public void RarityFilter_HasEqualType()
    {
        var filter = _registry.Filters.First(f => f.Name == "Rarity");
        Assert.That(filter.FilterType & FilterType.EQUAL, Is.Not.EqualTo(0));
    }

    [Test]
    public void PartyHatYearFilter_HasEqualType_NotRange()
    {
        var filter = _registry.Filters.First(f => f.Name == "PartyHatYear");
        Assert.That(filter.FilterType, Is.EqualTo(FilterType.EQUAL),
            "PartyHatYear should be EQUAL, not RANGE (matching coflnet)");
    }

    // ── OptionsGet ──

    [Test]
    public void BinFilter_OptionsIncludeTrueFalse()
    {
        var filter = _registry.Filters.First(f => f.Name == "Bin");
        var options = filter.OptionsGet(new FilterContext(new Dictionary<string, string>())).ToList();
        Assert.That(options, Does.Contain("true"));
        Assert.That(options, Does.Contain("false"));
    }

    [Test]
    public void StarsFilter_OptionsIncludeRange()
    {
        var filter = _registry.Filters.First(f => f.Name == "Stars");
        var options = filter.OptionsGet(new FilterContext(new Dictionary<string, string>())).ToList();
        Assert.That(options, Has.Count.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void RarityFilter_OptionsIncludeTiers()
    {
        var filter = _registry.Filters.First(f => f.Name == "Rarity");
        var options = filter.OptionsGet(new FilterContext(new Dictionary<string, string>())).ToList();
        Assert.That(options, Does.Contain("LEGENDARY"));
        Assert.That(options, Does.Contain("MYTHIC"));
    }

    // ── Filter.Apply returns queryable (no exceptions) ──

    [Test]
    public void AllFilters_ApplyWithEmptyFilter_ReturnsQueryUnchanged()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var query = db.Auctions.AsQueryable();
        var ctx = new FilterContext(new Dictionary<string, string>());

        foreach (var filter in _registry.Filters)
        {
            Assert.DoesNotThrow(() =>
            {
                var result = filter.Apply(query, ctx);
                Assert.That(result, Is.Not.Null, $"Filter '{filter.Name}' returned null");
            }, $"Filter '{filter.Name}' threw on Apply with empty context");
        }
    }

    [Test]
    public void AllFilters_ApplyWithValue_DoesNotThrow()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var query = db.Auctions.AsQueryable();

        foreach (var filter in _registry.Filters)
        {
            var options = filter.OptionsGet(new FilterContext(new Dictionary<string, string>())).ToList();
            var testValue = options.FirstOrDefault() ?? "5";
            var ctx = new FilterContext(new Dictionary<string, string> { { filter.Name, testValue } });

            Assert.DoesNotThrow(() =>
            {
                var result = filter.Apply(query, ctx);
                Assert.That(result, Is.Not.Null);
            }, $"Filter '{filter.Name}' threw on Apply with value '{testValue}'");
        }
    }

    // ── Gem filters exist per slot ──

    [TestCase("ruby0Gem")]
    [TestCase("ruby1Gem")]
    [TestCase("combat0Gem")]
    [TestCase("combat1Gem")]
    [TestCase("amethyst2Gem")]
    [TestCase("combat0GemType")]
    [TestCase("universal1GemType")]
    public void GemFilter_ExistsForSlot(string name)
    {
        var filter = _registry.Filters.FirstOrDefault(f => f.Name == name);
        Assert.That(filter, Is.Not.Null, $"Gem filter '{name}' not found");
    }

    // ── Attribute filters exist ──

    [TestCase("lifeline")]
    [TestCase("veteran")]
    [TestCase("mana_pool")]
    [TestCase("dominance")]
    [TestCase("vitality")]
    public void AttributeFilter_Exists(string name)
    {
        var filter = _registry.Filters.FirstOrDefault(f => f.Name == name);
        Assert.That(filter, Is.Not.Null, $"Attribute filter '{name}' not found");
    }

    // ── Kill counter filters exist ──

    [TestCase("zombie_kills")]
    [TestCase("eman_kills")]
    [TestCase("expertise_kills")]
    [TestCase("blaze_kills")]
    public void KillFilter_Exists(string name)
    {
        var filter = _registry.Filters.FirstOrDefault(f => f.Name == name);
        Assert.That(filter, Is.Not.Null, $"Kill filter '{name}' not found");
    }

    // ── Enchant filters exist per type ──

    [TestCase("sharpness")]
    [TestCase("ultimate_wise")]
    [TestCase("pristine")]
    [TestCase("efficiency")]
    public void EnchantBaseFilter_ExistsPerType(string name)
    {
        var filter = _registry.Filters.FirstOrDefault(f => f.Name == name);
        Assert.That(filter, Is.Not.Null, $"Enchant filter '{name}' not found");
    }

    // ── Enchant aliases exist ──

    [TestCase("ultimate_duplex")]
    [TestCase("prismatic")]
    [TestCase("gravity")]
    [TestCase("drain")]
    [TestCase("ExperienceEnchant")]
    [TestCase("ManaStealEnchant")]
    public void EnchantAlias_Exists(string name)
    {
        var filter = _registry.Filters.FirstOrDefault(f => f.Name == name);
        Assert.That(filter, Is.Not.Null, $"Enchant alias '{name}' not found");
    }

    // ── Rune filters exist ──

    [TestCase("RUNE_MUSIC")]
    [TestCase("RUNE_ENCHANT")]
    [TestCase("RUNE_TIDAL")]
    [TestCase("RUNE_DRAGON")]
    public void RuneFilter_Exists(string name)
    {
        var filter = _registry.Filters.FirstOrDefault(f => f.Name == name);
        Assert.That(filter, Is.Not.Null, $"Rune filter '{name}' not found");
    }

    // ── Skin filters exist ──

    [TestCase("Skin")]
    [TestCase("PetSkin")]
    [TestCase("DragonArmorSkin")]
    [TestCase("ReaperMaskSkin")]
    [TestCase("ShadowAssassinSkin")]
    public void SkinFilter_Exists(string name)
    {
        var filter = _registry.Filters.FirstOrDefault(f => f.Name == name);
        Assert.That(filter, Is.Not.Null, $"Skin filter '{name}' not found");
    }

    // ── Bool/flag filters ──

    [Test]
    public void SoldFilter_ChecksEndAndBid()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Seed an ended auction with a bid
        db.Auctions.Add(new Auction
        {
            Uuid = "sold-test",
            Tag = "TEST",
            End = DateTime.UtcNow.AddDays(-1),
            HighestBidAmount = 1000
        });
        db.SaveChanges();

        var filter = _registry.Filters.First(f => f.Name == "Sold");
        var ctx = new FilterContext(new Dictionary<string, string> { { "Sold", "true" } });
        var result = filter.Apply(db.Auctions, ctx).ToList();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Uuid, Is.EqualTo("sold-test"));
    }

    [Test]
    public void EverythingFilter_ReturnsAllAuctions()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tag = $"EVERYTHING-{Guid.NewGuid():N}";
        db.Auctions.Add(new Auction { Uuid = $"ev-a1-{tag}", Tag = tag });
        db.Auctions.Add(new Auction { Uuid = $"ev-a2-{tag}", Tag = tag });
        db.SaveChanges();

        var filter = _registry.Filters.First(f => f.Name == "Everything");
        var ctx = new FilterContext(new Dictionary<string, string> { { "Everything", "true" } });
        var result = filter.Apply(db.Auctions, ctx).ToList();

        Assert.That(result.Count, Is.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void ItemNameContainsFilter_FindsPartialMatch()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Auctions.Add(new Auction { Uuid = "a1", ItemName = "Hyperion" });
        db.Auctions.Add(new Auction { Uuid = "a2", ItemName = "Shadow Fury" });
        db.SaveChanges();

        var filter = _registry.Filters.First(f => f.Name == "ItemNameContains");
        var ctx = new FilterContext(new Dictionary<string, string> { { "ItemNameContains", "Hyper" } });
        var result = filter.Apply(db.Auctions, ctx).ToList();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].ItemName, Is.EqualTo("Hyperion"));
    }

    [Test]
    public void ItemTagFilter_ExactMatch()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Auctions.Add(new Auction { Uuid = "a1", Tag = "HYPERION" });
        db.Auctions.Add(new Auction { Uuid = "a2", Tag = "VALKYRIE" });
        db.SaveChanges();

        var filter = _registry.Filters.First(f => f.Name == "ItemTag");
        var ctx = new FilterContext(new Dictionary<string, string> { { "ItemTag", "HYPERION" } });
        var result = filter.Apply(db.Auctions, ctx).ToList();

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0].Tag, Is.EqualTo("HYPERION"));
    }

    [Test]
    public void ItemIdFilter_ExactMatch()
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Auctions.Add(new Auction { Uuid = "a1", ItemUid = "abc-123" });
        db.Auctions.Add(new Auction { Uuid = "a2", ItemUid = "def-456" });
        db.SaveChanges();

        var filter = _registry.Filters.First(f => f.Name == "ItemId");
        var ctx = new FilterContext(new Dictionary<string, string> { { "ItemId", "abc-123" } });
        var result = filter.Apply(db.Auctions, ctx).ToList();

        Assert.That(result, Has.Count.EqualTo(1));
    }

    // ── Computed filters are pass-through ──

    [TestCase("PricePerLevel")]
    [TestCase("PricePerUnit")]
    [TestCase("CostPerExpPlusBase")]
    public void ComputedFilter_IsPassThrough(string name)
    {
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tag = $"COMPUTED-{Guid.NewGuid():N}";
        db.Auctions.Add(new Auction { Uuid = $"cf-a1-{tag}", Tag = tag });
        db.Auctions.Add(new Auction { Uuid = $"cf-a2-{tag}", Tag = tag });
        var before = db.Auctions.Count();
        db.SaveChanges();

        var filter = _registry.Filters.First(f => f.Name == name);
        var ctx = new FilterContext(new Dictionary<string, string> { { name, "500" } });
        var result = filter.Apply(db.Auctions, ctx).ToList();

        Assert.That(result.Count, Is.GreaterThanOrEqualTo(before),
            $"{name} should be pass-through");
    }
}
