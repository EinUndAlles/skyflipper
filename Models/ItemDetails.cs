using System.ComponentModel.DataAnnotations;

namespace SkyFlipperSolo.Models;

/// <summary>
/// Stores item metadata beyond NBT data.
/// Tracks alternative names, descriptions, icons, and fallback tier/category.
/// Based on Coflnet.Sky.Commands.MC.ItemDetails pattern.
/// </summary>
public class ItemDetails
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Unique skyblock item tag (e.g., "ASPECT_OF_THE_END").
    /// </summary>
    [MaxLength(100)]
    [Required]
    public string Tag { get; set; } = string.Empty;

    /// <summary>
    /// Display name as it appears in-game.
    /// </summary>
    [MaxLength(200)]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Full item lore/description text.
    /// </summary>
    [MaxLength(5000)]
    public string? Description { get; set; }

    /// <summary>
    /// URL to item icon/texture.
    /// </summary>
    [MaxLength(500)]
    public string? IconUrl { get; set; }

    /// <summary>
    /// Minecraft item type (e.g., "skull", "sword", "bow").
    /// </summary>
    [MaxLength(50)]
    public string? MinecraftType { get; set; }

    /// <summary>
    /// Fallback tier if NBT parsing fails.
    /// </summary>
    public Tier? FallbackTier { get; set; }

    /// <summary>
    /// Fallback category if NBT parsing fails.
    /// </summary>
    public Category? FallbackCategory { get; set; }

    /// <summary>
    /// When this item was last seen in an auction.
    /// </summary>
    public DateTime LastSeen { get; set; }

    /// <summary>
    /// Alternative/searchable names for this item.
    /// </summary>
    public List<AlternativeName> AlternativeNames { get; set; } = new();
}

/// <summary>
/// Alternative/searchable names for items (e.g., "AOTE" for "Aspect of the End").
/// </summary>
public class AlternativeName
{
    [Key]
    public int Id { get; set; }

    public int ItemDetailsId { get; set; }
    public ItemDetails ItemDetails { get; set; } = null!;

    [MaxLength(100)]
    [Required]
    public string Name { get; set; } = string.Empty;
}
