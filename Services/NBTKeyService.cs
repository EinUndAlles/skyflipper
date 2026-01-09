using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services;

/// <summary>
/// Service for managing NBT key normalization to integer IDs.
/// Based on Coflnet reference pattern for storage efficiency.
/// Caches key mappings in memory to avoid repeated database lookups.
/// </summary>
public class NBTKeyService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NBTKeyService> _logger;
    private readonly Dictionary<string, short> _keyCache = new();
    private readonly object _lock = new();
    private bool _seeded = false;

    public NBTKeyService(IServiceScopeFactory scopeFactory, ILogger<NBTKeyService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Gets the ID for a key name, creating it if it doesn't exist.
    /// Thread-safe with in-memory caching.
    /// </summary>
    public async Task<short> GetOrCreateKeyId(string keyName)
    {
        // Check cache first
        lock (_lock)
        {
            if (_keyCache.TryGetValue(keyName, out var cachedId))
                return cachedId;
        }

        // Not in cache, query database
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var key = await context.NBTKeys.FirstOrDefaultAsync(k => k.KeyName == keyName);

        if (key == null)
        {
            // Create new key
            key = new NBTKey { KeyName = keyName };
            context.NBTKeys.Add(key);
            await context.SaveChangesAsync();

            _logger.LogInformation("Created new NBT key: {KeyName} (ID: {Id})", keyName, key.Id);
        }

        // Add to cache
        lock (_lock)
        {
            _keyCache[keyName] = key.Id;
        }

        return key.Id;
    }

    /// <summary>
    /// Seeds the database with common NBT keys based on Coflnet reference.
    /// Should be called on application startup.
    /// </summary>
    public async Task SeedCommonKeys()
    {
        if (_seeded) return;

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existingCount = await context.NBTKeys.CountAsync();
        if (existingCount > 0)
        {
            _logger.LogInformation("NBT keys already seeded ({Count} keys)", existingCount);
            _seeded = true;
            return;
        }

        var commonKeys = new List<string>
        {
            // Basic attributes
            "upgrade_level", "hot_potato_count", "rarity_upgrades", "anvil_uses", "timestamp",
            "exp", "candyUsed", "art_of_war_count", "mana_pool", "breaker",
            
            // Dungeon
            "dungeon_item_level", "stars", "dungeon_skill_req", "dungeon_paper_id",
            
            // Gems (per slot: COMBAT_0, COMBAT_1, DEFENSIVE_0, etc.)
            "COMBAT_0", "COMBAT_0_gem", "COMBAT_0_uuid", "COMBAT_0_quality",
            "COMBAT_1", "COMBAT_1_gem", "COMBAT_1_uuid", "COMBAT_1_quality",
            "DEFENSIVE_0", "DEFENSIVE_0_gem", "DEFENSIVE_0_uuid", "DEFENSIVE_0_quality",
            "UNIVERSAL_0", "UNIVERSAL_0_gem", "UNIVERSAL_0_uuid", "UNIVERSAL_0_quality",
            "OFFENSIVE_0", "OFFENSIVE_0_gem", "OFFENSIVE_0_uuid", "OFFENSIVE_0_quality",
            
            // Pets
            "pet_exp", "pet_level", "pet_tier", "pet_held_item", "pet_skin",
            
            // Potions
            "potion", "potion_type", "potion_name", "effect", "duration", "level",
            
            // Drills
            "drill_part_engine", "drill_part_fuel_tank", "drill_part_upgrade_module",
            
            // Backpacks
            "small_backpack_data", "medium_backpack_data", "large_backpack_data",
            "greater_backpack_data", "jumbo_backpack_data",
            
            // Event items
            "captured_player", "leaderboard_player", "mob_id", "cake_owner",
            "party_hat_color", "spray", "repelling_color",
            
            // Misc
            "heldItem", "skin", "candies_used", "zombie_kills", "ender_dragon_kills",
            "enderman_kills", "floor_completions", "master_completions"
        };

        var keys = commonKeys.Select(kn => new NBTKey { KeyName = kn }).ToList();
        context.NBTKeys.AddRange(keys);
        await context.SaveChangesAsync();

        _logger.LogInformation("Seeded {Count} common NBT keys", keys.Count);
        _seeded = true;

        // Load into cache
        lock (_lock)
        {
            foreach (var key in keys)
                _keyCache[key.KeyName] = key.Id;
        }
    }

    /// <summary>
    /// Pre-loads all keys from database into cache.
    /// Useful for optimizing bulk operations.
    /// </summary>
    public async Task LoadAllKeysIntoCache()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var allKeys = await context.NBTKeys.ToListAsync();

        lock (_lock)
        {
            _keyCache.Clear();
            foreach (var key in allKeys)
                _keyCache[key.KeyName] = key.Id;
        }

        _logger.LogInformation("Loaded {Count} NBT keys into cache", allKeys.Count);
    }
}
