using System.Threading.Channels;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services;

/// <summary>
/// Background service that fetches auctions from the Hypixel API.
/// Features: Tracks processed auction IDs to skip duplicates, delays between page batches.
/// </summary>
public class AuctionFetcherService : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuctionFetcherService> _logger;
    private readonly IConfiguration _configuration;
    private readonly Channel<HypixelAuction> _auctionChannel;

    private DateTime _lastApiUpdate = DateTime.MinValue;
    private int _fetchIntervalSeconds;
    private readonly HashSet<string> _processedAuctionIds = new(); // Track processed UUIDs
    private const int DelayBetweenPageBatchesMs = 250; // Delay between batches of 5 pages

    public AuctionFetcherService(
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory scopeFactory,
        ILogger<AuctionFetcherService> logger,
        IConfiguration configuration,
        Channel<HypixelAuction> auctionChannel)
    {
        _httpClientFactory = httpClientFactory;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
        _auctionChannel = auctionChannel;
        _fetchIntervalSeconds = configuration.GetValue("Flipper:FetchIntervalSeconds", 60);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AuctionFetcherService starting (duplicate tracking enabled)...");

        // Wait a bit for the app to fully start
        await Task.Delay(2000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await FetchAllAuctions(stoppingToken);

                // Hypixel API updates every ~60 seconds
                var waitTime = TimeSpan.FromSeconds(_fetchIntervalSeconds);
                _logger.LogInformation("Waiting {Seconds}s before next fetch...", waitTime.TotalSeconds);
                await Task.Delay(waitTime, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching auctions, retrying in 30s...");
                await Task.Delay(30000, stoppingToken);
            }
        }
    }

    private async Task FetchAllAuctions(CancellationToken stoppingToken)
    {
        var client = _httpClientFactory.CreateClient("HypixelApi");
        var startTime = DateTime.UtcNow;

        // Fetch first page to get total page count
        var firstPage = await FetchPage(client, 0, stoppingToken);
        if (firstPage == null)
        {
            _logger.LogWarning("Failed to fetch first page");
            return;
        }

        // Check if API has updated since last fetch
        if (firstPage.LastUpdatedDateTime <= _lastApiUpdate)
        {
            _logger.LogDebug("API not updated since last fetch, skipping...");
            return;
        }

        _lastApiUpdate = firstPage.LastUpdatedDateTime;
        var totalPages = firstPage.TotalPages;
        var totalAuctions = 0;
        var skippedDuplicates = 0;

        _logger.LogInformation("Fetching {Pages} pages, {Total} total auctions...", totalPages, firstPage.TotalAuctions);

        // Clear old processed IDs (auctions from previous API update cycle)
        // Keep a sliding window to handle auctions that span multiple updates
        if (_processedAuctionIds.Count > 500000) // Limit memory usage
        {
            _processedAuctionIds.Clear();
            _logger.LogDebug("Cleared processed auction ID cache (was over 500k)");
        }

        // Process first page
        var (processed, skipped) = await ProcessPage(firstPage, stoppingToken);
        totalAuctions += processed;
        skippedDuplicates += skipped;

        // Fetch remaining pages concurrently (in batches to avoid overwhelming the API)
        var batchSize = 5;
        for (int i = 1; i < totalPages; i += batchSize)
        {
            if (stoppingToken.IsCancellationRequested) break;

            var tasks = Enumerable.Range(i, Math.Min(batchSize, totalPages - i))
                .Select(page => FetchAndProcessPage(client, page, stoppingToken))
                .ToList();

            var results = await Task.WhenAll(tasks);
            totalAuctions += results.Sum(r => r.processed);
            skippedDuplicates += results.Sum(r => r.skipped);

            // Delay between page batches to reduce API load
            if (i + batchSize < totalPages)
            {
                await Task.Delay(DelayBetweenPageBatchesMs, stoppingToken);
            }
        }

        var elapsed = DateTime.UtcNow - startTime;
        _logger.LogInformation("✅ Fetched {Count} new auctions (skipped {Skipped} duplicates) in {Elapsed:F1}s", 
            totalAuctions, skippedDuplicates, elapsed.TotalSeconds);
    }

    private async Task<(int processed, int skipped)> FetchAndProcessPage(
        HttpClient client, 
        int page, 
        CancellationToken stoppingToken)
    {
        var pageData = await FetchPage(client, page, stoppingToken);
        if (pageData == null) return (0, 0);

        return await ProcessPage(pageData, stoppingToken);
    }

    private async Task<AuctionPageResponse?> FetchPage(HttpClient client, int page, CancellationToken stoppingToken)
    {
        try
        {
            var response = await client.GetAsync($"auctions?page={page}", stoppingToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch page {Page}: {Status}", page, response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(stoppingToken);
            var pageData = System.Text.Json.JsonSerializer.Deserialize<AuctionPageResponse>(content);

            return pageData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching page {Page}", page);
            return null;
        }
    }

    private async Task<(int processed, int skipped)> ProcessPage(
        AuctionPageResponse page, 
        CancellationToken stoppingToken)
    {
        int processed = 0;
        int skipped = 0;

        foreach (var auction in page.Auctions)
        {
            // Only process BIN auctions (flipping non-BIN is much harder)
            if (!auction.Bin)
            {
                skipped++;
                continue;
            }

            // Skip auctions that already ended
            if (auction.End < DateTime.UtcNow)
            {
                skipped++;
                continue;
            }

            // Skip already processed auctions (duplicates from same API update)
            if (_processedAuctionIds.Contains(auction.Uuid))
            {
                skipped++;
                continue;
            }

            // Mark as processed
            _processedAuctionIds.Add(auction.Uuid);

            // Push to channel for processing
            await _auctionChannel.Writer.WriteAsync(auction, stoppingToken);
            processed++;
        }

        return (processed, skipped);
    }
}
