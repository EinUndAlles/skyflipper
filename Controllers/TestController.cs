using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Services;

namespace SkyFlipperSolo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ReferenceAuctionService _referenceAuctionService;
    private readonly CacheKeyService _cacheKeyService;

    public TestController(
        AppDbContext context,
        ReferenceAuctionService referenceAuctionService,
        CacheKeyService cacheKeyService)
    {
        _context = context;
        _referenceAuctionService = referenceAuctionService;
        _cacheKeyService = cacheKeyService;
    }

    [HttpGet("db-status")]
    public async Task<ActionResult> GetDatabaseStatus()
    {
        var results = new
        {
            // Count records in each table
            TotalAuctions = await _context.Auctions.CountAsync(),
            ActiveAuctions = await _context.Auctions.CountAsync(a => a.Status == Models.AuctionStatus.ACTIVE),
            SoldAuctions = await _context.Auctions.CountAsync(a => a.Status == Models.AuctionStatus.SOLD),
            ExpiredAuctions = await _context.Auctions.CountAsync(a => a.Status == Models.AuctionStatus.EXPIRED),
            
            // Check enchantments
            TotalEnchantments = await _context.Enchantments.CountAsync(),
            AuctionsWithEnchantments = await _context.Enchantments
                .Select(e => e.AuctionId)
                .Distinct()
                .CountAsync(),
            
            // Check price history
            PriceHistoryRecords = await _context.AveragePrices.CountAsync(),
            
            // Check flips
            DetectedFlips = await _context.FlipOpportunities.CountAsync(),
            
            // Check sold prices
            SoldAuctionsWithPrice = await _context.Auctions
                .CountAsync(a => a.Status == Models.AuctionStatus.SOLD && a.SoldPrice != null),
            
            // Sample data
            SampleEnchantments = await _context.Enchantments
                .Include(e => e.Auction)
                .OrderByDescending(e => e.Id)
                .Take(5)
                .Select(e => new 
                {
                    e.Type,
                    e.Level,
                    ItemName = e.Auction != null ? e.Auction.ItemName : "N/A"
                })
                .ToListAsync(),
            
            // BIN auctions available for flip detection
            ActiveBINAuctions = await _context.Auctions
                .CountAsync(a => a.Bin && a.Status == Models.AuctionStatus.ACTIVE && a.End > DateTime.UtcNow)
        };

        return Ok(results);
    }

    [HttpGet("sample-auctions")]
    public async Task<ActionResult> GetSampleAuctions()
    {
        var samples = await _context.Auctions
            .Include(a => a.Enchantments)
            .OrderByDescending(a => a.FetchedAt)
            .Take(5)
            .Select(a => new
            {
                a.Uuid,
                a.ItemName,
                a.Tag,
                a.Status,
                a.SoldPrice,
                EnchantmentCount = a.Enchantments.Count,
                Enchantments = a.Enchantments.Select(e => new { e.Type, e.Level }).ToList()
            })
            .ToListAsync();

        return Ok(samples);
    }

    [HttpGet("reference-eval/{uuid}")]
    public async Task<ActionResult> EvaluateReferenceAuction(
        string uuid,
        [FromQuery] int hitCount = 0)
    {
        var normalizedUuid = uuid.Replace("-", "", StringComparison.Ordinal);

        var auction = await _context.Auctions
            .Include(a => a.Enchantments)
            .Include(a => a.Bids)
            .Include(a => a.NBTLookups)
                .ThenInclude(n => n.NBTKey)
            .Include(a => a.NBTLookups)
                .ThenInclude(n => n.NBTValue)
            .FirstOrDefaultAsync(a => a.Uuid == normalizedUuid);

        if (auction == null)
            return NotFound(new { Message = $"Auction {uuid} not found" });

        var debug = await _referenceAuctionService.DebugRelevantAuctionsAsync(auction, HttpContext.RequestAborted);
        var valuation = auction.Bin
            ? await _referenceAuctionService.EvaluateBinAuctionAsync(auction, hitCount, HttpContext.RequestAborted)
            : await _referenceAuctionService.EvaluateBidAuctionAsync(auction, hitCount, HttpContext.RequestAborted);

        return Ok(new
        {
            Auction = new
            {
                auction.Uuid,
                auction.ItemName,
                auction.Tag,
                auction.Tier,
                auction.Bin,
                auction.Status,
                auction.Count,
                auction.StartingBid,
                auction.HighestBidAmount,
                auction.End,
                CacheKey = _cacheKeyService.GeneratePriceCacheKey(auction)
            },
            ReferenceDebug = new
            {
                debug.InitialCount,
                debug.ExpandedDayCount,
                debug.ExpandedWeekCount,
                debug.ReducedCount,
                debug.RecentReducedCount,
                debug.AntiManipulationSourceCount,
                debug.BeforeAntiManipulationCount,
                debug.AfterAntiManipulationCount,
                SampleReferences = debug.References
                    .Take(10)
                    .Select(a => new
                    {
                        a.Uuid,
                        a.ItemName,
                        a.Tag,
                        a.Bin,
                        a.HighestBidAmount,
                        a.End
                    })
            },
            Valuation = valuation == null
                ? null
                : new
                {
                    valuation.TargetPrice,
                    valuation.EstimatedProfit,
                    valuation.ProfitMarginPercent,
                    valuation.DataSource,
                    valuation.ValueBreakdown,
                    valuation.ReferenceCount,
                    valuation.OldestReference
                }
        });
    }

    [HttpGet("reference-scan")]
    public async Task<ActionResult> ScanReferenceValuations(
        [FromQuery] string? tag = null,
        [FromQuery] int limit = 20,
        [FromQuery] bool binOnly = true)
    {
        var now = DateTime.UtcNow;
        var query = _context.Auctions
            .AsNoTracking()
            .Where(a => a.Status == Models.AuctionStatus.ACTIVE && a.End > now);

        if (binOnly)
            query = query.Where(a => a.Bin);

        if (!string.IsNullOrWhiteSpace(tag))
        {
            var normalizedTag = tag.ToUpperInvariant();
            query = query.Where(a => a.Tag == normalizedTag);
        }

        var auctions = await query
            .OrderByDescending(a => a.FetchedAt)
            .Take(Math.Clamp(limit, 1, 100))
            .Include(a => a.Enchantments)
            .Include(a => a.Bids)
            .Include(a => a.NBTLookups)
                .ThenInclude(n => n.NBTKey)
            .Include(a => a.NBTLookups)
                .ThenInclude(n => n.NBTValue)
            .ToListAsync();

        var results = new List<object>();
        foreach (var auction in auctions)
        {
            var debug = await _referenceAuctionService.DebugRelevantAuctionsAsync(auction, HttpContext.RequestAborted);
            var valuation = auction.Bin
                ? await _referenceAuctionService.EvaluateBinAuctionAsync(auction, 0, HttpContext.RequestAborted)
                : await _referenceAuctionService.EvaluateBidAuctionAsync(auction, 0, HttpContext.RequestAborted);

            results.Add(new
            {
                auction.Uuid,
                auction.ItemName,
                auction.Tag,
                auction.Bin,
                Price = auction.HighestBidAmount > 0 ? auction.HighestBidAmount : auction.StartingBid,
                CacheKey = _cacheKeyService.GeneratePriceCacheKey(auction),
                ReferencesAfterAntiManipulation = debug.AfterAntiManipulationCount,
                valuation?.TargetPrice,
                valuation?.EstimatedProfit,
                valuation?.ProfitMarginPercent,
                valuation?.DataSource
            });
        }

        return Ok(results);
    }
}
