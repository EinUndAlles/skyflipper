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
    private readonly NBTValueService _nbtValueService;

    public NbtParserService(ILogger<NbtParserService> logger, NBTKeyService nbtKeyService, NBTValueService nbtValueService)
    {
        _logger = logger;
        _nbtKeyService = nbtKeyService;
        _nbtValueService = nbtValueService;
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
            // Store raw NBT bytes for later processing
            if (!string.IsNullOrEmpty(hypixelAuction.ItemBytes))
            {
                auction.RawNbtBytes = hypixelAuction.ItemBytes;
            }

            // Parse NBT data if available
            if (!string.IsNullOrEmpty(hypixelAuction.ItemBytes))
            {
                ParseNbtData(auction, hypixelAuction.ItemBytes);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse NBT for auction {Uuid}", auction.Uuid);
        }

        // Handle bazaar enchanted books ONLY if no ItemBytes (no NBT)
        if (string.IsNullOrEmpty(hypixelAuction.ItemBytes) && auction.Tag == "ENCHANTED_BOOK")
        {
            ParseBazaarEnchantedBook(auction);
        }

        // Tier from lore fallback if tier is still UNKNOWN after parsing
        if (auction.Tier == Tier.UNKNOWN && !string.IsNullOrEmpty(hypixelAuction.ItemLore))
        {
            var loreLines = hypixelAuction.ItemLore.Split('\n');
            auction.Tier = ExtractTierFromLore(loreLines);
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
        // Use composite tag generation for special items (potions, runes, pets, abicase)
        // Reference: dev/Data/NBT.cs ItemIdFromExtra() lines 1104-1131
        var itemId = GetCompositeItemId(extraTag);
        if (!string.IsNullOrEmpty(itemId))
        {
            auction.Tag = itemId;
        }
        else
        {
            _logger.LogDebug("No 'id' in ExtraAttributes for auction {Uuid}, tags: {Tags}",
                auction.Uuid, string.Join(", ", extraTag.Tags.Select(t => t.Name)));
        }

        // Extract item UUID for deduplication
        var itemUuid = extraTag.Get<NbtString>("uuid")?.StringValue;
        if (!string.IsNullOrEmpty(itemUuid))
        {
            auction.ItemUid = itemUuid;
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

    /// <summary>
    /// Generates composite item IDs for special item types.
    /// Reference: dev/Data/NBT.cs ItemIdFromExtra() lines 1104-1131
    /// 
    /// Examples:
    /// - PET → PET_DRAGON, PET_WOLF
    /// - POTION → POTION_SPEED, POTION_STRENGTH
    /// - *_RUNE / UNIQUE_RUNE → UNIQUE_RUNE_ICE, COMMON_RUNE_BLOOD
    /// - ABICASE → ABICASE_model_name
    /// </summary>
    private string? GetCompositeItemId(NbtCompound extraTag)
    {
        var id = extraTag.Get<NbtString>("id")?.StringValue;
        if (string.IsNullOrEmpty(id))
            return null;

        // PET → PET_{petType}
        if (id == "PET")
        {
            id = GetPetId(extraTag, id);
        }
        // POTION → POTION_{potionType}
        else if (id == "POTION")
        {
            if (extraTag.TryGet("potion", out NbtTag? potionTag) && potionTag is NbtString potionStr)
            {
                id = $"{id}_{potionStr.StringValue}";
            }
        }
        // *RUNE → {base}_{runeType} (e.g., UNIQUE_RUNE_ICE)
        else if (id.EndsWith("RUNE"))
        {
            if (extraTag.TryGet("runes", out NbtTag? runesTag) && runesTag is NbtCompound runes)
            {
                var runeType = runes.Tags?.FirstOrDefault()?.Name;
                if (!string.IsNullOrEmpty(runeType))
                {
                    id = $"{id}_{runeType}";
                }
            }
        }
        // ABICASE → ABICASE_{model}
        else if (id == "ABICASE")
        {
            if (extraTag.TryGet("model", out NbtTag? modelTag) && modelTag is NbtString modelStr)
            {
                id = $"{id}_{modelStr.StringValue}";
            }
        }

        return id;
    }

    /// <summary>
    /// Extracts pet type from petInfo to create composite tag.
    /// Reference: dev/Data/NBT.cs GetPetId() lines 1133-1156
    /// </summary>
    private string GetPetId(NbtCompound extraTag, string baseId)
    {
        try
        {
            // petInfo is typically a JSON string
            if (extraTag.TryGet("petInfo", out NbtTag? petInfoTag))
            {
                if (petInfoTag is NbtString petInfoStr)
                {
                    var petData = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(petInfoStr.StringValue);
                    if (petData.TryGetProperty("type", out var typeElement))
                    {
                        var petType = typeElement.GetString();
                        if (!string.IsNullOrEmpty(petType))
                        {
                            return $"{baseId}_{petType}";
                        }
                    }
                }
                // petInfo can also be a compound (less common)
                else if (petInfoTag is NbtCompound petInfoCompound)
                {
                    if (petInfoCompound.TryGet("type", out NbtTag? typeTag) && typeTag is NbtString typeStr)
                    {
                        return $"{baseId}_{typeStr.StringValue}";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to extract pet type from petInfo");
        }

        return $"{baseId}_unknown";
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

    /// <summary>
    /// Flattens NBT data for cache key generation and price matching.
    /// Reference: dev/Data/NBT.cs FlattenNbtData() lines 493-612, ValidKeys lines 271-339
    /// </summary>
    private Dictionary<string, string> FlattenNbtData(NbtCompound extraTag)
    {
        var flat = new Dictionary<string, string>();

        // ===== BASIC ATTRIBUTES =====
        // Reference: dev/Data/NBT.cs ValidKeys + common value-affecting attributes
        var keysToExtract = new[]
        {
            // Core item attributes
            "dungeon_item_level", // Stars
            "upgrade_level",      // Stars (alternative)
            "rarity_upgrades",    // Recombobulator
            "hot_potato_count",   // Hot potato books
            "art_of_war_count",   // Art of war
            "farming_for_dummies_count",
            "ethermerge",
            "edition",
            "winning_bid",        // Midas
            "uuid",               // Item UID for deduplication
            
            // Pet attributes
            "candyUsed",
            "heldItem",
            "exp",
            "level",
            "skin",
            
            // Potion attributes - Reference: ValidKeys lines 273-285
            "potion",
            "potion_type",
            "potion_name",
            "splash",
            "extended",
            "enhanced",
            "effect",
            "duration",
            
            // Cosmetic attributes - Reference: ValidKeys lines 279-301
            "party_hat_color",
            "backpack_color",
            "repelling_color",
            "spray",
            
            // Special item attributes - Reference: ValidKeys lines 284-295
            "captured_player",     // Cake souls
            "leaderboard_player",
            "mob_id",
            "event",
            "initiator_player",
            "cake_owner",
            
            // Talisman/accessory attributes
            "talisman_enrichment",
            
            // Dye
            "dye_item",
            
            // Tool modes
            "fungi_cutter_mode",
            
            // Dungeon attributes - Reference: ValidKeys lines 275-286
            "dungeon_skill_req",
            "dungeon_paper_id",
            
            // Drill parts - Reference: ValidKeys lines 281-283
            "drill_part_engine",
            "drill_part_fuel_tank",
            "drill_part_upgrade_module",
            
            // Misc
            "last_potion_ingredient",
            "power_ability_scroll",
            "entity_required",
        };

        foreach (var key in keysToExtract)
        {
            if (extraTag.TryGet(key, out NbtTag? tag))
            {
                flat[key] = GetTagValue(tag);
            }
        }

        // ===== ABILITY SCROLL / MIXINS / UNLOCKED SLOTS - String Array Unwrapping =====
        // Reference: dev/Data/NBT.cs UnwarpStringArray() lines 636-651
        ExtractStringArray(extraTag, "ability_scroll", flat);
        ExtractStringArray(extraTag, "mixins", flat);
        ExtractStringArray(extraTag, "unlocked_slots", flat);

        // ===== PERSONAL COMPACTOR/DELETOR SLOTS =====
        // Reference: dev/Data/NBT.cs KeysWithItem lines 348-390
        for (int i = 0; i <= 11; i++)
        {
            var compactKey = $"personal_compact_{i}";
            var compactorKey = $"personal_compactor_{i}";
            var deletorKey = $"personal_deletor_{i}";
            
            if (extraTag.TryGet(compactKey, out NbtTag? compactTag))
                flat[compactKey] = GetTagValue(compactTag);
            if (extraTag.TryGet(compactorKey, out NbtTag? compactorTag))
                flat[compactorKey] = GetTagValue(compactorTag);
            if (i <= 9 && extraTag.TryGet(deletorKey, out NbtTag? deletorTag))
                flat[deletorKey] = GetTagValue(deletorTag);
        }

        // ===== FISHING ROD PARTS =====
        // Reference: dev/Data/NBT.cs UnwrapRodPart() lines 614-634
        ExtractRodPart(extraTag, "sinker", flat);
        ExtractRodPart(extraTag, "line", flat);
        ExtractRodPart(extraTag, "hook", flat);

        // ===== GEM SLOTS WITH QUALITY + UUID =====
        // Reference: dev/Data/NBT.cs lines 536-594
        if (extraTag.TryGet("gems", out NbtTag? gemsTag) && gemsTag is NbtCompound gems)
        {
            foreach (var gem in gems)
            {
                if (gem is NbtString gemStr)
                {
                    // Simple gem slot: COMBAT_0 = "PERFECT"
                    flat[gem.Name] = gemStr.StringValue;
                }
                else if (gem is NbtCompound gemCompound)
                {
                    // Complex gem slot with quality and uuid
                    // Reference: lines 566-580
                    if (gemCompound.TryGet("quality", out NbtTag? qualityTag))
                        flat[gem.Name] = GetTagValue(qualityTag);
                    if (gemCompound.TryGet("uuid", out NbtTag? uuidTag))
                        flat[$"{gem.Name}.uuid"] = GetTagValue(uuidTag);
                }
                else
                {
                    // Fallback for other types
                    flat[gem.Name] = GetTagValue(gem);
                }
            }
        }

        // ===== KUUDRA/CRIMSON ISLE ATTRIBUTES =====
        if (extraTag.TryGet("attributes", out NbtTag? attrsTag) && attrsTag is NbtCompound attrs)
        {
            foreach (var attr in attrs)
            {
                flat[attr.Name] = GetTagValue(attr);
            }
        }

        // ===== RUNES =====
        // Reference: dev/Data/NBT.cs lines 595-602
        // Flattens to RUNE_{runeName}={level} format
        if (extraTag.TryGet("runes", out NbtTag? runesTag) && runesTag is NbtCompound runes)
        {
            foreach (var rune in runes)
            {
                flat[$"RUNE_{rune.Name}"] = GetTagValue(rune);
            }
        }

        // ===== NECROMANCER SOULS =====
        // Reference: dev/Data/NBT.cs UnwrapSouls() lines 670-690
        if (extraTag.TryGet("necromancer_souls", out NbtTag? soulsTag) && soulsTag is NbtList soulsList)
        {
            var soulCounts = new Dictionary<string, int>();
            foreach (var soulEntry in soulsList)
            {
                if (soulEntry is NbtCompound soulCompound)
                {
                    foreach (var kv in soulCompound)
                    {
                        var soulName = GetTagValue(kv);
                        if (!string.IsNullOrEmpty(soulName))
                        {
                            soulCounts.TryGetValue(soulName, out int count);
                            soulCounts[soulName] = count + 1;
                        }
                    }
                }
            }
            foreach (var soul in soulCounts)
            {
                flat[soul.Key] = soul.Value.ToString();
            }
        }

        // ===== EFFECTS LIST (Potions) =====
        // Reference: dev/Data/NBT.cs UnwrapList() lines 653-668
        if (extraTag.TryGet("effects", out NbtTag? effectsTag) && effectsTag is NbtList effectsList)
        {
            foreach (var effectEntry in effectsList)
            {
                if (effectEntry is NbtCompound effectCompound)
                {
                    foreach (var kv in effectCompound)
                    {
                        flat[kv.Name] = GetTagValue(kv);
                    }
                }
            }
        }

        // ===== BACKPACK DATA (just mark presence) =====
        var backpackTypes = new[] { "small", "medium", "large", "greater", "jumbo", "new_year_cake_bag" };
        foreach (var bp in backpackTypes)
        {
            var key = $"{bp}_backpack_data";
            if (extraTag.Contains(key))
                flat[key] = "1";
        }
        if (extraTag.Contains("builder's_wand_data"))
            flat["builder's_wand_data"] = "1";

        return flat;
    }

    /// <summary>
    /// Extracts string array NBT and joins into space-separated string.
    /// Reference: dev/Data/NBT.cs UnwarpStringArray() lines 636-651
    /// </summary>
    private void ExtractStringArray(NbtCompound extraTag, string key, Dictionary<string, string> flat)
    {
        if (!extraTag.TryGet(key, out NbtTag? tag))
            return;

        if (tag is NbtList list)
        {
            var values = new List<string>();
            foreach (var item in list)
            {
                if (item is NbtString str)
                    values.Add(str.StringValue);
                else
                    values.Add(GetTagValue(item));
            }
            if (values.Count > 0)
            {
                values.Sort(); // Reference sorts alphabetically
                flat[key] = string.Join(" ", values);
            }
        }
        else if (tag is NbtString strTag)
        {
            flat[key] = strTag.StringValue;
        }
    }

    /// <summary>
    /// Extracts fishing rod part data (sinker, line, hook).
    /// Reference: dev/Data/NBT.cs UnwrapRodPart() lines 614-634
    /// </summary>
    private void ExtractRodPart(NbtCompound extraTag, string partName, Dictionary<string, string> flat)
    {
        if (!extraTag.TryGet(partName, out NbtTag? partTag))
            return;

        if (partTag is NbtCompound partCompound)
        {
            if (partCompound.TryGet("uuid", out NbtTag? uuidTag))
                flat[$"{partName}.uuid"] = GetTagValue(uuidTag);
            if (partCompound.TryGet("part", out NbtTag? partTypeTag))
                flat[$"{partName}.part"] = GetTagValue(partTypeTag);
        }
        else if (partTag is NbtString partStr)
        {
            flat[partName] = partStr.StringValue;
        }
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

        // Helper to add string lookup with value deduplication
        async Task AddString(string keyName, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            var keyId = await _nbtKeyService.GetOrCreateKeyId(keyName);
            var valueId = await _nbtValueService.GetOrCreateValueId(keyId, value);
            lookups.Add(new NBTLookup 
            { 
                AuctionId = auctionId, 
                KeyId = keyId, 
                ValueId = valueId,
                ValueString = value  // Keep temporarily for migration compatibility
            });
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
            // Backpacks are stored as byte arrays - just mark presence
            if (extraTag.TryGet(key, out NbtTag? tag))
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
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses bazaar enchanted books that don't have NBT data.
    /// Extract enchantment name and level from item name.
    /// Based on Coflnet reference NBT.cs lines 188-243.
    /// Example: "Enchanted Book (Protection V)" → enchantment=PROTECTION, level=5
    /// </summary>
    private void ParseBazaarEnchantedBook(Auction auction)
    {
        // Pattern: "Enchanted Book (Enchantment Name Level)"
        var match = System.Text.RegularExpressions.Regex.Match(
            auction.ItemName ?? "", 
            @"Enchanted Book \((.+?)\s+([IVX]+)\)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        if (!match.Success) return;

        var enchantName = match.Groups[1].Value.ToUpper().Replace(" ", "_");
        var levelRoman = match.Groups[2].Value;

        // Convert roman numerals to int
        var level = RomanToInt(levelRoman);

        // Try to parse enchantment type
        if (Enum.TryParse<EnchantmentType>(enchantName, true, out var enchType))
        {
            auction.Enchantments = new List<Enchantment>
            {
                new Enchantment(enchType, (byte)level)
            };
        }
    }

    /// <summary>
    /// Converts Roman numerals to integers.
    /// Handles subtractive notation: IV=4, IX=9, XL=40, XC=90, CD=400, CM=900
    /// </summary>
    private int RomanToInt(string roman)
    {
        if (string.IsNullOrEmpty(roman)) return 0;

        var romanMap = new Dictionary<char, int>
        {
            {'I', 1}, {'V', 5}, {'X', 10}, {'L', 50}, {'C', 100}, {'D', 500}, {'M', 1000}
        };

        int result = 0;

        for (int i = 0; i < roman.Length; i++)
        {
            int currentValue = romanMap.GetValueOrDefault(roman[i], 0);
            
            // Check if next character has higher value (subtractive notation)
            if (i + 1 < roman.Length && currentValue < romanMap.GetValueOrDefault(roman[i + 1], 0))
            {
                result -= currentValue;
            }
            else
            {
                result += currentValue;
            }
        }

        return result;
    }

    /// <summary>
    /// Extracts tier/rarity from item lore (last line).
    /// Based on Coflnet reference NBT.cs lines 245-269.
    /// Used as fallback when tier enum parsing fails.
    /// </summary>
    private Tier ExtractTierFromLore(string[] loreLines)
    {
        if (loreLines == null || loreLines.Length == 0)
            return Tier.UNKNOWN;

        var lastLine = loreLines.Last().ToUpper();

        if (lastLine.Contains("MYTHIC")) return Tier.MYTHIC;
        if (lastLine.Contains("LEGENDARY")) return Tier.LEGENDARY;
        if (lastLine.Contains("EPIC")) return Tier.EPIC;
        if (lastLine.Contains("RARE")) return Tier.RARE;
        if (lastLine.Contains("UNCOMMON")) return Tier.UNCOMMON;
        if (lastLine.Contains("COMMON")) return Tier.COMMON;
        if (lastLine.Contains("SPECIAL")) return Tier.SPECIAL;
        if (lastLine.Contains("VERY SPECIAL")) return Tier.VERY_SPECIAL;

        return Tier.UNKNOWN;
    }
}
