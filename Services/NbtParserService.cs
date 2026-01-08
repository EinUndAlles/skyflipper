using System.IO.Compression;
using fNbt;
using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services;

/// <summary>
/// Service for parsing item NBT data from the Hypixel API.
/// Based on: dev/Data/NBT.cs FillDetails, Enchantments methods.
/// </summary>
public class NbtParserService
{
    private readonly ILogger<NbtParserService> _logger;

    public NbtParserService(ILogger<NbtParserService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Converts a HypixelAuction to our internal Auction model,
    /// parsing the NBT data to extract item details.
    /// </summary>
    public Auction ParseAuction(HypixelAuction hypixelAuction)
    {
        var auction = new Auction
        {
            Uuid = hypixelAuction.Uuid.Replace("-", ""),
            ItemName = hypixelAuction.ItemName,
            StartingBid = hypixelAuction.StartingBid,
            HighestBidAmount = hypixelAuction.HighestBidAmount,
            Bin = hypixelAuction.Bin,
            Start = DateTime.SpecifyKind(hypixelAuction.Start, DateTimeKind.Utc),
            End = DateTime.SpecifyKind(hypixelAuction.End, DateTimeKind.Utc),
            AuctioneerId = hypixelAuction.Auctioneer,
            FetchedAt = DateTime.UtcNow
        };

        // Generate UId from UUID for faster lookups
        auction.UId = GetUIdFromUuid(auction.Uuid);

        // Parse tier
        if (!string.IsNullOrEmpty(hypixelAuction.Tier) && 
            Enum.TryParse<Tier>(hypixelAuction.Tier, true, out var tier))
        {
            auction.Tier = tier;
        }

        // Parse category
        if (!string.IsNullOrEmpty(hypixelAuction.Category) && 
            Enum.TryParse<Category>(hypixelAuction.Category, true, out var category))
        {
            auction.Category = category;
        }

        // Parse NBT data
        try
        {
            ParseNbtData(auction, hypixelAuction.ItemBytes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse NBT for auction {Uuid}", auction.Uuid);
        }

        return auction;
    }

    private void ParseNbtData(Auction auction, string itemBytes)
    {
        if (string.IsNullOrEmpty(itemBytes)) return;

        var bytes = Convert.FromBase64String(itemBytes);
        var nbtFile = ParseNbtFile(bytes);
        if (nbtFile == null)
        {
            _logger.LogDebug("ParseNbtFile returned null for auction {Uuid}", auction.Uuid);
            return;
        }

        // Hypixel NBT structure: root -> "i" (list) -> first item (compound)
        var root = nbtFile.RootTag.Get<NbtList>("i")?.Get<NbtCompound>(0);
        if (root == null)
        {
            root = nbtFile.RootTag as NbtCompound;
            _logger.LogDebug("No 'i' list in NBT, using root directly for auction {Uuid}", auction.Uuid);
        }
        if (root == null)
        {
            _logger.LogDebug("No root compound found for auction {Uuid}", auction.Uuid);
            return;
        }

        // Get the extra attributes tag
        var extraTag = GetExtraTag(root);
        if (extraTag == null)
        {
            _logger.LogDebug("ExtraAttributes not found for auction {Uuid}, root tags: {Tags}", 
                auction.Uuid, string.Join(", ", root.Tags.Select(t => t.Name)));
            return;
        }

        // Extract item ID (tag) - this is the Skyblock internal ID
        var itemId = extraTag.Get<NbtString>("id")?.StringValue;
        if (!string.IsNullOrEmpty(itemId))
        {
            auction.Tag = itemId;
        }
        else
        {
            _logger.LogDebug("No 'id' in ExtraAttributes for auction {Uuid}, tags: {Tags}",
                auction.Uuid, string.Join(", ", extraTag.Tags.Select(t => t.Name)));
        }

        // Extract enchantments
        auction.Enchantments = ParseEnchantments(extraTag);

        // Extract reforge
        if (extraTag.TryGet("modifier", out NbtTag? modifierTag) && modifierTag is NbtString modifierStr)
        {
            if (Enum.TryParse<Reforge>(modifierStr.StringValue, true, out var reforge))
            {
                auction.Reforge = reforge;
            }
        }

        // Extract flattened NBT data (stars, gems, hot potato books, etc.)
        var flatNbt = FlattenNbtData(extraTag);
        if (flatNbt.Count > 0)
        {
            auction.FlatenedNBTJson = System.Text.Json.JsonSerializer.Serialize(flatNbt);
        }

        // Extract item creation date
        if (extraTag.TryGet("timestamp", out NbtTag? timestampTag))
        {
            if (timestampTag is NbtLong timestampLong)
            {
                auction.ItemCreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(timestampLong.LongValue).UtcDateTime;
            }
            else if (timestampTag is NbtString timestampStr && 
                     DateTime.TryParse(timestampStr.StringValue, out var parsedDate))
            {
                auction.ItemCreatedAt = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
            }
        }
        
        // Extract Skull Texture (for pets/talismans/skulls)
        // Standard Minecraft saves SkullOwner in the "tag" compound
        if (root.TryGet("tag", out NbtTag? tagNbt) && tagNbt is NbtCompound tagCompound)
        {
             if (tagCompound.TryGet("SkullOwner", out NbtTag? skullOwnerTag) && skullOwnerTag is NbtCompound skullOwner)
             {
                 ExtractTextureFromSkullOwner(auction, skullOwner);
             }
        }
        else if (extraTag.TryGet("SkullOwner", out NbtTag? extraSkullOwner) && extraSkullOwner is NbtCompound extraSkullComp)
        {
             // Fallback to ExtraAttributes just in case
             ExtractTextureFromSkullOwner(auction, extraSkullComp);
        }
    }

    private NbtFile? ParseNbtFile(byte[] bytes)
    {
        try
        {
            // Original project uses LoadFromBuffer with GZip compression
            var file = new NbtFile();
            file.LoadFromBuffer(bytes, 0, bytes.Length, NbtCompression.GZip);
            return file;
        }
        catch
        {
            // Fall back to AutoDetect if GZip fails
            try
            {
                var file = new NbtFile();
                file.LoadFromBuffer(bytes, 0, bytes.Length, NbtCompression.AutoDetect);
                return file;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to parse NBT file");
                return null;
            }
        }
    }


    private NbtCompound? GetExtraTag(NbtCompound root)

    {
        // Try new format first (1.20.5+)
        if (root.TryGet("components", out NbtTag? components) && components is NbtCompound compoundComponents)
        {
            if (compoundComponents.TryGet("minecraft:custom_data", out NbtTag? customData) && 
                customData is NbtCompound customDataCompound)
            {
                return customDataCompound;
            }
        }

        // Fall back to old format
        if (root.TryGet("tag", out NbtTag? tag) && tag is NbtCompound tagCompound)
        {
            if (tagCompound.TryGet("ExtraAttributes", out NbtTag? extra) && extra is NbtCompound extraCompound)
            {
                return extraCompound;
            }
        }

        return null;
    }

    private List<Enchantment> ParseEnchantments(NbtCompound extraTag)
    {
        var enchantments = new List<Enchantment>();

        if (!extraTag.TryGet("enchantments", out NbtTag? enchTag) || enchTag is not NbtCompound enchCompound)
        {
            return enchantments;
        }

        foreach (var tag in enchCompound)
        {
            if (Enum.TryParse<EnchantmentType>(tag.Name, true, out var enchType))
            {
                byte level = 0;
                if (tag is NbtInt intTag) level = (byte)intTag.IntValue;
                else if (tag is NbtShort shortTag) level = (byte)shortTag.ShortValue;
                else if (tag is NbtByte byteTag) level = byteTag.ByteValue;

                enchantments.Add(new Enchantment(enchType, level));
            }
        }

        return enchantments;
    }

    private Dictionary<string, string> FlattenNbtData(NbtCompound extraTag)
    {
        var flat = new Dictionary<string, string>();

        // Key attributes to extract
        var keysToExtract = new[]
        {
            "dungeon_item_level", // Stars
            "upgrade_level", // Stars (alternative)
            "rarity_upgrades", // Recombobulator
            "hot_potato_count", // Hot potato books
            "art_of_war_count", // Art of war
            "farming_for_dummies_count",
            "ethermerge",
            "skin",
            "ability_scroll",
            "unlocked_slots",
            "edition",
            "winning_bid", // Midas
            "candyUsed", // Pet candy
            "heldItem", // Pet item
            "exp", // Pet exp
            "level", // Pet level
        };

        foreach (var key in keysToExtract)
        {
            if (extraTag.TryGet(key, out NbtTag? tag))
            {
                flat[key] = GetTagValue(tag);
            }
        }

        // Extract gem slots
        if (extraTag.TryGet("gems", out NbtTag? gemsTag) && gemsTag is NbtCompound gems)
        {
            foreach (var gem in gems)
            {
                flat[gem.Name] = GetTagValue(gem);
            }
        }

        // Extract attributes (Kuudra gear)
        if (extraTag.TryGet("attributes", out NbtTag? attrsTag) && attrsTag is NbtCompound attrs)
        {
            foreach (var attr in attrs)
            {
                flat[attr.Name] = GetTagValue(attr);
            }
        }

        return flat;
    }

    private string GetTagValue(NbtTag tag)
    {
        return tag switch
        {
            NbtString str => str.StringValue,
            NbtInt i => i.IntValue.ToString(),
            NbtLong l => l.LongValue.ToString(),
            NbtShort s => s.ShortValue.ToString(),
            NbtByte b => b.ByteValue.ToString(),
            NbtDouble d => d.DoubleValue.ToString(),
            NbtFloat f => f.FloatValue.ToString(),
            _ => tag.ToString() ?? ""
        };
    }

    private long GetUIdFromUuid(string uuid)
    {
        if (string.IsNullOrEmpty(uuid) || uuid.Length < 12)
            return 0;

        try
        {
            return Convert.ToInt64(uuid.Substring(0, 12), 16);
        }
        catch
        {
            return 0;
        }
    }


    private void ExtractTextureFromSkullOwner(Auction auction, NbtCompound skullOwner)
    {
        if (skullOwner.TryGet("Properties", out NbtTag? propertiesTag) && propertiesTag is NbtCompound properties)
        {
            if (properties.TryGet("textures", out NbtTag? texturesTag) && texturesTag is NbtList texturesList)
            {
                var textureEntry = texturesList.Get<NbtCompound>(0);
                if (textureEntry != null && textureEntry.TryGet("Value", out NbtTag? valueTag) && valueTag is NbtString valueStr)
                {
                    try 
                    {
                        var base64 = valueStr.StringValue;
                        // Standardize Base64
                        base64 = base64.Replace('-', '+').Replace('_', '/');
                        switch (base64.Length % 4)
                        {
                            case 2: base64 += "=="; break;
                            case 3: base64 += "="; break;
                        }

                        var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64));
                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("textures", out var texturesProp) &&
                            texturesProp.TryGetProperty("SKIN", out var skinProp) &&
                            skinProp.TryGetProperty("url", out var urlProp))
                        {
                             auction.Texture = urlProp.GetString();
                        }
                    }
                    catch
                    {
                        // Ignore parsing errors
                    }
                }
            }
        }
    }

}
