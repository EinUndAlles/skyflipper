using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services;

public sealed class NbtQueryHelper
{
    private readonly AppDbContext _dbContext;
    private readonly NbtLookupResolver _lookupResolver;
    private readonly Auction _auction;
    private readonly Dictionary<string, string> _targetFlatNbt;
    private readonly CancellationToken _stoppingToken;
    private readonly HashSet<string> _valuablePetItems;

    public NbtQueryHelper(
        AppDbContext dbContext,
        NbtLookupResolver lookupResolver,
        Auction auction,
        Dictionary<string, string> targetFlatNbt,
        HashSet<string> valuablePetItems,
        CancellationToken stoppingToken)
    {
        _dbContext = dbContext;
        _lookupResolver = lookupResolver;
        _auction = auction;
        _targetFlatNbt = targetFlatNbt;
        _valuablePetItems = valuablePetItems;
        _stoppingToken = stoppingToken;
    }

    public async Task<IQueryable<Auction>> AddNbtSelectAsync(IQueryable<Auction> query, string keyName)
    {
        var keyId = await _lookupResolver.GetKeyIdAsync(keyName, _stoppingToken);
        if (keyId == 0)
            return query;
        if (!_targetFlatNbt.TryGetValue(keyName, out var targetValue))
            return query.Where(a => !a.NBTLookups.Any(n => n.KeyId == keyId));

        if (!long.TryParse(targetValue, out var numericTarget))
        {
            var valueId = await _lookupResolver.GetValueIdAsync(keyId, targetValue, _stoppingToken);
            if (valueId == 0)
                return query;

            return query.Where(a => a.NBTLookups.Any(n =>
                n.KeyId == keyId && n.ValueId.HasValue && n.ValueId.Value == valueId));
        }

        var lowerLimit = (long)(numericTarget * 0.8);
        return query.Where(a => a.NBTLookups.Any(n =>
            n.KeyId == keyId &&
            n.ValueNumeric.HasValue &&
            n.ValueNumeric <= numericTarget && n.ValueNumeric > lowerLimit));
    }

    public async Task<IQueryable<Auction>> AddNbtRangeSelectAsync(IQueryable<Auction> query, string keyName, long maxDiff, int percentIncrease)
    {
        var keyId = await _lookupResolver.GetKeyIdAsync(keyName, _stoppingToken);
        if (keyId == 0)
            return query;
        if (!_targetFlatNbt.TryGetValue(keyName, out var targetValue))
            return query.Where(a => !a.NBTLookups.Any(n => n.KeyId == keyId));

        if (!long.TryParse(targetValue, out var numericTarget))
            return query;

        maxDiff += numericTarget * percentIncrease / 100;
        var min = numericTarget - maxDiff;
        var max = numericTarget + maxDiff;

        return query.Where(a => a.NBTLookups.Any(n =>
            n.KeyId == keyId &&
            n.ValueNumeric.HasValue &&
            n.ValueNumeric > min && n.ValueNumeric < max));
    }

    public async Task<IQueryable<Auction>> AddCandySelectAsync(IQueryable<Auction> query)
    {
        if (!_targetFlatNbt.TryGetValue("candyUsed", out var candyValue))
            return query;

        if (_targetFlatNbt.TryGetValue("exp", out var expString) &&
            double.TryParse(expString, out var exp) &&
            exp > 24_000_000 &&
            _targetFlatNbt.ContainsKey("skin"))
        {
            var heldItemKeyId = await _lookupResolver.GetKeyIdAsync("heldItem", _stoppingToken);
            if (heldItemKeyId == 0)
                return query;
            return query.Where(a => !a.NBTLookups.Any(n => n.KeyId == heldItemKeyId));
        }

        if (!long.TryParse(candyValue, out var numericCandy))
            return query;

        var keyId = await _lookupResolver.GetKeyIdAsync("candyUsed", _stoppingToken);
        if (keyId == 0)
            return query;
        if (numericCandy > 0)
        {
            return query.Where(a => a.NBTLookups.Any(n =>
                n.KeyId == keyId && n.ValueNumeric.HasValue && n.ValueNumeric > 0));
        }

        return query.Where(a => a.NBTLookups.Any(n =>
            n.KeyId == keyId && n.ValueNumeric.HasValue && n.ValueNumeric == 0));
    }

    public async Task<IQueryable<Auction>> AddPetItemSelectAsync(IQueryable<Auction> query)
    {
        if (_targetFlatNbt.TryGetValue("heldItem", out var heldItem))
        {
            var keyId = await _lookupResolver.GetKeyIdAsync("heldItem", _stoppingToken);
            if (keyId == 0)
                return query;
            if (CacheKeyService.ShouldPetItemMatch(_targetFlatNbt, _auction.StartingBid))
            {
                var valueId = await _lookupResolver.GetValueIdAsync(keyId, heldItem, _stoppingToken);
                if (valueId == 0)
                    return query;

                return query.Where(a => a.NBTLookups.Any(n =>
                    n.KeyId == keyId && n.ValueId.HasValue && n.ValueId.Value == valueId));
            }

            var valuableIds = await _lookupResolver.GetValueIdsAsync(keyId, _valuablePetItems, _stoppingToken);

            if (valuableIds.Count == 0)
                return query;

            return query.Where(a => !a.NBTLookups.Any(n =>
                n.KeyId == keyId && n.ValueId.HasValue && valuableIds.Contains(n.ValueId.Value)));
        }

        if (_targetFlatNbt.ContainsKey("candyUsed"))
        {
            var keyId = await _lookupResolver.GetKeyIdAsync("heldItem", _stoppingToken);
            if (keyId == 0)
                return query;
            return query.Where(a => !a.NBTLookups.Any(n => n.KeyId == keyId));
        }

        return query;
    }

}
