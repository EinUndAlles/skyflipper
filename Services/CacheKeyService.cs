using SkyFlipperSolo.Models;
using System.Text;
using System.Security.Cryptography;

namespace SkyFlipperSolo.Services;

/// <summary>
/// Service for generating NBT-aware cache keys for price comparison.
/// Creates unique identifiers that represent items with identical value-affecting properties.
/// Based on Coflnet's SkyFlipper cache key generation.
/// </summary>
public class CacheKeyService
{
    private readonly ILogger<CacheKeyService> _logger;

    public CacheKeyService(ILogger<CacheKeyService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Generates a cache key for price comparison that includes all value-affecting properties.
    /// </summary>
    public string GeneratePriceCacheKey(Auction auction)
    {
        if (auction == null) throw new ArgumentNullException(nameof(auction));

        var keyBuilder = new StringBuilder();
        keyBuilder.Append(auction.Tag);

        // Add stars/dungeon level
        var stars = GetStars(auction);
        if (stars > 0)
        {
            keyBuilder.Append($"_stars{stars}");
        }

        // Add recombobulated flag
        if (IsRecombobulated(auction))
        {
            keyBuilder.Append("_recomb");
        }

        // Add reforge (if not None)
        if (auction.Reforge != Reforge.None)
        {
            keyBuilder.Append($"_{auction.Reforge.ToString().ToLower()}");
        }

        // Handle pet-specific properties
        if (IsPet(auction.Tag))
        {
            var petLevel = GetPetLevel(auction);
            if (petLevel > 0)
            {
                // Group pets by level ranges (1-19, 20-39, 40-59, 60-79, 80-99, 100)
                var levelGroup = GetPetLevelGroup(petLevel);
                keyBuilder.Append($"_lvl{levelGroup}");
            }

            var heldItem = GetPetHeldItem(auction);
            if (!string.IsNullOrEmpty(heldItem))
            {
                keyBuilder.Append($"_held{heldItem.ToLower()}");
            }

            var skin = GetPetSkin(auction);
            if (!string.IsNullOrEmpty(skin))
            {
                keyBuilder.Append($"_skin{skin.ToLower()}");
            }

            var candyUsed = GetPetCandyUsed(auction);
            if (candyUsed > 0)
            {
                keyBuilder.Append($"_candy{candyUsed}");
            }
        }
        else
        {
            // Handle enchantments for non-pet items
            var enchantHash = GetEnchantmentHash(auction);
            if (!string.IsNullOrEmpty(enchantHash))
            {
                keyBuilder.Append($"_ench{enchantHash}");
            }

            // Add gemstones (only Perfect and Flawless add significant value)
            var gemHash = GetGemstoneHash(auction);
            if (!string.IsNullOrEmpty(gemHash))
            {
                keyBuilder.Append($"_gems{gemHash}");
            }

            // Add other value modifiers
            var modifiers = GetValueModifiers(auction);
            if (modifiers.Any())
            {
                keyBuilder.Append($"_mod{string.Join("", modifiers.OrderBy(m => m))}");
            }
        }

        var cacheKey = keyBuilder.ToString();
        _logger.LogDebug("Generated cache key for {Tag}: {CacheKey}", auction.Tag, cacheKey);

        return cacheKey;
    }

    /// <summary>
    /// Gets the star level from NBT data.
    /// </summary>
    private int GetStars(Auction auction)
    {
        if (auction.NBTLookups == null) return 0;

        // Try dungeon_item_level first (most common)
        var dungeonLevel = auction.NBTLookups
            .FirstOrDefault(n => n.NBTKey?.KeyName == "dungeon_item_level" || n.NBTKey?.KeyName == "stars");

        if (dungeonLevel?.ValueNumeric.HasValue == true)
        {
            return (int)dungeonLevel.ValueNumeric.Value;
        }

        // Try upgrade_level (alternative)
        var upgradeLevel = auction.NBTLookups
            .FirstOrDefault(n => n.NBTKey?.KeyName == "upgrade_level");

        return upgradeLevel?.ValueNumeric.HasValue == true ? (int)upgradeLevel.ValueNumeric.Value : 0;
    }

    /// <summary>
    /// Checks if item is recombobulated.
    /// </summary>
    private bool IsRecombobulated(Auction auction)
    {
        if (auction.NBTLookups == null) return false;

        var recomb = auction.NBTLookups
            .FirstOrDefault(n => n.NBTKey?.KeyName == "rarity_upgrades");

        return recomb?.ValueNumeric.HasValue == true && recomb.ValueNumeric.Value > 0;
    }

    /// <summary>
    /// Checks if the tag represents a pet.
    /// </summary>
    private bool IsPet(string tag)
    {
        return tag == "PET" || tag.StartsWith("PET_");
    }

    /// <summary>
    /// Gets pet level from NBT data.
    /// </summary>
    private int GetPetLevel(Auction auction)
    {
        if (auction.NBTLookups == null) return 0;

        var levelLookup = auction.NBTLookups
            .FirstOrDefault(n => n.NBTKey?.KeyName == "level");

        return levelLookup?.ValueNumeric.HasValue == true ? (int)levelLookup.ValueNumeric.Value : 0;
    }

    /// <summary>
    /// Groups pet levels into ranges for price comparison.
    /// </summary>
    private string GetPetLevelGroup(int level)
    {
        if (level >= 100) return "100";
        if (level >= 80) return "80-99";
        if (level >= 60) return "60-79";
        if (level >= 40) return "40-59";
        if (level >= 20) return "20-39";
        return "1-19";
    }

    /// <summary>
    /// Gets pet's held item.
    /// </summary>
    private string? GetPetHeldItem(Auction auction)
    {
        if (auction.NBTLookups == null) return null;

        var heldItemLookup = auction.NBTLookups
            .FirstOrDefault(n => n.NBTKey?.KeyName == "heldItem");

        return heldItemLookup?.ValueString;
    }

    /// <summary>
    /// Gets pet skin.
    /// </summary>
    private string? GetPetSkin(Auction auction)
    {
        if (auction.NBTLookups == null) return null;

        var skinLookup = auction.NBTLookups
            .FirstOrDefault(n => n.NBTKey?.KeyName == "skin");

        return skinLookup?.ValueString;
    }

    /// <summary>
    /// Gets pet candy used.
    /// </summary>
    private int GetPetCandyUsed(Auction auction)
    {
        if (auction.NBTLookups == null) return 0;

        var candyLookup = auction.NBTLookups
            .FirstOrDefault(n => n.NBTKey?.KeyName == "candyUsed");

        return candyLookup?.ValueNumeric.HasValue == true ? (int)candyLookup.ValueNumeric.Value : 0;
    }

    /// <summary>
    /// Creates a hash of enchantments that affect item value.
    /// Only includes high-level and special enchantments.
    /// </summary>
    private string GetEnchantmentHash(Auction auction)
    {
        if (auction.Enchantments == null || !auction.Enchantments.Any())
            return string.Empty;

        // Only include enchantments that significantly affect value
        var valuableEnchants = auction.Enchantments
            .Where(e => IsValuableEnchantment(e.Type, e.Level))
            .OrderBy(e => e.Type.ToString())
            .Select(e => $"{e.Type.ToString().ToLower()}{e.Level}")
            .ToList();

        if (!valuableEnchants.Any())
            return string.Empty;

        // Create a hash to keep cache keys manageable
        var enchantString = string.Join(",", valuableEnchants);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(enchantString));
        return BitConverter.ToString(hash).Replace("-", "").Substring(0, 8).ToLower();
    }

