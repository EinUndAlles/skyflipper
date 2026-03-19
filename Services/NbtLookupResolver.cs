using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;

namespace SkyFlipperSolo.Services;

public sealed class NbtLookupResolver
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrentDictionary<string, short> _keyIdCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<(short KeyId, string Value), int> _valueIdCache = new();

    public NbtLookupResolver(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<short> GetKeyIdAsync(string keyName, CancellationToken stoppingToken)
    {
        if (string.IsNullOrEmpty(keyName))
            return 0;

        if (_keyIdCache.TryGetValue(keyName, out var cached))
            return cached;

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var key = await dbContext.NBTKeys.AsNoTracking()
            .FirstOrDefaultAsync(k => k.KeyName == keyName, stoppingToken);

        if (key == null)
            return 0;

        _keyIdCache[keyName] = key.Id;
        return key.Id;
    }

    public async Task<int> GetValueIdAsync(short keyId, string value, CancellationToken stoppingToken)
    {
        if (keyId == 0 || string.IsNullOrEmpty(value))
            return 0;

        var cacheKey = (keyId, value);
        if (_valueIdCache.TryGetValue(cacheKey, out var cached))
            return cached;

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var valueEntity = await dbContext.NBTValues.AsNoTracking()
            .FirstOrDefaultAsync(v => v.KeyId == keyId && v.Value == value, stoppingToken);

        if (valueEntity == null)
            return 0;

        _valueIdCache[cacheKey] = valueEntity.Id;
        return valueEntity.Id;
    }

    public async Task<IReadOnlyList<int>> GetValueIdsAsync(
        short keyId,
        IReadOnlyCollection<string> values,
        CancellationToken stoppingToken)
    {
        if (keyId == 0 || values.Count == 0)
            return Array.Empty<int>();

        var result = new List<int>();
        var missing = new List<string>();

        foreach (var value in values)
        {
            if (string.IsNullOrEmpty(value))
                continue;

            var cacheKey = (keyId, value);
            if (_valueIdCache.TryGetValue(cacheKey, out var cached))
            {
                result.Add(cached);
            }
            else
            {
                missing.Add(value);
            }
        }

        if (missing.Count == 0)
            return result;

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var fetched = await dbContext.NBTValues.AsNoTracking()
            .Where(v => v.KeyId == keyId && missing.Contains(v.Value))
            .Select(v => new { v.Id, v.Value })
            .ToListAsync(stoppingToken);

        foreach (var entry in fetched)
        {
            _valueIdCache[(keyId, entry.Value)] = entry.Id;
            result.Add(entry.Id);
        }

        return result;
    }
}
