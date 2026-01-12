using SkyFlipperSolo.Models;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SkyFlipperSolo.Services;

/// <summary>
/// Service for generating NBT-aware cache keys for price comparison.
/// Creates unique identifiers that represent items with identical value-affecting properties.
/// Matches Coflnet's SkyFlipper FlippingEngine.cs GetCacheKey() logic EXACTLY.
/// 
/// Reference implementation (FlippingEngine.cs line 434-446):
/// var key = $"o{auction.Tag}{auction.ItemName}{auction.Tier}{auction.Count}";
/// if (relevantReforges.Contains(auction.Reforge))
///     key += auction.Reforge;
/// var relevant = ExtractRelevantEnchants(auction);
/// if (relevant.Count() == 0)
///     key += String.Concat(auction.Enchantments.Select(a => $"{a.Type}{a.Level}"));
/// else
///     key += String.Concat(relevant.Select(a => $"{a.Type}{a.Level}"));
/// key += String.Concat(auction.FlatenedNBT.Where(d => !ignoredNbt.Contains(d.Key)));
/// </summary>
public class CacheKeyService
{
    private readonly ILogger<CacheKeyService> _logger;

    // Reference: Constants.cs RelevantReforges - COMPLETE list from reference
    private static readonly HashSet<Reforge> RelevantReforges = new()
    {
        Reforge.Gilded,
        Reforge.Withered,
        Reforge.Spiritual,
        Reforge.Jaded,
        Reforge.Warped,
        Reforge.AoteStone,   // Warped on AOTE (alternate form)
        Reforge.Toil,
        Reforge.Fabled,
        Reforge.Giant,
        Reforge.Submerged,
        Reforge.Renowned,
        Reforge.Mossy,
        Reforge.Rooted,
        Reforge.Festive,
        Reforge.Lustrous,
        Reforge.Glacial,
        Reforge.Coldfused,   // Cold Fusion reforge
        Reforge.Moonglade,   // Moonglade armor reforge
        Reforge.BloodShot    // Blood Shot bow reforge
    };

    // Reference: Constants.cs AttributeKeys - Kuudra/Crimson Isle attributes (COMPLETE)
    private static readonly HashSet<string> AttributeKeys = new()
    {
        "lifeline", "breeze", "speed", "experience", "mana_pool",
        "life_regeneration", "blazing_resistance", "arachno_resistance",
        "undead_resistance", "blazing_fortune", "fishing_experience",
        "double_hook", "infection", "trophy_hunter", "fisherman", "hunter",
        "fishing_speed", "life_recovery", "ignition", "combo", "attack_speed",
        "midas_touch", "mana_regeneration", "veteran", "mending", "ender_resistance",
        "dominance", "ender", "mana_steal", "blazing", "elite", "arachno", "undead",
        "warrior", "deadeye", "fortitude", "magic_find"
    };

    // NBT keys to ignore (per FlippingEngine.cs line 404-405)
    private static readonly HashSet<string> IgnoredNbtKeys = new()
    {
        "uid", "spawnedFor", "bossId", "exp", "uuid", "hpc", "active",
        "uniqueId", "hideRightClick", "noMove", "hideInfo"
    };

    // NBT keys that need range-based matching (reference uses AddNbtRangeSelect)
    // These keys are normalized to ranges instead of exact values for cache key generation
    // Reference: FlippingEngine.cs lines 652-706, 882-893
    private static readonly Dictionary<string, (long RangeSize, int PercentIncrease)> NbtRangeKeys = new()
    {
        // Midas items - reference line 866-868
        { "winning_bid", (2_000_000, 3) },
        { "additional_coins", (2_000_000, 3) },
        // Edition items (collectibles) - reference line 683
        { "edition", (100, 10) },
        // Seconds held - reference line 666
        { "seconds_held", (20_000, 10) },
        // Eman kills (Final Destination) - reference line 706
        { "eman_kills", (1000, 10) },
    };

    // Keys that end with _kills need range matching - reference lines 736-746
    private static readonly Regex KillsKeyPattern = new Regex(@"_kills$", RegexOptions.Compiled);

