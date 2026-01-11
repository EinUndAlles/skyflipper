using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;
using SkyFlipperSolo.Services;

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

// Add CORS policy for frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Add PostgreSQL DbContext
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Database=skyflipperdb;Username=postgres;Password=postgres";
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

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
builder.Services.AddSingleton<ItemDetailsService>(); // Item metadata tracking
builder.Services.AddSingleton<CacheKeyService>(); // NBT-aware cache key generation
// Enable full functionality with background services
builder.Services.AddHostedService<AuctionFetcherService>();
builder.Services.AddHostedService<AuctionLifecycleService>(); // Comprehensive lifecycle management
builder.Services.AddHostedService<FlipperService>();
builder.Services.AddHostedService<SoldAuctionService>(); // Keep for auctions_ended API integration
builder.Services.AddHostedService<PriceAggregationService>();
builder.Services.AddHostedService<FlipDetectionService>();


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

app.MapControllers();

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
}

app.Run();
