using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FlipsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<FlipsController> _logger;

    public FlipsController(AppDbContext context, ILogger<FlipsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get current flip opportunities.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<FlipOpportunityDto>>> GetFlips(
        [FromQuery] double? minProfit = 10.0,
        [FromQuery] int maxResults = 50)
    {
        var flips = await _context.FlipOpportunities
            .Where(f => f.ProfitMarginPercent >= minProfit)
            .OrderByDescending(f => f.ProfitMarginPercent)
            .Take(maxResults)
            .ToListAsync();

        // Join with auctions to get additional details like tier
        var auctionUuids = flips.Select(f => f.AuctionUuid).ToList();
        var auctions = await _context.Auctions
            .Where(a => auctionUuids.Contains(a.Uuid))
            .ToDictionaryAsync(a => a.Uuid);

        var result = flips.Select(f => new FlipOpportunityDto
        {
            AuctionUuid = f.AuctionUuid,
            ItemTag = f.ItemTag,
            ItemName = f.ItemName,
            CurrentPrice = f.CurrentPrice,
            MedianPrice = f.MedianPrice,
            EstimatedProfit = f.EstimatedProfit,
            ProfitMarginPercent = f.ProfitMarginPercent,
            DetectedAt = f.DetectedAt,
            AuctionEnd = f.AuctionEnd,
            Tier = auctions.TryGetValue(f.AuctionUuid, out var auction) 
                ? auction.Tier.ToString() 
                : "UNKNOWN",
            Texture = auctions.TryGetValue(f.AuctionUuid, out var auc) 
                ? auc.Texture 
                : null
        }).ToList();

        return Ok(result);
    }

    /// <summary>
    /// Get price history for a specific item.
    /// </summary>
    [HttpGet("history/{tag}")]
    public async Task<ActionResult<List<ItemPriceHistory>>> GetPriceHistory(
        string tag,
        [FromQuery] int days = 30)
    {
        var cutoffDate = DateTime.UtcNow.Date.AddDays(-days);
        
        var history = await _context.PriceHistory
            .Where(p => p.ItemTag == tag && p.Date >= cutoffDate)
            .OrderByDescending(p => p.Date)
            .ToListAsync();

        if (history.Count == 0)
        {
            return NotFound(new { message = $"No price history found for {tag}" });
        }

        return Ok(history);
    }

    /// <summary>
    /// Get statistics about current flips.
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<FlipStats>> GetFlipStats()
    {
        var flips = await _context.FlipOpportunities.ToListAsync();

        if (flips.Count == 0)
        {
            return Ok(new FlipStats
            {
                TotalFlips = 0,
                AverageProfitMargin = 0,
                TotalPotentialProfit = 0,
                TopCategories = new List<string>()
            });
        }

        var stats = new FlipStats
        {
            TotalFlips = flips.Count,
            AverageProfitMargin = flips.Average(f => f.ProfitMarginPercent),
            TotalPotentialProfit = flips.Sum(f => f.EstimatedProfit),
            TopCategories = flips
                .GroupBy(f => f.ItemTag)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => g.Key)
                .ToList()
        };

        return Ok(stats);
    }
}

public class FlipOpportunityDto
{
    public string AuctionUuid { get; set; } = string.Empty;
    public string ItemTag { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public long CurrentPrice { get; set; }
    public long MedianPrice { get; set; }
    public long EstimatedProfit { get; set; }
    public double ProfitMarginPercent { get; set; }
    public DateTime DetectedAt { get; set; }
    public DateTime AuctionEnd { get; set; }
    public string Tier { get; set; } = string.Empty;
    public string? Texture { get; set; }
}

public class FlipStats
{
    public int TotalFlips { get; set; }
    public double AverageProfitMargin { get; set; }
    public long TotalPotentialProfit { get; set; }
    public List<string> TopCategories { get; set; } = new();
}
