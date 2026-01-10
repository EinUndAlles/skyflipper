using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace SkyFlipperSolo.Models;

/// <summary>
/// Stores NBT key names with integer IDs for efficient storage.
/// Based on Coflnet.Sky.Commands.MC.NBTKey approach.
/// Instead of storing "dungeon_item_level" 5M times, we store ID 42.
/// </summary>
public class NBTKey
{
    /// <summary>
    /// Unique identifier for this NBT key.
    /// Using short (Int16) to save space - supports up to 32,767 unique keys.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public short Id { get; set; }

    /// <summary>
    /// The actual NBT key name (e.g., "dungeon_item_level", "hot_potato_count").
    /// Indexed for fast lookups when getting/creating key IDs.
    /// </summary>
    [MaxLength(100)]
    [Required]
    public string KeyName { get; set; } = string.Empty;

    /// <summary>
    /// Navigation property - all lookups using this key.
    /// </summary>
    [JsonIgnore] // Prevent circular reference during serialization
    public List<NBTLookup> NBTLookups { get; set; } = new();
}
