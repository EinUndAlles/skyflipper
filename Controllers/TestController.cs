using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;

namespace SkyFlipperSolo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly AppDbContext _context;

    public TestController(AppDbContext context)
    {
        _context = context;
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
            PriceHistoryRecords = await _context.PriceHistory.CountAsync(),
            
            // Check flips
            DetectedFlips = await _context.FlipOpportunities.CountAsync(),
            
            // Check sold prices
            SoldAuctionsWithPrice = await _context.Auctions
                .CountAsync(a => a.Status == Models.AuctionStatus.SOLD && a.SoldPrice != null),
            
            // Sample data
            SampleEnchantments = await _context.Enchantments
                .Include(e => e.Auction)
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
}
