using Microsoft.EntityFrameworkCore;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Data;

/// <summary>
/// Entity Framework Core database context.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Auction> Auctions { get; set; }
    public DbSet<Enchantment> Enchantments { get; set; }
    public DbSet<Flip> Flips { get; set; }
    public DbSet<AveragePrice> AveragePrices { get; set; }
    public DbSet<FlipOpportunity> FlipOpportunities { get; set; }
    public DbSet<NbtData> NbtData { get; set; }
    public DbSet<NBTLookup> NBTLookups { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Auction configuration
        modelBuilder.Entity<Auction>(entity =>
        {
            entity.HasIndex(e => e.Uuid).IsUnique();
            entity.HasIndex(e => e.UId);
            entity.HasIndex(e => e.Tag);
            entity.HasIndex(e => e.End);
            entity.HasIndex(e => new { e.Tag, e.Tier, e.Reforge });
            
            // Composite indexes for price history and flip detection
            entity.HasIndex(e => new { e.Tag, e.End });
            entity.HasIndex(e => new { e.Bin, e.Status, e.End });
            entity.HasIndex(e => new { e.Status, e.End });

            entity.HasMany(e => e.Enchantments)
                .WithOne(e => e.Auction)
                .HasForeignKey(e => e.AuctionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.NBTLookups)
                .WithOne(e => e.Auction)
                .HasForeignKey(e => e.AuctionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.NbtData)
                .WithMany()
                .HasForeignKey(e => e.NbtDataId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Flip configuration
        modelBuilder.Entity<Flip>(entity =>
        {
            entity.HasIndex(e => e.FoundAt);
            entity.HasIndex(e => e.NotificationSent);

            entity.HasOne(e => e.Auction)
                .WithMany()
                .HasForeignKey(e => e.AuctionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Enchantment configuration
        modelBuilder.Entity<Enchantment>(entity =>
        {
            entity.HasIndex(e => new { e.AuctionId, e.Type });
        });

        // AveragePrice configuration (replaces ItemPriceHistory)
        modelBuilder.Entity<AveragePrice>(entity =>
        {
            // Unique composite index for preventing duplicate aggregates
            entity.HasIndex(e => new { e.ItemTag, e.Timestamp, e.Granularity }).IsUnique();
            // Index for querying by item and granularity
            entity.HasIndex(e => new { e.ItemTag, e.Granularity });
            // Index for timestamp-based queries
            entity.HasIndex(e => e.Timestamp);
        });

        // FlipOpportunity configuration
        modelBuilder.Entity<FlipOpportunity>(entity =>
        {
            entity.HasIndex(e => e.AuctionUuid);
            entity.HasIndex(e => e.DetectedAt);
            entity.HasIndex(e => e.ProfitMarginPercent);
        });

        // NBTLookup configuration
        modelBuilder.Entity<NBTLookup>(entity =>
        {
            // Composite index for filtering by key and numeric value (e.g., stars)
            entity.HasIndex(e => new { e.Key, e.ValueNumeric });
            // Composite index for filtering by key and string value (e.g., pet skins)
            entity.HasIndex(e => new { e.Key, e.ValueString });
            // Index on auction for efficient joins
            entity.HasIndex(e => e.AuctionId);
        });
    }
}