    // Pet items that should always match in cache key - reference lines 812-827
    private static readonly HashSet<string> ValuablePetItems = new()
    {
        "MINOS_RELIC", "QUICK_CLAW", "PET_ITEM_QUICK_CLAW", "PET_ITEM_TIER_BOOST",
        "PET_ITEM_LUCKY_CLOVER", "PET_ITEM_LUCKY_CLOVER_DROP", "GREEN_BANDANA",
        "PET_ITEM_COMBAT_SKILL_BOOST_EPIC", "PET_ITEM_FISHING_SKILL_BOOST_EPIC",
        "PET_ITEM_FORAGING_SKILL_BOOST_EPIC", "ALL_SKILLS_SUPER_BOOST", "PET_ITEM_EXP_SHARE"
    };

    // Pet level regex for extracting level from item name - reference line 902-912
    private static readonly Regex PetLevelRegex = new Regex(@"\[Lvl (\d+)\]", RegexOptions.Compiled);

    // Attribute shard weighting - reference FlippingEngine.cs lines 67-83
    // Higher weight = more valuable attribute, affects how we bucket attribute values
    // Weight 3 = exact match needed, Weight 1 = broader range matching
    private static readonly Dictionary<string, short> ShardAttributes = new()
    {
        { "mana_pool", 1 },
        { "breeze", 1 },
        { "speed", 2 },
        { "life_regeneration", 2 },  // especially valuable in combination with mana_pool
        { "fishing_experience", 2 },
        { "ignition", 2 },
        { "blazing_fortune", 2 },
        { "double_hook", 3 },
        { "mana_regeneration", 2 },
        { "mending", 3 },
        { "dominance", 3 },
        { "magic_find", 2 },
        { "veteran", 1 }
        // lifeline - too low volume
        // life_recovery - weight 3
    };

    // Cosmetic NBT keys that affect item value - reference FlippingEngine.cs lines 693-703
    private static readonly HashSet<string> CosmeticNbtKeys = new()
    {
        "MUSIC",           // Music discs
        "ENCHANT",         // Enchant rune type
        "DRAGON",          // Dragon type
        "TIDAL",           // Tidal items
        "party_hat_emoji"  // Party hat emojis
    };

    // Drill part keys - reference FlippingEngine.cs lines 714-719
    private static readonly HashSet<string> DrillPartKeys = new()
    {
        "drill_part_engine",
        "drill_part_fuel_tank",
        "drill_part_upgrade_module"
    };

    // Special item NBT keys that must always be included in cache key
    // Reference: FlippingEngine.cs lines 676-704
    private static readonly Dictionary<string, HashSet<string>> SpecialItemNbtKeys = new()
    {
        // Necrons Ladder - handles_found affects value
        { "NECRONS_LADDER", new HashSet<string> { "handles_found" } },
        // Dianas Bookshelf - chimera_found affects value  
        { "DIANAS_BOOKSHELF", new HashSet<string> { "chimera_found" } },
        // AOTV/AOTE - ethermerge is valuable
        { "ASPECT_OF_THE_VOID", new HashSet<string> { "ethermerge" } },
        { "ASPECT_OF_THE_END", new HashSet<string> { "ethermerge" } },
    };

