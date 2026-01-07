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

            entity.HasMany(e => e.Enchantments)
                .WithOne(e => e.Auction)
                .HasForeignKey(e => e.AuctionId)
                .OnDelete(DeleteBehavior.Cascade);
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
    }
}
