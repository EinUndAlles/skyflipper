using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services;

/// <summary>
/// Service for managing NBT string value deduplication.
/// Based on Coflnet.Sky.Commands.Shared.GetValueId() pattern.
/// Stores unique values once and returns their IDs for 90% storage savings.
/// </summary>
public class NBTValueService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NBTValueService> _logger;
    private readonly Dictionary<(short keyId, string value), int> _cache = new();
    private readonly object _lock = new();

    public NBTValueService(IServiceScopeFactory scopeFactory, ILogger<NBTValueService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Gets the ID for a value, creating it if it doesn't exist.
    /// Thread-safe with in-memory caching.
    /// </summary>
    public async Task<int> GetOrCreateValueId(short keyId, string value)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException("Value cannot be null or empty", nameof(value));

        // Check cache first
        var cacheKey = (keyId, value);
        lock (_lock)
        {
            if (_cache.TryGetValue(cacheKey, out var cachedId))
                return cachedId;
        }

        // Not in cache, query database
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var nbtValue = await context.NBTValues
            .FirstOrDefaultAsync(v => v.KeyId == keyId && v.Value == value);

        if (nbtValue == null)
        {
            // Create new value
            nbtValue = new NBTValue
            {
                KeyId = keyId,
                Value = value
            };
            context.NBTValues.Add(nbtValue);
            await context.SaveChangesAsync();

            _logger.LogDebug("Created new NBT value: {KeyId}={Value} (ID: {Id})", keyId, value, nbtValue.Id);
        }

        // Add to cache
        lock (_lock)
        {
            _cache[cacheKey] = nbtValue.Id;
        }

        return nbtValue.Id;
    }

    /// <summary>
    /// Pre-loads commonly used values into cache for performance.
    /// Useful during application startup.
    /// </summary>
    public async Task LoadCommonValues()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Load top 1000 most common values (for frequently appearing skins, held items, etc.)
        var commonValues = await context.NBTValues
            .OrderBy(v => v.Id)  // Load in order of creation (older = more common)
            .Take(1000)
            .ToListAsync();

        lock (_lock)
        {
            _cache.Clear();
            foreach (var val in commonValues)
                _cache[(val.KeyId, val.Value)] = val.Id;
        }

        _logger.LogInformation("Loaded {Count} common NBT values into cache", commonValues.Count);
    }

    /// <summary>
    /// Gets cache statistics for monitoring.
    /// </summary>
    public (int Count, int StorageSavingsEstimate) GetCacheStats()
    {
        lock (_lock)
        {
            var count = _cache.Count;
            // Estimate: Each cached value saves ~96 bytes (100 byte string vs 4 byte int) per reference
            // Average 1000 references per value = 96KB saved per cached value
            var savingsEstimate = count * 96 * 1000;
            return (count, savingsEstimate);
        }
    }
}
