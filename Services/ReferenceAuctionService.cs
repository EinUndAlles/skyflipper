using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services;

/// <summary>
/// Ports Coflnet's FlippingEngine reference-selection flow:
/// candidate auction -> reference query -> anti-manipulation -> weighted median.
/// </summary>
public class ReferenceAuctionService
{
    private static readonly DateTime UnlockedIntroduction = new(2021, 9, 4, 0, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan ReferenceCacheDuration = TimeSpan.FromHours(2);
    private static readonly HashSet<Reforge> RelevantReforges = new()
    {
        Reforge.Gilded, Reforge.Withered, Reforge.Spiritual, Reforge.Jaded, Reforge.Warped,
        Reforge.AoteStone, Reforge.Toil, Reforge.Fabled, Reforge.Giant, Reforge.Submerged,
        Reforge.Renowned, Reforge.Mossy, Reforge.Rooted, Reforge.Festive, Reforge.Lustrous,
        Reforge.Glacial, Reforge.Coldfused, Reforge.Moonglade, Reforge.BloodShot
    };

    private static readonly HashSet<string> AttributeKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "lifeline", "breeze", "speed", "experience", "mana_pool", "life_regeneration",
        "blazing_resistance", "arachno_resistance", "undead_resistance", "blazing_fortune",
        "fishing_experience", "double_hook", "infection", "trophy_hunter", "fisherman",
        "hunter", "fishing_speed", "life_recovery", "ignition", "combo", "attack_speed",
        "midas_touch", "mana_regeneration", "veteran", "mending", "ender_resistance",
        "dominance", "ender", "mana_steal", "blazing", "elite", "arachno", "undead",
        "warrior", "deadeye", "fortitude", "magic_find"
    };

