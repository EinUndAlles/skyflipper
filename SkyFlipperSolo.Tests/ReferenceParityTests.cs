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
