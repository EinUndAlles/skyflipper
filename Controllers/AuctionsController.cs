using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;
using SkyFlipperSolo.Services;
using System.Net.Http;
using System.Text.Json;
using System.Web;

namespace SkyFlipperSolo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuctionsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<AuctionsController> _logger;
    private readonly PropertiesSelectorService _propertiesSelector;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public AuctionsController(
        AppDbContext context,
        ILogger<AuctionsController> logger,
        PropertiesSelectorService propertiesSelector,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _propertiesSelector = propertiesSelector;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
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
    /// Get auctions by item tag with optional NBT filtering
    /// </summary>
    [HttpGet("by-tag/{tag}")]
    public async Task<IActionResult> GetByTag(
        string tag, 
        [FromQuery] int limit = 200, 
        [FromQuery] string? filter = null,
        [FromQuery] bool binOnly = true,
        [FromQuery] bool showEnded = false,
        [FromQuery] int? minStars = null,
        [FromQuery] int? maxStars = null,
        [FromQuery] string? enchantment = null,
        [FromQuery] int? minEnchantLevel = null,
        [FromQuery] long? minPrice = null,
        [FromQuery] long? maxPrice = null)
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
        
        // --- NBT-based filtering ---
        
        // Filter by star level (dungeon item level)
        if (minStars.HasValue || maxStars.HasValue)
        {
            var starKeyId = await _context.NBTKeys
                .Where(k => k.KeyName == "upgrade_level" || k.KeyName == "dungeon_item_level")
                .Select(k => k.Id)
                .FirstOrDefaultAsync();

            if (starKeyId > 0)
            {
                var auctionIdsWithStars = await _context.NBTLookups
                    .Include(nbt => nbt.Auction)
                    .Where(nbt => nbt.KeyId == starKeyId)
                    .Where(nbt => !minStars.HasValue || (nbt.ValueNumeric.HasValue && nbt.ValueNumeric.Value >= minStars.Value))
                    .Where(nbt => !maxStars.HasValue || (nbt.ValueNumeric.HasValue && nbt.ValueNumeric.Value <= maxStars.Value))
                    .Select(nbt => nbt.Auction!.Uuid)
                    .ToListAsync();

                query = query.Where(a => auctionIdsWithStars.Contains(a.Uuid));
            }
        }
        
        // Filter by enchantment
        if (!string.IsNullOrEmpty(enchantment))
        {
            // Parse the enchantment string to enum
            if (Enum.TryParse<EnchantmentType>(enchantment.ToLower(), true, out var enchantType))
            {
                var enchantQuery = _context.Enchantments
                    .Where(e => e.Type == enchantType);
                
                if (minEnchantLevel.HasValue)
                {
                    enchantQuery = enchantQuery.Where(e => e.Level >= minEnchantLevel.Value);
                }
                
                var auctionIdsWithEnchant = await enchantQuery
                    .Select(e => e.Auction!.Uuid)
                    .Distinct()
                    .ToListAsync();
                
                query = query.Where(a => auctionIdsWithEnchant.Contains(a.Uuid));
            }
        }
        
        // Filter by price range
        if (minPrice.HasValue)
        {
            query = query.Where(a => (a.HighestBidAmount > 0 ? a.HighestBidAmount : a.StartingBid) >= minPrice.Value);
        }
        if (maxPrice.HasValue)
        {
            query = query.Where(a => (a.HighestBidAmount > 0 ? a.HighestBidAmount : a.StartingBid) <= maxPrice.Value);
        }
        
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
            .Include(a => a.NBTLookups)
                .ThenInclude(nbt => nbt.NBTKey)
            .Include(a => a.NBTLookups)
                .ThenInclude(nbt => nbt.NBTValue)
            .Include(a => a.Bids)
            .FirstOrDefaultAsync(a => a.Uuid == uuid.Replace("-", ""));

        if (auction == null)
            return NotFound();

        // Get formatted properties for display
        List<ItemProperty> properties;
        try
        {
            properties = _propertiesSelector.GetProperties(auction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating properties for auction {Uuid}", auction.Uuid);
            properties = new List<ItemProperty>();
        }

        // Create enhanced response with formatted properties
        var response = new AuctionDetailResponse
        {
            // Original auction data
            Id = auction.Id,
            Uuid = auction.Uuid,
            UId = auction.UId,
            Tag = auction.Tag,
            ItemName = auction.ItemName,
            Count = auction.Count,
            StartingBid = auction.StartingBid,
            HighestBidAmount = auction.HighestBidAmount,
            Bin = auction.Bin,
            Start = auction.Start,
            End = auction.End,
            AuctioneerId = auction.AuctioneerId,
            Tier = auction.Tier,
            Category = auction.Category,
            Reforge = auction.Reforge,
            AnvilUses = auction.AnvilUses,
            ItemCreatedAt = auction.ItemCreatedAt,
            FetchedAt = auction.FetchedAt,
            Status = auction.Status,
            SoldPrice = auction.SoldPrice,
            SoldAt = auction.SoldAt,
            Texture = auction.Texture,
            ItemUid = auction.ItemUid,

            // Formatted properties for display
            Properties = properties,

            // Raw data for advanced users
            Enchantments = auction.Enchantments,
            NbtLookups = auction.NBTLookups?.Select(n => new
            {
                Key = n.NBTKey?.KeyName ?? n.Key,
                ValueNumeric = n.ValueNumeric,
                ValueString = n.ValueString,
                Value = n.NBTValue?.Value
            }).ToArray(),
            Bids = auction.Bids
        };

        return Ok(response);
    }

    /// <summary>
    /// Get filter options for a specific item tag
    /// </summary>
    [HttpGet("filters/{tag}")]
    public async Task<IActionResult> GetFiltersByTag(string tag)
    {
        var upperTag = tag.ToUpper();

        // Return common filters that apply to most items
        var commonFilters = new List<FilterOptions>
        {
            // Stars filter (dungeon item level)
            new FilterOptions
            {
                Name = "Stars",
                Type = FilterType.NUMERICAL | FilterType.RANGE,
                Options = new[] { "0", "1", "2", "3", "4", "5" }
            },
            // Rarity filter
            new FilterOptions
            {
                Name = "Rarity",
                Type = FilterType.EQUAL,
                Options = new[] { "COMMON", "UNCOMMON", "RARE", "EPIC", "LEGENDARY", "MYTHIC", "SPECIAL" }
            },
            // Reforge filter
            new FilterOptions
            {
                Name = "Reforge",
                Type = FilterType.EQUAL,
                Options = Enum.GetNames(typeof(Reforge))
            },
            // Enchantment filter
            new FilterOptions
            {
                Name = "Enchantment",
                Type = FilterType.EQUAL,
                Options = Enum.GetNames(typeof(EnchantmentType))
            },
            // BIN filter
            new FilterOptions
            {
                Name = "Bin",
                Type = FilterType.BOOLEAN,
                Options = new[] { "true", "false" }
            },
            // Min/Max price filter
            new FilterOptions
            {
                Name = "MinPrice",
                Type = FilterType.NUMERICAL | FilterType.RANGE,
                Options = new[] { "0", "50000000", "100000000", "5000000000", "10000000000" }
            },
            new FilterOptions
            {
                Name = "MaxPrice",
                Type = FilterType.NUMERICAL | FilterType.RANGE,
                Options = new[] { "0", "500000", "1000000", "10000000", "500000000" }
            }
        };

        return Ok(commonFilters);
    }

    /// <summary>
    /// Get trending/popular item tags
    /// </summary>
    [HttpGet("tags/popular")]
    public async Task<IActionResult> GetPopularTags([FromQuery] int limit = 20)
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
                
                // --- Remove Stars & Master Stars (✪, ➊, ➋, etc.) ---
                cleanName = System.Text.RegularExpressions.Regex.Replace(cleanName, @"[✪✫⚚➊➋➌➍➎➏➐➑➒]+", "").Trim();
                
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

                        // Handle both "Reforge " and "Reforge's " patterns (e.g., "Jerry's")
                        var hasReforgeSpace = cleanName.StartsWith(reforgeName + " ", StringComparison.OrdinalIgnoreCase);
                        var hasReforgeApostrophe = cleanName.StartsWith(reforgeName + "'s ", StringComparison.OrdinalIgnoreCase);

                        if (hasReforgeSpace || hasReforgeApostrophe)
                        {
                            // SAFETY CHECK: Does the Tag contain the reforge name?
                            // e.g. "Strong Dragon Chestplate" -> Tag "STRONG_DRAGON_..." -> Contains "STRONG"
                            // We do NOT want to strip "Strong" because it's part of the item identity (Tag).
                            // But "Heroic Aspect of the End" -> Tag "ASPECT_OF_THE_END" -> Strip "Heroic".
                            
                            if (!item.Tag.Contains(reforgeName.ToUpper()))
                            {
                                if (hasReforgeSpace)
                                    cleanName = cleanName.Substring(reforgeName.Length + 1);
                                else // hasReforgeApostrophe
                                    cleanName = cleanName.Substring(reforgeName.Length + 3); // Remove "Jerry's "
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

    /// <summary>
    /// Get price history for an item tag (from pre-aggregated data)
    /// </summary>
    [HttpGet("item/{tag}/price-history")]
    public async Task<IActionResult> GetPriceHistory(
        string tag,
        [FromQuery] int days = 30,
        [FromQuery] string granularity = "daily")
    {
        var upperTag = tag.ToUpper();
        var gran = granularity.ToLower() == "hourly" ? PriceGranularity.Hourly : PriceGranularity.Daily;
        
        // Limit days based on granularity (hourly only has 7 days of data)
        if (gran == PriceGranularity.Hourly && days > 7)
            days = 7;
        
        var cutoff = DateTime.UtcNow.AddDays(-days);
        
        var priceData = await _context.AveragePrices
            .Where(p => p.ItemTag == upperTag && 
                       p.Granularity == gran && 
                       p.Timestamp >= cutoff)
            .OrderBy(p => p.Timestamp)
            .Select(p => new 
            {
                p.Timestamp,
                p.Min,
                p.Max,
                p.Avg,
                p.Median,
                p.Volume
            })
            .ToListAsync();

        if (priceData.Count == 0)
        {
            return Ok(new 
            {
                ItemTag = upperTag,
                Granularity = granularity,
                Data = new List<object>(),
                Summary = (object?)null
            });
        }

        // Calculate summary statistics
        var totalVolume = priceData.Sum(p => p.Volume);
        var avgMedian = priceData.Average(p => p.Median);
        var firstPrice = priceData.First().Median;
        var lastPrice = priceData.Last().Median;
        var priceChange = firstPrice > 0 ? ((lastPrice - firstPrice) / firstPrice) * 100 : 0;
        
        string trend = "stable";
        if (priceChange > 5) trend = "increasing";
        else if (priceChange < -5) trend = "decreasing";

        return Ok(new 
        {
            ItemTag = upperTag,
            Granularity = granularity,
            Data = priceData,
            Summary = new 
            {
                TotalVolume = totalVolume,
                AvgMedian = avgMedian,
                PriceChange = Math.Round(priceChange, 2),
                Trend = trend,
                LowestMin = priceData.Min(p => p.Min),
                HighestMax = priceData.Max(p => p.Max)
            }
        });
    }

    /// <summary>
    /// Get price history for 1 day (hourly data points) - real-time from sold auctions
    /// Matches the Coflnet API format: /api/item/price/{itemTag}/history/day
    /// </summary>
    [HttpGet("item/price/{itemTag}/history/day")]
    public async Task<IActionResult> GetDayHistory(string itemTag, [FromQuery] IDictionary<string, string>? filters = null)
    {
        return await GetPriceHistoryFromAuctions(itemTag, TimeSpan.FromDays(1), true, filters);
    }

    /// <summary>
    /// Get price history for 1 week (hourly data points) - real-time from sold auctions
    /// </summary>
    [HttpGet("item/price/{itemTag}/history/week")]
    public async Task<IActionResult> GetWeekHistory(string itemTag, [FromQuery] IDictionary<string, string>? filters = null)
    {
        return await GetPriceHistoryFromAuctions(itemTag, TimeSpan.FromDays(7), true, filters);
    }

    /// <summary>
    /// Get price history for 1 month (daily data points) - real-time from sold auctions
    /// </summary>
    [HttpGet("item/price/{itemTag}/history/month")]
    public async Task<IActionResult> GetMonthHistory(string itemTag, [FromQuery] IDictionary<string, string>? filters = null)
    {
        return await GetPriceHistoryFromAuctions(itemTag, TimeSpan.FromDays(30), false, filters);
    }

    /// <summary>
    /// Get full price history (daily data points) - from pre-aggregated table
    /// </summary>
    [HttpGet("item/price/{itemTag}/history/full")]
    public async Task<IActionResult> GetFullHistory(string itemTag)
    {
        var upperTag = itemTag.ToUpper();
        
        var priceData = await _context.AveragePrices
            .Where(p => p.ItemTag == upperTag && p.Granularity == PriceGranularity.Daily)
            .OrderBy(p => p.Timestamp)
            .Select(p => new 
            {
                time = p.Timestamp,
                min = p.Min,
                max = p.Max,
                avg = p.Avg,
                volume = p.Volume
            })
            .ToListAsync();

        return Ok(priceData);
    }

    /// <summary>
    /// Core method to get price history from sold auctions with filter support
    /// This mimics the SkyBackendForFrontend PricesService.GetHistory() approach
    /// </summary>
    private async Task<IActionResult> GetPriceHistoryFromAuctions(
        string itemTag, 
        TimeSpan timeSpan, 
        bool hourlyGrouping,
        IDictionary<string, string>? filters = null)
    {
        var upperTag = itemTag.ToUpper();
        var start = DateTime.UtcNow.Subtract(timeSpan);
        var end = DateTime.UtcNow;
        
        // Build base query for sold auctions
        // Use SoldAt timestamp for when the auction was actually sold
        // Use SoldPrice if available (from auctions_ended API), otherwise HighestBidAmount
        var query = _context.Auctions
            .Where(a => a.Tag == upperTag)
            .Where(a => a.Status == AuctionStatus.SOLD && a.SoldAt != null)
            .Where(a => a.SoldAt > start && a.SoldAt < end);

        // Apply filters if provided
        if (filters != null && filters.Count > 0)
        {
            query = ApplyFilters(query, filters);
        }

        try
        {
            List<dynamic> dbResult;
            
            if (hourlyGrouping)
            {
                // Group by date and hour for day/week views
                // Use SoldPrice if available, otherwise HighestBidAmount
                dbResult = await query
                    .GroupBy(a => new { a.SoldAt!.Value.Date, a.SoldAt!.Value.Hour })
                    .Select(g => new 
                    {
                        Date = g.Key.Date,
                        Hour = g.Key.Hour,
                        Avg = g.Average(a => (double)(a.SoldPrice ?? a.HighestBidAmount)),
                        Max = g.Max(a => a.SoldPrice ?? a.HighestBidAmount),
                        Min = g.Min(a => a.SoldPrice ?? a.HighestBidAmount),
                        Volume = g.Count()
                    })
                    .OrderBy(x => x.Date).ThenBy(x => x.Hour)
                    .ToListAsync<dynamic>();
            }
            else
            {
                // Group by date only for month view
                // Use SoldPrice if available, otherwise HighestBidAmount
                dbResult = await query
                    .GroupBy(a => a.SoldAt!.Value.Date)
                    .Select(g => new 
                    {
                        Date = g.Key,
                        Hour = 0,
                        Avg = g.Average(a => (double)(a.SoldPrice ?? a.HighestBidAmount)),
                        Max = g.Max(a => a.SoldPrice ?? a.HighestBidAmount),
                        Min = g.Min(a => a.SoldPrice ?? a.HighestBidAmount),
                        Volume = g.Count()
                    })
                    .OrderBy(x => x.Date)
                    .ToListAsync<dynamic>();
            }

            // Convert to response format matching Coflnet API
            var result = dbResult.Select(r => new 
            {
                time = ((DateTime)r.Date).AddHours(r.Hour),
                min = (long)r.Min,
                max = (long)r.Max,
                avg = r.Avg,
                volume = (int)r.Volume
            }).ToList();

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching price history for {Tag}", upperTag);
            return Ok(new List<object>()); // Return empty on error
        }
    }

    /// <summary>
    /// Apply filters to auction query (simplified version of SkyFilter's FilterEngine)
    /// </summary>
    private IQueryable<Auction> ApplyFilters(IQueryable<Auction> query, IDictionary<string, string> filters)
    {
        foreach (var filter in filters)
        {
            if (string.IsNullOrEmpty(filter.Value))
                continue;

            switch (filter.Key.ToLower())
            {
                case "rarity":
                    if (Enum.TryParse<Tier>(filter.Value, true, out var tier))
                    {
                        query = query.Where(a => a.Tier == tier);
                    }
                    break;
                    
                case "reforge":
                    if (Enum.TryParse<Reforge>(filter.Value, true, out var reforge))
                    {
                        query = query.Where(a => a.Reforge == reforge);
                    }
                    break;
                    
                case "bin":
                    if (bool.TryParse(filter.Value, out var binOnly))
                    {
                        query = query.Where(a => a.Bin == binOnly);
                    }
                    break;
                    
                case "petitem":
                case "petname":
                case "namefilter":
                    // For pets, filter by name containing the value
                    query = query.Where(a => a.ItemName.Contains(filter.Value));
                    break;
                    
                // Add more filters as needed (Stars, Enchantments, etc.)
            }
        }
        
        return query;
    }

    /// <summary>
    /// Get price summary for an item (current market data)
    /// </summary>
    [HttpGet("item/price/{itemTag}")]
    public async Task<IActionResult> GetPriceSummary(string itemTag, [FromQuery] IDictionary<string, string>? filters = null)
    {
        var upperTag = itemTag.ToUpper();
        var days = 2;
        var start = DateTime.UtcNow.AddDays(-days);
        
        // Query sold auctions for price summary
        var query = _context.Auctions
            .Where(a => a.Tag == upperTag)
            .Where(a => a.Status == AuctionStatus.SOLD && a.SoldAt != null)
            .Where(a => a.SoldAt > start);

        if (filters != null && filters.Count > 0)
        {
            query = ApplyFilters(query, filters);
        }

        var prices = await query
            .Select(a => a.SoldPrice ?? a.HighestBidAmount)
            .ToListAsync();

        if (prices.Count == 0)
        {
            return Ok(new 
            {
                min = 0,
                max = 0,
                avg = 0,
                med = 0,
                volume = 0
            });
        }

        var sorted = prices.OrderBy(p => p).ToList();
        var median = sorted[sorted.Count / 2];

        return Ok(new 
        {
            min = sorted.First(),
            max = sorted.Last(),
            avg = sorted.Average(),
            med = median,
            volume = sorted.Count / days
        });
    }

    /// <summary>
    /// Get lowest BIN price for an item (Coflnet-compatible format)
    /// </summary>
    [HttpGet("item/price/{itemTag}/bin")]
    public async Task<IActionResult> GetLowestBin(string itemTag, [FromQuery] IDictionary<string, string>? filters = null)
    {
        var upperTag = itemTag.ToUpper();
        
        var query = _context.Auctions
            .Where(a => a.Tag == upperTag)
            .Where(a => a.Status == AuctionStatus.ACTIVE)
            .Where(a => a.Bin == true)
            .Where(a => a.End > DateTime.UtcNow);

        if (filters != null && filters.Count > 0)
        {
            query = ApplyFilters(query, filters);
        }

        var lowestBins = await query
            .OrderBy(a => a.StartingBid)
            .Take(2)
            .Select(a => new { a.Uuid, a.StartingBid, a.ItemName })
            .ToListAsync();

        if (lowestBins.Count == 0)
        {
            return Ok(new 
            {
                lowest = (long?)null,
                secondLowest = (long?)null,
                uuid = (string?)null
            });
        }

        return Ok(new 
        {
            lowest = lowestBins[0].StartingBid,
            secondLowest = lowestBins.Count > 1 ? lowestBins[1].StartingBid : (long?)null,
            uuid = lowestBins[0].Uuid,
            itemName = lowestBins[0].ItemName
        });
    }

    /// <summary>
    /// Get active auctions for an item with sorting options
    /// </summary>
    [HttpGet("item/{itemTag}/auctions/active")]
    public async Task<IActionResult> GetActiveAuctions(
        string itemTag, 
        [FromQuery] string sort = "price", 
        [FromQuery] int page = 0, 
        [FromQuery] int pageSize = 12,
        [FromQuery] IDictionary<string, string>? filters = null)
    {
        var upperTag = itemTag.ToUpper();
        
        var query = _context.Auctions
            .Where(a => a.Tag == upperTag)
            .Where(a => a.Status == AuctionStatus.ACTIVE)
            .Where(a => a.End > DateTime.UtcNow);

        if (filters != null && filters.Count > 0)
        {
            query = ApplyFilters(query, filters);
        }

        // Apply sorting
        query = sort.ToLower() switch
        {
            "price" or "lowest" => query.OrderBy(a => a.Bin ? a.StartingBid : a.HighestBidAmount),
            "price_desc" or "highest" => query.OrderByDescending(a => a.Bin ? a.StartingBid : a.HighestBidAmount),
            "ending" or "ending_soon" => query.OrderBy(a => a.End),
            _ => query.OrderBy(a => a.Bin ? a.StartingBid : a.HighestBidAmount)
        };

        var total = await query.CountAsync();
        
        var auctions = await query
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select(a => new 
            {
                a.Uuid,
                a.ItemName,
                a.Tag,
                a.Tier,
                Price = a.Bin ? a.StartingBid : a.HighestBidAmount,
                a.Bin,
                a.End,
                a.AuctioneerId,
                a.Texture,
                TimeRemaining = (a.End - DateTime.UtcNow).TotalSeconds
            })
            .ToListAsync();

        return Ok(new 
        {
            auctions,
            total,
            page,
            pageSize,
            hasMore = (page + 1) * pageSize < total
        });
    }

    /// <summary>
    /// Get recent sold auctions for an item
    /// </summary>
    [HttpGet("item/{itemTag}/auctions/sold")]
    public async Task<IActionResult> GetSoldAuctions(
        string itemTag, 
        [FromQuery] int page = 0, 
        [FromQuery] int pageSize = 12,
        [FromQuery] IDictionary<string, string>? filters = null)
    {
        var upperTag = itemTag.ToUpper();
        
        var query = _context.Auctions
            .Where(a => a.Tag == upperTag)
            .Where(a => a.Status == AuctionStatus.SOLD && a.SoldAt != null);

        if (filters != null && filters.Count > 0)
        {
            query = ApplyFilters(query, filters);
        }

        var total = await query.CountAsync();
        
        var auctions = await query
            .OrderByDescending(a => a.SoldAt)
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select(a => new 
            {
                a.Uuid,
                a.ItemName,
                a.Tag,
                a.Tier,
                Price = a.SoldPrice ?? a.HighestBidAmount,
                a.Bin,
                a.SoldAt,
                a.AuctioneerId,
                a.Texture
            })
            .ToListAsync();

        return Ok(new 
        {
            auctions,
            total,
            page,
            pageSize,
            hasMore = (page + 1) * pageSize < total
        });
    }

    /// <summary>
    /// Get related items based on similar tags or category
    /// </summary>
    [HttpGet("item/{itemTag}/related")]
    public async Task<IActionResult> GetRelatedItems(string itemTag, [FromQuery] int limit = 8)
    {
        var upperTag = itemTag.ToUpper();
        
        // Get info about the current item (category, tier, avg price)
        var currentItem = await _context.Auctions
            .Where(a => a.Tag == upperTag && a.Status == AuctionStatus.ACTIVE)
            .Select(a => new { a.Category, a.Tier })
            .FirstOrDefaultAsync();
        
        if (currentItem == null)
        {
            return Ok(new List<object>());
        }

        // Strategy 1: Find items with similar tag patterns
        // Split the tag by underscore and find items that share common parts
        var tagParts = upperTag.Split('_');
        var relatedByTag = new List<object>();
        var existingTags = new HashSet<string>(); // Track already added tags
        
        // For multi-part tags, find items that share key parts
        // e.g., HYPERION shares "NECRON" base with ASTRAEA, SCYLLA, VALKYRIE
        // e.g., ASPECT_OF_THE_VOID shares base with ASPECT_OF_THE_END
        if (tagParts.Length >= 2)
        {
            // Build patterns to search for
            var patterns = new List<string>();
            
            // For items like "ASPECT_OF_THE_END", search for "ASPECT_OF_THE"
            if (tagParts.Length >= 3)
            {
                patterns.Add(string.Join("_", tagParts.Take(tagParts.Length - 1)));
            }
            
            // Always add the first part as a pattern (e.g., "HYPERION" or "ASPECT")
            if (tagParts[0].Length >= 3)
            {
                patterns.Add(tagParts[0]);
            }
            
            foreach (var pattern in patterns)
            {
                if (relatedByTag.Count >= limit) break;
                
                var matches = await _context.Auctions
                    .Where(a => a.Tag != upperTag) // Exclude current item
                    .Where(a => a.Tag.Contains(pattern))
                    .Where(a => a.Status == AuctionStatus.ACTIVE)
                    .GroupBy(a => a.Tag)
                    .Select(g => new 
                    {
                        Tag = g.Key,
                        ItemName = g.First().ItemName,
                        Tier = g.First().Tier,
                        Category = g.First().Category,
                        Texture = g.First().Texture,
                        LowestPrice = g.Where(a => a.Bin).Min(a => (long?)a.StartingBid) ?? 
                                     g.Min(a => a.HighestBidAmount > 0 ? a.HighestBidAmount : a.StartingBid),
                        Count = g.Count()
                    })
                    .Where(x => x.Count > 0)
                    .OrderByDescending(x => x.Count)
                    .Take(limit)
                    .ToListAsync();
                
                // Add matches that aren't already in the list
                foreach (var match in matches)
                {
                    if (!existingTags.Contains(match.Tag))
                    {
                        relatedByTag.Add(match);
                        existingTags.Add(match.Tag);
                        if (relatedByTag.Count >= limit) break;
                    }
                }
            }
        }
        
        // Strategy 2: If we don't have enough related items, find items in the same category
        if (relatedByTag.Count < limit)
        {
            var categoryMatches = await _context.Auctions
                .Where(a => a.Tag != upperTag)
                .Where(a => a.Category == currentItem.Category)
                .Where(a => a.Tier == currentItem.Tier) // Same tier for relevance
                .Where(a => a.Status == AuctionStatus.ACTIVE)
                .GroupBy(a => a.Tag)
                .Select(g => new 
                {
                    Tag = g.Key,
                    ItemName = g.First().ItemName,
                    Tier = g.First().Tier,
                    Category = g.First().Category,
                    Texture = g.First().Texture,
                    LowestPrice = g.Where(a => a.Bin).Min(a => (long?)a.StartingBid) ?? 
                                 g.Min(a => a.HighestBidAmount > 0 ? a.HighestBidAmount : a.StartingBid),
                    Count = g.Count()
                })
                .Where(x => x.Count > 0)
                .OrderByDescending(x => x.Count)
                .Take(limit)
                .ToListAsync();
            
            // Add category matches that aren't already in the list
            foreach (var match in categoryMatches)
            {
                if (!existingTags.Contains(match.Tag))
                {
                    relatedByTag.Add(match);
                    existingTags.Add(match.Tag);
                    if (relatedByTag.Count >= limit) break;
                }
            }
        }
        
        return Ok(relatedByTag.Take(limit));
    }

    /// <summary>
    /// Resolve a Minecraft UUID to username using Hypixel API
    /// </summary>
    [HttpGet("player/{uuid}/name")]
    public async Task<IActionResult> GetPlayerName(string uuid)
    {
        var apiKey = _configuration.GetValue<string>("HypixelApi:Key");
        var fallbackName = $"Player_{uuid.Substring(0, 8)}";

        // If no API key is configured, return formatted fallback
        if (string.IsNullOrEmpty(apiKey))
        {
            return Ok(new { name = fallbackName, note = "Hypixel API key not configured" });
        }

        try
        {
            var client = _httpClientFactory.CreateClient("HypixelApi");

            // Change the base address to the main Hypixel API for player endpoint
            client.BaseAddress = new Uri("https://api.hypixel.net/");

            var response = await client.GetAsync($"player?key={HttpUtility.UrlEncode(apiKey)}&uuid={uuid}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Hypixel API returned {StatusCode} for UUID {Uuid}", response.StatusCode, uuid);
                return Ok(new { name = fallbackName, note = $"API error: {response.StatusCode}" });
            }

            var content = await response.Content.ReadAsStringAsync();
            var playerData = JsonSerializer.Deserialize<JsonElement>(content);

            // Check if the API call was successful
            if (playerData.TryGetProperty("success", out var success) && success.GetBoolean() == false)
            {
                var cause = playerData.TryGetProperty("cause", out var causeProp) ? causeProp.GetString() : "Unknown error";
                return Ok(new { name = fallbackName, note = $"API error: {cause}" });
            }

            // Extract the display name
            if (playerData.TryGetProperty("player", out var player) &&
                player.TryGetProperty("displayname", out var displayName))
            {
                var actualName = displayName.GetString();
                if (!string.IsNullOrEmpty(actualName))
                {
                    return Ok(new { name = actualName });
                }
            }

            // Fallback if we couldn't find the display name
            return Ok(new { name = fallbackName, note = "Display name not found in player data" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching player name for UUID {Uuid}", uuid);
            return Ok(new { name = fallbackName, note = "Error fetching from API" });
        }
    }
}
