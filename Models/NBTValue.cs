using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SkyFlipperSolo.Models;

/// <summary>
/// Stores unique NBT string values for deduplication.
/// Based on Coflnet.Sky.Core.NBTValue pattern.
/// Instead of storing "DRAGON_NEON" 10,000 times in NBTLookup.ValueString,
/// we store it once here and reference it by ID for 90% storage savings.
/// </summary>
public class NBTValue
{
    /// <summary>
    /// Unique identifier for this value.
    /// </summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>
    /// The NBT key this value belongs to.
    /// Helps with deduplication - same value for different keys are stored separately.
    /// </summary>
    public short KeyId { get; set; }
    
    /// <summary>
    /// Navigation property to NBTKey.
    /// </summary>
    public NBTKey NBTKey { get; set; } = null!;

    /// <summary>
    /// The actual string value stored once.
    /// Examples: "DRAGON_NEON", "PET_ITEM_TIER_BOOST", "PERFECT"
    /// </summary>
    [MaxLength(200)]
    [Required]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Navigation property - all lookups using this value.
    /// </summary>
    public List<NBTLookup> NBTLookups { get; set; } = new();
}
