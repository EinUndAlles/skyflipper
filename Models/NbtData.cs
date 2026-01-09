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
    /// Compressed NBT byte array (using NBT format, no additional compression needed)
    /// </summary>
    [MaxLength(1000)]
    public byte[] Data { get; set; } = Array.Empty<byte>();
}
