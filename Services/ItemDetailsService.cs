using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;
using System.Text.RegularExpressions;

namespace SkyFlipperSolo.Services;

/// <summary>
/// Service for managing item metadata beyond NBT data.
/// Tracks alternative names, descriptions, icons, and fallback tier/category.
/// Based on Coflnet.Sky.Items.ItemDetailsExtractor pattern.
/// </summary>
public class ItemDetailsService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ItemDetailsService> _logger;
    private readonly Dictionary<string, ItemDetails> _cache = new();
    private readonly object _lock = new();

    public ItemDetailsService(IServiceScopeFactory scopeFactory, ILogger<ItemDetailsService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Gets or creates item details for a tag.
    /// Updates last seen timestamp.
    /// </summary>
    public async Task<ItemDetails> GetOrCreateItemDetails(string tag, string itemName, Tier tier, Category category, string? lore = null)
    {
        if (string.IsNullOrEmpty(tag))
            throw new ArgumentException("Tag cannot be null or empty", nameof(tag));

        // Clean the item name for display (remove levels from runes/potions, etc.)
        var cleanedName = CleanItemNameForDisplay(tag, itemName);

        // Check cache
        lock (_lock)
        {
            if (_cache.TryGetValue(tag, out var cached))
                return cached;
        }

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var details = await context.ItemDetails.FirstOrDefaultAsync(d => d.Tag == tag);

        if (details == null)
        {
            // Create new item details
            details = new ItemDetails
            {
                Tag = tag,
                DisplayName = cleanedName,
                Description = lore,
                FallbackTier = tier,
                FallbackCategory = category,
                LastSeen = DateTime.UtcNow
            };
            context.ItemDetails.Add(details);
            await context.SaveChangesAsync();

            _logger.LogInformation("Created item details for {Tag}: {Name}", tag, cleanedName);
        }
        else
        {
            // Update last seen
            details.LastSeen = DateTime.UtcNow;

            // Update display name if changed
            if (!string.IsNullOrEmpty(cleanedName) && details.DisplayName != cleanedName)
                details.DisplayName = cleanedName;

            await context.SaveChangesAsync();
        }

        // Cache it
        lock (_lock)
        {
            _cache[tag] = details;
        }

        return details;
    }

    /// <summary>
    /// Cleans item names for display by removing level indicators, stars, etc.
    /// Mirrors the cleaning logic from AuctionsController.SearchItems
    /// </summary>
    private static string CleanItemNameForDisplay(string tag, string itemName)
    {
        var cleanName = itemName;

        // Remove Stars & Master Stars (✪, ➊, ➋, etc.)
        cleanName = System.Text.RegularExpressions.Regex.Replace(cleanName, @"[✪✫⚚➊➋➌➍➎➏➐➑➒]+", "").Trim();

        // Pet cleaning (though pets should have composite tags)
        if (tag == "PET" || tag.StartsWith("PET_"))
        {
            // Remove [Lvl 123] prefix
            cleanName = System.Text.RegularExpressions.Regex.Replace(cleanName, @"^\[Lvl \d+\]\s+", "");
            // Remove (Rarity) suffix
            cleanName = System.Text.RegularExpressions.Regex.Replace(cleanName, @"\s\(\w+\)$", "");
        }
        // Potion cleaning
        else if (tag.StartsWith("POTION_"))
        {
            // Remove Roman numeral levels: "Speed V Potion" → "Speed Potion"
            cleanName = System.Text.RegularExpressions.Regex.Replace(
                cleanName,
                @"\s+(X{0,1}(?:IX|IV|V?I{0,3}))\s+(?=Potion|Splash)",
                " ",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            cleanName = cleanName.Trim();
        }
        // Rune cleaning
        else if (tag.Contains("RUNE_"))
        {
            // Remove level indicators: "Blood Rune III COMMON" → "Blood Rune COMMON"
            cleanName = System.Text.RegularExpressions.Regex.Replace(
                cleanName,
                @"\s+(X{0,1}(?:IX|IV|V?I{0,3}))(\s|$)",
                "$2",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            cleanName = cleanName.Trim();
        }
        // Reforge cleaning (same logic as AuctionsController)
        else
        {
            var reforgeNames = Enum.GetNames(typeof(SkyFlipperSolo.Models.Reforge));
            foreach (var reforgeName in reforgeNames)
            {
                if (reforgeName == "None") continue;

                var hasReforgeSpace = cleanName.StartsWith(reforgeName + " ", StringComparison.OrdinalIgnoreCase);
                var hasReforgeApostrophe = cleanName.StartsWith(reforgeName + "'s ", StringComparison.OrdinalIgnoreCase);

                if (hasReforgeSpace || hasReforgeApostrophe)
                {
                    if (!tag.Contains(reforgeName.ToUpper()))
                    {
                        if (hasReforgeSpace)
                            cleanName = cleanName.Substring(reforgeName.Length + 1);
                        else // hasReforgeApostrophe
                            cleanName = cleanName.Substring(reforgeName.Length + 3);
                        break;
                    }
                }
            }
        }

        return cleanName;
    }

    /// <summary>
    /// Adds an alternative name for an item (e.g., "AOTE" for "Aspect of the End").
    /// </summary>
    public async Task AddAlternativeName(string tag, string altName)
    {
        if (string.IsNullOrEmpty(tag) || string.IsNullOrEmpty(altName))
            return;

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var details = await context.ItemDetails.FirstOrDefaultAsync(d => d.Tag == tag);
        if (details == null) return;

        // Check if alternative name already exists
        var exists = await context.AlternativeNames
            .AnyAsync(a => a.ItemDetailsId == details.Id && a.Name == altName);

        if (!exists)
        {
            context.AlternativeNames.Add(new AlternativeName
            {
                ItemDetailsId = details.Id,
                Name = altName
            });
            await context.SaveChangesAsync();
            _logger.LogInformation("Added alternative name '{Alt}' for {Tag}", altName, tag);
        }
    }

    /// <summary>
    /// Searches for items by name or alternative name.
    /// </summary>
    public async Task<List<ItemDetails>> SearchItems(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<ItemDetails>();

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        query = query.ToUpper();

        return await context.ItemDetails
            .Where(d => d.Tag.Contains(query) || 
                       d.DisplayName!.Contains(query) ||
                       d.AlternativeNames.Any(a => a.Name.Contains(query)))
            .OrderByDescending(d => d.LastSeen)
            .ThenBy(d => d.Tag)
            .Take(50)
            .ToListAsync();
    }
}
