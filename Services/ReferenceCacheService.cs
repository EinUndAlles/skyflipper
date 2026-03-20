using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services;

public sealed class ReferenceCacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(2);

    private readonly IDistributedCache _distributedCache;
    private readonly IServiceScopeFactory _scopeFactory;

    public ReferenceCacheService(IDistributedCache distributedCache, IServiceScopeFactory scopeFactory)
    {
        _distributedCache = distributedCache;
        _scopeFactory = scopeFactory;
    }

    public async Task<RelevantReferenceResult?> GetAsync(string cacheKey, CancellationToken stoppingToken)
    {
        var payload = await _distributedCache.GetAsync(cacheKey, stoppingToken);
        if (payload == null || payload.Length == 0)
            return null;

        var snapshot = JsonSerializer.Deserialize<ReferenceCacheSnapshot>(payload, SerializerOptions);
        if (snapshot == null || snapshot.ReferenceIds.Count == 0)
            return null;

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var auctions = await dbContext.Auctions
            .AsNoTracking()
            .Where(a => snapshot.ReferenceIds.Contains(a.Id))
            .Include(a => a.Bids)
            .Include(a => a.Enchantments)
            .Include(a => a.NBTLookups)
                .ThenInclude(n => n.NBTKey)
            .Include(a => a.NBTLookups)
                .ThenInclude(n => n.NBTValue)
            .ToListAsync(stoppingToken);

        if (auctions.Count == 0)
            return null;

        var ordered = snapshot.ReferenceIds
            .Select(id => auctions.FirstOrDefault(a => a.Id == id))
            .Where(a => a != null)
            .Cast<Auction>()
            .ToList();

        return new RelevantReferenceResult(ordered, snapshot.Oldest);
    }

    public async Task SetAsync(string cacheKey, RelevantReferenceResult result, CancellationToken stoppingToken)
    {
        if (result.References.Count == 0)
            return;

        var snapshot = new ReferenceCacheSnapshot(
            result.References.Select(a => a.Id).ToList(),
            result.Oldest);

        var payload = JsonSerializer.SerializeToUtf8Bytes(snapshot, SerializerOptions);
        await _distributedCache.SetAsync(cacheKey, payload, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheDuration
        }, stoppingToken);
    }
}

public sealed record ReferenceCacheSnapshot(List<int> ReferenceIds, DateTime Oldest);
