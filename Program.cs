using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Data;
using SkyFlipperSolo.Models;
using SkyFlipperSolo.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

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
builder.Services.AddHostedService<AuctionFetcherService>();
builder.Services.AddHostedService<FlipperService>();
builder.Services.AddHostedService<SoldAuctionService>();
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

// Auto-migrate database in development
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (app.Environment.IsDevelopment())
    {
        await dbContext.Database.MigrateAsync();
    }
}

app.Run();
