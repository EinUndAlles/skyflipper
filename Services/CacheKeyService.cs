using SkyFlipperSolo.Models;
using System.Text;
using System.Security.Cryptography;

namespace SkyFlipperSolo.Services;

/// <summary>
/// Service for generating NBT-aware cache keys for price comparison.
/// Creates unique identifiers that represent items with identical value-affecting properties.
/// Matches Coflnet's SkyFlipper FlippingEngine.cs GetCacheKey() logic exactly.
/// </summary>
public class CacheKeyService
{
    private readonly ILogger<CacheKeyService> _logger;

    // Reference: Constants.cs RelevantReforges - only these reforges significantly affect price
    // Note: Some reforges from reference project may not exist in our enum - we only include what we have
    private static readonly HashSet<Reforge> RelevantReforges = new()
    {
        Reforge.Gilded,
        Reforge.Withered,
        Reforge.Spiritual,
        Reforge.Jaded,
        Reforge.Warped,
        Reforge.Toil,
        Reforge.Fabled,
        Reforge.Giant,
        Reforge.Submerged,
        Reforge.Renowned,
        Reforge.Mossy,
        Reforge.Rooted,
        Reforge.Festive,
        Reforge.Lustrous,
        Reforge.Glacial
        // Note: coldfused, moonglade, blood_shot not in our Reforge enum - omitted
    };

    // Reference: Constants.cs AttributeKeys - Kuudra/Crimson Isle attributes
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

    // NBT keys to ignore (per FlippingEngine.cs)
    private static readonly HashSet<string> IgnoredNbtKeys = new()
    {
        "uid", "spawnedFor", "bossId", "exp", "uuid", "hpc", "active",
        "uniqueId", "hideRightClick", "noMove", "hideInfo"
    };

    public CacheKeyService(ILogger<CacheKeyService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generates a cache key for price comparison that includes all value-affecting properties.
    /// Reference: FlippingEngine.cs GetCacheKey() method
    /// Key format: o{Tag}{ItemName}{Tier}{Count}{Reforge?}{Enchants}{FlattenedNBT}
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

        // Add enchantments - reference includes ALL enchants in key
        var enchantHash = GetEnchantmentString(auction);
        if (!string.IsNullOrEmpty(enchantHash))
        {
            keyBuilder.Append(enchantHash);
        }

        // Add all relevant NBT data (matching FlippingEngine.cs line 444)
        var nbtString = GetFlattenedNbtString(auction);
        if (!string.IsNullOrEmpty(nbtString))
        {
            keyBuilder.Append(nbtString);
        }

        var cacheKey = keyBuilder.ToString();
        _logger.LogDebug("Generated cache key for {Tag}: {CacheKey}", auction.Tag, cacheKey);

        return cacheKey;
    }

    /// <summary>
    /// Generates a "base" cache key that excludes gemstones.
    /// Used for finding the base value of an item when gems will be valued separately.
    /// Reference: Used when no exact match found, to get base price + add gem value.
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

        var enchantHash = GetEnchantmentString(auction);
        if (!string.IsNullOrEmpty(enchantHash))
        {
            keyBuilder.Append(enchantHash);
        }

        // Exclude gemstones from base key - they're valued separately
        var nbtString = GetFlattenedNbtString(auction, excludeGems: true);
        if (!string.IsNullOrEmpty(nbtString))
        {
            keyBuilder.Append(nbtString);
        }

