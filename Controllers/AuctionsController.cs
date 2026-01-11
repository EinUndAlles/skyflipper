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

        return Ok(auction);
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
    [ResponseCache(Duration = 120, VaryByQueryKeys = new[] { "*" })]
    public async Task<IActionResult> GetDayHistory(string itemTag, [FromQuery] IDictionary<string, string>? filters = null)
    {
        return await GetPriceHistoryFromAuctions(itemTag, TimeSpan.FromDays(1), true, filters);
    }

    /// <summary>
    /// Get price history for 1 week (hourly data points) - real-time from sold auctions
    /// </summary>
    [HttpGet("item/price/{itemTag}/history/week")]
    [ResponseCache(Duration = 1800, VaryByQueryKeys = new[] { "*" })]
    public async Task<IActionResult> GetWeekHistory(string itemTag, [FromQuery] IDictionary<string, string>? filters = null)
    {
        return await GetPriceHistoryFromAuctions(itemTag, TimeSpan.FromDays(7), true, filters);
    }

    /// <summary>
    /// Get price history for 1 month (daily data points) - real-time from sold auctions
    /// </summary>
    [HttpGet("item/price/{itemTag}/history/month")]
    [ResponseCache(Duration = 7200, VaryByQueryKeys = new[] { "*" })]
    public async Task<IActionResult> GetMonthHistory(string itemTag, [FromQuery] IDictionary<string, string>? filters = null)
    {
        return await GetPriceHistoryFromAuctions(itemTag, TimeSpan.FromDays(30), false, filters);
    }

    /// <summary>
    /// Get full price history (daily data points) - from pre-aggregated table
    /// </summary>
    [HttpGet("item/price/{itemTag}/history/full")]
    [ResponseCache(Duration = 7200)]
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
        var query = _context.Auctions
            .Where(a => a.Tag == upperTag)
            .Where(a => a.End > start && a.End < end)
            .Where(a => a.HighestBidAmount > 0) // Only sold auctions
            .Where(a => a.Status == AuctionStatus.SOLD || a.End < DateTime.UtcNow);

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
                dbResult = await query
                    .GroupBy(a => new { a.End.Date, a.End.Hour })
                    .Select(g => new 
                    {
                        Date = g.Key.Date,
                        Hour = g.Key.Hour,
                        Avg = g.Average(a => (double)a.HighestBidAmount),
                        Max = g.Max(a => a.HighestBidAmount),
                        Min = g.Min(a => a.HighestBidAmount),
                        Volume = g.Count()
                    })
                    .OrderBy(x => x.Date).ThenBy(x => x.Hour)
                    .ToListAsync<dynamic>();
            }
            else
            {
                // Group by date only for month view
                dbResult = await query
                    .GroupBy(a => a.End.Date)
                    .Select(g => new 
                    {
                        Date = g.Key,
                        Hour = 0,
                        Avg = g.Average(a => (double)a.HighestBidAmount),
                        Max = g.Max(a => a.HighestBidAmount),
                        Min = g.Min(a => a.HighestBidAmount),
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
    [ResponseCache(Duration = 1800, VaryByQueryKeys = new[] { "*" })]
    public async Task<IActionResult> GetPriceSummary(string itemTag, [FromQuery] IDictionary<string, string>? filters = null)
    {
        var upperTag = itemTag.ToUpper();
        var days = 2;
        var start = DateTime.UtcNow.AddDays(-days);
        
        var query = _context.Auctions
            .Where(a => a.Tag == upperTag)
            .Where(a => a.End > start && a.End < DateTime.UtcNow)
            .Where(a => a.HighestBidAmount > 0);

        if (filters != null && filters.Count > 0)
        {
            query = ApplyFilters(query, filters);
        }

        var prices = await query
            .Select(a => a.HighestBidAmount)
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
}
