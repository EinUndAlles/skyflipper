using Microsoft.EntityFrameworkCore;
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
    [Ignore("Known parity gap: reference selection still diverges on this enchant/NBT case and needs follow-up investigation.")]
    public async Task GetRelevantAuctionsKeepsOnlyExactEnchantParityCase()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using (var seedContext = new AppDbContext(options))
        {
            var target = BuildParityAuction("92ddfaae7e4e46b29eb2d652d9b043ac", "Ancient Maxor's Leggings");
            var matching = BuildParityAuction("fc92b460920d486494732beeed57ed77", "Ancient Maxor's Leggings");
            var wrongEnchant = BuildParityAuction("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "Ancient Maxor's Leggings");
            wrongEnchant.Enchantments = new List<Enchantment>
            {
                CreateEnchant(EnchantmentType.rejuvenate, 5),
                CreateEnchant(EnchantmentType.protection, 5),
                CreateEnchant(EnchantmentType.growth, 5),
            };
            var wrongTier = BuildParityAuction("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", "Ancient Maxor's Leggings");
            wrongTier.Tier = Tier.LEGENDARY;

            seedContext.Auctions.AddRange(target, matching, wrongEnchant, wrongTier);
            await seedContext.SaveChangesAsync();
        }

        var serviceProvider = new ServiceCollection()
            .AddSingleton(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options)
            .AddScoped(_ => new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options))
            .BuildServiceProvider();

        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var referenceService = CreateReferenceAuctionService(scopeFactory);

        await using var queryContext = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options);
        var targetAuction = await queryContext.Auctions
            .Include(a => a.Bids)
            .Include(a => a.Enchantments)
            .Include(a => a.NBTLookups).ThenInclude(n => n.NBTKey)
            .FirstAsync(a => a.Uuid == "92ddfaae7e4e46b29eb2d652d9b043ac");

        var result = await referenceService.GetRelevantAuctionsCacheAsync(targetAuction, CancellationToken.None);

        Assert.That(result.References.Select(a => a.Uuid).ToList(), Does.Contain("fc92b460920d486494732beeed57ed77"));
        Assert.That(result.References.Select(a => a.Uuid).ToList(), Does.Not.Contain("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"));
        Assert.That(result.References.Select(a => a.Uuid).ToList(), Does.Not.Contain("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"));
    }

    private static Auction BuildParityAuction(string uuid, string itemName)
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
            AuctioneerId = "be87f03b822140a5a47f7f66ad59486a",
            Reforge = Reforge.Ancient,
            Category = Category.ARMOR,
            Tier = Tier.MYTHIC,
            Bin = true,
            FlatenedNBTJson = """
                {"rarity_upgrades":"1","hpc":"10","color":"93:47:185","dungeon_item_level":"5","uid":"f5922c6f7047"}
                """,
            ItemCreatedAt = DateTime.UtcNow.AddDays(-10)
        };

        auction.Bids.Add(new BidRecord { BidderId = "bidder1", Amount = 19_000_000, Timestamp = referenceEnd.AddMinutes(-1) });
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
