using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SkyFlipperSolo.Models;
using SkyFlipperSolo.Services;

namespace SkyFlipperSolo.Tests;

[TestFixture]
public class CacheKeyServiceTests
{
    // ── ExtractRelevantEnchants (static) ──

    [Test]
    public void ExtractRelevantEnchants_FiltersIrrelevant()
    {
        var enchants = new List<Enchantment>
        {
            new(EnchantmentType.cleave, 4),
            new(EnchantmentType.sharpness, 7),
            new(EnchantmentType.ultimate_wise, 5)
        };

        var result = CacheKeyService.ExtractRelevantEnchants(enchants).Select(e => e.Type).ToList();

        Assert.That(result, Does.Contain(EnchantmentType.sharpness));
        Assert.That(result, Does.Contain(EnchantmentType.ultimate_wise));
        Assert.That(result, Does.Not.Contain(EnchantmentType.cleave));
    }

    [Test]
    public void ExtractRelevantEnchants_EmptyList_ReturnsEmpty()
    {
        var result = CacheKeyService.ExtractRelevantEnchants(new List<Enchantment>());
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void ExtractRelevantEnchants_Null_ReturnsEmpty()
    {
        var result = CacheKeyService.ExtractRelevantEnchants(null);
        Assert.That(result, Is.Empty);
    }

    // ── ShouldPetItemMatch (static) ──

    [Test]
    public void ShouldPetItemMatch_RequiresExpKey()
    {
        var flatNbt = new Dictionary<string, string> { ["heldItem"] = "PET_ITEM_TIER_BOOST" };
        Assert.That(CacheKeyService.ShouldPetItemMatch(flatNbt, 10), Is.False);
    }

    [Test]
    public void ShouldPetItemMatch_TierBoostAlwaysMatches()
    {
        var flatNbt = new Dictionary<string, string>
        {
            ["heldItem"] = "PET_ITEM_TIER_BOOST",
            ["exp"] = "100"
        };
        Assert.That(CacheKeyService.ShouldPetItemMatch(flatNbt, 10), Is.True);
    }

    [TestCase("PET_ITEM_ALL_SKILLS_BOOST_COMMON", 30_000_000, false)]
    [TestCase("PET_ITEM_ALL_SKILLS_BOOST_COMMON", 3_000_000, true)]
    [TestCase("PET_ITEM_TIER_BOOST", 30_000_000, true)]
    [TestCase("PET_ITEM_TIER_BOOST", 300_000, true)]
    public void ShouldPetItemMatch_MatchesCoflnetCases(string item, long exp, bool expected)
    {
        var flatNbt = new Dictionary<string, string>
        {
            ["heldItem"] = item,
            ["exp"] = exp.ToString()
        };
        Assert.That(CacheKeyService.ShouldPetItemMatch(flatNbt, 10), Is.EqualTo(expected));
    }

    [Test]
    public void ShouldPetItemMatch_NoHeldItem_ReturnsFalse()
    {
        var flatNbt = new Dictionary<string, string> { ["exp"] = "1000" };
        Assert.That(CacheKeyService.ShouldPetItemMatch(flatNbt, 10), Is.False);
    }

    // ── IsPet (static) ──

    [TestCase("PET_DRAGON", true)]
    [TestCase("PET_ENDERMAN", true)]
    [TestCase("HYPERION", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    public void IsPet_WorksCorrectly(string? tag, bool expected)
    {
        Assert.That(CacheKeyService.IsPet(tag), Is.EqualTo(expected));
    }

    // ── IsArmor (static) ──

    [TestCase("CRIMSON_CHESTPLATE", true)]
    [TestCase("AURORA_LEGGINGS", true)]
    [TestCase("HYPERION", false)]
    [TestCase("", false)]
    public void IsArmor_WorksCorrectly(string? tag, bool expected)
    {
        Assert.That(CacheKeyService.IsArmor(tag), Is.EqualTo(expected));
    }

    // ── DoesRecombMatter (static) ──

    [Test]
    public void DoesRecombMatter_Armor_ReturnsTrue()
    {
        Assert.That(CacheKeyService.DoesRecombMatter(Category.UNKNOWN, "CRIMSON_CHESTPLATE"), Is.True);
    }

    [Test]
    public void DoesRecombMatter_Weapon_ReturnsTrue()
    {
        Assert.That(CacheKeyService.DoesRecombMatter(Category.UNKNOWN, "HYPERION"), Is.True);
    }

    // ── GeneratePriceCacheKey (instance) ──

    [Test]
    public void GeneratePriceCacheKey_IncludesTag()
    {
        var service = CreateService();
        var auction = new Auction
        {
            Tag = "HYPERION",
            Tier = Tier.LEGENDARY,
            Count = 1
        };
        var key = service.GeneratePriceCacheKey(auction);
        Assert.That(key, Does.Contain("HYPERION"));
        Assert.That(key, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void GeneratePriceCacheKey_PetIncludesNormalizedLevel()
    {
        var service = CreateService();
        var auction = new Auction
        {
            Tag = "PET_DRAGON",
            ItemName = "[Lvl 75] Test Pet",
            Tier = Tier.LEGENDARY,
            Count = 1
        };
        var key = service.GeneratePriceCacheKey(auction);
        // Last digit of pet level should be underscored
        Assert.That(key, Does.Contain("[Lvl 7_]"));
    }

    [Test]
    public void GeneratePriceCacheKey_DifferentTiers_DifferentKeys()
    {
        var service = CreateService();
        var auction1 = new Auction { Tag = "HYPERION", Tier = Tier.LEGENDARY, Count = 1 };
        var auction2 = new Auction { Tag = "HYPERION", Tier = Tier.MYTHIC, Count = 1 };

        var key1 = service.GeneratePriceCacheKey(auction1);
        var key2 = service.GeneratePriceCacheKey(auction2);

        Assert.That(key1, Is.Not.EqualTo(key2));
    }

    [Test]
    public void GeneratePriceCacheKey_DifferentTags_DifferentKeys()
    {
        var service = CreateService();
        var auction1 = new Auction { Tag = "HYPERION", Tier = Tier.LEGENDARY, Count = 1 };
        var auction2 = new Auction { Tag = "VALKYRIE", Tier = Tier.LEGENDARY, Count = 1 };

        var key1 = service.GeneratePriceCacheKey(auction1);
        var key2 = service.GeneratePriceCacheKey(auction2);

        Assert.That(key1, Is.Not.EqualTo(key2));
    }

    [Test]
    public void GeneratePriceCacheKey_NonNull_ReturnsString()
    {
        var service = CreateService();
        var auction = new Auction { Tag = "TEST", Tier = Tier.COMMON, Count = 1 };
        var key = service.GeneratePriceCacheKey(auction);
        Assert.That(key, Is.Not.Null);
        Assert.That(key, Is.Not.Empty);
    }

    private static CacheKeyService CreateService()
    {
        return new CacheKeyService(NullLogger<CacheKeyService>.Instance);
    }
}
