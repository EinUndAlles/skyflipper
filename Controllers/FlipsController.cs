using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;
using SkyFlipperSolo.Services;

namespace SkyFlipperSolo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FlipsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ReferenceAuctionService _referenceAuctionService;
    private readonly CacheKeyService _cacheKeyService;
    private readonly ILogger<FlipsController> _logger;

    public FlipsController(
        AppDbContext context,
        ReferenceAuctionService referenceAuctionService,
        CacheKeyService cacheKeyService,
        ILogger<FlipsController> logger)
    {
        _context = context;
        _referenceAuctionService = referenceAuctionService;
        _cacheKeyService = cacheKeyService;
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
            Seller = auctions.TryGetValue(f.AuctionUuid, out var sellerAuction)
                ? sellerAuction.AuctioneerId
                : null,
            Texture = auctions.TryGetValue(f.AuctionUuid, out var auc) 
                ? auc.Texture 
                : null,
            ValueBreakdown = f.ValueBreakdown,
            Volume = f.ReferenceCount
        }).ToList();

        return Ok(result);
    }

    /// <summary>
    /// Get price history for a specific item.
    /// </summary>
    [HttpGet("history/{tag}")]
    public async Task<ActionResult<PriceHistoryResponse>> GetPriceHistory(
        string tag,
        [FromQuery] int days = 30)
    {
        var cutoffDate = DateTime.UtcNow.Date.AddDays(-days);
        var upperTag = tag.ToUpperInvariant();

        var history = await _context.AveragePrices
            .Where(p => (p.ItemTag == upperTag || (p.ItemTag == string.Empty && p.CacheKey.StartsWith("o" + upperTag))) &&
                        p.Timestamp >= cutoffDate &&
                        p.Granularity == PriceGranularity.Daily)
            .GroupBy(p => p.Timestamp)
            .OrderBy(g => g.Key)
            .Select(g => new PriceHistoryPoint
            {
                Time = g.Key,
                Min = g.Min(x => x.Min),
                Max = g.Max(x => x.Max),
                Avg = g.Average(x => x.Avg),
                Volume = g.Sum(x => x.Volume)
            })
            .ToListAsync();

        return Ok(new PriceHistoryResponse
        {
            Filterable = true,
            Bazaar = false,
            Filters = Array.Empty<string>(),
            Prices = history
        });
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

    /// <summary>
    /// Debug reference selection for a given auction UUID.
    /// </summary>
    [HttpGet("debug/reference/{uuid}")]
    public async Task<ActionResult<ReferenceDebugResponse>> GetReferenceDebug(string uuid, CancellationToken stoppingToken)
    {
        var auction = await _context.Auctions
            .Include(a => a.Enchantments)
            .Include(a => a.NBTLookups)
                .ThenInclude(n => n.NBTValue)
            .Include(a => a.Bids)
            .FirstOrDefaultAsync(a => a.Uuid == uuid.Replace("-", ""), stoppingToken);

        if (auction == null)
            return NotFound();

        var debug = await _referenceAuctionService.DebugRelevantAuctionsAsync(auction, stoppingToken);

        var response = new ReferenceDebugResponse
        {
            AuctionUuid = auction.Uuid,
            CacheKey = _cacheKeyService.GeneratePriceCacheKey(auction),
            InitialCount = debug.InitialCount,
            ExpandedDayCount = debug.ExpandedDayCount,
            ExpandedWeekCount = debug.ExpandedWeekCount,
            ReducedCount = debug.ReducedCount,
            RecentReducedCount = debug.RecentReducedCount,
            AntiManipulationSourceCount = debug.AntiManipulationSourceCount,
            BeforeAntiManipulationCount = debug.BeforeAntiManipulationCount,
            AfterAntiManipulationCount = debug.AfterAntiManipulationCount,
            References = debug.References.Select(a => new ReferenceAuctionSummary
            {
                AuctionUuid = a.Uuid,
                ItemTag = a.Tag,
                ItemName = a.ItemName,
                Tier = a.Tier,
                Reforge = a.Reforge,
                Count = a.Count,
                Status = a.Status,
                SoldAt = a.SoldAt,
                End = a.End,
                Price = a.SoldPrice ?? a.HighestBidAmount,
                CacheKey = _cacheKeyService.GeneratePriceCacheKey(a)
            }).ToList()
        };

        return Ok(response);
    }

    /// <summary>
    /// Debug cache key for a given auction UUID.
    /// </summary>
    [HttpGet("debug/cache-key/{uuid}")]
    public async Task<ActionResult<CacheKeyDebugResponse>> GetCacheKeyDebug(string uuid, CancellationToken stoppingToken)
    {
        var auction = await _context.Auctions
            .Include(a => a.Enchantments)
            .Include(a => a.NBTLookups)
                .ThenInclude(n => n.NBTValue)
            .FirstOrDefaultAsync(a => a.Uuid == uuid.Replace("-", ""), stoppingToken);

        if (auction == null)
            return NotFound();

        var cacheKey = _cacheKeyService.GeneratePriceCacheKey(auction);
        var response = new CacheKeyDebugResponse
        {
            AuctionUuid = auction.Uuid,
            CacheKey = cacheKey,
            ItemTag = auction.Tag,
            ItemName = auction.ItemName,
            Tier = auction.Tier,
            Reforge = auction.Reforge
        };

        return Ok(response);
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
    public string? Seller { get; set; }
    public string? Texture { get; set; }
    public string ValueBreakdown { get; set; } = string.Empty;
    public int Volume { get; set; }

    // Compatibility aliases for consumers that prefer shorter/cofl-like field names.
    public string Uuid => AuctionUuid;
    public long Price => CurrentPrice;
    public long TargetPrice => MedianPrice;
    public long Profit => EstimatedProfit;
    public double ProfitPercent => ProfitMarginPercent;
    public DateTime End => AuctionEnd;
}

public class FlipStats
{
    public int TotalFlips { get; set; }
    public double AverageProfitMargin { get; set; }
    public long TotalPotentialProfit { get; set; }
    public List<string> TopCategories { get; set; } = new();
}

public class ReferenceDebugResponse
{
    public string AuctionUuid { get; set; } = string.Empty;
    public string CacheKey { get; set; } = string.Empty;
    public int InitialCount { get; set; }
    public int ExpandedDayCount { get; set; }
    public int ExpandedWeekCount { get; set; }
    public int ReducedCount { get; set; }
    public int RecentReducedCount { get; set; }
    public int AntiManipulationSourceCount { get; set; }
    public int BeforeAntiManipulationCount { get; set; }
    public int AfterAntiManipulationCount { get; set; }
    public List<ReferenceAuctionSummary> References { get; set; } = new();
}

public class ReferenceAuctionSummary
{
    public string AuctionUuid { get; set; } = string.Empty;
    public string ItemTag { get; set; } = string.Empty;
    public string? ItemName { get; set; }
    public Tier Tier { get; set; }
    public Reforge Reforge { get; set; }
    public int Count { get; set; }
    public AuctionStatus Status { get; set; }
    public DateTime? SoldAt { get; set; }
    public DateTime End { get; set; }
    public long Price { get; set; }
    public string CacheKey { get; set; } = string.Empty;
}

public class CacheKeyDebugResponse
{
    public string AuctionUuid { get; set; } = string.Empty;
    public string CacheKey { get; set; } = string.Empty;
    public string ItemTag { get; set; } = string.Empty;
    public string? ItemName { get; set; }
    public Tier Tier { get; set; }
    public Reforge Reforge { get; set; }
}
