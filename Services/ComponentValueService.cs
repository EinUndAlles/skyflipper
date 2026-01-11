using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services;

/// <summary>
/// Service responsible for fetching Bazaar prices and calculating the value of item components
/// (Gemstones, Scrolls, Recombs, etc.).
/// </summary>
public class ComponentValueService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ComponentValueService> _logger;
    private const string BAZAAR_CACHE_KEY = "BazaarPrices";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    // Manual mapping of internal names to Bazaar product IDs
    private static readonly Dictionary<string, string> ComponentMappings = new()
    {
        { "RECOMBOBULATOR_3000", "RECOMBOBULATOR_3000" },
        { "HOT_POTATO_BOOK", "HOT_POTATO_BOOK" },
        { "FUMING_POTATO_BOOK", "FUMING_POTATO_BOOK" },
        { "ART_OF_WAR", "THE_ART_OF_WAR" },
        { "FARMING_FOR_DUMMIES", "FARMING_FOR_DUMMIES" },
        // Gemstones (fine, flawless, perfect)
        { "RUBY", "RUBY" },
        { "AMETHYST", "AMETHYST" },
        { "JASPER", "JASPER" },
        { "SAPPHIRE", "SAPPHIRE" },
        { "AMBER", "AMBER" },
        { "TOPAZ", "TOPAZ" },
        { "JADE", "JADE" },
        { "OPAL", "OPAL" },
        // Scrolls (Necron Blade)
        { "WITHER_SHIELD_SCROLL", "WITHER_SHIELD_SCROLL" },
        { "IMPLOSION_SCROLL", "IMPLOSION_SCROLL" },
        { "SHADOW_WARP_SCROLL", "SHADOW_WARP_SCROLL" }
    };

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
    /// Calculates the value of significant components on an item.
    /// </summary>
    public async Task<(long TotalValue, string Breakdown)> GetOrCalculateItemComponentValue(Auction auction)
    {
        var prices = await GetBazaarPrices();
        if (prices == null) return (0, string.Empty);

        long totalValue = 0;
        var breakdownParts = new List<string>();

        // 1. Check for Recombobulater
        if (IsRecombobulated(auction))
        {
            if (prices.TryGetValue("RECOMBOBULATOR_3000", out var recombPrice))
            {
                totalValue += recombPrice;
                breakdownParts.Add($"Recomb: +{FormatPrice(recombPrice)}");
            }
        }

        // 2. Check for Gemstones
        if (auction.NBTLookups != null)
        {
            var gemValue = CalculateGemstoneValue(auction, prices);
            if (gemValue > 0)
            {
                totalValue += gemValue;
                breakdownParts.Add($"Gems: +{FormatPrice(gemValue)}");
            }
        }

        // 3. Check for Scrolls (Necron Blade)
        var scrollValue = await CalculateScrollValue(auction, prices);
        if (scrollValue > 0)
        {
            totalValue += scrollValue;
            breakdownParts.Add($"Scrolls: +{FormatPrice(scrollValue)}");
        }
        
        // 4. Art of War
        var artOfWar = auction.NBTLookups?.FirstOrDefault(n => n.NBTKey?.KeyName == "art_of_war_count");
        if (artOfWar?.ValueNumeric.HasValue == true && artOfWar.ValueNumeric.Value > 0)
        {
             if (prices.TryGetValue("THE_ART_OF_WAR", out var aowPrice))
             {
                 var val = aowPrice * (long)artOfWar.ValueNumeric.Value;
                 totalValue += val;
                 breakdownParts.Add($"AoW: +{FormatPrice(val)}");
             }
        }

        return (totalValue, string.Join(", ", breakdownParts));
    }

    /// <summary>
    /// Gets ONLY the gemstone value for an item.
    /// Used by PriceAggregationService to subtract gem value from sale prices.
    /// This matches the reference project's approach in FlippingEngine.cs line 340.
    /// </summary>
    public async Task<(long GemValue, string Breakdown)> GetGemstoneValueOnly(Auction auction)
    {
        var prices = await GetBazaarPrices();
        if (prices == null) return (0, string.Empty);

        if (auction.NBTLookups == null || !auction.NBTLookups.Any())
            return (0, string.Empty);

        var gemValue = CalculateGemstoneValue(auction, prices);
        return (gemValue, gemValue > 0 ? $"Gems: {FormatPrice(gemValue)}" : string.Empty);
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
                
                foreach (var mapping in ComponentMappings)
                {
                    TryAddPrice(products, result, mapping.Value);
                    TryAddPrice(products, result, "FINE_" + mapping.Value + "_GEM");
                    TryAddPrice(products, result, "FLAWLESS_" + mapping.Value + "_GEM");
                    TryAddPrice(products, result, "PERFECT_" + mapping.Value + "_GEM");
                }

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

    /// <summary>
    /// Gets the value of gemstones on an item, strictly matching reference project logic.
    /// Only values PERFECT and FLAWLESS gems.
    /// Applies fee deduction: -500k for Perfect, -100k for Flawless.
    /// Handles COMBAT/DEFENSIVE/UNIVERSAL slots by looking up the actual gem type.
    /// </summary>
    private long CalculateGemstoneValue(Auction auction, Dictionary<string, long> prices)
    {
        if (auction.NBTLookups == null || !auction.NBTLookups.Any())
            return 0;

        long totalWorth = 0;
        
        // Find all gem slots with PERFECT or FLAWLESS quality
        var relevantGems = auction.NBTLookups
            .Where(n => n.ValueString == "PERFECT" || n.ValueString == "FLAWLESS")
            .ToList();

        foreach (var gem in relevantGems)
        {
            if (gem.NBTKey == null) continue;
            var keySlug = gem.NBTKey.KeyName.ToUpper();
            string gemType = "";

            // First, try to identify gem type directly from key name
            if (keySlug.Contains("AMBER")) gemType = "AMBER";
            else if (keySlug.Contains("TOPAZ")) gemType = "TOPAZ";
            else if (keySlug.Contains("JADE")) gemType = "JADE";
            else if (keySlug.Contains("SAPPHIRE")) gemType = "SAPPHIRE";
            else if (keySlug.Contains("AMETHYST")) gemType = "AMETHYST";
            else if (keySlug.Contains("JASPER")) gemType = "JASPER";
            else if (keySlug.Contains("RUBY")) gemType = "RUBY";
            else if (keySlug.Contains("OPAL")) gemType = "OPAL";
            // Reference project: Handle COMBAT/DEFENSIVE/UNIVERSAL slots
            // These slots store the gem type in a separate NBT key: "{slotName}_gem"
            else if (keySlug.StartsWith("COMBAT") || keySlug.StartsWith("DEFENSIVE") || keySlug.StartsWith("UNIVERSAL"))
            {
                // Look for the companion key that specifies the actual gem type
                // e.g., "COMBAT_0" quality is PERFECT, "COMBAT_0_gem" = "JASPER"
                var gemTypeKey = gem.NBTKey.KeyName + "_gem";
                var gemTypeEntry = auction.NBTLookups
                    .FirstOrDefault(n => n.NBTKey?.KeyName.Equals(gemTypeKey, StringComparison.OrdinalIgnoreCase) == true);
                
                if (gemTypeEntry?.ValueString != null)
                {
                    gemType = gemTypeEntry.ValueString.ToUpper();
                }
                else
                {
                    continue; // Can't determine gem type, skip
                }
            }
            else 
            {
                continue; // Unknown gem slot
            }

            var priceKey = $"{gem.ValueString}_{gemType}_GEM"; 

            if (prices.TryGetValue(priceKey, out long price))
            {
                var val = price;
                // Reference project fee deduction (GemPriceService.cs lines 63-66)
                if (gem.ValueString == "PERFECT") val -= 500_000;
                else if (gem.ValueString == "FLAWLESS") val -= 100_000;

                if (val > 0)
                {
                    totalWorth += val;
                }
            }
        }

        return totalWorth;
    }

    private async Task<long> CalculateScrollValue(Auction auction, Dictionary<string, long> prices)
    {
        long value = 0;
        if (auction.NBTLookups == null) return 0;
        
        var scrolls = auction.NBTLookups
            .Where(n => n.NBTKey?.KeyName == "ability_scroll")
            .Select(n => n.ValueString)
            .ToList();

        foreach (var scroll in scrolls)
        {
            if (scroll != null && prices.TryGetValue(scroll, out var price))
            {
                value += price;
            }
        }
        return value;
    }

    private bool IsRecombobulated(Auction auction)
    {
        return auction.NBTLookups?.Any(n => n.NBTKey?.KeyName == "rarity_upgrades" && n.ValueNumeric > 0) ?? false;
    }

    private string FormatPrice(long price)
    {
        if (price >= 1_000_000) return $"{price / 1_000_000.0:F1}m";
        if (price >= 1_000) return $"{price / 1_000.0:F1}k";
        return price.ToString();
    }
}