    private static readonly HashSet<string> ValuablePetItems = new(StringComparer.OrdinalIgnoreCase)
    {
        "MINOS_RELIC", "QUICK_CLAW", "PET_ITEM_QUICK_CLAW", "PET_ITEM_TIER_BOOST",
        "PET_ITEM_LUCKY_CLOVER", "PET_ITEM_LUCKY_CLOVER_DROP", "GREEN_BANDANA",
        "PET_ITEM_COMBAT_SKILL_BOOST_EPIC", "PET_ITEM_FISHING_SKILL_BOOST_EPIC",
        "PET_ITEM_FORAGING_SKILL_BOOST_EPIC", "ALL_SKILLS_SUPER_BOOST", "PET_ITEM_EXP_SHARE"
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly CacheKeyService _cacheKeyService;
    private readonly ComponentValueService _componentValueService;
    private readonly ILogger<ReferenceAuctionService> _logger;

    public ReferenceAuctionService(
        IServiceScopeFactory scopeFactory,
        IMemoryCache memoryCache,
        CacheKeyService cacheKeyService,
        ComponentValueService componentValueService,
        ILogger<ReferenceAuctionService> logger)
    {
        _scopeFactory = scopeFactory;
        _memoryCache = memoryCache;
        _cacheKeyService = cacheKeyService;
        _componentValueService = componentValueService;
        _logger = logger;
    }

    public async Task<FlipValuationResult?> EvaluateBinAuctionAsync(
        Auction auction,
        int hitCount,
        CancellationToken stoppingToken)
    {
        var totalPrice = auction.HighestBidAmount > 0 ? auction.HighestBidAmount : auction.StartingBid;
        return await EvaluateAuctionAsync(auction, totalPrice, hitCount, "BIN", stoppingToken);
    }

    public async Task<FlipValuationResult?> EvaluateBidAuctionAsync(
        Auction auction,
        int hitCount,
        CancellationToken stoppingToken)
    {
        var expectedPrice = auction.HighestBidAmount == 0
            ? auction.StartingBid
            : (long)(auction.HighestBidAmount * 1.1);

        return await EvaluateAuctionAsync(auction, expectedPrice, hitCount, "Bid", stoppingToken);
    }

    private async Task<FlipValuationResult?> EvaluateAuctionAsync(
        Auction auction,
        long totalPurchasePrice,
        int hitCount,
        string dataSourcePrefix,
        CancellationToken stoppingToken)
    {
        if (auction.Count <= 0 ||
            auction.ItemName == "null" ||
            auction.Tag == "ATTRIBUTE_SHARD" ||
            auction.Tag.Contains(':') ||
            CacheKeyService.HasMasterCryptSols(auction))
        {
            return null;
        }

        var purchasePricePerItem = totalPurchasePrice / auction.Count;
        if (purchasePricePerItem < 3_000)
            return null;

        var references = await GetRelevantAuctionsCacheAsync(auction, stoppingToken);
        if (references.References.Count <= 7)
            return null;

        var medianPrice = await GetWeightedMedianAsync(auction, references.References);
        var binReferenceCount = references.References.Count(a => a.Bin);
        if (references.References.Count > binReferenceCount * 2)
            medianPrice /= 2;

        var hitCountReduction = Math.Pow(1.05, hitCount);
        var (additionalWorth, gemBreakdown) = await _componentValueService.GetGemstoneValue(auction);

        var recommendedBuyUnder = (medianPrice * 0.9 + additionalWorth) / hitCountReduction;
        if (recommendedBuyUnder < 1_000_000)
            recommendedBuyUnder *= 0.9;

        if (purchasePricePerItem > recommendedBuyUnder || recommendedBuyUnder < 100_000)
            return null;

        var countMultiplier = auction.Count > 1 ? 0.9 : 1d;
        var targetPrice = (long)((medianPrice * auction.Count * countMultiplier / hitCountReduction) + additionalWorth);
        if (targetPrice <= totalPurchasePrice)
            return null;

        var estimatedProfit = targetPrice - totalPurchasePrice;
        var marginPercent = targetPrice == 0 ? 0 : estimatedProfit * 100.0 / targetPrice;
        var refAgeDays = Math.Max(0, (int)(DateTime.UtcNow - references.Oldest).TotalDays);
        var breakdown = additionalWorth > 0
            ? $"Base: {medianPrice / 1_000_000.0:F1}m + Gems: {gemBreakdown}"
            : $"Base: {medianPrice / 1_000_000.0:F1}m";

        return new FlipValuationResult(
            _cacheKeyService.GeneratePriceCacheKey(auction),
            targetPrice,
            estimatedProfit,
            marginPercent,
            $"{dataSourcePrefix} refs ({references.References.Count}, {refAgeDays}d)",
            breakdown,
            references.References.Count,
            references.Oldest);
    }

    public async Task<RelevantReferenceResult> GetRelevantAuctionsCacheAsync(Auction auction, CancellationToken stoppingToken)
    {
        var cacheKey = _cacheKeyService.GeneratePriceCacheKey(auction);
        if (_memoryCache.TryGetValue<RelevantReferenceResult>(cacheKey, out var cached))
            return cached!;

        var fetched = await GetRelevantAuctionsAsync(auction, stoppingToken);
        _memoryCache.Set(cacheKey, fetched, ReferenceCacheDuration);
        return fetched;
    }

    public async Task<RelevantReferenceDebugResult> DebugRelevantAuctionsAsync(Auction auction, CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var clearedName = auction.Reforge != Reforge.None ? RemoveReforgePrefix(auction.ItemName) : auction.ItemName;
        var youngest = DateTime.UtcNow;
        var oldest = DateTime.UtcNow - TimeSpan.FromHours(2);

        var relevantEnchants = CacheKeyService.ExtractRelevantEnchants(auction.Enchantments);
        var ultimate = relevantEnchants.FirstOrDefault(e => e.Type.ToString().StartsWith("ultimate_", StringComparison.OrdinalIgnoreCase));

        var initial = await GetSelect(auction, clearedName, youngest, oldest, ultimate, relevantEnchants, 30, false, dbContext, stoppingToken);
        var stageReferences = initial.ToList();
        var initialCount = stageReferences.Count;
        var expandedDayCount = 0;
        var expandedWeekCount = 0;
        var reducedCount = 0;
        var recentReducedCount = 0;
        var antiManipulationSourceCount = 0;

        if (stageReferences.Count < 11)
        {
            youngest = oldest;
            oldest = DateTime.UtcNow - TimeSpan.FromDays(1.5);
            var dayReferences = await GetSelect(auction, clearedName, youngest, oldest, ultimate, relevantEnchants, 90, false, dbContext, stoppingToken);
            expandedDayCount = dayReferences.Count;
            stageReferences = stageReferences.Concat(dayReferences).ToList();

            if (stageReferences.Count < 50)
            {
                youngest = oldest;
                oldest = DateTime.UtcNow - TimeSpan.FromDays(8);
                var weekReferences = await GetSelect(auction, clearedName, youngest, oldest, ultimate, relevantEnchants, 120, false, dbContext, stoppingToken);
                expandedWeekCount = weekReferences.Count;
                stageReferences.AddRange(weekReferences);

                if (stageReferences.Count < 10)
                {
                    youngest = DateTime.UtcNow;
                    clearedName = clearedName.Replace("âœª", "", StringComparison.Ordinal).Trim();
                    var reducedReferences = await GetSelect(auction, clearedName, youngest, oldest, ultimate, relevantEnchants, 120, true, dbContext, stoppingToken);
                    reducedCount = reducedReferences.Count;
                    stageReferences = reducedReferences;

                    var recentReducedReferences = await GetSelect(
                        auction,
                        clearedName,
                        youngest,
                        DateTime.UtcNow - TimeSpan.FromDays(0.5),
                        ultimate,
                        relevantEnchants,
                        10,
                        true,
                        dbContext,
                        stoppingToken);
                    recentReducedCount = recentReducedReferences.Count;
                    stageReferences.AddRange(recentReducedReferences);
                }
            }
        }
        else if (stageReferences.Count >= 30)
        {
            var veryRecent = await GetSelect(
                auction,
                clearedName,
                youngest,
                DateTime.UtcNow - TimeSpan.FromMinutes(15),
                ultimate,
                relevantEnchants,
                20,
                true,
                dbContext,
                stoppingToken);
            recentReducedCount = veryRecent.Count;
            stageReferences.AddRange(veryRecent);
        }

        if (stageReferences.Count > 0 && stageReferences.All(a => a.End > DateTime.UtcNow - TimeSpan.FromDays(1)))
        {
            var antiManipulationReferences = await GetSelect(
                auction,
                clearedName,
                DateTime.UtcNow - TimeSpan.FromDays(5),
                DateTime.UtcNow - TimeSpan.FromDays(6),
                ultimate,
                relevantEnchants,
                40,
                true,
                dbContext,
                stoppingToken);
            antiManipulationSourceCount = antiManipulationReferences.Count;
            stageReferences.AddRange(antiManipulationReferences);
        }

        var beforeAntiManipulationCount = stageReferences.Count;
        var afterAntiManipulation = ApplyAntiMarketManipulation(stageReferences);
        var afterAntiManipulationCount = afterAntiManipulation.Count;

        if (!CacheKeyService.HasMasterCryptSols(auction))
            afterAntiManipulation = afterAntiManipulation.Where(a => !CacheKeyService.HasMasterCryptSols(a)).ToList();

        return new RelevantReferenceDebugResult(
            afterAntiManipulation,
            initialCount,
            expandedDayCount,
            expandedWeekCount,
            reducedCount,
            recentReducedCount,
            antiManipulationSourceCount,
            beforeAntiManipulationCount,
            afterAntiManipulationCount);
    }

    private async Task<RelevantReferenceResult> GetRelevantAuctionsAsync(Auction auction, CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var clearedName = auction.Reforge != Reforge.None ? RemoveReforgePrefix(auction.ItemName) : auction.ItemName;
        var youngest = DateTime.UtcNow;
        var oldest = DateTime.UtcNow - TimeSpan.FromHours(2);

        var relevantEnchants = CacheKeyService.ExtractRelevantEnchants(auction.Enchantments);
        var ultimate = relevantEnchants.FirstOrDefault(e => e.Type.ToString().StartsWith("ultimate_", StringComparison.OrdinalIgnoreCase));

        var relevantAuctions = await GetSelect(auction, clearedName, youngest, oldest, ultimate, relevantEnchants, 30, false, dbContext, stoppingToken);

        if (relevantAuctions.Count < 11)
        {
            youngest = oldest;
            oldest = DateTime.UtcNow - TimeSpan.FromDays(1.5);
            relevantAuctions = relevantAuctions
                .Concat(await GetSelect(auction, clearedName, youngest, oldest, ultimate, relevantEnchants, 90, false, dbContext, stoppingToken))
                .ToList();

            if (relevantAuctions.Count < 50)
            {
                youngest = oldest;
                oldest = DateTime.UtcNow - TimeSpan.FromDays(8);
                relevantAuctions.AddRange(await GetSelect(auction, clearedName, youngest, oldest, ultimate, relevantEnchants, 120, false, dbContext, stoppingToken));
                if (relevantAuctions.Count < 10)
                {
                    youngest = DateTime.UtcNow;
                    clearedName = clearedName.Replace("✪", "", StringComparison.Ordinal).Trim();
                    relevantAuctions = await GetSelect(auction, clearedName, youngest, oldest, ultimate, relevantEnchants, 120, true, dbContext, stoppingToken);
                    relevantAuctions.AddRange(await GetSelect(
                        auction,
                        clearedName,
                        youngest,
                        DateTime.UtcNow - TimeSpan.FromDays(0.5),
                        ultimate,
                        relevantEnchants,
                        10,
                        true,
                        dbContext,
                        stoppingToken));
                }
            }
        }
        else if (relevantAuctions.Count >= 30)
        {
            relevantAuctions.AddRange(await GetSelect(
                auction,
                clearedName,
                youngest,
                DateTime.UtcNow - TimeSpan.FromMinutes(15),
                ultimate,
                relevantEnchants,
                20,
                true,
                dbContext,
                stoppingToken));
        }

        if (relevantAuctions.Count > 0 && relevantAuctions.All(a => a.End > DateTime.UtcNow - TimeSpan.FromDays(1)))
        {
            relevantAuctions.AddRange(await GetSelect(
                auction,
                clearedName,
                DateTime.UtcNow - TimeSpan.FromDays(5),
                DateTime.UtcNow - TimeSpan.FromDays(6),
                ultimate,
                relevantEnchants,
                40,
                true,
                dbContext,
                stoppingToken));
        }

        relevantAuctions = ApplyAntiMarketManipulation(relevantAuctions);

        if (!CacheKeyService.HasMasterCryptSols(auction))
            relevantAuctions = relevantAuctions.Where(a => !CacheKeyService.HasMasterCryptSols(a)).ToList();

        return new RelevantReferenceResult(relevantAuctions, oldest);
    }

    private async Task<List<Auction>> GetSelect(
        Auction auction,
        string clearedName,
        DateTime youngest,
        DateTime oldest,
        Enchantment? ultimate,
        List<Enchantment> relevantEnchants,
        int limit,
        bool reduced,
        AppDbContext dbContext,
        CancellationToken stoppingToken)
    {
        var baseQuery = dbContext.Auctions
            .AsNoTracking()
            .Where(a => a.Tag == auction.Tag &&
                        a.Uuid != auction.Uuid &&
                        a.End > oldest &&
                        a.End < youngest &&
                        a.HighestBidAmount > 0)
            .Include(a => a.Bids)
            .Include(a => a.Enchantments)
            .Include(a => a.NBTLookups)
                .ThenInclude(n => n.NBTKey)
            .Include(a => a.NBTLookups)
                .ThenInclude(n => n.NBTValue)
            .OrderByDescending(a => a.End);

        var roughLimit = Math.Max(limit * 8, 200);
        var candidates = await baseQuery.Take(roughLimit).ToListAsync(stoppingToken);
        var targetFlatNbt = GetFlatNbt(auction);

        var filtered = candidates
            .Where(candidate => MatchesCandidate(candidate, auction, clearedName, targetFlatNbt, ultimate, relevantEnchants, reduced))
            .Take(limit)
            .ToList();

        return filtered;
    }

    private bool MatchesCandidate(
        Auction candidate,
        Auction target,
        string clearedName,
        Dictionary<string, string> targetFlatNbt,
        Enchantment? ultimate,
        List<Enchantment> relevantEnchants,
        bool reduced)
    {
        var debug = EvaluateCandidateMatch(candidate, target, clearedName, targetFlatNbt, ultimate, relevantEnchants, reduced);
        return debug.IsMatch;
    }

    public CandidateMatchDebug EvaluateCandidateMatch(
        Auction candidate,
        Auction target,
        string clearedName,
        Dictionary<string, string> targetFlatNbt,
        Enchantment? ultimate,
        List<Enchantment> relevantEnchants,
        bool reduced)
    {
        var recombMatters = CacheKeyService.DoesRecombMatter(target.Category, target.Tag);
        var tierMatches = !(target.Tag != "ENCHANTED_BOOK" && (recombMatters || CacheKeyService.IsPet(target.Tag)) && candidate.Tier != target.Tier);

        var reforgeMatches = ReforgeMatches(candidate, target, reduced);

        var stackMatches = !(target.Count == 64 && !reduced && candidate.Count != target.Count);

        var nameMatches = NameMatches(candidate, target, clearedName);

        var flatNbtMatches = FlatNbtMatches(candidate, target, targetFlatNbt, recombMatters, reduced);

        var enchantmentsMatch = EnchantmentsMatch(candidate, target, relevantEnchants, ultimate, reduced);

        return new CandidateMatchDebug(
            tierMatches && reforgeMatches && stackMatches && nameMatches && flatNbtMatches && enchantmentsMatch,
            tierMatches,
            reforgeMatches,
            stackMatches,
            nameMatches,
            flatNbtMatches,
            enchantmentsMatch);
    }

    private static bool ReforgeMatches(Auction candidate, Auction target, bool reduced)
    {
        var shouldDropAncient = reduced && target.Reforge == Reforge.Ancient;
        if (RelevantReforges.Contains(target.Reforge) && !shouldDropAncient)
            return candidate.Reforge == target.Reforge;

        return !RelevantReforges.Contains(candidate.Reforge);
    }

    private static bool NameMatches(Auction candidate, Auction target, string clearedName)
    {
        if (target.ItemName != clearedName && !string.IsNullOrEmpty(clearedName))
            return candidate.ItemName.Contains(clearedName, StringComparison.Ordinal);

        if (target.Tag.StartsWith("PET", StringComparison.Ordinal) && target.ItemName.StartsWith("[", StringComparison.Ordinal))
        {
            var expectedPattern = GetPetLevelSelectValue(target.ItemName);
            return PetNameMatches(candidate.ItemName, expectedPattern);
        }

        return candidate.ItemName == clearedName;
    }

    private bool FlatNbtMatches(Auction candidate, Auction target, Dictionary<string, string> targetFlatNbt, bool recombMatters, bool reduced)
    {
        var candidateFlatNbt = GetFlatNbt(candidate);

        if ((target.Tag.EndsWith("MIDAS_STAFF", StringComparison.Ordinal) || target.Tag.EndsWith("MIDAS_SWORD", StringComparison.Ordinal)) &&
            !RangeMatches(candidateFlatNbt, targetFlatNbt, "additional_coins", 2_000_000, 3))
            return false;

        if ((target.Tag.EndsWith("MIDAS_STAFF", StringComparison.Ordinal) || target.Tag.EndsWith("MIDAS_SWORD", StringComparison.Ordinal)) &&
            !RangeMatches(candidateFlatNbt, targetFlatNbt, "winning_bid", 2_000_000, 3))
            return false;

        if (targetFlatNbt.ContainsKey("seconds_held") && !RangeMatches(candidateFlatNbt, targetFlatNbt, "seconds_held", 20_000, 10))
            return false;

        if ((target.Tag.Contains("HOE", StringComparison.Ordinal) || targetFlatNbt.ContainsKey("farming_for_dummies_count")) &&
            !SelectStyleMatch(candidateFlatNbt, targetFlatNbt, "farming_for_dummies_count"))
            return false;

        if (target.Tag == "CAKE_SOUL" && !SelectStyleMatch(candidateFlatNbt, targetFlatNbt, "captured_player"))
            return false;

        if (targetFlatNbt.ContainsKey("rarity_upgrades") && recombMatters &&
            !SelectStyleMatch(candidateFlatNbt, targetFlatNbt, "rarity_upgrades"))
            return false;

        if ((target.Tag == "ASPECT_OF_THE_VOID" || target.Tag == "ASPECT_OF_THE_END") &&
            !SelectStyleMatch(candidateFlatNbt, targetFlatNbt, "ethermerge"))
            return false;

        if (target.Tag == "NECRONS_LADDER" && !SelectStyleMatch(candidateFlatNbt, targetFlatNbt, "handles_found"))
            return false;

        if (target.Tag == "DIANAS_BOOKSHELF" && !SelectStyleMatch(candidateFlatNbt, targetFlatNbt, "chimera_found"))
            return false;

        if (targetFlatNbt.ContainsKey("edition") && !RangeMatches(candidateFlatNbt, targetFlatNbt, "edition", 100, 10))
            return false;

        if (targetFlatNbt.ContainsKey("new_years_cake") && !SelectStyleMatch(candidateFlatNbt, targetFlatNbt, "new_years_cake"))
            return false;

        if (targetFlatNbt.ContainsKey("dungeon_item_level") && !reduced &&
            !SelectStyleMatch(candidateFlatNbt, targetFlatNbt, "dungeon_item_level"))
            return false;

        if (targetFlatNbt.ContainsKey("candyUsed") && !CandyMatches(candidateFlatNbt, targetFlatNbt))
            return false;

        foreach (var exactKey in new[] { "art_of_war_count", "MUSIC", "ENCHANT", "DRAGON", "TIDAL", "ability_scroll", "party_hat_emoji" })
        {
            if (targetFlatNbt.ContainsKey(exactKey) && !SelectStyleMatch(candidateFlatNbt, targetFlatNbt, exactKey))
                return false;
        }

        if (target.Tag.Contains("FINAL_DESTINATION", StringComparison.Ordinal) &&
            !RangeMatches(candidateFlatNbt, targetFlatNbt, "eman_kills", 1000, 10))
            return false;

        if (targetFlatNbt.Keys.Any(k => AttributeKeys.Contains(k)))
        {
            foreach (var attributeKey in AttributeKeys)
            {
                if (!SelectStyleMatch(candidateFlatNbt, targetFlatNbt, attributeKey))
                    return false;
            }
        }

        if (target.Tag.Contains("_DRILL", StringComparison.Ordinal))
        {
            foreach (var drillKey in new[] { "drill_part_engine", "drill_part_fuel_tank", "drill_part_upgrade_module" })
            {
                if (!SelectStyleMatch(candidateFlatNbt, targetFlatNbt, drillKey))
                    return false;
            }
        }

        if (!PetItemMatches(candidateFlatNbt, targetFlatNbt, target.StartingBid))
            return false;

        if (targetFlatNbt.ContainsKey("skin") && !StringEquals(candidateFlatNbt, targetFlatNbt, "skin"))
            return false;

        if ((targetFlatNbt.ContainsKey("color") || CacheKeyService.IsArmor(target.Tag)) &&
            (!SelectStyleMatch(candidateFlatNbt, targetFlatNbt, "color") || !SelectStyleMatch(candidateFlatNbt, targetFlatNbt, "dye_item")))
            return false;

        foreach (var item in targetFlatNbt.Where(k => k.Key.EndsWith("_kills", StringComparison.Ordinal)))
        {
            if (!KillRangeMatches(candidateFlatNbt, item.Key, item.Value))
                return false;
        }

        if (targetFlatNbt.ContainsKey("unlocked_slots"))
        {
            if (!SelectStyleMatch(candidateFlatNbt, targetFlatNbt, "unlocked_slots"))
                return false;
            if (candidate.ItemCreatedAt <= UnlockedIntroduction)
                return false;
        }

        if (targetFlatNbt.ContainsKey("gemstone_slots") && !SelectStyleMatch(candidateFlatNbt, targetFlatNbt, "gemstone_slots"))
            return false;

        return true;
    }

    private static bool EnchantmentsMatch(
        Auction candidate,
        Auction target,
        List<Enchantment> relevantEnchants,
        Enchantment? ultimate,
        bool reduced)
    {
        if (relevantEnchants.Count > 0)
        {
            var targetRelevant = reduced
                ? new List<Enchantment> { SelectBestEnchant(relevantEnchants) }
                : relevantEnchants;

            var candidateRelevant = CacheKeyService.ExtractRelevantEnchants(candidate.Enchantments);
            if (ultimate != null && !candidateRelevant.Any(e => e.Type == ultimate.Type && e.Level == ultimate.Level))
                return false;

            foreach (var required in targetRelevant.Where(e => e.Type != ultimate?.Type))
            {
                if (!candidateRelevant.Any(e => e.Type == required.Type && e.Level == required.Level))
                    return false;
            }

            var allowedTypes = targetRelevant.Select(e => e.Type).ToHashSet();
            if (ultimate != null)
                allowedTypes.Add(ultimate.Type);

            return candidateRelevant.All(e => allowedTypes.Contains(e.Type));
        }

        if (target.Tag == "ENCHANTED_BOOK" && target.Enchantments.Count == 1)
        {
            return candidate.Enchantments.Count == 1 &&
                   candidate.Enchantments[0].Type == target.Enchantments[0].Type &&
                   candidate.Enchantments[0].Level == target.Enchantments[0].Level;
        }

        if (target.Tag == "ENCHANTED_BOOK" && target.Enchantments.Count == 2)
        {
            return candidate.Enchantments.Count == 2 &&
                   target.Enchantments.All(t => candidate.Enchantments.Any(c => c.Type == t.Type && c.Level == t.Level));
        }

        if (target.Enchantments.Any())
            return !CacheKeyService.ExtractRelevantEnchants(candidate.Enchantments).Any();

        return !candidate.Enchantments.Any();
    }

    public async Task<long> GetWeightedMedianAsync(Auction auction, List<Auction> relevantAuctions)
    {
        var auctions = (await Task.WhenAll(relevantAuctions.Select(async reference => new
        {
            Auction = reference,
            Value = reference.HighestBidAmount / Math.Max(reference.Count, 1) / (reference.Count == auction.Count ? 1 : 3) - (await _componentValueService.GetGemstoneValueOnly(reference)).GemValue
        }))).ToList();

        var fullTime = auctions.Select(a => a.Value)
            .OrderByDescending(a => a)
            .Skip(relevantAuctions.Count / 2)
            .FirstOrDefault();

        var shortTerm = auctions.OrderByDescending(a => a.Auction.End)
            .Take(3)
            .OrderByDescending(a => a.Value)
            .Select(a => a.Value)
            .Skip(1)
            .FirstOrDefault();

        if (auctions.Count > 10 && relevantAuctions.All(a => a.End > DateTime.UtcNow - TimeSpan.FromHours(20)))
        {
            fullTime = auctions.Select(a => a.Value)
                .OrderBy(a => a)
                .Skip(relevantAuctions.Count / 4)
                .FirstOrDefault();
        }

        return Math.Min(fullTime, shortTerm);
    }

    private static List<Auction> ApplyAntiMarketManipulation(List<Auction> relevantAuctions)
    {
        var counter = 1;
        if (relevantAuctions.Count > 1)
        {
            relevantAuctions = relevantAuctions
                .Where(a => a.Bids.Count > 0)
                .GroupBy(a => a.AuctioneerId)
                .Select(g => g.OrderBy(a => a.HighestBidAmount).First())
                .GroupBy(a => a.Bids.OrderByDescending(b => b.Amount).First().BidderId)
                .Select(g => g.First())
                .GroupBy(a => a.ItemUid ?? counter++.ToString())
                .Select(g => g.First())
                .ToList();
        }

        if (counter > 2)
        {
            var pairs = relevantAuctions
                .Where(a => a.Bids.Count > 0 && !string.IsNullOrEmpty(a.AuctioneerId))
                .Select(a =>
                {
                    var buyer = a.Bids.OrderByDescending(b => b.Amount).First().BidderId;
                    var seller = a.AuctioneerId!;
                    return string.CompareOrdinal(buyer, seller) < 0 ? (buyer, seller) : (seller, buyer);
                })
                .GroupBy(a => a)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToHashSet();

            if (pairs.Count > 0)
            {
                relevantAuctions = relevantAuctions.Where(a =>
                {
                    if (a.Bids.Count == 0 || string.IsNullOrEmpty(a.AuctioneerId))
                        return true;
                    var buyer = a.Bids.OrderByDescending(b => b.Amount).First().BidderId;
                    var seller = a.AuctioneerId!;
                    var pair = string.CompareOrdinal(buyer, seller) < 0 ? (buyer, seller) : (seller, buyer);
                    return !pairs.Contains(pair);
                }).ToList();
            }
        }

        foreach (var item in relevantAuctions)
            item.Bids = new List<BidRecord>();

        return relevantAuctions;
    }

    private static Dictionary<string, string> GetFlatNbt(Auction auction)
    {
        if (!string.IsNullOrEmpty(auction.FlatenedNBTJson))
        {
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(auction.FlatenedNBTJson) ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
            }
        }

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var lookup in auction.NBTLookups)
        {
            var key = lookup.NBTKey?.KeyName ?? lookup.Key;
            if (string.IsNullOrEmpty(key))
                continue;

            var value = lookup.ValueString ??
                        lookup.NBTValue?.Value ??
                        lookup.ValueNumeric?.ToString();

            if (!string.IsNullOrEmpty(value))
                dict[key] = value;
        }

