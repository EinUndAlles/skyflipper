using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services;

/// <summary>
/// Service responsible for fetching Bazaar prices and calculating gemstone values.
/// Reference: GemPriceService.cs - Only gemstones are explicitly valued.
/// All other components (recombs, scrolls, HPB, etc.) are matched via NBT-based cache keys.
/// </summary>
public class ComponentValueService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ComponentValueService> _logger;
    private const string BAZAAR_CACHE_KEY = "BazaarPrices";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    // Gemstone types that can appear in items
    private static readonly string[] GemTypes = { "RUBY", "AMETHYST", "JASPER", "SAPPHIRE", "AMBER", "TOPAZ", "JADE", "OPAL" };

    public ComponentValueService(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        ILogger<ComponentValueService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Calculates the value of gemstones on an item.
    /// Reference: GemPriceService.cs GetGemstoneWorth()
    /// Only PERFECT and FLAWLESS gems add significant value.
    /// Applies fee deduction: -500k for Perfect, -100k for Flawless.
    /// </summary>
    public async Task<(long TotalValue, string Breakdown)> GetGemstoneValue(Auction auction)
    {
        var prices = await GetBazaarPrices();
        if (prices == null) return (0, string.Empty);

        if (auction.NBTLookups == null || !auction.NBTLookups.Any())
            return (0, string.Empty);

        long totalWorth = 0;
        var breakdownParts = new List<string>();
        
        // Find all gem slots with PERFECT or FLAWLESS quality
        var relevantGems = auction.NBTLookups
            .Where(n => n.ValueString == "PERFECT" || n.ValueString == "FLAWLESS")
            .ToList();

        foreach (var gem in relevantGems)
        {
            if (gem.NBTKey == null) continue;
            var keySlug = gem.NBTKey.KeyName.ToUpper();
            string? gemType = null;

            // Try to identify gem type directly from key name
            foreach (var type in GemTypes)
            {
                if (keySlug.Contains(type))
                {
                    gemType = type;
                    break;
                }
            }

            // Reference: GemPriceService.cs lines 128-130
            // Handle COMBAT/DEFENSIVE/UNIVERSAL slots - look up actual gem type
            if (gemType == null && (keySlug.StartsWith("COMBAT") || keySlug.StartsWith("DEFENSIVE") || keySlug.StartsWith("UNIVERSAL")))
            {
                var gemTypeKey = gem.NBTKey.KeyName + "_gem";
                var gemTypeEntry = auction.NBTLookups
                    .FirstOrDefault(n => n.NBTKey?.KeyName.Equals(gemTypeKey, StringComparison.OrdinalIgnoreCase) == true);
                
                if (gemTypeEntry?.ValueString != null)
                {
                    gemType = gemTypeEntry.ValueString.ToUpper();
                }
            }

            if (gemType == null) continue;

            var priceKey = $"{gem.ValueString}_{gemType}_GEM";

            if (prices.TryGetValue(priceKey, out long price))
            {
                var val = price;
                // Reference: GemPriceService.cs lines 63-66 - fee deduction
                if (gem.ValueString == "PERFECT") val -= 500_000;
                else if (gem.ValueString == "FLAWLESS") val -= 100_000;

                if (val > 0)
                {
                    totalWorth += val;
                    breakdownParts.Add($"{gem.ValueString} {gemType}: +{FormatPrice(val)}");
                }
            }
        }

        var breakdown = breakdownParts.Any() ? string.Join(", ", breakdownParts) : string.Empty;
        return (totalWorth, breakdown);
    }

    /// <summary>
    /// Alias for backward compatibility with existing code.
    /// </summary>
    public async Task<(long GemValue, string Breakdown)> GetGemstoneValueOnly(Auction auction)
    {
        return await GetGemstoneValue(auction);
    }

    /// <summary>
    /// Alias for backward compatibility - now only returns gem value.
    /// Other components are matched via NBT keys, not valued separately.
    /// </summary>
    public async Task<(long TotalValue, string Breakdown)> GetOrCalculateItemComponentValue(Auction auction)
    {
        return await GetGemstoneValue(auction);
    }

    private async Task<Dictionary<string, long>?> GetBazaarPrices()
    {
        return await _cache.GetOrCreateAsync(BAZAAR_CACHE_KEY, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            try
            {
                var client = _httpClientFactory.CreateClient();
                var response = await client.GetAsync("https://api.hypixel.net/skyblock/bazaar");
                response.EnsureSuccessStatusCode();
                
                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                var products = doc.RootElement.GetProperty("products");
                
                var result = new Dictionary<string, long>();
                
                // Load gem prices for FLAWLESS and PERFECT tiers
                foreach (var gemType in GemTypes)
                {
                    TryAddPrice(products, result, $"FLAWLESS_{gemType}_GEM");
                    TryAddPrice(products, result, $"PERFECT_{gemType}_GEM");
                }

                _logger.LogInformation("Loaded {Count} gem prices from Bazaar", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch bazaar prices");
                return null;
            }
        });
    }

    private void TryAddPrice(JsonElement products, Dictionary<string, long> result, string productId)
    {
        if (products.TryGetProperty(productId, out var productData))
        {
            if (productData.TryGetProperty("quick_status", out var status))
            {
                if (status.TryGetProperty("sellPrice", out var price))
                {
                    result[productId] = (long)price.GetDouble();
                }
            }
        }
    }

    private string FormatPrice(long price)
    {
        if (price >= 1_000_000) return $"{price / 1_000_000.0:F1}m";
        if (price >= 1_000) return $"{price / 1_000.0:F1}k";
        return price.ToString();
    }
}
