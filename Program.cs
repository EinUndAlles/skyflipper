using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Hubs;
using SkyFlipperSolo.Models;
using SkyFlipperSolo.Services;
using SkyFlipperSolo.Services.Filters;
using Microsoft.Extensions.Caching.Distributed;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Serialize enums as strings instead of numbers
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Add CORS policy for frontend (with credentials for SignalR)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // Required for SignalR
    });
});

// Add SignalR for real-time WebSocket communication
// Add SignalR for real-time WebSocket communication
builder.Services.AddSignalR();
builder.Services.AddMemoryCache(); // Required for ComponentValueService cashing
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetValue<string>("Redis:ConnectionString") ?? "localhost:6379";
});

// Add PostgreSQL DbContext with retry on transient failures (including deadlocks)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Database=skyflipperdb;Username=postgres;Password=postgres";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: new[] { "40P01" } // PostgreSQL deadlock error code
        );
    }));

// Add HttpClient for Hypixel API
builder.Services.AddHttpClient("HypixelApi", client =>
{
    client.BaseAddress = new Uri("https://api.hypixel.net/v2/skyblock/");
    client.DefaultRequestHeaders.Add("User-Agent", "SkyFlipperSolo/1.0");
});

// Add channel for passing auctions between services
builder.Services.AddSingleton(Channel.CreateUnbounded<HypixelAuction>(new UnboundedChannelOptions
{
    SingleReader = false,
    SingleWriter = false
}));

// Add services
builder.Services.AddSingleton<NbtParserService>();
builder.Services.AddSingleton<NBTKeyService>(); // NBT key normalization service
builder.Services.AddSingleton<NBTValueService>(); // NBT value deduplication service
builder.Services.AddSingleton<NbtLookupResolver>(); // NBT key/value ID resolver
builder.Services.AddSingleton<ItemDetailsService>(); // Item metadata tracking
builder.Services.AddSingleton<CacheKeyService>(); // NBT-aware cache key generation
builder.Services.AddSingleton<ReferenceCacheService>(); // Redis-backed reference cache
builder.Services.AddSingleton<ReferenceAuctionService>(); // Coflnet-style reference auction matching
builder.Services.AddSingleton<PropertiesSelectorService>(); // Item property formatting
builder.Services.AddSingleton<ComponentValueService>(); // Component valuation service
builder.Services.AddSingleton<FilterRegistry>();
builder.Services.AddSingleton<FilterEngine>();
builder.Services.AddScoped<StarsFilter>();
builder.Services.AddSingleton<RarityFilter>();
builder.Services.AddSingleton<ReforgeFilter>();
builder.Services.AddSingleton<BinFilter>();
builder.Services.AddSingleton<StartingBidFilter>();
builder.Services.AddSingleton<HighestBidFilter>();
builder.Services.AddSingleton<CountFilter>();
builder.Services.AddSingleton<EnchantmentFilter>();
builder.Services.AddSingleton<EnchantLvlFilter>();
builder.Services.AddScoped<HotPotatoCountFilter>();
builder.Services.AddScoped<ArtOfTheWarFilter>();
builder.Services.AddScoped<FarmingForDummiesFilter>();
builder.Services.AddScoped<RecombobulatedFilter>();
builder.Services.AddScoped<EthermergeFilter>();
builder.Services.AddScoped<AbilityScrollFilter>();
builder.Services.AddScoped<SkinFilter>();
builder.Services.AddScoped<WinningBidFilter>();
builder.Services.AddScoped<EditionFilter>();
builder.Services.AddScoped<CapturedPlayerFilter>();
builder.Services.AddSingleton<EndBeforeFilter>();
builder.Services.AddSingleton<EndAfterFilter>();
builder.Services.AddSingleton<ItemCreatedBeforeFilter>();
builder.Services.AddSingleton<ItemCreatedAfterFilter>();
builder.Services.AddScoped<PetLevelFilter>();
builder.Services.AddScoped<PetItemFilter>();
builder.Services.AddScoped<PetSkinFilter>();
builder.Services.AddScoped<PetExpFilter>();
builder.Services.AddScoped<ColorFilter>();
builder.Services.AddScoped<HexColorListFilter>();
builder.Services.AddScoped<ExoticColorFilter>();
builder.Services.AddScoped<DyeItemFilter>();
builder.Services.AddScoped<UnlockedSlotsFilter>();
builder.Services.AddScoped<UnlockedSlotsMatchFilter>();
builder.Services.AddScoped<HasAttributeFilter>();
builder.Services.AddScoped<PerfectGemsCountFilter>();
builder.Services.AddScoped<FlawlessGemsCountFilter>();