        return keyBuilder.ToString();
    }

    /// <summary>
    /// Gets the enchantment string for cache key.
    /// Reference: FlippingEngine.cs lines 440-443 - includes relevant enchants in key
    /// </summary>
    private string GetEnchantmentString(Auction auction)
    {
        if (auction.Enchantments == null || !auction.Enchantments.Any())
            return string.Empty;

        // Extract relevant enchants (high level or special)
        var relevantEnchants = ExtractRelevantEnchants(auction.Enchantments);

        if (relevantEnchants.Count == 0)
        {
            // If no relevant enchants, include all enchants in key
            return string.Join("", auction.Enchantments
                .OrderBy(e => e.Type.ToString())
                .Select(e => $"{e.Type}{e.Level}"));
        }
        else
        {
            // Only include relevant enchants
            return string.Join("", relevantEnchants
                .OrderBy(e => e.Type.ToString())
                .Select(e => $"{e.Type}{e.Level}"));
        }
    }

    /// <summary>
    /// Extracts enchantments that significantly affect price.
    /// Reference: FlippingEngine.cs ExtractRelevantEnchants()
    /// </summary>
    private List<Enchantment> ExtractRelevantEnchants(ICollection<Enchantment> enchantments)
    {
        if (enchantments == null) return new List<Enchantment>();

        return enchantments
            .Where(e => IsRelevantEnchant(e.Type, e.Level))
            .ToList();
    }

    /// <summary>
    /// Checks if an enchantment is relevant for pricing.
    /// Reference: Constants.cs RelevantEnchants - enchants at or above these levels matter
    /// </summary>
    private bool IsRelevantEnchant(EnchantmentType type, int level)
    {
        // Ultimate enchants are always relevant
        if (type.ToString().StartsWith("ultimate_", StringComparison.OrdinalIgnoreCase))
            return true;

        // High level enchants (6+) are generally relevant
        if (level >= 6)
            return true;

        // Specific valuable enchants at lower levels
        // Note: Only include enchants that exist in our EnchantmentType enum
        return type switch
        {
            EnchantmentType.vicious when level >= 1 => true,
            EnchantmentType.pristine when level >= 1 => true,
            EnchantmentType.overload when level >= 2 => true,
            EnchantmentType.compact when level >= 1 => true,
            EnchantmentType.cultivating when level >= 1 => true,
            EnchantmentType.divine_gift when level >= 1 => true,
            EnchantmentType.dedication when level >= 1 => true,
            EnchantmentType.expertise when level >= 1 => true,
            EnchantmentType.first_strike when level >= 5 => true,
            EnchantmentType.triple_strike when level >= 5 => true,
            EnchantmentType.life_steal when level >= 5 => true,
            EnchantmentType.looting when level >= 5 => true,
            EnchantmentType.scavenger when level >= 5 => true,
            EnchantmentType.syphon when level >= 5 => true,
            EnchantmentType.chance when level >= 5 => true,
            EnchantmentType.snipe when level >= 4 => true,
            EnchantmentType.counter_strike when level >= 5 => true,
            EnchantmentType.experience when level >= 5 => true,
            // Note: smoldering, green_thumb, prosperity, pesterminator not in our enum - omitted
            _ => false
        };
    }

    /// <summary>
    /// Gets flattened NBT string for cache key.
    /// Reference: FlippingEngine.cs line 444 - concatenates all non-ignored NBT
    /// </summary>
    private string GetFlattenedNbtString(Auction auction, bool excludeGems = false)
    {
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

            // Format: key=value or key=numericValue
            if (!string.IsNullOrEmpty(nbt.ValueString))
            {
                nbtParts.Add($"{key}={nbt.ValueString}");
            }
            else if (nbt.ValueNumeric.HasValue)
            {
                nbtParts.Add($"{key}={nbt.ValueNumeric.Value}");
            }
        }

        return string.Join(",", nbtParts);
    }

    /// <summary>
    /// Checks if an NBT key is gemstone-related.
    /// </summary>
    private bool IsGemstoneKey(string key)
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
    /// </summary>
    public bool IsPet(string tag)
    {
        return tag == "PET" || tag.StartsWith("PET_");
    }

    /// <summary>
    /// Checks if rarity matters for this item category/tag.
    /// Reference: Constants.cs DoesRecombMatter()
    /// </summary>
    public bool DoesRecombMatter(Category category, string? tag)
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