    // Reference: Constants.cs RelevantEnchants - COMPLETE list with minimum levels
    // Format: (EnchantmentType, MinLevel) - enchant is relevant if level >= MinLevel
    private static readonly Dictionary<EnchantmentType, byte> RelevantEnchants = new()
    {
        // Combat enchants
        { EnchantmentType.first_strike, 5 },
        { EnchantmentType.triple_strike, 5 },
        { EnchantmentType.life_steal, 5 },
        { EnchantmentType.looting, 5 },
        { EnchantmentType.scavenger, 5 },
        { EnchantmentType.syphon, 5 },
        { EnchantmentType.vicious, 1 },
        { EnchantmentType.chance, 5 },
        { EnchantmentType.snipe, 4 },
        { EnchantmentType.pristine, 1 },
        { EnchantmentType.overload, 2 },
        { EnchantmentType.smite, 7 },
        { EnchantmentType.giant_killer, 7 },
        { EnchantmentType.luck, 7 },
        { EnchantmentType.compact, 1 },
        { EnchantmentType.counter_strike, 5 },
        { EnchantmentType.experience, 5 },
        { EnchantmentType.cultivating, 1 },
        { EnchantmentType.divine_gift, 1 },
        { EnchantmentType.dedication, 1 },
        { EnchantmentType.expertise, 1 },
        { EnchantmentType.power, 7 },
        { EnchantmentType.reflection, 5 },
        
        // Ultimate enchants - always relevant at level 1+ unless specified
        { EnchantmentType.ultimate_bank, 6 },
        { EnchantmentType.ultimate_jerry, 6 },
        { EnchantmentType.ultimate_last_stand, 3 },
        { EnchantmentType.ultimate_no_pain_no_gain, 5 },
        { EnchantmentType.ultimate_rend, 3 },
        { EnchantmentType.ultimate_swarm, 3 },
        { EnchantmentType.ultimate_wisdom, 3 },
        { EnchantmentType.ultimate_the_one, 4 },
        { EnchantmentType.ultimate_chimera, 1 },
        { EnchantmentType.ultimate_legion, 1 },
        { EnchantmentType.ultimate_duplex, 1 },
        { EnchantmentType.ultimate_fatal_tempo, 1 },
        { EnchantmentType.ultimate_flash, 1 },
        { EnchantmentType.ultimate_habanero_tactics, 4 },
        { EnchantmentType.ultimate_inferno, 1 },
        { EnchantmentType.ultimate_combo, 1 },
        { EnchantmentType.ultimate_one_for_all, 1 },
        { EnchantmentType.ultimate_reiterate, 1 },
        { EnchantmentType.ultimate_soul_eater, 1 },
        
        // Other valuable enchants
        { EnchantmentType.smarty_pants, 2 },
        { EnchantmentType.big_brain, 3 },
        { EnchantmentType.rejuvenate, 5 },
        { EnchantmentType.transylvanian, 5 },
        { EnchantmentType.true_protection, 1 },
        { EnchantmentType.sugar_rush, 3 },
        
        // Farming enchants
        { EnchantmentType.turbo_cactus, 5 },
        { EnchantmentType.turbo_cane, 5 },
        { EnchantmentType.turbo_carrot, 5 },
        { EnchantmentType.turbo_cocoa, 5 },
        { EnchantmentType.turbo_melon, 5 },
        { EnchantmentType.turbo_mushrooms, 5 },
        { EnchantmentType.turbo_potato, 5 },
        { EnchantmentType.turbo_pumpkin, 5 },
        { EnchantmentType.turbo_warts, 5 },
        { EnchantmentType.turbo_wheat, 5 },
    };

