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
    private readonly CacheKeyService _cacheKeyService;
    private readonly ComponentValueService _componentValueService;
    private readonly NbtLookupResolver _nbtLookupResolver;
    private readonly ReferenceCacheService _referenceCacheService;
    private readonly ILogger<ReferenceAuctionService> _logger;

    public ReferenceAuctionService(
        IServiceScopeFactory scopeFactory,
        CacheKeyService cacheKeyService,
        ComponentValueService componentValueService,
        NbtLookupResolver nbtLookupResolver,
        ReferenceCacheService referenceCacheService,
        ILogger<ReferenceAuctionService> logger)
    {
        _scopeFactory = scopeFactory;
        _cacheKeyService = cacheKeyService;
        _componentValueService = componentValueService;
        _nbtLookupResolver = nbtLookupResolver;
        _referenceCacheService = referenceCacheService;
        _logger = logger;
    }

    public async Task<FlipValuationResult?> EvaluateBinAuctionAsync(
        Auction auction,
        int hitCount,
        CancellationToken stoppingToken)
    {
        using var metricTimer = FlipMetrics.MeasureReferenceSelection();
        FlipMetrics.ReferenceSelectionCalls.Inc();
        var totalPrice = auction.HighestBidAmount > 0 ? auction.HighestBidAmount : auction.StartingBid;
        var result = await EvaluateAuctionAsync(auction, totalPrice, hitCount, "BIN", stoppingToken);
        if (result == null || result.ReferenceCount == 0)
            FlipMetrics.ReferenceSelectionEmpty.Inc();
        else
            FlipMetrics.ReferenceCount.Set(result.ReferenceCount);
        return result;
    }

    public async Task<FlipValuationResult?> EvaluateBidAuctionAsync(
        Auction auction,
        int hitCount,
        CancellationToken stoppingToken)
    {
        using var metricTimer = FlipMetrics.MeasureReferenceSelection();
        FlipMetrics.ReferenceSelectionCalls.Inc();
        var expectedPrice = auction.HighestBidAmount == 0
            ? auction.StartingBid
            : (long)(auction.HighestBidAmount * 1.1);

        var result = await EvaluateAuctionAsync(auction, expectedPrice, hitCount, "Bid", stoppingToken);
        if (result == null || result.ReferenceCount == 0)
            FlipMetrics.ReferenceSelectionEmpty.Inc();
        else
            FlipMetrics.ReferenceCount.Set(result.ReferenceCount);
        return result;
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
        var cached = await _referenceCacheService.GetAsync(cacheKey, stoppingToken);
        if (cached != null)
            return cached;

        var fetched = await GetRelevantAuctionsAsync(auction, stoppingToken);
        await _referenceCacheService.SetAsync(cacheKey, fetched, stoppingToken);
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

        if (stageReferences.Count > 0 && stageReferences.All(a => GetReferenceTimestamp(a) > DateTime.UtcNow - TimeSpan.FromDays(1)))
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

        if (relevantAuctions.Count > 0 && relevantAuctions.All(a => GetReferenceTimestamp(a) > DateTime.UtcNow - TimeSpan.FromDays(1)))
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
                        ((((a.Status == AuctionStatus.SOLD && a.SoldAt.HasValue) &&
                           a.SoldAt > oldest &&
                           a.SoldAt < youngest)) ||
                         (((a.Status != AuctionStatus.SOLD || !a.SoldAt.HasValue) &&
                           a.End > oldest &&
                           a.End < youngest))) &&
                        (a.HighestBidAmount > 0 || a.SoldPrice.HasValue))
            .Include(a => a.Bids)
            .Include(a => a.Enchantments)
            .Include(a => a.NBTLookups)
                .ThenInclude(n => n.NBTKey)
            .Include(a => a.NBTLookups)
                .ThenInclude(n => n.NBTValue)
            .OrderByDescending(a => a.Status == AuctionStatus.SOLD && a.SoldAt.HasValue ? a.SoldAt : a.End)
            .AsQueryable();

        var targetFlatNbt = GetFlatNbt(auction);
        IQueryable<Auction> select = baseQuery;

        var recombMatters = CacheKeyService.DoesRecombMatter(auction.Category, auction.Tag);
        if (auction.Tag != "ENCHANTED_BOOK" && (recombMatters || CacheKeyService.IsPet(auction.Tag)))
            select = select.Where(a => a.Tier == auction.Tier);

        var shouldDropAncient = reduced && auction.Reforge == Reforge.Ancient;
        if (RelevantReforges.Contains(auction.Reforge) && !shouldDropAncient)
            select = select.Where(a => a.Reforge == auction.Reforge);
        else
            select = select.Where(a => !RelevantReforges.Contains(a.Reforge));

        if (auction.Count == 64 && !reduced)
            select = select.Where(a => a.Count == auction.Count);

        if (auction.ItemName != clearedName && !string.IsNullOrEmpty(clearedName))
        {
            select = select.Where(a => EF.Functions.Like(a.ItemName, "%" + clearedName));
        }
        else if (auction.Tag.StartsWith("PET", StringComparison.Ordinal) && auction.ItemName.StartsWith("[", StringComparison.Ordinal))
        {
            var petPattern = GetPetLevelSelectValue(auction.ItemName);
            select = select.Where(a => EF.Functions.Like(a.ItemName, petPattern));
        }
        else
        {
            select = select.Where(a => a.ItemName == clearedName);
        }

        var nbtQuery = new NbtQueryHelper(dbContext, _nbtLookupResolver, auction, targetFlatNbt, ValuablePetItems, stoppingToken);

        if (auction.Tag.EndsWith("MIDAS_STAFF", StringComparison.Ordinal) || auction.Tag.EndsWith("MIDAS_SWORD", StringComparison.Ordinal))
        {
            select = await nbtQuery.AddNbtRangeSelectAsync(select, "winning_bid", 2_000_000, 3);
            select = await nbtQuery.AddNbtRangeSelectAsync(select, "additional_coins", 2_000_000, 3);
            oldest -= TimeSpan.FromDays(5);
        }

        if (targetFlatNbt.ContainsKey("seconds_held"))
            select = await nbtQuery.AddNbtRangeSelectAsync(select, "seconds_held", 20_000, 10);

        if (auction.Tag.Contains("HOE", StringComparison.Ordinal) || targetFlatNbt.ContainsKey("farming_for_dummies_count"))
            select = await nbtQuery.AddNbtSelectAsync(select, "farming_for_dummies_count");

        if (auction.Tag == "CAKE_SOUL")
            select = await nbtQuery.AddNbtSelectAsync(select, "captured_player");

        if (targetFlatNbt.ContainsKey("rarity_upgrades") && recombMatters)
            select = await nbtQuery.AddNbtSelectAsync(select, "rarity_upgrades");

        if (auction.Tag == "ASPECT_OF_THE_VOID" || auction.Tag == "ASPECT_OF_THE_END")
            select = await nbtQuery.AddNbtSelectAsync(select, "ethermerge");

        if (auction.Tag == "NECRONS_LADDER")
            select = await nbtQuery.AddNbtSelectAsync(select, "handles_found");

        if (auction.Tag == "DIANAS_BOOKSHELF")
            select = await nbtQuery.AddNbtSelectAsync(select, "chimera_found");

        if (targetFlatNbt.ContainsKey("edition"))
            select = await nbtQuery.AddNbtRangeSelectAsync(select, "edition", 100, 10);

        if (targetFlatNbt.ContainsKey("new_years_cake"))
            select = await nbtQuery.AddNbtSelectAsync(select, "new_years_cake");

        if (targetFlatNbt.ContainsKey("dungeon_item_level") && !reduced)
            select = await nbtQuery.AddNbtSelectAsync(select, "dungeon_item_level");

        if (targetFlatNbt.ContainsKey("candyUsed"))
            select = await nbtQuery.AddCandySelectAsync(select);

        foreach (var exactKey in new[] { "art_of_war_count", "MUSIC", "ENCHANT", "DRAGON", "TIDAL", "ability_scroll", "party_hat_emoji" })
        {
            if (targetFlatNbt.ContainsKey(exactKey))
                select = await nbtQuery.AddNbtSelectAsync(select, exactKey);
        }

        if (auction.Tag.Contains("FINAL_DESTINATION", StringComparison.Ordinal))
            select = await nbtQuery.AddNbtRangeSelectAsync(select, "eman_kills", 1000, 10);

        if (targetFlatNbt.Keys.Any(k => AttributeKeys.Contains(k)))
        {
            foreach (var attributeKey in AttributeKeys)
                select = await nbtQuery.AddNbtSelectAsync(select, attributeKey);
        }

        if (auction.Tag.Contains("_DRILL", StringComparison.Ordinal))
        {
            select = await nbtQuery.AddNbtSelectAsync(select, "drill_part_engine");
            select = await nbtQuery.AddNbtSelectAsync(select, "drill_part_fuel_tank");
            select = await nbtQuery.AddNbtSelectAsync(select, "drill_part_upgrade_module");
        }

        select = await nbtQuery.AddPetItemSelectAsync(select);

        if (targetFlatNbt.ContainsKey("skin"))
            select = await nbtQuery.AddNbtSelectAsync(select, "skin");

        if (targetFlatNbt.ContainsKey("color") || CacheKeyService.IsArmor(auction.Tag))
        {
            select = await nbtQuery.AddNbtSelectAsync(select, "color");
            select = await nbtQuery.AddNbtSelectAsync(select, "dye_item");
        }

        foreach (var key in targetFlatNbt.Keys.Where(k => k.EndsWith("_kills", StringComparison.Ordinal)))
            select = await nbtQuery.AddNbtRangeSelectAsync(select, key, 0, 20);

        if (targetFlatNbt.ContainsKey("unlocked_slots"))
        {
            select = await nbtQuery.AddNbtSelectAsync(select, "unlocked_slots");
            select = select.Where(a => a.ItemCreatedAt > UnlockedIntroduction);
        }

        if (targetFlatNbt.ContainsKey("gemstone_slots"))
            select = await nbtQuery.AddNbtSelectAsync(select, "gemstone_slots");

        var roughLimit = Math.Max(limit * 8, 200);
        var candidates = await select
            .Include(a => a.NBTLookups)
                .ThenInclude(n => n.NBTKey)
            .Include(a => a.NBTLookups)
                .ThenInclude(n => n.NBTValue)
            .Take(roughLimit)
            .ToListAsync(stoppingToken);

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
            Value = GetReferencePrice(reference) / Math.Max(reference.Count, 1) / (reference.Count == auction.Count ? 1 : 3) - (await _componentValueService.GetGemstoneValueOnly(reference)).GemValue
        }))).ToList();

        var fullTime = auctions.Select(a => a.Value)
            .OrderByDescending(a => a)
            .Skip(relevantAuctions.Count / 2)
            .FirstOrDefault();

        var shortTerm = auctions.OrderByDescending(a => GetReferenceTimestamp(a.Auction))
            .Take(3)
            .OrderByDescending(a => a.Value)
            .Select(a => a.Value)
            .Skip(1)
            .FirstOrDefault();

        if (auctions.Count > 10 && relevantAuctions.All(a => GetReferenceTimestamp(a) > DateTime.UtcNow - TimeSpan.FromHours(20)))
        {
            fullTime = auctions.Select(a => a.Value)
                .OrderBy(a => a)
                .Skip(relevantAuctions.Count / 4)
                .FirstOrDefault();
        }

        return Math.Min(fullTime, shortTerm);
    }

    public static List<Auction> ApplyAntiMarketManipulation(List<Auction> relevantAuctions)
    {
        if (relevantAuctions.Count > 1)
        {
            var counter = 1;
            relevantAuctions = relevantAuctions
                .GroupBy(a => a.AuctioneerId ?? $"unknown_seller_{counter++}")
                .Select(g => g.OrderBy(GetReferencePrice).First())
                .ToList();

            counter = 1;
            relevantAuctions = relevantAuctions
                .GroupBy(a => GetWinningBidder(a) ?? $"unknown_buyer_{counter++}")
                .Select(g => g.First())
                .ToList();

            counter = 1;
            relevantAuctions = relevantAuctions
                .GroupBy(a => a.ItemUid ?? $"no_uid_{counter++}")
                .Select(g => g.First())
                .ToList();

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
            var key = lookup.NBTKey?.KeyName;
            if (string.IsNullOrEmpty(key))
                continue;

            var value = lookup.NBTValue?.Value ?? lookup.ValueNumeric?.ToString();
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
        if (enchants == null || enchants.Count == 0)
            return new Enchantment();

        foreach (var item in WorthOrderLevels)
        {
            foreach (var enchant in enchants)
            {
                if (enchant.Type == item.Type && enchant.Level == item.Level)
                    return enchant;
            }
        }

        foreach (var type in WorthOrder)
        {
            foreach (var enchant in enchants)
            {
                if (enchant.Type == type)
                    return enchant;
            }
        }

        return new Enchantment();
    }

    private static readonly List<EnchantmentType> WorthOrder = new()
    {
        EnchantmentType.ultimate_chimera,
        EnchantmentType.counter_strike,
        EnchantmentType.big_brain,
        EnchantmentType.vicious,
        EnchantmentType.ultimate_one_for_all,
        EnchantmentType.dragon_hunter,
        EnchantmentType.fire_aspect,
        EnchantmentType.triple_strike,
        EnchantmentType.venomous,
        EnchantmentType.pristine,
        EnchantmentType.cubism,
        EnchantmentType.execute,
        EnchantmentType.ender_slayer,
        EnchantmentType.sharpness,
        EnchantmentType.prosecute,
        EnchantmentType.ultimate_legion,
        EnchantmentType.power,
        EnchantmentType.impaling,
        EnchantmentType.giant_killer,
        EnchantmentType.ultimate_soul_eater,
        EnchantmentType.cleave,
        EnchantmentType.cultivating,
        EnchantmentType.telekinesis,
        EnchantmentType.expertise,
        EnchantmentType.compact,
        EnchantmentType.growth,
        EnchantmentType.syphon,
        EnchantmentType.protection,
        EnchantmentType.critical,
        EnchantmentType.smarty_pants,
        EnchantmentType.vampirism,
        EnchantmentType.snipe,
        EnchantmentType.thunderbolt,
        EnchantmentType.first_strike,
        EnchantmentType.titan_killer,
        EnchantmentType.experience,
        EnchantmentType.lethality,
        EnchantmentType.overload,
        EnchantmentType.luck,
        EnchantmentType.thunderlord,
        EnchantmentType.ultimate_swarm,
        EnchantmentType.scavenger,
        EnchantmentType.life_steal,
        EnchantmentType.replenish,
        EnchantmentType.smite,
        EnchantmentType.chance,
        EnchantmentType.looting,
        EnchantmentType.true_protection,
        EnchantmentType.bane_of_arthropods,
        EnchantmentType.harvesting,
        EnchantmentType.ultimate_rend,
        EnchantmentType.sugar_rush,
        EnchantmentType.fire_protection,
        EnchantmentType.blast_protection,
        EnchantmentType.delicate,
        EnchantmentType.projectile_protection,
        EnchantmentType.ultimate_last_stand,
        EnchantmentType.ultimate_wisdom,
        EnchantmentType.depth_strider,
        EnchantmentType.frail,
        EnchantmentType.lure,
        EnchantmentType.magnet,
        EnchantmentType.ultimate_wise,
        EnchantmentType.piercing,
        EnchantmentType.turbo_pumpkin,
        EnchantmentType.blessing,
        EnchantmentType.infinite_quiver,
        EnchantmentType.caster,
        EnchantmentType.feather_falling,
        EnchantmentType.fortune,
        EnchantmentType.turbo_melon,
        EnchantmentType.respite,
        EnchantmentType.turbo_mushrooms,
        EnchantmentType.luck_of_the_sea,
        EnchantmentType.aqua_affinity,
        EnchantmentType.turbo_potato,
        EnchantmentType.turbo_cactus,
        EnchantmentType.spiked_hook,
        EnchantmentType.turbo_warts,
        EnchantmentType.turbo_cane,
        EnchantmentType.flame,
        EnchantmentType.angler,
        EnchantmentType.thorns,
        EnchantmentType.mana_steal,
        EnchantmentType.turbo_coco,
        EnchantmentType.turbo_carrot,
        EnchantmentType.respiration,
        EnchantmentType.aiming,
        EnchantmentType.rejuvenate,
        EnchantmentType.smelting_touch,
        EnchantmentType.punch,
        EnchantmentType.frost_walker,
        EnchantmentType.ultimate_jerry,
        EnchantmentType.ultimate_combo,
        EnchantmentType.turbo_wheat,
        EnchantmentType.ultimate_bank,
        EnchantmentType.efficiency,
        EnchantmentType.rainbow,
        EnchantmentType.silk_touch,
        EnchantmentType.knockback,
        EnchantmentType.ultimate_no_pain_no_gain
    };

    private static readonly List<(EnchantmentType Type, int Level)> WorthOrderLevels = new()
    {
        (EnchantmentType.ultimate_chimera, 2),
        (EnchantmentType.growth, 7),
        (EnchantmentType.syphon, 5),
        (EnchantmentType.looting, 5),
        (EnchantmentType.snipe, 4),
        (EnchantmentType.first_strike, 5),
        (EnchantmentType.triple_strike, 5),
        (EnchantmentType.ultimate_chimera, 1),
        (EnchantmentType.big_brain, 5),
        (EnchantmentType.power, 7),
        (EnchantmentType.luck, 7),
        (EnchantmentType.cubism, 6),
        (EnchantmentType.critical, 7),
        (EnchantmentType.counter_strike, 5),
        (EnchantmentType.giant_killer, 7),
        (EnchantmentType.ender_slayer, 7),
        (EnchantmentType.execute, 6),
        (EnchantmentType.sharpness, 7),
        (EnchantmentType.protection, 7),
        (EnchantmentType.venomous, 6),
        (EnchantmentType.vicious, 5),
        (EnchantmentType.life_steal, 5),
        (EnchantmentType.chance, 5),
        (EnchantmentType.scavenger, 5),
        (EnchantmentType.dragon_hunter, 5),
        (EnchantmentType.vicious, 4),
        (EnchantmentType.big_brain, 3),
        (EnchantmentType.ultimate_legion, 5),
        (EnchantmentType.ultimate_soul_eater, 5),
        (EnchantmentType.cleave, 6),
        (EnchantmentType.pristine, 5),
        (EnchantmentType.ultimate_swarm, 5),
        (EnchantmentType.ultimate_one_for_all, 1),
        (EnchantmentType.overload, 5),
        (EnchantmentType.sharpness, 1),
        (EnchantmentType.prosecute, 6),
        (EnchantmentType.smarty_pants, 5),
        (EnchantmentType.ultimate_legion, 4),
        (EnchantmentType.vicious, 3),
        (EnchantmentType.ultimate_soul_eater, 4),
        (EnchantmentType.titan_killer, 7),
        (EnchantmentType.pristine, 4),
        (EnchantmentType.dragon_hunter, 4),
        (EnchantmentType.fire_protection, 7),
        (EnchantmentType.ultimate_rend, 5),
        (EnchantmentType.thunderbolt, 6),
        (EnchantmentType.blast_protection, 7),
        (EnchantmentType.overload, 4),
        (EnchantmentType.growth, 6),
        (EnchantmentType.smarty_pants, 4),
        (EnchantmentType.ultimate_legion, 3),
        (EnchantmentType.ultimate_soul_eater, 3),
        (EnchantmentType.protection, 6),
        (EnchantmentType.ultimate_swarm, 4),
        (EnchantmentType.pristine, 3),
        (EnchantmentType.dragon_hunter, 3),
        (EnchantmentType.ultimate_last_stand, 5),
        (EnchantmentType.ultimate_wisdom, 5),
        (EnchantmentType.harvesting, 1),
        (EnchantmentType.overload, 3),
        (EnchantmentType.snipe, 1),
        (EnchantmentType.fire_aspect, 2),
        (EnchantmentType.ultimate_wise, 5),
        (EnchantmentType.ender_slayer, 6),
        (EnchantmentType.turbo_pumpkin, 5),
        (EnchantmentType.turbo_mushrooms, 5),
        (EnchantmentType.smarty_pants, 3),
        (EnchantmentType.turbo_cactus, 5),
        (EnchantmentType.ultimate_rend, 4),
        (EnchantmentType.bane_of_arthropods, 7),
        (EnchantmentType.ultimate_legion, 2),
        (EnchantmentType.ultimate_soul_eater, 2),
        (EnchantmentType.turbo_melon, 5),
        (EnchantmentType.projectile_protection, 7),
        (EnchantmentType.ultimate_swarm, 3),
        (EnchantmentType.thunderbolt, 4),
        (EnchantmentType.pristine, 2),
        (EnchantmentType.ultimate_last_stand, 4),
        (EnchantmentType.bane_of_arthropods, 1),
        (EnchantmentType.dragon_hunter, 2),
        (EnchantmentType.experience, 4),
        (EnchantmentType.ultimate_wisdom, 4),
        (EnchantmentType.smite, 7),
        (EnchantmentType.turbo_potato, 5),
        (EnchantmentType.ultimate_rend, 3),
        (EnchantmentType.impaling, 3),
        (EnchantmentType.vampirism, 6),
        (EnchantmentType.sharpness, 6),
        (EnchantmentType.fire_aspect, 1),
        (EnchantmentType.turbo_mushrooms, 4),
        (EnchantmentType.growth, 1),
        (EnchantmentType.cultivating, 1),
        (EnchantmentType.expertise, 1),
        (EnchantmentType.ultimate_wise, 4),
        (EnchantmentType.harvesting, 6),
        (EnchantmentType.compact, 1),
        (EnchantmentType.overload, 2),
        (EnchantmentType.turbo_pumpkin, 4),
        (EnchantmentType.infinite_quiver, 8),
        (EnchantmentType.turbo_carrot, 5),
        (EnchantmentType.turbo_cactus, 4),
        (EnchantmentType.cubism, 5),
        (EnchantmentType.giant_killer, 6),
        (EnchantmentType.turbo_cane, 5),
        (EnchantmentType.telekinesis, 1),
        (EnchantmentType.lethality, 6),
        (EnchantmentType.venomous, 5),
        (EnchantmentType.bane_of_arthropods, 5),
        (EnchantmentType.execute, 5),
        (EnchantmentType.respite, 5),
        (EnchantmentType.cleave, 5),
        (EnchantmentType.smarty_pants, 2),
        (EnchantmentType.turbo_melon, 4),
        (EnchantmentType.projectile_protection, 1),
        (EnchantmentType.ultimate_legion, 1),
        (EnchantmentType.ultimate_soul_eater, 1),
        (EnchantmentType.thunderlord, 6),
        (EnchantmentType.sugar_rush, 3),
        (EnchantmentType.infinite_quiver, 9),
        (EnchantmentType.ultimate_jerry, 5),
        (EnchantmentType.ultimate_last_stand, 3),
        (EnchantmentType.pristine, 1),
        (EnchantmentType.power, 4),
        (EnchantmentType.syphon, 4),
        (EnchantmentType.turbo_mushrooms, 3),
        (EnchantmentType.replenish, 1),
        (EnchantmentType.ultimate_combo, 5),
        (EnchantmentType.power, 6),
        (EnchantmentType.first_strike, 4),
        (EnchantmentType.ultimate_swarm, 2),
        (EnchantmentType.turbo_potato, 4),
        (EnchantmentType.magnet, 4),
        (EnchantmentType.ultimate_wisdom, 3),
        (EnchantmentType.titan_killer, 6),
        (EnchantmentType.dragon_hunter, 1),
        (EnchantmentType.true_protection, 1),
        (EnchantmentType.turbo_coco, 5),
        (EnchantmentType.critical, 3),
        (EnchantmentType.spiked_hook, 3),
        (EnchantmentType.frail, 3),
        (EnchantmentType.aiming, 3),
        (EnchantmentType.turbo_wheat, 5),
        (EnchantmentType.turbo_cane, 4),
        (EnchantmentType.feather_falling, 8),
        (EnchantmentType.lethality, 5),
        (EnchantmentType.feather_falling, 9),
        (EnchantmentType.turbo_carrot, 4),
        (EnchantmentType.ultimate_wise, 3),
        (EnchantmentType.rejuvenate, 5),
        (EnchantmentType.turbo_pumpkin, 3),
        (EnchantmentType.ultimate_rend, 2),
        (EnchantmentType.life_steal, 4),
        (EnchantmentType.life_steal, 3),
        (EnchantmentType.scavenger, 3),
        (EnchantmentType.vampirism, 5),
        (EnchantmentType.turbo_cactus, 3),
        (EnchantmentType.scavenger, 4),
        (EnchantmentType.overload, 1),
        (EnchantmentType.experience, 3),
        (EnchantmentType.growth, 3),
        (EnchantmentType.delicate, 5),
        (EnchantmentType.giant_killer, 5),
        (EnchantmentType.looting, 3),
        (EnchantmentType.infinite_quiver, 7),
        (EnchantmentType.luck, 6),
        (EnchantmentType.smarty_pants, 1),
        (EnchantmentType.luck, 5),
        (EnchantmentType.critical, 5),
        (EnchantmentType.spiked_hook, 4),
        (EnchantmentType.efficiency, 1),
        (EnchantmentType.critical, 6),
        (EnchantmentType.ultimate_combo, 4),
        (EnchantmentType.respite, 4),
        (EnchantmentType.sugar_rush, 2),
        (EnchantmentType.turbo_melon, 3),
        (EnchantmentType.turbo_warts, 5),
        (EnchantmentType.ultimate_jerry, 4),
        (EnchantmentType.luck_of_the_sea, 4),
        (EnchantmentType.looting, 4),
        (EnchantmentType.turbo_coco, 4),
        (EnchantmentType.smite, 1),
        (EnchantmentType.ender_slayer, 5),
        (EnchantmentType.ender_slayer, 3),
        (EnchantmentType.ultimate_swarm, 1),
        (EnchantmentType.prosecute, 5),
        (EnchantmentType.turbo_potato, 3),
        (EnchantmentType.turbo_carrot, 3),
        (EnchantmentType.ultimate_no_pain_no_gain, 5),
        (EnchantmentType.fortune, 4),
        (EnchantmentType.ultimate_rend, 1),
        (EnchantmentType.turbo_wheat, 4),
        (EnchantmentType.power, 3),
        (EnchantmentType.turbo_cane, 3),
        (EnchantmentType.sharpness, 5),
        (EnchantmentType.rejuvenate, 4),
        (EnchantmentType.depth_strider, 3),
        (EnchantmentType.ultimate_last_stand, 2),
        (EnchantmentType.ultimate_wisdom, 2),
        (EnchantmentType.snipe, 3),
        (EnchantmentType.chance, 4),
        (EnchantmentType.feather_falling, 1),
        (EnchantmentType.turbo_mushrooms, 2),
        (EnchantmentType.thunderbolt, 5),
        (EnchantmentType.sugar_rush, 1),
        (EnchantmentType.turbo_coco, 3),
        (EnchantmentType.feather_falling, 10),
        (EnchantmentType.caster, 6),
        (EnchantmentType.lure, 6),
        (EnchantmentType.frail, 6),
        (EnchantmentType.blast_protection, 6),
        (EnchantmentType.ultimate_wise, 2),
        (EnchantmentType.thunderlord, 5),
        (EnchantmentType.blessing, 5),
        (EnchantmentType.piercing, 1),
        (EnchantmentType.bane_of_arthropods, 4),
        (EnchantmentType.triple_strike, 4),
        (EnchantmentType.turbo_warts, 4),
        (EnchantmentType.magnet, 6),
        (EnchantmentType.smite, 5),
        (EnchantmentType.turbo_pumpkin, 2),
        (EnchantmentType.turbo_cactus, 2),
        (EnchantmentType.depth_strider, 2),
        (EnchantmentType.venomous, 3),
        (EnchantmentType.growth, 4),
        (EnchantmentType.smite, 6),
        (EnchantmentType.ultimate_combo, 3),
        (EnchantmentType.lure, 4),
        (EnchantmentType.punch, 2),
        (EnchantmentType.luck_of_the_sea, 6),
        (EnchantmentType.cubism, 2),
        (EnchantmentType.power, 5),
        (EnchantmentType.infinite_quiver, 10),
        (EnchantmentType.ultimate_no_pain_no_gain, 4),
        (EnchantmentType.turbo_warts, 1),
        (EnchantmentType.sharpness, 4),
        (EnchantmentType.spiked_hook, 5),
        (EnchantmentType.mana_steal, 2),
        (EnchantmentType.ultimate_last_stand, 1),
        (EnchantmentType.angler, 6),
        (EnchantmentType.aqua_affinity, 1),
        (EnchantmentType.frost_walker, 2),
        (EnchantmentType.turbo_wheat, 3),
        (EnchantmentType.spiked_hook, 6),
        (EnchantmentType.turbo_melon, 2),
        (EnchantmentType.flame, 1),
        (EnchantmentType.turbo_warts, 3),
        (EnchantmentType.frail, 5),
        (EnchantmentType.turbo_potato, 2),
        (EnchantmentType.syphon, 3),
        (EnchantmentType.thorns, 3),
        (EnchantmentType.aiming, 5),
        (EnchantmentType.respite, 3),
        (EnchantmentType.caster, 4),
        (EnchantmentType.ultimate_wisdom, 1),
        (EnchantmentType.harvesting, 5),
        (EnchantmentType.rejuvenate, 3),
        (EnchantmentType.ultimate_bank, 5),
        (EnchantmentType.mana_steal, 1),
        (EnchantmentType.mana_steal, 3),
        (EnchantmentType.turbo_cane, 2),
        (EnchantmentType.feather_falling, 6),
        (EnchantmentType.ultimate_wise, 1),
        (EnchantmentType.ultimate_jerry, 3),
        (EnchantmentType.infinite_quiver, 2),
        (EnchantmentType.protection, 3),
        (EnchantmentType.turbo_carrot, 2),
        (EnchantmentType.blessing, 4),
        (EnchantmentType.chance, 3),
        (EnchantmentType.fire_protection, 6),
        (EnchantmentType.respiration, 3),
        (EnchantmentType.caster, 5),
        (EnchantmentType.turbo_pumpkin, 1),
        (EnchantmentType.knockback, 2),
        (EnchantmentType.turbo_coco, 2),
        (EnchantmentType.sharpness, 2),
        (EnchantmentType.venomous, 4),
        (EnchantmentType.feather_falling, 7),
        (EnchantmentType.cleave, 4),
        (EnchantmentType.angler, 5),
        (EnchantmentType.infinite_quiver, 5),
        (EnchantmentType.turbo_mushrooms, 1),
        (EnchantmentType.smelting_touch, 1),
        (EnchantmentType.harvesting, 4),
        (EnchantmentType.rejuvenate, 2),
        (EnchantmentType.ender_slayer, 4),
        (EnchantmentType.protection, 5),
        (EnchantmentType.turbo_warts, 2),
        (EnchantmentType.infinite_quiver, 6),
        (EnchantmentType.turbo_melon, 1),
        (EnchantmentType.projectile_protection, 6),
        (EnchantmentType.turbo_cactus, 1),
        (EnchantmentType.turbo_carrot, 1),
        (EnchantmentType.respite, 2),
        (EnchantmentType.turbo_wheat, 2),
        (EnchantmentType.turbo_potato, 1),
        (EnchantmentType.ultimate_bank, 1),
        (EnchantmentType.ultimate_combo, 2),
        (EnchantmentType.turbo_cane, 1),
        (EnchantmentType.scavenger, 2),
        (EnchantmentType.ultimate_no_pain_no_gain, 3),
        (EnchantmentType.chance, 2),
        (EnchantmentType.first_strike, 3),
        (EnchantmentType.ultimate_bank, 4),
        (EnchantmentType.growth, 5),
        (EnchantmentType.frail, 4),
        (EnchantmentType.luck_of_the_sea, 5),
        (EnchantmentType.ender_slayer, 2),
        (EnchantmentType.feather_falling, 5),
        (EnchantmentType.lethality, 4),
        (EnchantmentType.ultimate_bank, 3),
        (EnchantmentType.protection, 4),
        (EnchantmentType.efficiency, 5),
        (EnchantmentType.punch, 1),
        (EnchantmentType.luck, 4),
        (EnchantmentType.silk_touch, 1),
        (EnchantmentType.ultimate_jerry, 2),
        (EnchantmentType.looting, 2),
        (EnchantmentType.rainbow, 1),
        (EnchantmentType.respite, 1),
        (EnchantmentType.turbo_wheat, 1),
        (EnchantmentType.turbo_coco, 1),
        (EnchantmentType.protection, 1),
        (EnchantmentType.ultimate_combo, 1),
        (EnchantmentType.knockback, 1),
        (EnchantmentType.fortune, 3),
        (EnchantmentType.frost_walker, 1),
        (EnchantmentType.cubism, 4),
        (EnchantmentType.rejuvenate, 1),
        (EnchantmentType.vampirism, 4),
        (EnchantmentType.giant_killer, 4),
        (EnchantmentType.projectile_protection, 5),
        (EnchantmentType.lure, 5),
        (EnchantmentType.magnet, 5),
        (EnchantmentType.prosecute, 4),
        (EnchantmentType.impaling, 2),
        (EnchantmentType.execute, 4),
        (EnchantmentType.syphon, 2),
        (EnchantmentType.critical, 4),
        (EnchantmentType.bane_of_arthropods, 6),
        (EnchantmentType.ultimate_jerry, 1),
        (EnchantmentType.ultimate_bank, 2),
        (EnchantmentType.aiming, 4),
        (EnchantmentType.angler, 4),
        (EnchantmentType.triple_strike, 3),
        (EnchantmentType.respiration, 2),
        (EnchantmentType.life_steal, 2),
        (EnchantmentType.experience, 2),
        (EnchantmentType.titan_killer, 4),
        (EnchantmentType.power, 1),
        (EnchantmentType.blast_protection, 5),
        (EnchantmentType.projectile_protection, 3),
        (EnchantmentType.titan_killer, 5),
        (EnchantmentType.thorns, 2),
        (EnchantmentType.fire_protection, 4),
        (EnchantmentType.fire_protection, 5),
        (EnchantmentType.ultimate_no_pain_no_gain, 2),
        (EnchantmentType.magnet, 1),
        (EnchantmentType.thunderlord, 4),
        (EnchantmentType.sharpness, 3),
        (EnchantmentType.efficiency, 4),
        (EnchantmentType.protection, 2),
        (EnchantmentType.snipe, 2),
        (EnchantmentType.fortune, 2),
        (EnchantmentType.infinite_quiver, 3),
        (EnchantmentType.blast_protection, 4),
        (EnchantmentType.efficiency, 3),
        (EnchantmentType.ultimate_no_pain_no_gain, 1),
        (EnchantmentType.scavenger, 1),
        (EnchantmentType.efficiency, 2),
        (EnchantmentType.feather_falling, 4),
        (EnchantmentType.smite, 4),
        (EnchantmentType.harvesting, 2),
        (EnchantmentType.venomous, 2),
        (EnchantmentType.infinite_quiver, 4),
        (EnchantmentType.fire_protection, 1),
        (EnchantmentType.projectile_protection, 4),
        (EnchantmentType.infinite_quiver, 1),
        (EnchantmentType.projectile_protection, 2),
        (EnchantmentType.harvesting, 3),
        (EnchantmentType.smite, 3),
        (EnchantmentType.blast_protection, 3),
        (EnchantmentType.smite, 2),
        (EnchantmentType.impaling, 1)
    };

    private static long GetReferencePrice(Auction auction)
    {
        if (auction.SoldPrice.HasValue && auction.SoldPrice.Value > 0)
            return auction.SoldPrice.Value;

        if (auction.HighestBidAmount > 0)
            return auction.HighestBidAmount;

        return auction.StartingBid;
    }

    private static string? GetWinningBidder(Auction auction)
    {
        return auction.Bids
            .OrderByDescending(b => b.Amount)
            .FirstOrDefault()
            ?.BidderId;
    }

    private static DateTime GetReferenceTimestamp(Auction auction)
    {
        if (auction.Status == AuctionStatus.SOLD && auction.SoldAt.HasValue)
            return auction.SoldAt.Value;

        return auction.End;
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
