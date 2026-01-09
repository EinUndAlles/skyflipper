using System.ComponentModel.DataAnnotations;

namespace SkyFlipperSolo.Models;

/// <summary>
/// Stores compressed NBT data for auctions.
/// Based on NBTLookup.Data in Coflnet reference project.
/// </summary>
public class NbtData
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Compressed NBT data as byte array.
    /// Increased from 1000 to 10000 to handle complex items (many enchants/gems/stars).
    /// </summary>
    [MaxLength(10000)]
    public byte[] Data { get; set; } = Array.Empty<byte>();
}
