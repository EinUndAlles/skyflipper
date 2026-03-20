using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;
using System.Text.RegularExpressions;

namespace SkyFlipperSolo.Services.Filters;

public sealed class CapturedPlayerFilter : IFilter, IApplicableFilter
{
    private readonly AppDbContext _dbContext;

    public CapturedPlayerFilter(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public string Name => "CapturedPlayer";
    public FilterType FilterType => FilterType.EQUAL | FilterType.TEXT | FilterType.PLAYER_WITH_RANK;

    public IEnumerable<string> OptionsGet(FilterContext context) => Array.Empty<string>();

    public IQueryable<Auction> Apply(IQueryable<Auction> query, FilterContext context)
    {
        var name = context.Get(Name);
        var keyId = _dbContext.NBTKeys
            .Where(k => k.KeyName == "captured_player")
            .Select(k => k.Id)
            .FirstOrDefault();

        if (keyId <= 0)
            return query;

        if (string.IsNullOrWhiteSpace(name))
            return query.Where(a => !a.NBTLookups.Any(n => n.KeyId == keyId));

        if (!name.Contains("]", StringComparison.Ordinal))
        {
            var pattern = $"%{name.Trim()}%";
            var valueIds = _dbContext.NBTValues
                .Where(v => v.KeyId == keyId && EF.Functions.Like(v.Value, pattern))
                .Select(v => v.Id);
            return query.Where(a => a.NBTLookups.Any(n => n.KeyId == keyId && valueIds.Contains(n.ValueId ?? 0))
                                   || EF.Functions.Like(a.ItemName, pattern));
        }

        var parts = name.Split("] ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var playerName = parts.Length > 1 ? parts[1] : string.Empty;
        var regex = new Regex(parts[0].Trim('[', ']', '+') + ".*\\] " + playerName, RegexOptions.IgnoreCase);

        var candidates = _dbContext.NBTValues
            .Where(v => v.KeyId == keyId)
            .ToList()
            .Where(v => regex.IsMatch(v.Value))
            .Select(v => v.Id)
            .ToList();

        return query.Where(a => a.NBTLookups.Any(n => n.KeyId == keyId && n.ValueId.HasValue && candidates.Contains(n.ValueId.Value))
                               || regex.IsMatch(a.ItemName));
    }

    public bool IsApplicable(FilterApplicabilityContext context)
    {
        return context.Tag == "CAKE_SOUL" || context.NbtKeys.Contains("captured_player");
    }
}