        return dict;
    }

    private static string RemoveReforgePrefix(string itemName)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            return itemName;

        var idx = itemName.IndexOf(' ');
        return idx > 0 ? itemName[(idx + 1)..] : itemName;
    }

    private static string GetPetLevelSelectValue(string itemName)
    {
        var sb = new StringBuilder(itemName);
        if (sb.Length > 8 && sb[6] == ']')
            sb[5] = '_';
        else if (sb.Length > 9 && sb[8] == ']')
            sb[7] = '_';
        else if (sb.Length > 7)
            sb[6] = '_';
        return sb.ToString();
    }

    private static bool PetNameMatches(string candidateName, string pattern)
    {
        if (candidateName.Length != pattern.Length)
            return false;

        for (var i = 0; i < pattern.Length; i++)
        {
            if (pattern[i] != '_' && pattern[i] != candidateName[i])
                return false;
        }

        return true;
    }

    private static bool SelectStyleMatch(Dictionary<string, string> candidate, Dictionary<string, string> target, string key)
    {
        if (!target.TryGetValue(key, out var targetValue))
            return !candidate.ContainsKey(key);

        if (!candidate.TryGetValue(key, out var candidateValue))
            return false;

        if (!long.TryParse(targetValue, out var numericTarget))
            return string.Equals(candidateValue, targetValue, StringComparison.OrdinalIgnoreCase);

        if (!long.TryParse(candidateValue, out var numericCandidate))
            return false;

        var lowerLimit = (long)(numericTarget * 0.8);
        return numericCandidate <= numericTarget && numericCandidate > lowerLimit;
    }

    private static bool StringEquals(Dictionary<string, string> candidate, Dictionary<string, string> target, string key)
    {
        if (!target.TryGetValue(key, out var targetValue))
            return !candidate.ContainsKey(key);
        return candidate.TryGetValue(key, out var candidateValue) &&
               string.Equals(candidateValue, targetValue, StringComparison.OrdinalIgnoreCase);
    }

    private static bool RangeMatches(Dictionary<string, string> candidate, Dictionary<string, string> target, string key, long maxDiff, int percentIncrease)
    {
        if (!target.TryGetValue(key, out var targetValue))
            return !candidate.ContainsKey(key);
        if (!candidate.TryGetValue(key, out var candidateValue))
            return false;
        if (!long.TryParse(targetValue, out var targetNumeric) || !long.TryParse(candidateValue, out var candidateNumeric))
            return false;

        maxDiff += targetNumeric * percentIncrease / 100;
        return candidateNumeric > targetNumeric - maxDiff && candidateNumeric < targetNumeric + maxDiff;
    }

    private static bool KillRangeMatches(Dictionary<string, string> candidate, string key, string targetValue)
    {
        if (!candidate.TryGetValue(key, out var candidateValue))
            return false;
        if (!int.TryParse(targetValue, out var targetNumeric) || !int.TryParse(candidateValue, out var candidateNumeric))
            return false;

        var max = targetNumeric * 1.2;
        var min = targetNumeric * 0.8;
        return candidateNumeric >= min && candidateNumeric < max;
    }

    private static bool PetItemMatches(Dictionary<string, string> candidate, Dictionary<string, string> target, long startingBid)
    {
        if (target.TryGetValue("heldItem", out var heldItem))
        {
            if (CacheKeyService.ShouldPetItemMatch(target, startingBid))
                return candidate.TryGetValue("heldItem", out var candidateItem) &&
                       string.Equals(candidateItem, heldItem, StringComparison.OrdinalIgnoreCase);

            return !candidate.TryGetValue("heldItem", out var candidateHeldItem) || !ValuablePetItems.Contains(candidateHeldItem);
        }

        if (target.ContainsKey("candyUsed"))
            return !candidate.ContainsKey("heldItem");

        return true;
    }

    private static bool CandyMatches(Dictionary<string, string> candidate, Dictionary<string, string> target)
    {
        if (!target.TryGetValue("candyUsed", out var targetCandy) || !long.TryParse(targetCandy, out var targetValue))
            return true;

        if (target.TryGetValue("exp", out var expString) &&
            double.TryParse(expString, out var exp) &&
            exp > 24_000_000 &&
            target.ContainsKey("skin"))
        {
            return !candidate.ContainsKey("heldItem");
        }

        if (!candidate.TryGetValue("candyUsed", out var candidateCandy) || !long.TryParse(candidateCandy, out var candidateValue))
            return false;

        return targetValue > 0 ? candidateValue > 0 : candidateValue == 0;
    }

    private static Enchantment SelectBestEnchant(List<Enchantment> enchants)
    {
        return enchants
            .OrderByDescending(e => e.Level)
            .ThenBy(e => e.Type.ToString(), StringComparer.Ordinal)
            .First();
    }
}

public sealed record RelevantReferenceResult(List<Auction> References, DateTime Oldest);

public sealed record RelevantReferenceDebugResult(
    List<Auction> References,
    int InitialCount,
    int ExpandedDayCount,
    int ExpandedWeekCount,
    int ReducedCount,
    int RecentReducedCount,
    int AntiManipulationSourceCount,
    int BeforeAntiManipulationCount,
    int AfterAntiManipulationCount);

public sealed record CandidateMatchDebug(
    bool IsMatch,
    bool TierMatches,
    bool ReforgeMatches,
    bool StackMatches,
    bool NameMatches,
    bool FlatNbtMatches,
    bool EnchantmentsMatch);

public sealed record FlipValuationResult(
    string CacheKey,
    long TargetPrice,
    long EstimatedProfit,
    double ProfitMarginPercent,
    string DataSource,
    string ValueBreakdown,
    int ReferenceCount,
    DateTime OldestReference);
