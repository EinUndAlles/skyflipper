using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuctionsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<AuctionsController> _logger;

    public AuctionsController(AppDbContext context, ILogger<AuctionsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get statistics about collected auctions
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var totalAuctions = await _context.Auctions.CountAsync();
        var binAuctions = await _context.Auctions.CountAsync(a => a.Bin);
        var recentAuctions = await _context.Auctions.CountAsync(a => a.FetchedAt > DateTime.UtcNow.AddMinutes(-5));
        var uniqueTags = await _context.Auctions.Select(a => a.Tag).Distinct().CountAsync();
        
        var oldestFetch = await _context.Auctions.MinAsync(a => (DateTime?)a.FetchedAt);
        var newestFetch = await _context.Auctions.MaxAsync(a => (DateTime?)a.FetchedAt);

        return Ok(new
        {
            TotalAuctions = totalAuctions,
            BinAuctions = binAuctions,
            RecentAuctions = recentAuctions,
            UniqueItemTags = uniqueTags,
            OldestFetch = oldestFetch,
            NewestFetch = newestFetch,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get recent auctions (paginated)
    /// </summary>
    [HttpGet("recent")]
    public async Task<IActionResult> GetRecentAuctions([FromQuery] int limit = 50, [FromQuery] int offset = 0)
    {
        var auctions = await _context.Auctions
            .OrderByDescending(a => a.FetchedAt)
            .Skip(offset)
            .Take(limit)
            .Select(a => new
            {
                a.Uuid,
                a.ItemName,
                a.Tag,
                a.Tier,
                a.Reforge,
                Price = a.HighestBidAmount > 0 ? a.HighestBidAmount : a.StartingBid,
                a.Bin,
                a.End,
                a.FetchedAt,
                EnchantmentCount = a.Enchantments.Count,
                a.Texture
            })
            .ToListAsync();

        return Ok(auctions);
    }

    /// <summary>
    /// Admin endpoint to cleanup old ended auctions (mark as sold)
    /// </summary>
    [HttpPost("cleanup-old")]
    public async Task<IActionResult> CleanupOldAuctions()
    {
        var now = DateTime.UtcNow;
        var oldAuctions = await _context.Auctions
            .Where(a => a.End < now && a.Status == AuctionStatus.ACTIVE)
            .ToListAsync();

        if (oldAuctions.Count > 0)
        {
            foreach (var auction in oldAuctions)
            {
                auction.Status = AuctionStatus.EXPIRED;
            }
            
            await _context.SaveChangesAsync();
            return Ok(new { Message = "Cleanup complete", Count = oldAuctions.Count });
        }

        return Ok(new { Message = "No old auctions to cleanup", Count = 0 });
    }


    /// <summary>
    /// Get auctions by item tag
    /// </summary>
    [HttpGet("by-tag/{tag}")]
    public async Task<IActionResult> GetByTag(
        string tag, 
        [FromQuery] int limit = 200, 
        [FromQuery] string? filter = null,
        [FromQuery] bool binOnly = true,
        [FromQuery] bool showEnded = false)
    {
        var upperTag = tag.ToUpper();
        
        // Build base query
        var query = _context.Auctions.Where(a => a.Tag == upperTag);
        
        // If filter is provided and tag is PET, filter by name containing the filter
        if (!string.IsNullOrEmpty(filter) && (upperTag == "PET" || upperTag.StartsWith("PET_")))
        {
            query = query.Where(a => a.ItemName.Contains(filter));
        }
        
        // Filter by BIN only (default: true)
        if (binOnly)
        {
            query = query.Where(a => a.Bin);
        }
        
        // Filter by active auctions only (default: hide ended)
        if (!showEnded)
        {
            query = query.Where(a => a.End > DateTime.UtcNow);
        }
        
        // Always filter out sold/expired auctions (only show active)
        query = query.Where(a => a.Status == AuctionStatus.ACTIVE);
        
        var auctions = await query
            .OrderBy(a => a.HighestBidAmount > 0 ? a.HighestBidAmount : a.StartingBid) // Sort by price (cheapest first)
            .Take(limit)
            .Select(a => new
            {
                a.Uuid,
                a.ItemName,
                a.Tag,
                a.Tier,
                a.Reforge,
                Price = a.HighestBidAmount > 0 ? a.HighestBidAmount : a.StartingBid,
                a.Bin,
                a.End,
                a.Texture,
                a.FetchedAt
            })
            .ToListAsync();

        return Ok(auctions);
    }

    /// <summary>
    /// Get a single auction by UUID
    /// </summary>
    [HttpGet("{uuid}")]
    public async Task<IActionResult> GetAuction(string uuid)
    {
        var auction = await _context.Auctions
            .Include(a => a.Enchantments)
            .FirstOrDefaultAsync(a => a.Uuid == uuid.Replace("-", ""));

        if (auction == null)
            return NotFound();

        return Ok(auction);
    }

    /// <summary>
    /// Get top item tags by count
    /// </summary>
    [HttpGet("top-tags")]
    public async Task<IActionResult> GetTopTags([FromQuery] int limit = 20)
    {
        var tags = await _context.Auctions
            .Where(a => !string.IsNullOrEmpty(a.Tag))
            .GroupBy(a => a.Tag)
            .Select(g => new { Tag = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(limit)
            .ToListAsync();

        return Ok(tags);
    }

    /// <summary>
    /// Search items by name or tag (autocomplete)
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> SearchItems([FromQuery] string query, [FromQuery] int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Ok(new List<object>());

        query = query.ToUpper();

        // 1. Fetch a larger pool of potential matches to filter/group in memory
        var rawMatches = await _context.Auctions
            .Where(a => a.ItemName.ToUpper().Contains(query) || a.Tag.Contains(query))
            .OrderByDescending(a => a.End) // Get recent ones first
            .Take(100) // Fetch enough to cover duplicates/variations
            .Select(a => new
            {
                a.ItemName,
                a.Tag,
                a.Tier,
                a.Texture,
                a.Reforge,
                EnchantmentCount = a.Enchantments.Count,
                a.End
            })
            .ToListAsync();

        var reforgeNames = Enum.GetNames(typeof(Reforge));

        // 2. Process and Group
        var groupedItems = rawMatches
            .Select(item => 
            {
                var cleanName = item.ItemName;
                
                // --- Pet Cleaning ---
                // Tag is "PET" (generic) or starts with "PET_" (specific skins/types)
                if (item.Tag == "PET" || item.Tag.StartsWith("PET_"))
                {
                    // Remove [Lvl 123] prefix
                    cleanName = System.Text.RegularExpressions.Regex.Replace(cleanName, @"^\[Lvl \d+\]\s+", "");
                    // Remove (Rarity) suffix
                    cleanName = System.Text.RegularExpressions.Regex.Replace(cleanName, @"\s\(\w+\)$", "");
                }
                // --- Reforge Cleaning ---
                else 
                {
                    // Check if name starts with any known reforge
                    foreach (var reforgeName in reforgeNames)
                    {
                        if (reforgeName == "None") continue;

                        if (cleanName.StartsWith(reforgeName + " ", StringComparison.OrdinalIgnoreCase))
                        {
                            // SAFETY CHECK: Does the Tag contain the reforge name?
                            // e.g. "Strong Dragon Chestplate" -> Tag "STRONG_DRAGON_..." -> Startswith "Strong "
                            // We do NOT want to strip "Strong" because it's part of the item identity (Tag).
                            // But "Heroic Aspect of the End" -> Tag "ASPECT_OF_THE_END" -> Strip "Heroic".
                            
                            if (!item.Tag.Contains(reforgeName.ToUpper()))
                            {
                                cleanName = cleanName.Substring(reforgeName.Length + 1);
                                break; // Only remove one prefix
                            }
                        }
                    }
                }

                return new { CleanName = cleanName, Original = item };
            })
            .GroupBy(x => x.CleanName)
            .Select(g => 
            {
                // Pick the "best" representative for this CleanName
                var best = g.OrderByDescending(x => x.Original.Reforge == Reforge.None) // Prefer None
                            .ThenBy(x => x.Original.EnchantmentCount == 0)              // Prefer Clean
                            .ThenByDescending(x => x.Original.End)                      // Prefer Newest
                            .First()
                            .Original;
                
                var isPet = best.Tag == "PET" || best.Tag.StartsWith("PET_");
                var displayName = isPet ? g.Key + " Pet" : g.Key;
                
                return new 
                {
                    ItemName = displayName,
                    Tag = best.Tag,
                    best.Tier,
                    best.Texture,
                    // Include filter for pets (cleaned name without "Pet" suffix) so frontend can filter by name
                    Filter = isPet ? g.Key : ""
                };
            })
            // Sort by relevance: Prioritize items whose CleanName starts with or contains the query
            .OrderByDescending(x => x.ItemName.ToUpper() == query) // Exact match first
            .ThenByDescending(x => x.ItemName.ToUpper().StartsWith(query)) // Starts with
            .ThenByDescending(x => x.ItemName.ToUpper().Contains(query)) // Contains
            .Take(limit)
            .ToList();

        return Ok(groupedItems);
    }
}
