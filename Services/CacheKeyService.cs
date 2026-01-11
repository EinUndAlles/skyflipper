using SkyFlipperSolo.Models;
using System.Text;
using System.Text.Json;

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
    /// </summary>
    public string GeneratePriceCacheKey(Auction auction)
    {
        if (auction == null) throw new ArgumentNullException(nameof(auction));

        var keyBuilder = new StringBuilder();
        
        // Prefix 'o' like reference project
        keyBuilder.Append('o');
        keyBuilder.Append(auction.Tag);
        keyBuilder.Append(auction.ItemName ?? "");
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
    /// We need to match this format exactly for cache key compatibility!
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
                    var filtered = flatNbt
                        .Where(d => !IgnoredNbtKeys.Contains(d.Key))
                        .Where(d => !excludeGems || !IsGemstoneKey(d.Key));

                    // Reference uses String.Concat which calls ToString() on each KeyValuePair
                    // KeyValuePair<string,string>.ToString() returns "[key, value]"
                    return string.Concat(filtered.Select(kvp => $"[{kvp.Key}, {kvp.Value}]"));
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

        var nbtParts = new List<string>();

        foreach (var nbt in auction.NBTLookups.OrderBy(n => n.NBTKey?.KeyName))
        {
            if (nbt.NBTKey == null) continue;
            var key = nbt.NBTKey.KeyName;

            // Skip ignored keys
            if (IgnoredNbtKeys.Contains(key))
                continue;

            // Skip gem-related keys if excluding gems
            if (excludeGems && IsGemstoneKey(key))
                continue;

            // Match the [key, value] format of KeyValuePair.ToString()
            if (!string.IsNullOrEmpty(nbt.ValueString))
            {
                nbtParts.Add($"[{key}, {nbt.ValueString}]");
            }
            else if (nbt.ValueNumeric.HasValue)
            {
                nbtParts.Add($"[{key}, {nbt.ValueNumeric.Value}]");
            }
        }

        return string.Concat(nbtParts);
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