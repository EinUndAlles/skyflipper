using System.Threading.Channels;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services;

/// <summary>
/// Background service that fetches auctions from the Hypixel API.
/// Based on: SkyUpdater/Updater.cs LoadPage and RunUpdate methods.
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
        _logger.LogInformation("AuctionFetcherService starting...");

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

        _logger.LogInformation("Fetching {Pages} pages, {Total} total auctions...", totalPages, firstPage.TotalAuctions);

        // Process first page
        await ProcessPage(firstPage, stoppingToken);
        totalAuctions += firstPage.Auctions.Count;

        // Fetch remaining pages concurrently (in batches to avoid overwhelming the API)
        var batchSize = 5;
        for (int i = 1; i < totalPages; i += batchSize)
        {
            if (stoppingToken.IsCancellationRequested) break;

            var tasks = Enumerable.Range(i, Math.Min(batchSize, totalPages - i))
                .Select(page => FetchAndProcessPage(client, page, stoppingToken))
                .ToList();

            var results = await Task.WhenAll(tasks);
            totalAuctions += results.Sum();
        }

        var elapsed = DateTime.UtcNow - startTime;
        _logger.LogInformation("Fetched {Count} auctions in {Elapsed:F1}s", totalAuctions, elapsed.TotalSeconds);
    }

    private async Task<int> FetchAndProcessPage(HttpClient client, int page, CancellationToken stoppingToken)
    {
        var pageData = await FetchPage(client, page, stoppingToken);
        if (pageData == null) return 0;

        await ProcessPage(pageData, stoppingToken);
        return pageData.Auctions.Count;
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

    private async Task ProcessPage(AuctionPageResponse page, CancellationToken stoppingToken)
    {
        foreach (var auction in page.Auctions)
        {
            // Only process BIN auctions (flipping non-BIN is much harder)
            if (!auction.Bin) continue;

            // Skip auctions that already ended
            if (auction.End < DateTime.UtcNow) continue;

            // Push to channel for processing
            await _auctionChannel.Writer.WriteAsync(auction, stoppingToken);
        }
    }
}
