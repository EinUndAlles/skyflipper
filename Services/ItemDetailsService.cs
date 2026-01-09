using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

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
                DisplayName = itemName,
                Description = lore,
                FallbackTier = tier,
                FallbackCategory = category,
                LastSeen = DateTime.UtcNow
            };
            context.ItemDetails.Add(details);
            await context.SaveChangesAsync();

            _logger.LogInformation("Created item details for {Tag}: {Name}", tag, itemName);
        }
        else
        {
            // Update last seen
            details.LastSeen = DateTime.UtcNow;
            
            // Update display name if changed
            if (!string.IsNullOrEmpty(itemName) && details.DisplayName != itemName)
                details.DisplayName = itemName;

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
            .Take(50)
            .ToListAsync();
    }
}