    /// <summary>
    /// Determines if an enchantment significantly affects item value.
    /// </summary>
    private bool IsValuableEnchantment(EnchantmentType type, int level)
    {
        // Ultimate enchants are always valuable
        if (type.ToString().Contains("ULTIMATE"))
            return true;

        // High level enchants (7+) are valuable
        if (level >= 7)
            return true;

        // Special valuable enchants
        EnchantmentType[] valuableTypes = {
            EnchantmentType.sharpness, EnchantmentType.protection, EnchantmentType.efficiency,
            EnchantmentType.fortune, EnchantmentType.power, EnchantmentType.vicious,
            EnchantmentType.first_strike, EnchantmentType.giant_killer, EnchantmentType.execute,
            EnchantmentType.lethality, EnchantmentType.luck, EnchantmentType.looting,
            EnchantmentType.scavenger, EnchantmentType.smite, EnchantmentType.bane_of_arthropods,
            EnchantmentType.depth_strider, EnchantmentType.feather_falling
        };

        return valuableTypes.Contains(type) && level >= 5;
    }

    /// <summary>
    /// Creates a hash of gemstones that affect item value.
    /// Only Perfect and Flawless gems add significant value.
    /// </summary>
    private string GetGemstoneHash(Auction auction)
    {
        if (auction.NBTLookups == null) return string.Empty;

        // Find all gem-related lookups
        var gemLookups = auction.NBTLookups
            .Where(n => n.NBTKey != null && (
                n.NBTKey.KeyName.Contains("PERFECT") ||
                n.NBTKey.KeyName.Contains("FLAWLESS") ||
                n.NBTKey.KeyName.Contains("FINE") ||
                n.NBTKey.KeyName.Contains("ROUGH")))
            .Where(n => n.ValueString != null)
            .OrderBy(n => n.NBTKey.KeyName)
            .Select(n => $"{n.NBTKey.KeyName}:{n.ValueString}")
            .ToList();

        if (!gemLookups.Any())
            return string.Empty;

        // Create hash of gem configuration
        var gemString = string.Join(",", gemLookups);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(gemString));
        return BitConverter.ToString(hash).Replace("-", "").Substring(0, 8).ToLower();
    }

    /// <summary>
    /// Gets other value-affecting modifiers.
    /// </summary>
    private List<string> GetValueModifiers(Auction auction)
    {
        var modifiers = new List<string>();

        if (auction.NBTLookups == null) return modifiers;

        // Hot potato books
        var hotPotato = auction.NBTLookups
            .FirstOrDefault(n => n.NBTKey?.KeyName == "hot_potato_count");
        if (hotPotato?.ValueNumeric.HasValue == true && hotPotato.ValueNumeric.Value > 0)
        {
            modifiers.Add($"hpb{(int)hotPotato.ValueNumeric.Value}");
        }

        // Art of war
        var artOfWar = auction.NBTLookups
            .FirstOrDefault(n => n.NBTKey?.KeyName == "art_of_war_count");
        if (artOfWar?.ValueNumeric.HasValue == true && artOfWar.ValueNumeric.Value > 0)
        {
            modifiers.Add($"aow{(int)artOfWar.ValueNumeric.Value}");
        }

        // Farming for dummies
        var ffd = auction.NBTLookups
            .FirstOrDefault(n => n.NBTKey?.KeyName == "farming_for_dummies_count");
        if (ffd?.ValueNumeric.HasValue == true && ffd.ValueNumeric.Value > 0)
        {
            modifiers.Add($"ffd{(int)ffd.ValueNumeric.Value}");
        }

        // Ethermerge
        var ethermerge = auction.NBTLookups
            .FirstOrDefault(n => n.NBTKey?.KeyName == "ethermerge");
        if (ethermerge?.ValueNumeric.HasValue == true && ethermerge.ValueNumeric.Value > 0)
        {
            modifiers.Add("ether");
        }

        // Winning bid (Midas)
        var winningBid = auction.NBTLookups
            .FirstOrDefault(n => n.NBTKey?.KeyName == "winning_bid");
        if (winningBid?.ValueNumeric.HasValue == true && winningBid.ValueNumeric.Value > 0)
        {
            modifiers.Add($"midas{(int)winningBid.ValueNumeric.Value}");
        }

        return modifiers;
    }
}