// Kills / counter filters
builder.Services.AddScoped<ZombieKillsFilter>();
builder.Services.AddScoped<SpiderKillsFilter>();
builder.Services.AddScoped<EmanKillsFilter>();
builder.Services.AddScoped<ExpertiseKillsFilter>();
builder.Services.AddScoped<RaiderKillsFilter>();
builder.Services.AddScoped<SwordKillsFilter>();
builder.Services.AddScoped<BloodGodKillsFilter>();
builder.Services.AddScoped<BlazeKillsFilter>();
builder.Services.AddScoped<YogsKilledFilter>();
builder.Services.AddScoped<BlazeConsumerFilter>();
builder.Services.AddScoped<RunicKillsFilter>();
builder.Services.AddScoped<HandlesFoundFilter>();

// Stat counter filters
builder.Services.AddScoped<BaseStatBoostFilter>();
builder.Services.AddScoped<ManaDisintegratorFilter>();
builder.Services.AddScoped<FarmedCultivatingFilter>();
builder.Services.AddScoped<MinedCropsFilter>();
builder.Services.AddScoped<BlocksBrokenFilter>();
builder.Services.AddScoped<ThunderChargeFilter>();
builder.Services.AddScoped<CollectedCoinsFilter>();
builder.Services.AddScoped<ChimeraFoundFilter>();
builder.Services.AddScoped<PickonimbusDurabilityFilter>();
builder.Services.AddScoped<IntelligenceEarnedFilter>();
builder.Services.AddScoped<RaffleWinCountFilter>();
builder.Services.AddScoped<RaffleYearCountFilter>();

// Special filters
builder.Services.AddScoped<IntelligenceBonusFilter>();

// Rune filters
builder.Services.AddScoped<MusicRuneFilter>();
builder.Services.AddScoped<EnchantRuneFilter>();
builder.Services.AddScoped<TidalRuneFilter>();
builder.Services.AddScoped<EndRuneFilter>();

// Item-specific skin filters
builder.Services.AddScoped<DragonArmorSkinFilter>();
builder.Services.AddScoped<ReaperMaskSkinFilter>();
builder.Services.AddScoped<SnowSuiteSkinFilter>();
builder.Services.AddScoped<TarantulaHelmetSkinFilter>();
builder.Services.AddScoped<FrozenBlazeSkinFilter>();
builder.Services.AddScoped<PerfectHelmetSkinFilter>();
builder.Services.AddScoped<DiversMaskSkinFilter>();
builder.Services.AddScoped<ShadowAssassinSkinFilter>();

// Bool/flag filters
builder.Services.AddScoped<IsShinyFilter>();
builder.Services.AddScoped<ArtOfPeaceFilter>();
builder.Services.AddScoped<WoodSingularityFilter>();
builder.Services.AddScoped<ModelFilter>();
builder.Services.AddScoped<SoldFilter>();
builder.Services.AddScoped<CleanFilter>();

// Drill/equipment filters
builder.Services.AddScoped<DrillPartEngineFilter>();
builder.Services.AddScoped<DrillPartFuelTankFilter>();
builder.Services.AddScoped<DrillPartUpgradeModuleFilter>();
builder.Services.AddScoped<PowerAbilityScrollFilter>();
builder.Services.AddScoped<TunedTransmissionFilter>();

// Misc string filters
builder.Services.AddScoped<SellerFilter>();
builder.Services.AddScoped<CakeOwnerFilter>();
builder.Services.AddScoped<CakeYearFilter>();
builder.Services.AddScoped<PartyHatYearFilter>();
builder.Services.AddScoped<PartyHatColorFilter>();
builder.Services.AddScoped<PartyHatEmojiFilter>();
builder.Services.AddScoped<FairyColorFilter>();
builder.Services.AddScoped<CrystalColorFilter>();
// Enable full functionality with background services
builder.Services.AddHostedService<AuctionFetcherService>();
builder.Services.AddHostedService<AuctionLifecycleService>(); // Comprehensive lifecycle management
builder.Services.AddHostedService<FlipperService>();
builder.Services.AddHostedService<SoldAuctionService>(); // Keep for auctions_ended API integration
builder.Services.AddHostedService<PriceAggregationService>();
builder.Services.AddHostedService<FlipDetectionService>();
builder.Services.AddHostedService<BidFlipDetectionService>(); // Non-BIN auction flip detection
builder.Services.AddHostedService<FlipBroadcastService>(); // Real-time flip notifications via SignalR


var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseHttpMetrics();

app.MapControllers();

app.MapMetrics();

// Map SignalR hub for real-time flip notifications
app.MapHub<FlipHub>("/hubs/flips");

// Simple health check endpoint
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Time = DateTime.UtcNow }))
    .WithName("HealthCheck")
    .WithOpenApi();

// Auto-migrate database and seed NBT keys
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Apply database migrations for full functionality
    try
    {
        await dbContext.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied successfully");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not apply migrations - some features may not work properly");
    }

    // Seed common NBT keys (only if database is accessible)
    try
    {
        var nbtKeyService = scope.ServiceProvider.GetRequiredService<NBTKeyService>();
        await nbtKeyService.SeedCommonKeys();
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not seed NBT keys - database may not be fully set up");
    }

    try
    {
        FilterBootstrapper.RegisterCoreFilters(scope.ServiceProvider);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not register core filters");
    }
}

app.Run();
