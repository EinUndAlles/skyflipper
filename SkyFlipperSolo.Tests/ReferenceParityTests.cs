using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;
using SkyFlipperSolo.Services;

namespace SkyFlipperSolo.Tests;

[TestFixture]
public class ReferenceParityTests
{
    [Test]
    public void ExtractRelevantEnchantsMatchesCoflnetCases()
    {
        var enchants = new List<Enchantment>
        {
            new(EnchantmentType.cleave, 4),
            new(EnchantmentType.cubism, 6),
            new(EnchantmentType.ultimate_one_for_all, 1),
            new(EnchantmentType.snipe, 4),
            new(EnchantmentType.compact, 7)
        };

        var result = CacheKeyService.ExtractRelevantEnchants(enchants).Select(e => e.Type).ToList();

        CollectionAssert.Contains(result, EnchantmentType.ultimate_one_for_all);
        CollectionAssert.Contains(result, EnchantmentType.cubism);
        CollectionAssert.Contains(result, EnchantmentType.snipe);
        CollectionAssert.DoesNotContain(result, EnchantmentType.cleave);
    }

    [TestCase("PET_ITEM_ALL_SKILLS_BOOST_COMMON", 30_000_000, false)]
    [TestCase("PET_ITEM_ALL_SKILLS_BOOST_COMMON", 3_000_000, true)]
    [TestCase("PET_ITEM_TIER_BOOST", 30_000_000, true)]
    [TestCase("PET_ITEM_TIER_BOOST", 300_000, true)]
    public void ShouldPetItemMatchMatchesCoflnetCases(string item, long exp, bool expected)
    {
        var flatNbt = new Dictionary<string, string>
        {
            ["heldItem"] = item,
            ["exp"] = exp.ToString()
        };

        Assert.That(CacheKeyService.ShouldPetItemMatch(flatNbt, 10), Is.EqualTo(expected));
    }

    [Test]
    public void ShouldPetItemMatchRequiresExpGuard()
    {
        var flatNbt = new Dictionary<string, string>
        {
            ["heldItem"] = "PET_ITEM_TIER_BOOST"
        };

        Assert.That(CacheKeyService.ShouldPetItemMatch(flatNbt, 10), Is.False);
    }

    [TestCase("[Lvl 143] Test", "[Lvl 14_] Test")]
    [TestCase("[Lvl 14] Test", "[Lvl 1_] Test")]
    [TestCase("[Lvl 1] Test", "[Lvl _] Test")]
    public void PetLevelNormalizationMatchesCoflnetCases(string itemName, string normalizedName)
    {
        var service = CreateCacheKeyService();
        var key = service.GeneratePriceCacheKey(new Auction
        {
            Tag = "PET_DRAGON",
            ItemName = itemName,
            Tier = Tier.LEGENDARY,
            Count = 1
        });

        StringAssert.Contains(normalizedName, key);
    }

    [Test]
    public async Task GetWeightedMedianMatchesCoflnetCase()
    {
        var service = CreateReferenceAuctionService();
        var low = new Auction { HighestBidAmount = 3, End = DateTime.UtcNow.AddSeconds(-5), Count = 1 };
        var target = new Auction { HighestBidAmount = 7, End = DateTime.UtcNow.AddSeconds(-6), Count = 1 };
        var highest = new Auction { HighestBidAmount = 11, End = DateTime.UtcNow.AddSeconds(-7), Count = 1 };
        var newest = new Auction { HighestBidAmount = 4, End = DateTime.UtcNow, Count = 1 };

        var references = new List<Auction> { low, target, target, newest, highest, highest };

        var result = await service.GetWeightedMedianAsync(new Auction { Count = 1 }, references);

        Assert.That(result, Is.EqualTo(newest.HighestBidAmount));
    }

    [Test]
    public void SelectBestEnchantMatchesWorthOrder()
    {
        var enchants = new List<Enchantment>
        {
            new(EnchantmentType.growth, 5),
            new(EnchantmentType.ultimate_chimera, 1),
            new(EnchantmentType.ultimate_chimera, 2),
            new(EnchantmentType.smite, 7)
        };

        var method = typeof(ReferenceAuctionService)
            .GetMethod("SelectBestEnchant", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        Assert.That(method, Is.Not.Null, "SelectBestEnchant method not found.");

        var result = (Enchantment)method!.Invoke(null, new object[] { enchants })!;

        Assert.That(result.Type, Is.EqualTo(EnchantmentType.ultimate_chimera));
        Assert.That(result.Level, Is.EqualTo(2));
    }

    [Test]
    public async Task GetRelevantAuctionsKeepsOnlyExactEnchantParityCase()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var dbRoot = new InMemoryDatabaseRoot();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName, dbRoot)
            .Options;

