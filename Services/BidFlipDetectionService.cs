using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Hubs;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services;

/// <summary>
/// Detects non-BIN flips ending soon using the same Coflnet-style reference auction engine.
/// </summary>
public class BidFlipDetectionService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BidFlipDetectionService> _logger;
    private readonly ReferenceAuctionService _referenceAuctionService;
    private readonly CacheKeyService _cacheKeyService;
    private readonly IHubContext<FlipHub> _hubContext;

    private const string FlipSubscribersGroup = "FlipSubscribers";
    private const int DetectionIntervalSeconds = 15;
    private static readonly TimeSpan MinTimeToEnd = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxTimeToEnd = TimeSpan.FromMinutes(2);
    private const int MaxHitCount = 20;
    private static readonly TimeSpan HitCountTtl = TimeSpan.FromHours(2);

    public BidFlipDetectionService(
        IServiceScopeFactory scopeFactory,
        ILogger<BidFlipDetectionService> logger,
        ReferenceAuctionService referenceAuctionService,
        CacheKeyService cacheKeyService,
        IHubContext<FlipHub> hubContext)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _referenceAuctionService = referenceAuctionService;
        _cacheKeyService = cacheKeyService;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BidFlipDetectionService starting (reference-auction mode)...");
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DetectBidFlips(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error detecting bid flips");
            }

            await Task.Delay(TimeSpan.FromSeconds(DetectionIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("BidFlipDetectionService stopped");
    }

    private async Task DetectBidFlips(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;

        var minEndTime = now.Add(MinTimeToEnd);
        var maxEndTime = now.Add(MaxTimeToEnd);

        var hitCounts = await dbContext.FlipHitCounts
            .AsNoTracking()
            .Where(h => h.LastHitAt > now - HitCountTtl)
            .ToDictionaryAsync(h => h.CacheKey, h => h.HitCount, stoppingToken);

        var endingAuctions = await dbContext.Auctions
            .Where(a => !a.Bin &&
                        a.Status == AuctionStatus.ACTIVE &&
                        a.End > minEndTime &&
                        a.End < maxEndTime)
            .Include(a => a.Enchantments)
            .Include(a => a.Bids)
            .Include(a => a.NBTLookups)
                .ThenInclude(n => n.NBTKey)
            .Include(a => a.NBTLookups)
                .ThenInclude(n => n.NBTValue)
            .ToListAsync(stoppingToken);

        if (endingAuctions.Count == 0)
            return;

        var bidFlips = new List<FlipOpportunity>();
        var hitUpdates = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var auction in endingAuctions)
        {
            var probeKey = _cacheKeyService.GeneratePriceCacheKey(auction);
            var currentHitCount = hitCounts.TryGetValue(probeKey, out var existing)
                ? Math.Min(existing, MaxHitCount)
                : 0;

            var valuation = await _referenceAuctionService.EvaluateBidAuctionAsync(auction, currentHitCount, stoppingToken);
            if (valuation == null)
                continue;

            var expectedPurchasePrice = auction.HighestBidAmount == 0
                ? auction.StartingBid
                : (long)(auction.HighestBidAmount * 1.1);

            bidFlips.Add(new FlipOpportunity
            {
                AuctionUuid = auction.Uuid,
                ItemTag = auction.Tag,
                ItemName = auction.ItemName,
                CurrentPrice = expectedPurchasePrice,
                MedianPrice = valuation.TargetPrice,
                EstimatedProfit = valuation.EstimatedProfit,
                ProfitMarginPercent = valuation.ProfitMarginPercent,
                AuctionEnd = auction.End,
                DataSource = valuation.DataSource,
                ValueBreakdown = valuation.ValueBreakdown
            });

            if (!hitUpdates.ContainsKey(valuation.CacheKey))
                hitUpdates[valuation.CacheKey] = 0;
            hitUpdates[valuation.CacheKey]++;
        }

        foreach (var flip in bidFlips.OrderByDescending(f => f.ProfitMarginPercent))
        {
            var notification = new FlipNotification
            {
                AuctionUuid = flip.AuctionUuid,
                ItemTag = flip.ItemTag,
                ItemName = flip.ItemName,
                CurrentPrice = flip.CurrentPrice,
                MedianPrice = flip.MedianPrice,
                EstimatedProfit = flip.EstimatedProfit,
                ProfitMarginPercent = flip.ProfitMarginPercent,
                DetectedAt = flip.DetectedAt,
                AuctionEnd = flip.AuctionEnd,
                DataSource = flip.DataSource
            };

            await _hubContext.Clients.Group(FlipSubscribersGroup)
                .SendAsync("NewBidFlip", notification, stoppingToken);
        }

        if (hitUpdates.Count > 0)
            await BatchUpdateHitCounts(dbContext, hitUpdates, stoppingToken);

        if (bidFlips.Count > 0)
            _logger.LogInformation("Found {Count} bid flips", bidFlips.Count);
    }

    private async Task BatchUpdateHitCounts(AppDbContext dbContext, Dictionary<string, int> hitUpdates, CancellationToken stoppingToken)
    {
        var cacheKeys = hitUpdates.Keys.ToList();
        var existingRecords = await dbContext.FlipHitCounts
            .Where(h => cacheKeys.Contains(h.CacheKey))
            .ToDictionaryAsync(h => h.CacheKey, stoppingToken);

        var now = DateTime.UtcNow;
        var newRecords = new List<FlipHitCount>();

        foreach (var (cacheKey, incrementBy) in hitUpdates)
        {
            if (existingRecords.TryGetValue(cacheKey, out var existing))
            {
                existing.HitCount += incrementBy;
                existing.LastHitAt = now;
            }
            else
            {
                newRecords.Add(new FlipHitCount
                {
                    CacheKey = cacheKey,
                    HitCount = incrementBy,
                    LastHitAt = now
                });
            }
        }

        if (newRecords.Count > 0)
            dbContext.FlipHitCounts.AddRange(newRecords);

        await dbContext.SaveChangesAsync(stoppingToken);
    }
}
