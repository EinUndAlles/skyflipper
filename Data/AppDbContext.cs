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
    public DbSet<NBTKey> NBTKeys { get; set; } // For key name normalization
    public DbSet<NBTValue> NBTValues { get; set; } // For string value deduplication
    public DbSet<BidRecord> BidRecords { get; set; }
    public DbSet<ItemDetails> ItemDetails { get; set; }
    public DbSet<AlternativeName> AlternativeNames { get; set; }
    public DbSet<FlipHitCount> FlipHitCounts { get; set; }

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
            entity.HasIndex(e => e.ItemUid); // For UID deduplication
            entity.HasIndex(e => new { e.Tag, e.Tier, e.Reforge });

            // Composite indexes for price history and flip detection
            entity.HasIndex(e => new { e.Tag, e.End });
            entity.HasIndex(e => new { e.Bin, e.Status, e.End });
            entity.HasIndex(e => new { e.Status, e.End });
            
            // Index for sold auction queries (price aggregation)
            entity.HasIndex(e => new { e.Status, e.SoldAt });

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
            entity.HasIndex(e => new { e.CacheKey, e.Timestamp, e.Granularity }).IsUnique();
            // Index for querying by cache key and granularity
            entity.HasIndex(e => new { e.CacheKey, e.Granularity });
            // Index for timestamp-based queries
            entity.HasIndex(e => e.Timestamp);
            // Keep ItemTag index for backward compatibility
            entity.HasIndex(e => new { e.ItemTag, e.Granularity });
            // Index for volume filtering (used in flip detection)
            entity.HasIndex(e => new { e.Granularity, e.Timestamp, e.Volume });
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
            // Relationship to NBTKey
            entity.HasOne(l => l.NBTKey)
                .WithMany(k => k.NBTLookups)
                .HasForeignKey(l => l.KeyId)
                .OnDelete(DeleteBehavior.Restrict);

            // NEW: Composite indexes for KeyId-based queries
            entity.HasIndex(e => new { e.KeyId, e.ValueNumeric });
            entity.HasIndex(e => new { e.KeyId, e.ValueString });

            // OLD: Keep for migration compatibility (will be removed after migration)
            entity.HasIndex(e => new { e.Key, e.ValueNumeric });
            entity.HasIndex(e => new { e.Key, e.ValueString });

            // Index on auction for efficient joins
            entity.HasIndex(e => e.AuctionId);
        });

        // NBTKey configuration
        modelBuilder.Entity<NBTKey>(entity =>
        {
            entity.HasIndex(k => k.KeyName).IsUnique();
        });

        // NBTValue configuration
        modelBuilder.Entity<NBTValue>(entity =>
        {
            // Relationship to NBTKey
            entity.HasOne(v => v.NBTKey)
                .WithMany()
                .HasForeignKey(v => v.KeyId)
                .OnDelete(DeleteBehavior.Restrict);

            // Unique constraint: same value for same key stored once
            entity.HasIndex(v => new { v.KeyId, v.Value }).IsUnique();
        });

        // NBTLookup - add ValueId relationship
        modelBuilder.Entity<NBTLookup>()
            .HasOne(l => l.NBTValue)
            .WithMany(v => v.NBTLookups)
            .HasForeignKey(l => l.ValueId)
            .OnDelete(DeleteBehavior.Restrict);

        // NEW: Composite index for ValueId queries
        modelBuilder.Entity<NBTLookup>()
            .HasIndex(e => new { e.KeyId, e.ValueId });

        // BidRecord configuration
        modelBuilder.Entity<BidRecord>(entity =>
        {
            entity.HasIndex(b => b.AuctionId);
            entity.HasIndex(b => b.BidderId);
            entity.HasIndex(b => b.Timestamp);
        });

        // ItemDetails configuration
        modelBuilder.Entity<ItemDetails>(entity =>
        {
            entity.HasIndex(i => i.Tag).IsUnique();
            entity.HasIndex(i => i.LastSeen);
        });

        // AlternativeName configuration
        modelBuilder.Entity<AlternativeName>(entity =>
        {
            entity.HasIndex(a => a.Name);
        });

        // FlipHitCount configuration
        modelBuilder.Entity<FlipHitCount>(entity =>
        {
            entity.HasIndex(e => e.CacheKey).IsUnique();
            entity.HasIndex(e => e.LastHitAt);
            entity.HasIndex(e => e.HitCount);
        });
    }
}