    public CacheKeyService(ILogger<CacheKeyService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generates a cache key for price comparison that includes all value-affecting properties.
    /// 
    /// CRITICAL: This MUST match the reference implementation EXACTLY!
    /// Reference: FlippingEngine.cs GetCacheKey() method (lines 434-446)
    /// 
    /// Key format: o{Tag}{ItemName}{Tier}{Count}{Reforge?}{Enchants}{FlattenedNBT}
    /// 
    /// The reference concatenates FlatenedNBT directly using:
    ///   key += String.Concat(auction.FlatenedNBT.Where(d => !ignoredNbt.Contains(d.Key)));
    /// 
    /// This produces output like: [key1, value1][key2, value2] (Dictionary KeyValuePair ToString format)
    /// 
    /// ENHANCEMENTS for matching:
    /// - Pet levels are normalized to ranges (e.g., Lvl 95-99 → Lvl 9_)
    /// - NBT values like edition, kills, winning_bid use range matching
    /// </summary>
    public string GeneratePriceCacheKey(Auction auction)
    {
        if (auction == null) throw new ArgumentNullException(nameof(auction));

        var keyBuilder = new StringBuilder();
        
        // Prefix 'o' like reference project
        keyBuilder.Append('o');
        keyBuilder.Append(auction.Tag);
        
        // Pet level normalization - reference line 895-912 (GetPetLevelSelectVal)
        // Normalizes last digit of level to wildcard for range matching
        var itemName = NormalizePetLevel(auction.ItemName, auction.Tag);
        keyBuilder.Append(itemName ?? "");
        
        keyBuilder.Append((int)auction.Tier);
        keyBuilder.Append(auction.Count);

        // Only include relevant reforges (per reference Constants.cs)
        if (RelevantReforges.Contains(auction.Reforge))
        {
            keyBuilder.Append(auction.Reforge.ToString());
        }

        // Add enchantments - reference logic (lines 439-443):
        // var relevant = ExtractRelevantEnchants(auction);
        // if (relevant.Count() == 0)
        //     key += String.Concat(auction.Enchantments.Select(a => $"{a.Type}{a.Level}"));
        // else
        //     key += String.Concat(relevant.Select(a => $"{a.Type}{a.Level}"));
        var enchantString = GetEnchantmentString(auction);
        keyBuilder.Append(enchantString);

        // Add flattened NBT data (line 444):
        // key += String.Concat(auction.FlatenedNBT.Where(d => !ignoredNbt.Contains(d.Key)));
        var nbtString = GetFlattenedNbtString(auction);
        keyBuilder.Append(nbtString);

        var cacheKey = keyBuilder.ToString();
        _logger.LogDebug("Generated cache key for {Tag}: {CacheKey}", auction.Tag, cacheKey);

        return cacheKey;
    }

    /// <summary>
    /// Normalizes pet level in item name for range matching.
    /// Reference: FlippingEngine.cs GetPetLevelSelectVal() lines 902-912
    /// 
    /// Examples:
    /// - "[Lvl 100] Dragon" → "[Lvl 10_] Dragon" (matches 100-109)
    /// - "[Lvl 95] Wolf" → "[Lvl 9_] Wolf" (matches 90-99)
    /// - "[Lvl 5] Bee" → "[Lvl _] Bee" (matches 1-9)
    /// </summary>
    private static string NormalizePetLevel(string? itemName, string? tag)
    {
        if (string.IsNullOrEmpty(itemName) || string.IsNullOrEmpty(tag))
            return itemName ?? "";
        
        // Only applies to pets
        if (!IsPet(tag))
            return itemName;

        var match = PetLevelRegex.Match(itemName);
        if (!match.Success)
            return itemName;

        var levelStr = match.Groups[1].Value;
        if (levelStr.Length == 0)
            return itemName;

        // Reference logic: replace last digit with underscore
        // [Lvl 100] → replace char at position 7 (after "[Lvl 10")
        // [Lvl 95] → replace char at position 6 (after "[Lvl 9")
        // [Lvl 5] → replace char at position 5 (after "[Lvl ")
        var sb = new StringBuilder(itemName);
        var levelEndIndex = match.Index + match.Length - 1; // Position of ']'
        var lastDigitIndex = levelEndIndex - 1; // Position of last digit before ']'
        
        if (lastDigitIndex >= 0 && lastDigitIndex < sb.Length && char.IsDigit(sb[lastDigitIndex]))
        {
            sb[lastDigitIndex] = '_';
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates a "base" cache key that excludes gemstones.
    /// Used for finding the base value of an item when gems will be valued separately.
    /// </summary>
    public string GenerateBaseCacheKey(Auction auction)
    {
        if (auction == null) throw new ArgumentNullException(nameof(auction));

        var keyBuilder = new StringBuilder();
        
        keyBuilder.Append('o');
        keyBuilder.Append(auction.Tag);
        keyBuilder.Append(auction.ItemName ?? "");
        keyBuilder.Append((int)auction.Tier);
        keyBuilder.Append(auction.Count);

        if (RelevantReforges.Contains(auction.Reforge))
        {
            keyBuilder.Append(auction.Reforge.ToString());
        }

        var enchantString = GetEnchantmentString(auction);
        keyBuilder.Append(enchantString);

        // Exclude gemstones from base key - they're valued separately
        var nbtString = GetFlattenedNbtString(auction, excludeGems: true);
        keyBuilder.Append(nbtString);

        return keyBuilder.ToString();
    }

    /// <summary>
    /// Gets the enchantment string for cache key.
    /// 
    /// Reference: FlippingEngine.cs lines 439-443:
    /// var relevant = ExtractRelevantEnchants(auction);
    /// if (relevant.Count() == 0)
    ///     key += String.Concat(auction.Enchantments.Select(a => $"{a.Type}{a.Level}"));
    /// else
    ///     key += String.Concat(relevant.Select(a => $"{a.Type}{a.Level}"));
    /// </summary>
    private string GetEnchantmentString(Auction auction)
    {
        if (auction.Enchantments == null || !auction.Enchantments.Any())
            return string.Empty;

        // Extract relevant enchants using reference algorithm
        var relevantEnchants = ExtractRelevantEnchants(auction.Enchantments);

        if (relevantEnchants.Count == 0)
        {
            // If no relevant enchants, include ALL enchants in key (reference behavior)
            // Note: Reference doesn't sort, but we sort for consistency
            return string.Concat(auction.Enchantments
                .Select(e => $"{e.Type}{e.Level}"));
        }
        else
        {
            // Only include relevant enchants
            return string.Concat(relevantEnchants
                .Select(e => $"{e.Type}{e.Level}"));
        }
    }

    /// <summary>
    /// Extracts enchantments that significantly affect price.
    /// 
    /// Reference: FlippingEngine.cs ExtractRelevantEnchants() (line 595-598):
    /// return auction.Enchantments?.Where(e => 
    ///     (!RelevantEnchants.ContainsKey(e.Type) && e.Level >= 6) || 
    ///     (RelevantEnchants.TryGetValue(e.Type, out byte lvl)) && e.Level >= lvl)
    ///     .ToList();
    /// </summary>
    public static List<Enchantment> ExtractRelevantEnchants(ICollection<Enchantment>? enchantments)
    {
        if (enchantments == null) return new List<Enchantment>();

        return enchantments
            .Where(e => IsRelevantEnchant(e.Type, e.Level))
            .ToList();
    }

    /// <summary>
    /// Checks if an enchantment is relevant for pricing.
    /// 
    /// Reference logic (FlippingEngine.cs line 597):
    /// (!RelevantEnchants.ContainsKey(e.Type) && e.Level >= 6) || 
    /// (RelevantEnchants.TryGetValue(e.Type, out byte lvl)) && e.Level >= lvl
    /// 
    /// Translation:
    /// 1. If NOT in RelevantEnchants dict AND level >= 6 → relevant
    /// 2. If IN RelevantEnchants dict AND level >= minimum → relevant
    /// </summary>
    private static bool IsRelevantEnchant(EnchantmentType type, int level)
    {
        // Check if it's in our known relevant enchants with minimum level
        if (RelevantEnchants.TryGetValue(type, out byte minLevel))
        {
            return level >= minLevel;
        }

        // Ultimate enchants are always relevant (they start with "ultimate_")
        if (type.ToString().StartsWith("ultimate_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // For enchants NOT in the dictionary, level 6+ is relevant
        return level >= 6;
    }

    /// <summary>
    /// Gets flattened NBT string for cache key.
    /// 
    /// CRITICAL: Reference uses FlatenedNBT dictionary and concatenates KeyValuePair objects:
    /// key += String.Concat(auction.FlatenedNBT.Where(d => !ignoredNbt.Contains(d.Key)));
    /// 
    /// KeyValuePair.ToString() produces: [key, value]
    /// So the output looks like: [dungeon_item_level, 5][rarity_upgrades, 1]...
    /// 
    /// ENHANCEMENTS for matching:
    /// - Range values (Midas, edition, kills) are normalized to range buckets
    /// - Pet held items use ShouldPetItemMatch logic
    /// - Special handling for captured_player (Cake Soul)
    /// </summary>
    private string GetFlattenedNbtString(Auction auction, bool excludeGems = false)
    {
        // Try to use FlatenedNBTJson first (matches reference's FlatenedNBT dictionary)
        if (!string.IsNullOrEmpty(auction.FlatenedNBTJson))
        {
            try
            {
                var flatNbt = JsonSerializer.Deserialize<Dictionary<string, string>>(auction.FlatenedNBTJson);
                if (flatNbt != null && flatNbt.Count > 0)
                {
                    return BuildNbtString(flatNbt, auction, excludeGems);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse FlatenedNBTJson for auction {Uuid}", auction.Uuid);
            }
        }

        // Fallback to NBTLookups if FlatenedNBTJson is not available
        if (auction.NBTLookups == null || !auction.NBTLookups.Any())
            return string.Empty;

        // Convert NBTLookups to dictionary format
        var nbtDict = new Dictionary<string, string>();
        foreach (var nbt in auction.NBTLookups)
        {
            if (nbt.NBTKey == null) continue;
            var key = nbt.NBTKey.KeyName;
            var value = !string.IsNullOrEmpty(nbt.ValueString) 
                ? nbt.ValueString 
                : nbt.ValueNumeric?.ToString() ?? "";
            nbtDict[key] = value;
        }

        return BuildNbtString(nbtDict, auction, excludeGems);
    }

    /// <summary>
    /// Builds the NBT portion of cache key with range normalization and special handling.
    /// </summary>
    private string BuildNbtString(Dictionary<string, string> flatNbt, Auction auction, bool excludeGems)
    {
        var nbtParts = new List<string>();

        foreach (var kvp in flatNbt.OrderBy(k => k.Key))
        {
            var key = kvp.Key;
            var value = kvp.Value;

            // Skip ignored keys
            if (IgnoredNbtKeys.Contains(key))
                continue;

            // Skip gem-related keys if excluding gems
            if (excludeGems && IsGemstoneKey(key))
                continue;

            // Handle special keys
            
            // Pet held item - reference lines 787-806 (AddPetItemSelect)
            if (key == "heldItem")
            {
                if (ShouldPetItemMatch(flatNbt, auction.StartingBid))
                {
                    nbtParts.Add($"[{key}, {value}]");
                }
                // If not matching, we exclude it entirely (reference excludes valuable pet items)
                continue;
            }

            // Pet candy handling - reference AddCandySelect() lines 850-862
            // Special logic: max exp pets with skins compare by skin absence, not candy
            // Otherwise: binary check (candy > 0 vs candy == 0)
            if (key == "candyUsed")
            {
                var candyValue = GetCandyCacheValue(flatNbt, value);
                if (candyValue != null)
                {
                    nbtParts.Add($"[{key}, {candyValue}]");
                }
                continue;
            }

            // Cake Soul - captured_player - reference line 671
            if (key == "captured_player")
            {
                nbtParts.Add($"[{key}, {value}]");
                continue;
            }

            // Cosmetic NBT keys - reference lines 693-703
            // MUSIC, ENCHANT, DRAGON, TIDAL, party_hat_emoji
            if (CosmeticNbtKeys.Contains(key))
            {
                nbtParts.Add($"[{key}, {value}]");
                continue;
            }

            // Armor color/dye matching - reference lines 730-734
            // IsArmour checks: _CHESTPLATE, _BOOTS, _HELMET, _LEGGINGS
            if (key == "color" || key == "dye_item")
            {
                if (IsArmor(auction.Tag) || flatNbt.ContainsKey("color"))
                {
                    nbtParts.Add($"[{key}, {value}]");
                }
                continue;
            }

            // Drill parts matching - reference lines 714-719
            if (DrillPartKeys.Contains(key) && auction.Tag.Contains("_DRILL"))
            {
                nbtParts.Add($"[{key}, {value}]");
                continue;
            }

            // Gemstone slots - reference lines 748-758
            // unlocked_slots and gemstone_slots affect item value
            // These are included in cache key for proper matching
            if (key == "unlocked_slots" || key == "gemstone_slots")
            {
                nbtParts.Add($"[{key}, {value}]");
                continue;
            }

            // Attribute keys with weighted matching - reference lines 67-83, 708-711
            if (AttributeKeys.Contains(key))
            {
                // Check if this is a shard attribute with specific weighting
                if (ShardAttributes.TryGetValue(key, out var weight))
                {
                    // Higher weight = narrower range (more exact matching needed)
                    // Weight 3 = exact, Weight 2 = 20% range, Weight 1 = 40% range
                    var rangePercent = weight switch
                    {
                        3 => 10,   // Narrow range for valuable attributes
                        2 => 20,   // Medium range
                        _ => 40    // Broader range for common attributes
                    };
                    var normalizedValue = NormalizeToRange(value, 0, rangePercent);
                    nbtParts.Add($"[{key}, {normalizedValue}]");
                }
                else
                {
                    // Standard attribute - include directly
                    nbtParts.Add($"[{key}, {value}]");
                }
                continue;
            }

            // Special item-specific NBT keys - reference lines 676-704
            // These keys are critical for specific items (Necrons Ladder, Dianas Bookshelf, AOTV/AOTE)
            if (SpecialItemNbtKeys.TryGetValue(auction.Tag, out var requiredKeys) && requiredKeys.Contains(key))
            {
                // Always include these keys exactly for their specific items
                nbtParts.Add($"[{key}, {value}]");
                continue;
            }

            // Range-based keys (Midas, edition, kills, seconds_held)
            if (NbtRangeKeys.TryGetValue(key, out var rangeConfig))
            {
                var normalizedValue = NormalizeToRange(value, rangeConfig.RangeSize, rangeConfig.PercentIncrease);
                nbtParts.Add($"[{key}, {normalizedValue}]");
                continue;
            }

            // Keys ending with _kills need range matching - reference lines 736-746
            if (KillsKeyPattern.IsMatch(key) && long.TryParse(value, out var killsVal))
            {
                // Reference uses 20% range (val * 0.8 to val * 1.2)
                var rangeVal = NormalizeToRange(value, 0, 20);
                nbtParts.Add($"[{key}, {rangeVal}]");
                continue;
            }

            // Standard key-value pair
            nbtParts.Add($"[{key}, {value}]");
        }

        return string.Concat(nbtParts);
    }

    /// <summary>
    /// Normalizes a numeric value to a range bucket for cache key matching.
    /// Reference: FlippingEngine.cs AddNbtRangeSelect() lines 882-893
    /// 
    /// Creates range buckets so similar values match the same cache key.
    /// </summary>
    private static string NormalizeToRange(string valueStr, long baseRange, int percentIncrease)
    {
        if (!long.TryParse(valueStr, out var value))
            return valueStr; // Return as-is if not numeric

        // Calculate effective range: baseRange + (value * percentIncrease / 100)
        var effectiveRange = baseRange + (value * percentIncrease / 100);
        if (effectiveRange == 0) effectiveRange = 1;

        // Bucket the value
        var bucket = (value / effectiveRange) * effectiveRange;
        return bucket.ToString();
    }

    /// <summary>
    /// Determines if a pet's held item should be included in the cache key match.
    /// Reference: FlippingEngine.cs ShouldPetItemMatch() lines 808-833
    /// 
    /// Some pet items are always valuable and should match exactly.
    /// Skill boost items only matter if pet has room to grow (exp < 24M).
    /// </summary>
    public static bool ShouldPetItemMatch(Dictionary<string, string> flatNbt, long startingBid)
    {
        if (!flatNbt.TryGetValue("heldItem", out var heldItem) || string.IsNullOrEmpty(heldItem))
            return false;

        // Valuable pet items always match exactly - reference lines 812-827
        if (ValuablePetItems.Contains(heldItem))
            return true;

        // For skill boost items, check if there's room to grow
        // Reference line 831-832: only if exp < 24M and it's a skill boost
        if (heldItem.Contains("SKILL") && heldItem.Contains("BOOST"))
        {
            if (flatNbt.TryGetValue("exp", out var expStr) && 
                double.TryParse(expStr, out var exp) && 
                exp >= 24_000_000)
            {
                // Max level pet, skill boost doesn't add value
                return false;
            }
            // Low value items don't care about boost - reference line 832
            if (startingBid >= 20_000_000)
            {
                return false;
            }
            return true;
        }

        // Other held items - include in match
        return true;
    }

    /// <summary>
    /// Gets the cache key value for candyUsed.
    /// Reference: FlippingEngine.cs AddCandySelect() lines 850-862
    /// 
    /// Special logic:
    /// - If pet has max exp (>24M) AND has a skin → return null (compare by skin, not candy)
    /// - Otherwise: binary check (">0" vs "0")
    /// </summary>
    private static string? GetCandyCacheValue(Dictionary<string, string> flatNbt, string candyValue)
    {
        // Parse candy value
        if (!long.TryParse(candyValue, out var candy))
            return candyValue;

        // Special case: max exp pet with skin - reference lines 854-858
        // For max level pets with skins, candy doesn't matter - they compare by skin instead
        if (flatNbt.TryGetValue("exp", out var expStr) &&
            double.TryParse(expStr, out var exp) &&
            exp > 24_000_000 &&
            flatNbt.ContainsKey("skin"))
        {
            // Don't include candy in cache key - skin matching takes precedence
            return null;
        }

        // Binary check: candy > 0 vs candy == 0 - reference lines 859-861
        return candy > 0 ? ">0" : "0";
    }

    /// <summary>
    /// Checks if an NBT key is gemstone-related.
    /// </summary>
    private static bool IsGemstoneKey(string key)
    {
        var upper = key.ToUpperInvariant();
        return upper.Contains("RUBY") || upper.Contains("AMBER") || upper.Contains("TOPAZ") ||
               upper.Contains("JADE") || upper.Contains("SAPPHIRE") || upper.Contains("AMETHYST") ||
               upper.Contains("JASPER") || upper.Contains("OPAL") ||
               upper.StartsWith("COMBAT_") || upper.StartsWith("DEFENSIVE_") || upper.StartsWith("UNIVERSAL_") ||
               upper == "UNLOCKED_SLOTS" || upper == "GEMSTONE_SLOTS";
    }

    /// <summary>
    /// Checks if the tag represents a pet.
    /// Reference: NBT.IsPet()
    /// </summary>
    public static bool IsPet(string? tag)
    {
        if (string.IsNullOrEmpty(tag)) return false;
        return tag == "PET" || tag.StartsWith("PET_");
    }

    /// <summary>
    /// Checks if the tag represents armor.
    /// Reference: FlippingEngine.cs IsArmour() line 771-774
    /// </summary>
    public static bool IsArmor(string? tag)
    {
        if (string.IsNullOrEmpty(tag)) return false;
        return tag.EndsWith("_CHESTPLATE") || tag.EndsWith("_BOOTS") || 
               tag.EndsWith("_HELMET") || tag.EndsWith("_LEGGINGS");
    }

    /// <summary>
    /// Checks if an auction contains Master Crypt Sols.
    /// Reference: FlippingEngine.cs lines 535-536
    /// 
    /// Master Crypt items are excluded from matching unless the target has them too.
    /// </summary>
    public static bool HasMasterCryptSols(Dictionary<string, string>? flatNbt)
    {
        if (flatNbt == null) return false;
        return flatNbt.Keys.Any(k => k.StartsWith("MASTER_CRYPT", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Checks if an auction contains Master Crypt Sols from NBTLookups.
    /// </summary>
    public static bool HasMasterCryptSols(Auction auction)
    {
        if (!string.IsNullOrEmpty(auction.FlatenedNBTJson))
        {
            try
            {
                var flatNbt = JsonSerializer.Deserialize<Dictionary<string, string>>(auction.FlatenedNBTJson);
                return HasMasterCryptSols(flatNbt);
            }
            catch { }
        }

        return auction.NBTLookups?.Any(n => 
            n.NBTKey?.KeyName?.StartsWith("MASTER_CRYPT", StringComparison.OrdinalIgnoreCase) == true) == true;
    }

    /// <summary>
    /// Checks if rarity matters for this item category/tag.
    /// Reference: Constants.cs DoesRecombMatter()
    /// </summary>
    public static bool DoesRecombMatter(Category category, string? tag)
    {
        if (category == Category.WEAPON || category == Category.ARMOR || 
            category == Category.ACCESSORIES || category == Category.UNKNOWN || tag == null)
            return true;

        string[] endings = { "CLOAK", "NECKLACE", "BELT", "GLOVES", "BRACELET", "HOE", 
                            "PICKAXE", "GAUNTLET", "WAND", "ROD", "DRILL", "INFINI_VACUUM", 
                            "POWER_ORB", "GRIFFIN_UPGRADE_STONE_EPIC" };
        
        return endings.Any(e => tag.Contains(e));
    }
}