        await using (var seedContext = new AppDbContext(options))
        {
            var target = BuildParityAuction("92ddfaae7e4e46b29eb2d652d9b043ac", "Ancient Maxor's Leggings", "seller-target", "uid-target", "bidder-target");
            var matching = Enumerable.Range(0, 11)
                .Select(i => BuildParityAuction(
                    $"fc92b460920d486494732beeed57ed{i:00}",
                    "Ancient Maxor's Leggings",
                    $"seller-match-{i}",
                    $"uid-match-{i}",
                    $"bidder-match-{i}"))
                .ToList();
            var wrongEnchant = BuildParityAuction("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "Ancient Maxor's Leggings", "seller-wrong-enchant", "uid-wrong-enchant", "bidder-wrong-enchant");
            wrongEnchant.Enchantments = new List<Enchantment>
            {
                CreateEnchant(EnchantmentType.rejuvenate, 5),
                CreateEnchant(EnchantmentType.protection, 5),
                CreateEnchant(EnchantmentType.growth, 5),
            };
            var wrongTier = BuildParityAuction("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "Ancient Maxor's Leggings", "seller-wrong-tier", "uid-wrong-tier", "bidder-wrong-tier");
            wrongTier.Tier = Tier.LEGENDARY;

            seedContext.Auctions.Add(target);
            seedContext.Auctions.AddRange(matching);
            seedContext.Auctions.AddRange(wrongEnchant, wrongTier);
            await seedContext.SaveChangesAsync();
        }

        var serviceProvider = new ServiceCollection()
            .AddSingleton(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName, dbRoot).Options)
            .AddScoped(_ => new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName, dbRoot).Options))
            .BuildServiceProvider();

        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var referenceService = CreateReferenceAuctionService(scopeFactory);

        await using var queryContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName, dbRoot).Options);
        var targetAuction = await queryContext.Auctions
            .Include(a => a.Bids)
            .Include(a => a.Enchantments)
            .Include(a => a.NBTLookups).ThenInclude(n => n.NBTKey)
            .FirstAsync(a => a.Uuid == "92ddfaae7e4e46b29eb2d652d9b043ac");

        var result = await referenceService.GetRelevantAuctionsCacheAsync(targetAuction, CancellationToken.None);
        var trace = await referenceService.DebugRelevantAuctionsAsync(targetAuction, CancellationToken.None);
        var candidateAuction = await queryContext.Auctions
            .Include(a => a.Bids)
            .Include(a => a.Enchantments)
            .Include(a => a.NBTLookups).ThenInclude(n => n.NBTKey)
            .FirstAsync(a => a.AuctioneerId == "seller-match-0");

        var clearedName = "Maxor's Leggings";
        var targetFlatNbt = new Dictionary<string, string>
        {
            ["rarity_upgrades"] = "1",
            ["hpc"] = "10",
            ["color"] = "93:47:185",
            ["dungeon_item_level"] = "5",
            ["uid"] = "uid-target"
        };
        var relevantEnchants = CacheKeyService.ExtractRelevantEnchants(targetAuction.Enchantments);
        var debug = referenceService.EvaluateCandidateMatch(candidateAuction, targetAuction, clearedName, targetFlatNbt, null, relevantEnchants, false);

        Assert.That(debug.IsMatch, Is.True,
            $"tier={debug.TierMatches}, reforge={debug.ReforgeMatches}, stack={debug.StackMatches}, name={debug.NameMatches}, nbt={debug.FlatNbtMatches}, enchants={debug.EnchantmentsMatch}");

        Assert.That(trace.InitialCount + trace.ExpandedDayCount + trace.ExpandedWeekCount + trace.ReducedCount + trace.RecentReducedCount + trace.AntiManipulationSourceCount, Is.GreaterThan(0),
            $"initial={trace.InitialCount}, day={trace.ExpandedDayCount}, week={trace.ExpandedWeekCount}, reduced={trace.ReducedCount}, recentReduced={trace.RecentReducedCount}, antiSource={trace.AntiManipulationSourceCount}, beforeAnti={trace.BeforeAntiManipulationCount}, afterAnti={trace.AfterAntiManipulationCount}");

        Assert.That(result.References.Select(a => a.AuctioneerId).ToList(), Does.Contain("seller-match-0"),
            $"initial={trace.InitialCount}, day={trace.ExpandedDayCount}, week={trace.ExpandedWeekCount}, reduced={trace.ReducedCount}, recentReduced={trace.RecentReducedCount}, antiSource={trace.AntiManipulationSourceCount}, beforeAnti={trace.BeforeAntiManipulationCount}, afterAnti={trace.AfterAntiManipulationCount}");
        Assert.That(result.References.Select(a => a.Uuid).ToList(), Does.Not.Contain("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));
        Assert.That(result.References.Select(a => a.Uuid).ToList(), Does.Not.Contain("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"));
    }

    [Test]
    public void EvaluateCandidateMatch_RequiresValuablePetItemToMatchExactly()
    {
        var referenceService = CreateReferenceAuctionService();
        var target = new Auction
        {
            Tag = "PET_DRAGON",
            ItemName = "[Lvl 100] Dragon",
            Tier = Tier.LEGENDARY,
            Category = Category.MISC,
            StartingBid = 50_000_000,
            FlatenedNBTJson = """{"heldItem":"PET_ITEM_TIER_BOOST","exp":"25000000","candyUsed":"0"}"""
        };

        var matchingCandidate = new Auction
        {
            Tag = "PET_DRAGON",
            ItemName = "[Lvl 103] Dragon",
            Tier = Tier.LEGENDARY,
            Category = Category.MISC,
            StartingBid = 50_000_000,
            FlatenedNBTJson = """{"heldItem":"PET_ITEM_TIER_BOOST","exp":"25000000","candyUsed":"0"}"""
        };

        var wrongPetItemCandidate = new Auction
        {
            Tag = "PET_DRAGON",
            ItemName = "[Lvl 103] Dragon",
            Tier = Tier.LEGENDARY,
            Category = Category.MISC,
            StartingBid = 50_000_000,
            FlatenedNBTJson = """{"heldItem":"PET_ITEM_EXP_SHARE","exp":"25000000","candyUsed":"0"}"""
        };

        var relevantEnchants = CacheKeyService.ExtractRelevantEnchants(target.Enchantments);
        var targetFlatNbt = new Dictionary<string, string>
        {
            ["heldItem"] = "PET_ITEM_TIER_BOOST",
            ["exp"] = "25000000",
            ["candyUsed"] = "0"
        };

        var matching = referenceService.EvaluateCandidateMatch(matchingCandidate, target, "[Lvl 100] Dragon", targetFlatNbt, null, relevantEnchants, false);
        var wrong = referenceService.EvaluateCandidateMatch(wrongPetItemCandidate, target, "[Lvl 100] Dragon", targetFlatNbt, null, relevantEnchants, false);

        Assert.That(matching.IsMatch, Is.True,
            $"tier={matching.TierMatches}, reforge={matching.ReforgeMatches}, stack={matching.StackMatches}, name={matching.NameMatches}, nbt={matching.FlatNbtMatches}, enchants={matching.EnchantmentsMatch}");
        Assert.That(wrong.IsMatch, Is.False);
        Assert.That(wrong.FlatNbtMatches, Is.False);
    }

    [Test]
    public void EvaluateCandidateMatch_AllowsMidasRangeParity()
    {
        var referenceService = CreateReferenceAuctionService();
        var target = new Auction
        {
            Tag = "MIDAS_SWORD",
            ItemName = "Gilded Midas Sword",
            Tier = Tier.LEGENDARY,
            Category = Category.WEAPON,
            Reforge = Reforge.Gilded,
            StartingBid = 120_000_000,
            FlatenedNBTJson = """{"winning_bid":"100000000","additional_coins":"100000000"}"""
        };

        var inRangeCandidate = new Auction
        {
            Tag = "MIDAS_SWORD",
            ItemName = "Gilded Midas Sword",
            Tier = Tier.LEGENDARY,
            Category = Category.WEAPON,
            Reforge = Reforge.Gilded,
            StartingBid = 121_000_000,
            FlatenedNBTJson = """{"winning_bid":"101000000","additional_coins":"101000000"}"""
        };

        var outOfRangeCandidate = new Auction
        {
            Tag = "MIDAS_SWORD",
            ItemName = "Gilded Midas Sword",
            Tier = Tier.LEGENDARY,
            Category = Category.WEAPON,
            Reforge = Reforge.Gilded,
            StartingBid = 150_000_000,
            FlatenedNBTJson = """{"winning_bid":"110000000","additional_coins":"110000000"}"""
        };

        var targetFlatNbt = new Dictionary<string, string>
        {
            ["winning_bid"] = "100000000",
            ["additional_coins"] = "100000000"
        };

        var inRange = referenceService.EvaluateCandidateMatch(inRangeCandidate, target, "Midas Sword", targetFlatNbt, null, new List<Enchantment>(), false);
        var outOfRange = referenceService.EvaluateCandidateMatch(outOfRangeCandidate, target, "Midas Sword", targetFlatNbt, null, new List<Enchantment>(), false);

        Assert.That(inRange.IsMatch, Is.True,
            $"tier={inRange.TierMatches}, reforge={inRange.ReforgeMatches}, stack={inRange.StackMatches}, name={inRange.NameMatches}, nbt={inRange.FlatNbtMatches}, enchants={inRange.EnchantmentsMatch}");
        Assert.That(outOfRange.IsMatch, Is.False);
        Assert.That(outOfRange.FlatNbtMatches, Is.False);
    }

    [Test]
    public void EvaluateCandidateMatch_AllowsFinalDestinationKillRangeParity()
    {
        var referenceService = CreateReferenceAuctionService();
        var target = new Auction
        {
            Tag = "FINAL_DESTINATION_CHESTPLATE",
            ItemName = "Final Destination Chestplate",
            Tier = Tier.LEGENDARY,
            Category = Category.ARMOR,
            StartingBid = 40_000_000,
            FlatenedNBTJson = """{"eman_kills":"10000"}"""
        };

        var inRangeCandidate = new Auction
        {
            Tag = "FINAL_DESTINATION_CHESTPLATE",
            ItemName = "Final Destination Chestplate",
            Tier = Tier.LEGENDARY,
            Category = Category.ARMOR,
            StartingBid = 41_000_000,
            FlatenedNBTJson = """{"eman_kills":"10800"}"""
        };

        var outOfRangeCandidate = new Auction
        {
            Tag = "FINAL_DESTINATION_CHESTPLATE",
            ItemName = "Final Destination Chestplate",
            Tier = Tier.LEGENDARY,
            Category = Category.ARMOR,
            StartingBid = 41_000_000,
            FlatenedNBTJson = """{"eman_kills":"12500"}"""
        };

        var targetFlatNbt = new Dictionary<string, string>
        {
            ["eman_kills"] = "10000"
        };

        var inRange = referenceService.EvaluateCandidateMatch(inRangeCandidate, target, "Destination Chestplate", targetFlatNbt, null, new List<Enchantment>(), false);
        var outOfRange = referenceService.EvaluateCandidateMatch(outOfRangeCandidate, target, "Destination Chestplate", targetFlatNbt, null, new List<Enchantment>(), false);

        Assert.That(inRange.IsMatch, Is.True,
            $"tier={inRange.TierMatches}, reforge={inRange.ReforgeMatches}, stack={inRange.StackMatches}, name={inRange.NameMatches}, nbt={inRange.FlatNbtMatches}, enchants={inRange.EnchantmentsMatch}");
        Assert.That(outOfRange.IsMatch, Is.False);
        Assert.That(outOfRange.FlatNbtMatches, Is.False);
    }

    [Test]
    public void EvaluateCandidateMatch_RequiresDyedArmorParity()
    {
        var referenceService = CreateReferenceAuctionService();
        var target = new Auction
        {
            Tag = "CRIMSON_CHESTPLATE",
            ItemName = "Crimson Chestplate",
            Tier = Tier.MYTHIC,
            Category = Category.ARMOR,
            StartingBid = 55_000_000,
            FlatenedNBTJson = """{"color":"255:0:0","dye_item":"DYE_FLAME"}"""
        };

        var matchingCandidate = new Auction
        {
            Tag = "CRIMSON_CHESTPLATE",
            ItemName = "Crimson Chestplate",
            Tier = Tier.MYTHIC,
            Category = Category.ARMOR,
            StartingBid = 56_000_000,
            FlatenedNBTJson = """{"color":"255:0:0","dye_item":"DYE_FLAME"}"""
        };

        var wrongColorCandidate = new Auction
        {
            Tag = "CRIMSON_CHESTPLATE",
            ItemName = "Crimson Chestplate",
            Tier = Tier.MYTHIC,
            Category = Category.ARMOR,
            StartingBid = 56_000_000,
            FlatenedNBTJson = """{"color":"0:0:255","dye_item":"DYE_FLAME"}"""
        };

        var wrongDyeCandidate = new Auction
        {
            Tag = "CRIMSON_CHESTPLATE",
            ItemName = "Crimson Chestplate",
            Tier = Tier.MYTHIC,
            Category = Category.ARMOR,
            StartingBid = 56_000_000,
            FlatenedNBTJson = """{"color":"255:0:0","dye_item":"DYE_AZURE"}"""
        };

        var targetFlatNbt = new Dictionary<string, string>
        {
            ["color"] = "255:0:0",
            ["dye_item"] = "DYE_FLAME"
        };

        var matching = referenceService.EvaluateCandidateMatch(matchingCandidate, target, "Crimson Chestplate", targetFlatNbt, null, new List<Enchantment>(), false);
        var wrongColor = referenceService.EvaluateCandidateMatch(wrongColorCandidate, target, "Crimson Chestplate", targetFlatNbt, null, new List<Enchantment>(), false);
        var wrongDye = referenceService.EvaluateCandidateMatch(wrongDyeCandidate, target, "Crimson Chestplate", targetFlatNbt, null, new List<Enchantment>(), false);

        Assert.That(matching.IsMatch, Is.True,
            $"tier={matching.TierMatches}, reforge={matching.ReforgeMatches}, stack={matching.StackMatches}, name={matching.NameMatches}, nbt={matching.FlatNbtMatches}, enchants={matching.EnchantmentsMatch}");
        Assert.That(wrongColor.IsMatch, Is.False);
        Assert.That(wrongColor.FlatNbtMatches, Is.False);
        Assert.That(wrongDye.IsMatch, Is.False);
        Assert.That(wrongDye.FlatNbtMatches, Is.False);
    }

    [Test]
    public void EvaluateCandidateMatch_RequiresDrillPartParity()
    {
        var referenceService = CreateReferenceAuctionService();
        var target = new Auction
        {
            Tag = "DIVAN_DRILL",
            ItemName = "Divan's Drill",
            Tier = Tier.MYTHIC,
            Category = Category.MISC,
            StartingBid = 900_000_000,
            FlatenedNBTJson = """{"drill_part_engine":"ENGINE_X655","drill_part_fuel_tank":"TANK_5","drill_part_upgrade_module":"MODULE_GOBLIN"}"""
        };

        var matchingCandidate = new Auction
        {
            Tag = "DIVAN_DRILL",
            ItemName = "Divan's Drill",
            Tier = Tier.MYTHIC,
            Category = Category.MISC,
            StartingBid = 905_000_000,
            FlatenedNBTJson = """{"drill_part_engine":"ENGINE_X655","drill_part_fuel_tank":"TANK_5","drill_part_upgrade_module":"MODULE_GOBLIN"}"""
        };

        var wrongPartCandidate = new Auction
        {
            Tag = "DIVAN_DRILL",
            ItemName = "Divan's Drill",
            Tier = Tier.MYTHIC,
            Category = Category.MISC,
            StartingBid = 905_000_000,
            FlatenedNBTJson = """{"drill_part_engine":"ENGINE_X655","drill_part_fuel_tank":"TANK_5","drill_part_upgrade_module":"MODULE_AMBER"}"""
        };

        var targetFlatNbt = new Dictionary<string, string>
        {
            ["drill_part_engine"] = "ENGINE_X655",
            ["drill_part_fuel_tank"] = "TANK_5",
            ["drill_part_upgrade_module"] = "MODULE_GOBLIN"
        };

        var matching = referenceService.EvaluateCandidateMatch(matchingCandidate, target, "Divan's Drill", targetFlatNbt, null, new List<Enchantment>(), false);
        var wrongPart = referenceService.EvaluateCandidateMatch(wrongPartCandidate, target, "Divan's Drill", targetFlatNbt, null, new List<Enchantment>(), false);

        Assert.That(matching.IsMatch, Is.True,
            $"tier={matching.TierMatches}, reforge={matching.ReforgeMatches}, stack={matching.StackMatches}, name={matching.NameMatches}, nbt={matching.FlatNbtMatches}, enchants={matching.EnchantmentsMatch}");
        Assert.That(wrongPart.IsMatch, Is.False);
        Assert.That(wrongPart.FlatNbtMatches, Is.False);
    }

    [Test]
    public void EvaluateCandidateMatch_RequiresAbilityScrollParity()
    {
        var referenceService = CreateReferenceAuctionService();
        var target = new Auction
        {
            Tag = "WITHER_BLADE",
            ItemName = "Hyperion",
            Tier = Tier.MYTHIC,
            Category = Category.WEAPON,
            StartingBid = 1_500_000_000,
            FlatenedNBTJson = """{"ability_scroll":"IMPLOSION SHADOW_WARP WITHER_SHIELD"}"""
        };

        var matchingCandidate = new Auction
        {
            Tag = "WITHER_BLADE",
            ItemName = "Hyperion",
            Tier = Tier.MYTHIC,
            Category = Category.WEAPON,
            StartingBid = 1_520_000_000,
            FlatenedNBTJson = """{"ability_scroll":"IMPLOSION SHADOW_WARP WITHER_SHIELD"}"""
        };

        var wrongScrollCandidate = new Auction
        {
            Tag = "WITHER_BLADE",
            ItemName = "Hyperion",
            Tier = Tier.MYTHIC,
            Category = Category.WEAPON,
            StartingBid = 1_520_000_000,
            FlatenedNBTJson = """{"ability_scroll":"IMPLOSION WITHER_SHIELD"}"""
        };

        var targetFlatNbt = new Dictionary<string, string>
        {
            ["ability_scroll"] = "IMPLOSION SHADOW_WARP WITHER_SHIELD"
        };

        var matching = referenceService.EvaluateCandidateMatch(matchingCandidate, target, "Hyperion", targetFlatNbt, null, new List<Enchantment>(), false);
        var wrongScroll = referenceService.EvaluateCandidateMatch(wrongScrollCandidate, target, "Hyperion", targetFlatNbt, null, new List<Enchantment>(), false);

        Assert.That(matching.IsMatch, Is.True,
            $"tier={matching.TierMatches}, reforge={matching.ReforgeMatches}, stack={matching.StackMatches}, name={matching.NameMatches}, nbt={matching.FlatNbtMatches}, enchants={matching.EnchantmentsMatch}");
        Assert.That(wrongScroll.IsMatch, Is.False);
        Assert.That(wrongScroll.FlatNbtMatches, Is.False);
    }

    [Test]
    public void EvaluateCandidateMatch_RequiresAttributeGearParity()
    {
        var referenceService = CreateReferenceAuctionService();
        var target = new Auction
        {
            Tag = "CRIMSON_HELMET",
            ItemName = "Crimson Helmet",
            Tier = Tier.MYTHIC,
            Category = Category.ARMOR,
            StartingBid = 120_000_000,
            FlatenedNBTJson = """{"mana_pool":"7","dominance":"5"}"""
        };

        var matchingCandidate = new Auction
        {
            Tag = "CRIMSON_HELMET",
            ItemName = "Crimson Helmet",
            Tier = Tier.MYTHIC,
            Category = Category.ARMOR,
            StartingBid = 121_000_000,
            FlatenedNBTJson = """{"mana_pool":"6","dominance":"5"}"""
        };

        var missingAttributeCandidate = new Auction
        {
            Tag = "CRIMSON_HELMET",
            ItemName = "Crimson Helmet",
            Tier = Tier.MYTHIC,
            Category = Category.ARMOR,
            StartingBid = 121_000_000,
            FlatenedNBTJson = """{"mana_pool":"6"}"""
        };

        var targetFlatNbt = new Dictionary<string, string>
        {
            ["mana_pool"] = "7",
            ["dominance"] = "5"
        };

        var matching = referenceService.EvaluateCandidateMatch(matchingCandidate, target, "Crimson Helmet", targetFlatNbt, null, new List<Enchantment>(), false);
        var missingAttribute = referenceService.EvaluateCandidateMatch(missingAttributeCandidate, target, "Crimson Helmet", targetFlatNbt, null, new List<Enchantment>(), false);

        Assert.That(matching.IsMatch, Is.True,
            $"tier={matching.TierMatches}, reforge={matching.ReforgeMatches}, stack={matching.StackMatches}, name={matching.NameMatches}, nbt={matching.FlatNbtMatches}, enchants={matching.EnchantmentsMatch}");
        Assert.That(missingAttribute.IsMatch, Is.False);
        Assert.That(missingAttribute.FlatNbtMatches, Is.False);
    }

    [Test]
    public void EvaluateCandidateMatch_RequiresCakeSoulCapturedPlayerParity()
    {
        var referenceService = CreateReferenceAuctionService();
        var target = new Auction
        {
            Tag = "CAKE_SOUL",
            ItemName = "Cake Soul",
            Tier = Tier.EPIC,
            Category = Category.MISC,
            StartingBid = 5_000_000,
            FlatenedNBTJson = """{"captured_player":"Technoblade"}"""
        };

        var matchingCandidate = new Auction
        {
            Tag = "CAKE_SOUL",
            ItemName = "Cake Soul",
            Tier = Tier.EPIC,
            Category = Category.MISC,
            StartingBid = 5_200_000,
            FlatenedNBTJson = """{"captured_player":"Technoblade"}"""
        };

        var wrongPlayerCandidate = new Auction
        {
            Tag = "CAKE_SOUL",
            ItemName = "Cake Soul",
            Tier = Tier.EPIC,
            Category = Category.MISC,
            StartingBid = 5_200_000,
            FlatenedNBTJson = """{"captured_player":"Minikloon"}"""
        };

        var targetFlatNbt = new Dictionary<string, string>
        {
            ["captured_player"] = "Technoblade"
        };

        var matching = referenceService.EvaluateCandidateMatch(matchingCandidate, target, "Cake Soul", targetFlatNbt, null, new List<Enchantment>(), false);
        var wrongPlayer = referenceService.EvaluateCandidateMatch(wrongPlayerCandidate, target, "Cake Soul", targetFlatNbt, null, new List<Enchantment>(), false);

        Assert.That(matching.IsMatch, Is.True,
            $"tier={matching.TierMatches}, reforge={matching.ReforgeMatches}, stack={matching.StackMatches}, name={matching.NameMatches}, nbt={matching.FlatNbtMatches}, enchants={matching.EnchantmentsMatch}");
        Assert.That(wrongPlayer.IsMatch, Is.False);
        Assert.That(wrongPlayer.FlatNbtMatches, Is.False);
    }

    [Test]
    public void ApplyAntiMarketManipulation_DedupesBySellerToLowestPrice()
    {
        var auctions = new List<Auction>
        {
            BuildManipAuction("a1", "seller-a", "buyer-a", 5_000_000, "uid-a"),
            BuildManipAuction("a2", "seller-a", "buyer-b", 4_000_000, "uid-b"),
            BuildManipAuction("a3", "seller-c", "buyer-c", 6_000_000, "uid-c")
        };

        var result = ReferenceAuctionService.ApplyAntiMarketManipulation(auctions);

        Assert.That(result.Select(a => a.Uuid).ToList(), Does.Contain("a2"));
        Assert.That(result.Select(a => a.Uuid).ToList(), Does.Not.Contain("a1"));
        Assert.That(result.Select(a => a.Uuid).ToList(), Does.Contain("a3"));
    }

    [Test]
    public void ApplyAntiMarketManipulation_DedupesByBuyer()
    {
        var auctions = new List<Auction>
        {
            BuildManipAuction("b1", "seller-a", "buyer-a", 5_000_000, "uid-a"),
            BuildManipAuction("b2", "seller-b", "buyer-a", 4_000_000, "uid-b"),
            BuildManipAuction("b3", "seller-c", "buyer-c", 6_000_000, "uid-c")
        };

        var result = ReferenceAuctionService.ApplyAntiMarketManipulation(auctions);

        Assert.That(result.Select(a => a.Uuid).ToList(), Does.Contain("b1"));
        Assert.That(result.Select(a => a.Uuid).ToList(), Does.Not.Contain("b2"));
        Assert.That(result.Select(a => a.Uuid).ToList(), Does.Contain("b3"));
    }

    [Test]
    public void ApplyAntiMarketManipulation_DedupesByUid()
    {
        var auctions = new List<Auction>
        {
            BuildManipAuction("c1", "seller-a", "buyer-a", 5_000_000, "same-uid"),
            BuildManipAuction("c2", "seller-b", "buyer-b", 4_000_000, "same-uid"),
            BuildManipAuction("c3", "seller-c", "buyer-c", 6_000_000, "other-uid")
        };

        var result = ReferenceAuctionService.ApplyAntiMarketManipulation(auctions);

        Assert.That(result.Count(a => a.ItemUid == "same-uid"), Is.EqualTo(1));
        Assert.That(result.Select(a => a.Uuid).ToList(), Does.Contain("c3"));
    }

    [Test]
    public void ApplyAntiMarketManipulation_RemovesBackAndForthTradingWithoutUid()
    {
        var auctions = new List<Auction>
        {
            BuildManipAuction("d1", "seller-a", "buyer-a", 5_000_000, null),
            BuildManipAuction("d2", "buyer-a", "seller-a", 4_000_000, null),
            BuildManipAuction("d3", "seller-c", "buyer-c", 6_000_000, "uid-c")
        };

        var result = ReferenceAuctionService.ApplyAntiMarketManipulation(auctions);

        Assert.That(result.Select(a => a.Uuid).ToList(), Does.Not.Contain("d1"));
        Assert.That(result.Select(a => a.Uuid).ToList(), Does.Not.Contain("d2"));
        Assert.That(result.Select(a => a.Uuid).ToList(), Does.Contain("d3"));
    }

    [Test]
    public async Task EvaluateBinAuction_AppliesHitCountDecayParity()
    {
        var target = BuildValuationTargetAuction("flip-hit", startingBid: 8_000_000, highestBidAmount: 8_000_000, count: 1, bin: true);
        var references = BuildReferenceSet("hit", new[] { 12_000_000L, 12_000_000L, 12_000_000L, 12_000_000L, 12_000_000L, 12_000_000L, 12_000_000L, 12_000_000L }, true);

        var baselineService = CreateSeededReferenceAuctionService(target, references);
        var decayedService = CreateSeededReferenceAuctionService(target, references);

        var baseline = await baselineService.EvaluateBinAuctionAsync(target, 0, CancellationToken.None);
        var decayed = await decayedService.EvaluateBinAuctionAsync(target, 4, CancellationToken.None);

        Assert.That(baseline, Is.Not.Null);
        Assert.That(decayed, Is.Not.Null);
        Assert.That(decayed!.TargetPrice, Is.LessThan(baseline!.TargetPrice));
        Assert.That(decayed.EstimatedProfit, Is.LessThan(baseline.EstimatedProfit));
    }

    [Test]
    public async Task EvaluateBinAuction_HalvesMedianWhenTooManyNonBinReferences()
    {
        var target = BuildValuationTargetAuction("flip-nonbin", startingBid: 5_000_000, highestBidAmount: 5_000_000, count: 1, bin: true);
        var mostlyBinRefs = BuildReferenceSet("bin", new[] { 12_000_000L, 12_000_000L, 12_000_000L, 12_000_000L, 12_000_000L, 12_000_000L, 12_000_000L, 12_000_000L }, true);
        var mostlyAuctionRefs = BuildReferenceSet("auc", new[] { 12_000_000L, 12_000_000L, 12_000_000L, 12_000_000L, 12_000_000L, 12_000_000L, 12_000_000L, 12_000_000L }, false);
        mostlyAuctionRefs[0].Bin = true;
        mostlyAuctionRefs[1].Bin = true;

        var binService = CreateSeededReferenceAuctionService(target, mostlyBinRefs);
        var auctionHeavyService = CreateSeededReferenceAuctionService(target, mostlyAuctionRefs);

        var baseline = await binService.EvaluateBinAuctionAsync(target, 0, CancellationToken.None);
        var halved = await auctionHeavyService.EvaluateBinAuctionAsync(target, 0, CancellationToken.None);

        Assert.That(baseline, Is.Not.Null);
        Assert.That(halved, Is.Not.Null);
        Assert.That(halved!.TargetPrice, Is.LessThan(baseline!.TargetPrice));
        Assert.That(halved.EstimatedProfit, Is.LessThan(baseline.EstimatedProfit));
    }

    [Test]
    public async Task EvaluateBinAuction_AppliesExtraLowValueMarginRule()
    {
        var target = BuildValuationTargetAuction("flip-lowvalue", startingBid: 780_000, highestBidAmount: 780_000, count: 1, bin: true);
        var references = BuildReferenceSet("low", new[] { 900_000L, 900_000L, 900_000L, 900_000L, 900_000L, 900_000L, 900_000L, 900_000L }, true);
        var service = CreateSeededReferenceAuctionService(target, references);

        var result = await service.EvaluateBinAuctionAsync(target, 0, CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task EvaluateBinAuction_AppliesStackCountPenalty()
    {
        var singleTarget = BuildValuationTargetAuction("flip-single", startingBid: 20_000_000, highestBidAmount: 20_000_000, count: 1, bin: true);
        var stackTarget = BuildValuationTargetAuction("flip-stack", startingBid: 40_000_000, highestBidAmount: 40_000_000, count: 2, bin: true);
        var singleRefs = BuildReferenceSet("single", new[] { 30_000_000L, 30_000_000L, 30_000_000L, 30_000_000L, 30_000_000L, 30_000_000L, 30_000_000L, 30_000_000L }, true);
        var stackRefs = BuildReferenceSet("stack", new[] { 60_000_000L, 60_000_000L, 60_000_000L, 60_000_000L, 60_000_000L, 60_000_000L, 60_000_000L, 60_000_000L }, true, count: 2);

        var singleService = CreateSeededReferenceAuctionService(singleTarget, singleRefs);
        var stackService = CreateSeededReferenceAuctionService(stackTarget, stackRefs);

        var single = await singleService.EvaluateBinAuctionAsync(singleTarget, 0, CancellationToken.None);
        var stack = await stackService.EvaluateBinAuctionAsync(stackTarget, 0, CancellationToken.None);

        Assert.That(single, Is.Not.Null);
        Assert.That(stack, Is.Not.Null);
        Assert.That(stack!.TargetPrice, Is.LessThan(single!.TargetPrice * 2));
        Assert.That(stack.ProfitMarginPercent, Is.LessThan(single.ProfitMarginPercent));
    }

    private static Auction BuildParityAuction(string uuid, string itemName, string sellerId, string itemUid, string bidderId)
    {
        var referenceEnd = DateTime.UtcNow.AddMinutes(-20);
        var auction = new Auction
        {
            Uuid = uuid,
            UId = Math.Abs(uuid.GetHashCode()),
            Count = 1,
            StartingBid = 19_000_000,
            HighestBidAmount = 19_000_000,
            Tag = "SPEED_WITHER_LEGGINGS",
            ItemName = itemName,
            Start = referenceEnd.AddHours(-1),
            End = referenceEnd,
            AuctioneerId = sellerId,
            Reforge = Reforge.Ancient,
            Category = Category.ARMOR,
            Tier = Tier.MYTHIC,
            Bin = true,
            FlatenedNBTJson = $$"""
                {"rarity_upgrades":"1","hpc":"10","color":"93:47:185","dungeon_item_level":"5","uid":"{{itemUid}}"}
                """,
            ItemCreatedAt = DateTime.UtcNow.AddDays(-10),
            ItemUid = itemUid
        };

        auction.Bids.Add(new BidRecord { BidderId = bidderId, Amount = 19_000_000, Timestamp = referenceEnd.AddMinutes(-1) });
        auction.Enchantments.Add(CreateEnchant(EnchantmentType.thorns, 3));
        auction.Enchantments.Add(CreateEnchant(EnchantmentType.rejuvenate, 5));
        auction.Enchantments.Add(CreateEnchant(EnchantmentType.protection, 6));
        auction.Enchantments.Add(CreateEnchant(EnchantmentType.growth, 5));
        return auction;
    }

    private static Auction BuildManipAuction(string uuid, string sellerId, string buyerId, long highestBidAmount, string? itemUid)
    {
        var auction = new Auction
        {
            Uuid = uuid,
            Tag = "HYPERION",
            ItemName = "Hyperion",
            Tier = Tier.MYTHIC,
            Category = Category.WEAPON,
            Count = 1,
            HighestBidAmount = highestBidAmount,
            StartingBid = highestBidAmount,
            AuctioneerId = sellerId,
            ItemUid = itemUid,
            End = DateTime.UtcNow.AddMinutes(-10),
            FlatenedNBTJson = itemUid == null ? "{}" : $$"""{"uid":"{{itemUid}}"}"""
        };
        auction.Bids.Add(new BidRecord
        {
            BidderId = buyerId,
            Amount = highestBidAmount,
            Timestamp = DateTime.UtcNow.AddMinutes(-11)
        });
        return auction;
    }

    private static Auction BuildValuationTargetAuction(string uuid, long startingBid, long highestBidAmount, int count, bool bin)
    {
        return new Auction
        {
            Uuid = uuid,
            Tag = "HYPERION",
            ItemName = "Hyperion",
            Tier = Tier.MYTHIC,
            Category = Category.WEAPON,
            Count = count,
            StartingBid = startingBid,
            HighestBidAmount = highestBidAmount,
            Bin = bin,
            Start = DateTime.UtcNow.AddMinutes(-5),
            End = DateTime.UtcNow.AddMinutes(30),
            AuctioneerId = "target-seller",
            Reforge = Reforge.Withered,
            ItemCreatedAt = DateTime.UtcNow.AddDays(-10),
            FlatenedNBTJson = """{}"""
        };
    }

    private static List<Auction> BuildReferenceSet(string prefix, IReadOnlyList<long> prices, bool bin, int count = 1)
    {
        return prices.Select((price, i) => new Auction
        {
            Uuid = $"{prefix}-{i}",
            Tag = "HYPERION",
            ItemName = "Hyperion",
            Tier = Tier.MYTHIC,
            Category = Category.WEAPON,
            Count = count,
            StartingBid = price,
            HighestBidAmount = price,
            Bin = bin,
            Start = DateTime.UtcNow.AddHours(-3),
            End = DateTime.UtcNow.AddHours(-2).AddMinutes(i),
            AuctioneerId = $"{prefix}-seller-{i}",
            Reforge = Reforge.Withered,
            ItemCreatedAt = DateTime.UtcNow.AddDays(-10),
            ItemUid = $"{prefix}-uid-{i}",
            FlatenedNBTJson = $$"""{"uid":"{{prefix}}-uid-{{i}}"}"""
        }).Select((auction, i) =>
        {
            auction.Bids.Add(new BidRecord
            {
                BidderId = $"{prefix}-buyer-{i}",
                Amount = auction.HighestBidAmount,
                Timestamp = auction.End.AddMinutes(-1)
            });
            return auction;
        }).ToList();
    }

    private static Enchantment CreateEnchant(EnchantmentType type, byte level) => new(type, level);

    private static CacheKeyService CreateCacheKeyService()
        => new(NullLogger<CacheKeyService>.Instance);

    private static ReferenceAuctionService CreateReferenceAuctionService(IServiceScopeFactory? scopeFactory = null)
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var componentValueService = new ComponentValueService(
            new StaticHttpClientFactory(),
            memoryCache,
            NullLogger<ComponentValueService>.Instance);

        return new ReferenceAuctionService(
            scopeFactory ?? new EmptyScopeFactory(),
            memoryCache,
            CreateCacheKeyService(),
            componentValueService,
            NullLogger<ReferenceAuctionService>.Instance);
    }

    private static ReferenceAuctionService CreateSeededReferenceAuctionService(Auction target, List<Auction> references)
    {
        var dbName = Guid.NewGuid().ToString("N");
        var dbRoot = new InMemoryDatabaseRoot();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName, dbRoot)
            .Options;

        using (var context = new AppDbContext(options))
        {
            context.Auctions.Add(CloneAuction(target));
            context.Auctions.AddRange(references.Select(CloneAuction));
            context.SaveChanges();
        }

        var provider = new ServiceCollection()
            .AddSingleton(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName, dbRoot).Options)
            .AddScoped(_ => new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName, dbRoot).Options))
            .BuildServiceProvider();

        return CreateReferenceAuctionService(provider.GetRequiredService<IServiceScopeFactory>());
    }

    private static Auction CloneAuction(Auction source)
    {
        var clone = new Auction
        {
            Uuid = source.Uuid,
            UId = source.UId,
            Tag = source.Tag,
            ItemName = source.ItemName,
            Tier = source.Tier,
            Category = source.Category,
            Count = source.Count,
            StartingBid = source.StartingBid,
            HighestBidAmount = source.HighestBidAmount,
            Bin = source.Bin,
            Start = source.Start,
            End = source.End,
            AuctioneerId = source.AuctioneerId,
            Reforge = source.Reforge,
            ItemCreatedAt = source.ItemCreatedAt,
            ItemUid = source.ItemUid,
            FlatenedNBTJson = source.FlatenedNBTJson
        };

        foreach (var bid in source.Bids)
        {
            clone.Bids.Add(new BidRecord
            {
                BidderId = bid.BidderId,
                Amount = bid.Amount,
                Timestamp = bid.Timestamp
            });
        }

        foreach (var enchantment in source.Enchantments)
        {
            clone.Enchantments.Add(new Enchantment(enchantment.Type, enchantment.Level));
        }

        foreach (var lookup in source.NBTLookups)
        {
            clone.NBTLookups.Add(new NBTLookup
            {
                KeyId = lookup.KeyId,
                ValueId = lookup.ValueId,
                ValueNumeric = lookup.ValueNumeric,
                NBTKey = lookup.NBTKey,
                NBTValue = lookup.NBTValue
            });
        }

        return clone;
    }

    private sealed class StaticHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name = "")
        {
            return new HttpClient(new StaticHttpHandler());
        }
    }

    private sealed class StaticHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""{"products":{}}""")
            };
            return Task.FromResult(response);
        }
    }

    private sealed class EmptyScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new InvalidOperationException("ScopeFactory not configured for this test.");
    }
}
