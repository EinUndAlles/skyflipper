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
    /// Get auctions by item tag
    /// </summary>
    [HttpGet("by-tag/{tag}")]
    public async Task<IActionResult> GetByTag(string tag, [FromQuery] int limit = 50)
    {
        var auctions = await _context.Auctions
            .Where(a => a.Tag == tag.ToUpper())
            .OrderByDescending(a => a.End)
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
}
