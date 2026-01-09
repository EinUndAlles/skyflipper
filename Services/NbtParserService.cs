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
    private readonly NBTKeyService _nbtKeyService;

    public NbtParserService(ILogger<NbtParserService> logger, NBTKeyService nbtKeyService)
    {
        _logger = logger;
        _nbtKeyService = nbtKeyService;
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
            // Store raw NBT bytes for later NbtData/NBTLookup creation
            auction.RawNbtBytes = hypixelAuction.ItemBytes;
            
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

        // Extract reforge (for weapons/armor)
        var reforgeStr = extraTag.Get<NbtString>("modifier")?.StringValue;
        if (!string.IsNullOrEmpty(reforgeStr) && Enum.TryParse<Reforge>(reforgeStr, true, out var reforge))
        {
            auction.Reforge = reforge;
        }

        // Extract anvil uses (important for value calculation)
        if (extraTag.TryGet("anvil_uses", out NbtTag? anvilTag))
        {
            auction.AnvilUses = anvilTag switch
            {
                NbtInt i => (short)i.IntValue,
                NbtShort s => s.ShortValue,
                NbtByte b => (short)b.ByteValue,
                _ => 0
            };
        }

        // Extract flattened NBT data
        var flatNbt = FlattenNbtData(extraTag);
        if (flatNbt.Count > 0)
        {
            auction.FlatenedNBTJson = System.Text.Json.JsonSerializer.Serialize(flatNbt);
        }

        // Extract item count (for stacked items)
        auction.Count = GetItemCount(root);

        // Extract item creation timestamp (check both locations)
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
        // Also check root-level timestamp (Coflnet reference checks both)
        else if (root.TryGet("timestamp", out NbtTag? rootTimestamp) && rootTimestamp is NbtLong rootTs)
        {
            auction.ItemCreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(rootTs.LongValue).UtcDateTime;
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

        if (!extraTag.TryGet("enchantments", out NbtTag? enchTag))
        {
            return enchantments;
        }

        if (enchTag is not NbtCompound enchCompound)
        {
            return enchantments;
        }

        // Use Names property like reference implementation (line 1071 in dev/Data/NBT.cs)
        foreach (var name in enchCompound.Names)
        {
            if (Enum.TryParse<EnchantmentType>(name, true, out var enchType))
            {
                // Get value using Get<NbtInt> like reference (line 1083)
                var level = (byte)enchCompound.Get<fNbt.NbtInt>(name).IntValue;
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


    /// <summary>
    /// Creates NbtData from NBT compound for storage in database.
    /// </summary>
    public NbtData? CreateNbtData(NbtCompound extraTag)
    {
        try
        {
            var nbtFile = new NbtFile(extraTag);
            using var ms = new MemoryStream();
            nbtFile.SaveToStream(ms, NbtCompression.None); // No compression (NBT format already efficient)
            return new NbtData { Data = ms.ToArray() };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create NbtData");
            return null;
        }
    }

    /// <summary>
    /// Extracts item count from NBT (important for stacked items).
    /// </summary>
    private int GetItemCount(NbtCompound root)
    {
        // New format (1.20.5+)
        if (root.TryGet("count", out NbtTag? countTag))
        {
            if (countTag is NbtByte countByte)
                return countByte.ByteValue;
            if (countTag is NbtInt countInt)
                return countInt.IntValue;
        }
        
        // Old format
        if (root.TryGet("Count", out countTag))
        {
            if (countTag is NbtByte countByteOld)
                return countByteOld.ByteValue;
            if (countTag is NbtInt countIntOld)
                return countIntOld.IntValue;
        }
        
        return 1; // Default to 1
    }

    /// <summary>
    /// Creates indexed NBT lookup entries for 60+ important attributes.
    /// Based on Coflnet reference for comprehensive attribute extraction.
    /// Uses NBT key normalization for storage efficiency.
    /// </summary>
    public async Task<List<NBTLookup>> CreateLookupAsync(NbtCompound extraTag, int auctionId)
    {
        var lookups = new List<NBTLookup>();

        // Helper to add numeric lookup
        async Task AddNumeric(string keyName, long value)
        {
            var keyId = await _nbtKeyService.GetOrCreateKeyId(keyName);
            lookups.Add(new NBTLookup { AuctionId = auctionId, KeyId = keyId, ValueNumeric = value });
        }

        // Helper to add string lookup
        async Task AddString(string keyName, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            var keyId = await _nbtKeyService.GetOrCreateKeyId(keyName);
            lookups.Add(new NBTLookup { AuctionId = auctionId, KeyId = keyId, ValueString = value });
        }

        // ==== BASIC ATTRIBUTES ====
        var basicNumeric = new[] { "upgrade_level", "hot_potato_count", "rarity_upgrades", 
            "anvil_uses", "exp", "candyUsed", "art_of_war_count", "mana_pool", "breaker",
            "farming_for_dummies_count", "winning_bid" };

        foreach (var key in basicNumeric)
        {
            if (extraTag.TryGet(key, out NbtTag? tag))
            {
                long? value = tag switch
                {
                    NbtInt i => i.IntValue,
                    NbtLong l => l.LongValue,
                    NbtShort s => s.ShortValue,
                    NbtByte b => b.ByteValue,
                    _ => null
                };
                if (value.HasValue)
                    await AddNumeric(key, value.Value);
            }
        }

        // ==== DUNGEON ====
        var dungeonNumeric = new[] { "dungeon_item_level", "stars", "dungeon_skill_req", "dungeon_paper_id" };
        foreach (var key in dungeonNumeric)
        {
            if (extraTag.TryGet(key, out NbtTag? tag) && tag is NbtInt intTag)
                await AddNumeric(key, intTag.IntValue);
        }

        // ==== STRING ATTRIBUTES ====
        var stringKeys = new[] { "skin", "heldItem", "ability_scroll", "cake_owner", 
            "party_hat_color", "spray", "repelling_color", "captured_player", "leaderboard_player", "mob_id" };

        foreach (var key in stringKeys)
        {
            if (extraTag.TryGet(key, out NbtTag? tag) && tag is NbtString strTag)
                await AddString(key, strTag.StringValue);
        }

        // ==== GEMS WITH QUALITY + UUID ====
        if (extraTag.TryGet("gems", out NbtTag? gemsTag) && gemsTag is NbtCompound gems)
        {
            var slotPrefixes = new[] { "COMBAT_0", "COMBAT_1", "DEFENSIVE_0", "UNIVERSAL_0", "OFFENSIVE_0" };
            
            foreach (var slot in slotPrefixes)
            {
                if (gems.TryGet(slot, out NbtTag? slotTag))
                {
                    if (slotTag is NbtString gemType)
                    {
                        await AddString(slot, gemType.StringValue);
                    }
                    else if (slotTag is NbtCompound gemData)
                    {
                        if (gemData.TryGet("type", out NbtTag? typeTag) && typeTag is NbtString gemTypeStr)
                            await AddString($"{slot}_gem", gemTypeStr.StringValue);
                        
                        if (gemData.TryGet("quality", out NbtTag? qualityTag) && qualityTag is NbtString qualityStr)
                            await AddString($"{slot}_quality", qualityStr.StringValue);
                        
                        if (gemData.TryGet("uuid", out NbtTag? uuidTag) && uuidTag is NbtString uuidStr)
                            await AddString($"{slot}_uuid", uuidStr.StringValue);
                    }
                }
            }
        }

        // ==== PET INFO UNWRAPPING ====
        if (extraTag.TryGet("petInfo", out NbtTag? petInfoTag) && petInfoTag is NbtString petInfoStr)
        {
            try
            {
                var petData = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(petInfoStr.StringValue);
                
                if (petData.TryGetProperty("exp", out var exp) && exp.TryGetDouble(out var expVal))
                    await AddNumeric("pet_exp", (long)expVal);
                
                if (petData.TryGetProperty("tier", out var tier))
                    await AddString("pet_tier", tier.GetString() ?? "");
                
                if (petData.TryGetProperty("heldItem", out var heldItem))
                    await AddString("pet_held_item", heldItem.GetString() ?? "");
                
                if (petData.TryGetProperty("skin", out var skin))
                    await AddString("pet_skin", skin.GetString() ?? "");
            }
            catch { /* Ignore malformed pet data */ }
        }

        // ==== POTIONS ====
        var potionKeys = new[] { "potion", "potion_type", "potion_name", "effect", "enhanced" };
        foreach (var key in potionKeys)
        {
            if (extraTag.TryGet(key, out NbtTag? tag) && tag is NbtString strTag)
                await AddString(key, strTag.StringValue);
        }

        if (extraTag.TryGet("duration", out NbtTag? durationTag) && durationTag is NbtInt durationInt)
            await AddNumeric("duration", durationInt.IntValue);
        
        if (extraTag.TryGet("level", out NbtTag? levelTag) && levelTag is NbtInt levelInt)
            await AddNumeric("level", levelInt.IntValue);

        // ==== DRILLS ====
        var drillParts = new[] { "drill_part_engine", "drill_part_fuel_tank", "drill_part_upgrade_module" };
        foreach (var part in drillParts)
        {
            if (extraTag.TryGet(part, out NbtTag? tag) && tag is NbtString partStr)
                await AddString(part, partStr.StringValue);
        }

        // ==== BACKPACKS ====
        var backpackSizes = new[] { "small", "medium", "large", "greater", "jumbo" };
        foreach (var size in backpackSizes)
        {
            var key = $"{size}_backpack_data";
            if (extraTag.TryGet(key, out NbtTag? tag) && tag is NbtByteArray)
                await AddNumeric(key, 1);
        }

        // ==== NECROMANCER SOULS UNWRAPPING ====
        if (extraTag.TryGet("necromancer_souls", out NbtTag? soulsTag) && soulsTag is NbtCompound souls)
        {
            foreach (var soul in souls)
            {
                long? value = soul switch
                {
                    NbtInt i => i.IntValue,
                    NbtLong l => l.LongValue,
                    _ => null
                };
                if (value.HasValue)
                    await AddNumeric($"soul_{soul.Name}", value.Value);
            }
        }

        // ==== RUNES UNWRAPPING ====
        if (extraTag.TryGet("runes", out NbtTag? runesTag) && runesTag is NbtCompound runes)
        {
            foreach (var rune in runes)
            {
                if (rune is NbtInt runeLevel)
                    await AddNumeric($"RUNE_{rune.Name}", runeLevel.IntValue);
            }
        }

        // ==== KILL/COMPLETION STATS ====
        var statKeys = new[] { "zombie_kills", "ender_dragon_kills", "enderman_kills", 
            "floor_completions", "master_completions" };
        foreach (var key in statKeys)
        {
            if (extraTag.TryGet(key, out NbtTag? tag) && tag is NbtInt intTag)
                await AddNumeric(key, intTag.IntValue);
        }

        return lookups;
    }

    /// <summary>
    /// Public helper method to extract ExtraAttributes compound from raw NBT bytes.
    /// Used by FlipperService to create NbtData and NBTLookups after auction is saved.
    /// </summary>
    public NbtCompound? GetExtraTagFromBytes(string? itemBytes)
    {
        if (string.IsNullOrEmpty(itemBytes)) return null;

        try
        {
            var bytes = Convert.FromBase64String(itemBytes);
            var nbtFile = ParseNbtFile(bytes);
            if (nbtFile == null) return null;

            var root = nbtFile.RootTag.Get<NbtList>("i")?.Get<NbtCompound>(0);
            if (root == null)
                root = nbtFile.RootTag as NbtCompound;
            if (root == null) return null;

            return GetExtraTag(root);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to extract ExtraTag from NBT bytes");
            return null;
        }
    }

}
