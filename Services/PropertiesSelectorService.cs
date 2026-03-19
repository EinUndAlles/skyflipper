using SkyFlipperSolo.Models;

namespace SkyFlipperSolo.Services;

/// <summary>
/// Service for selecting and formatting important item properties for display.
/// Based on Coflnet.Sky.Flipper.PropertiesSelector.
/// </summary>
public class PropertiesSelectorService
{
    /// <summary>
    /// Gets formatted properties for an auction item.
    /// </summary>
    public List<ItemProperty> GetProperties(Auction auction)
    {
        var properties = new List<ItemProperty>();

        // Add winning bid if present
        if (auction.NBTLookups.Any(n => n.NBTKey?.KeyName == "winning_bid"))
        {
            var winningBid = auction.NBTLookups.FirstOrDefault(n => n.NBTKey?.KeyName == "winning_bid");
            if (winningBid?.ValueNumeric.HasValue == true)
            {
                properties.Add(new ItemProperty
                {
                    Name = "Top Bid",
                    Value = $"{((long)winningBid.ValueNumeric.Value).ToString("N0")} Coins",
                    Importance = 20,
                    Category = "Price"
                });
            }
        }

        // Add Hot Potato Books
        var hpb = auction.NBTLookups.FirstOrDefault(n => n.NBTKey?.KeyName == "hot_potato_count");
        if (hpb?.ValueNumeric.HasValue == true && hpb.ValueNumeric.Value > 0)
        {
            properties.Add(new ItemProperty
            {
                Name = "Hot Potato Books",
                Value = $"{(int)hpb.ValueNumeric.Value}",
                Importance = 12,
                Category = "Enhancement"
            });
        }

        // Add Art of War
        var artOfWar = auction.NBTLookups.FirstOrDefault(n => n.NBTKey?.KeyName == "art_of_war_count");
        if (artOfWar?.ValueNumeric.HasValue == true && artOfWar.ValueNumeric.Value > 0)
        {
            properties.Add(new ItemProperty
            {
                Name = "Art of War",
                Value = $"{(int)artOfWar.ValueNumeric.Value}",
                Importance = 12,
                Category = "Enhancement"
            });
        }

        // Add Farming for Dummies
        var ffd = auction.NBTLookups.FirstOrDefault(n => n.NBTKey?.KeyName == "farming_for_dummies_count");
        if (ffd?.ValueNumeric.HasValue == true && ffd.ValueNumeric.Value > 0)
        {
            properties.Add(new ItemProperty
            {
                Name = "Farming for Dummies",
                Value = $"{(int)ffd.ValueNumeric.Value}",
                Importance = 11,
                Category = "Enhancement"
            });
        }

        // Add Recombobulated status
        var recomb = auction.NBTLookups.FirstOrDefault(n => n.NBTKey?.KeyName == "rarity_upgrades");
        if (recomb?.ValueNumeric.HasValue == true && recomb.ValueNumeric.Value > 0)
        {
            properties.Add(new ItemProperty
            {
                Name = "Recombobulated",
                Value = "✓",
                Importance = 12,
                Category = "Enhancement"
            });
        }

        // Add Ethermerge
        var ethermerge = auction.NBTLookups.FirstOrDefault(n => n.NBTKey?.KeyName == "ethermerge");
        if (ethermerge?.ValueNumeric.HasValue == true && ethermerge.ValueNumeric.Value > 0)
        {
            properties.Add(new ItemProperty
            {
                Name = "Ethermerge",
                Value = "✓",
                Importance = 10,
                Category = "Enhancement"
            });
        }

        // Add Midas winning bid
        var midasBid = auction.NBTLookups.FirstOrDefault(n => n.NBTKey?.KeyName == "winning_bid");
        if (midasBid?.ValueNumeric.HasValue == true && midasBid.ValueNumeric.Value > 0)
        {
            properties.Add(new ItemProperty
            {
                Name = "Midas Winning Bid",
                Value = $"{((long)midasBid.ValueNumeric.Value).ToString("N0")} Coins",
                Importance = 15,
                Category = "Price"
            });
        }

        // Add item count if > 1
        if (auction.Count > 1)
        {
            properties.Add(new ItemProperty
            {
                Name = "Count",
                Value = $"×{auction.Count}",
                Importance = 12,
                Category = "Basic"
            });
        }

        // Add pet properties
        if (IsPet(auction.Tag))
        {
            // Pet Level - extract from item name first (e.g., "[Lvl 100] Dragon")
            // This is the most reliable source as NBT exp requires complex calculation
            var levelMatch = System.Text.RegularExpressions.Regex.Match(auction.ItemName ?? "", @"\[Lvl (\d+)\]");
            if (levelMatch.Success && int.TryParse(levelMatch.Groups[1].Value, out var level))
            {
                properties.Add(new ItemProperty
                {
                    Name = "Level",
                    Value = $"{level}",
                    Importance = 14,
                    Category = "Pet"
                });
            }
            else
            {
                // Fallback: try to get from NBT exp (check both "exp" and "pet_exp" keys)
                var exp = auction.NBTLookups.FirstOrDefault(n => 
                    n.NBTKey?.KeyName == "exp" || n.NBTKey?.KeyName == "pet_exp");
                if (exp?.ValueNumeric.HasValue == true)
                {
                    var calcLevel = CalculatePetLevel(exp.ValueNumeric.Value);
                    properties.Add(new ItemProperty
                    {
                        Name = "Level",
                        Value = $"{calcLevel}",
                        Importance = 14,
                        Category = "Pet"
                    });
                }
            }

            // Held item (check both "heldItem" and "pet_held_item" keys)
            var heldItem = auction.NBTLookups.FirstOrDefault(n =>
                n.NBTKey?.KeyName == "heldItem" || n.NBTKey?.KeyName == "pet_held_item");
            if (heldItem?.NBTValue?.Value != null)
            {
                properties.Add(new ItemProperty
                {
                    Name = "Held Item",
                    Value = TagToName(heldItem.NBTValue.Value),
                    Importance = 12,
                    Category = "Pet",
                    ItemTag = heldItem.NBTValue.Value // Link to /item/PET_ITEM_*
                });
            }

            // Skin (check both "skin" and "pet_skin" keys)
            var skin = auction.NBTLookups.FirstOrDefault(n =>
                n.NBTKey?.KeyName == "skin" || n.NBTKey?.KeyName == "pet_skin");
            if (skin?.NBTValue?.Value != null)
            {
                // Skin tags are stored without PET_SKIN_ prefix, need to construct full tag
                var skinTag = skin.NBTValue.Value.StartsWith("PET_SKIN_")
                    ? skin.NBTValue.Value
                    : $"PET_SKIN_{skin.NBTValue.Value}";
                properties.Add(new ItemProperty
                {
                    Name = "Skin",
                    Value = TagToName(skin.NBTValue.Value),
                    Importance = 15,
                    Category = "Pet",
                    ItemTag = skinTag // Link to /item/PET_SKIN_*
                });
            }

            // Candy used
            var candyUsed = auction.NBTLookups.FirstOrDefault(n => n.NBTKey?.KeyName == "candyUsed");
            if (candyUsed?.ValueNumeric.HasValue == true && candyUsed.ValueNumeric.Value > 0)
            {
                properties.Add(new ItemProperty
                {
                    Name = "Pet Candy Used",
                    Value = $"{(int)candyUsed.ValueNumeric.Value}",
                    Importance = 11,
                    Category = "Pet"
                });
            }
        }
        else
        {
            // Add non-pet specific properties
            // Add gemstones
            var gemProperties = GetGemstoneProperties(auction);
            properties.AddRange(gemProperties);

            // Add ability scroll
            var abilityScroll = auction.NBTLookups.FirstOrDefault(n => n.NBTKey?.KeyName == "ability_scroll");
            if (abilityScroll?.NBTValue?.Value != null)
            {
                properties.Add(new ItemProperty
                {
                    Name = "Ability Scroll",
                    Value = TagToName(abilityScroll.NBTValue.Value),
                    Importance = 10,
                    Category = "Enhancement",
                    ItemTag = abilityScroll.NBTValue.Value // Link to /item/IMPLOSION_SCROLL etc.
                });
            }
        }

        // Add Year property (New Year's Cake)
        var year = auction.NBTLookups.FirstOrDefault(n => n.NBTKey?.KeyName == "year");
        if (year?.ValueNumeric.HasValue == true)
        {
            properties.Add(new ItemProperty
            {
                Name = "Year",
                Value = $"{(int)year.ValueNumeric.Value}",
                Importance = 15,
                Category = "Special"
            });
        }

        // Add Captured Player property (Cake Souls)
        var capturedPlayer = auction.NBTLookups.FirstOrDefault(n => n.NBTKey?.KeyName == "captured_player");
        if (capturedPlayer?.NBTValue?.Value != null)
        {
            properties.Add(new ItemProperty
            {
                Name = "Captured Player",
                Value = capturedPlayer.NBTValue.Value,
                Importance = 15,
                Category = "Special"
            });
        }

        // Add Edition property (limited edition items)
        var edition = auction.NBTLookups.FirstOrDefault(n => n.NBTKey?.KeyName == "edition");
        if (edition?.ValueNumeric.HasValue == true)
        {
            properties.Add(new ItemProperty
            {
                Name = "Edition",
                Value = $"#{(int)edition.ValueNumeric.Value}",
                Importance = 16,
                Category = "Special"
            });
        }

        // Add Potion properties
        if (auction.Tag.StartsWith("POTION_"))
        {
            var potionLevel = auction.NBTLookups.FirstOrDefault(n => n.NBTKey?.KeyName == "level");
            if (potionLevel?.ValueNumeric.HasValue == true)
            {
                properties.Add(new ItemProperty
                {
                    Name = "Potion Level",
                    Value = $"{(int)potionLevel.ValueNumeric.Value}",
                    Importance = 14,
                    Category = "Potion"
                });
            }

            var splash = auction.NBTLookups.FirstOrDefault(n => n.NBTKey?.KeyName == "splash");
            if (splash?.ValueNumeric.HasValue == true && splash.ValueNumeric.Value > 0)
            {
                properties.Add(new ItemProperty
                {
                    Name = "Splash",
                    Value = "✓",
                    Importance = 12,
                    Category = "Potion"
                });
            }

            var enhanced = auction.NBTLookups.FirstOrDefault(n => n.NBTKey?.KeyName == "enhanced");
            if (enhanced != null)
            {
                properties.Add(new ItemProperty
                {
                    Name = "Enhanced",
                    Value = "✓",
                    Importance = 12,
                    Category = "Potion"
                });
            }
        }

        // Add Rune level property
        if (auction.Tag.Contains("RUNE_"))
        {
            // Rune levels are stored with keys like "RUNE_BLOOD", "RUNE_ICE" where value is the level
            var runeLookups = auction.NBTLookups.Where(n => 
                n.NBTKey?.KeyName != null && 
                n.NBTKey.KeyName.StartsWith("RUNE_") && 
                n.ValueNumeric.HasValue);
            
            foreach (var rune in runeLookups)
            {
                properties.Add(new ItemProperty
                {
                    Name = "Rune Level",
                    Value = $"{(int)rune.ValueNumeric!.Value}",
                    Importance = 14,
                    Category = "Rune"
                });
                break; // Only show one rune level (items typically have one rune)
            }
        }

        // Add enchantments
        var enchantmentProperties = GetEnchantmentProperties(auction);
        properties.AddRange(enchantmentProperties);

        // Add basic properties that are always useful to display
        if (auction.Bin)
        {
            properties.Add(new ItemProperty
            {
                Name = "Buy It Now",
                Value = "Available",
                Importance = 5,
                Category = "Basic"
            });
        }

        if (auction.Count > 1)
        {
            properties.Add(new ItemProperty
            {
                Name = "Stack Size",
                Value = $"{auction.Count}",
                Importance = 8,
                Category = "Basic"
            });
        }

        if (auction.AnvilUses > 0)
        {
            properties.Add(new ItemProperty
            {
                Name = "Anvil Uses",
                Value = $"{auction.AnvilUses}",
                Importance = 6,
                Category = "Enhancement"
            });
        }

        return properties.OrderByDescending(p => p.Importance).ToList();
    }

    /// <summary>
    /// Gets gemstone properties with quality information.
    /// </summary>
    private List<ItemProperty> GetGemstoneProperties(Auction auction)
    {
        var properties = new List<ItemProperty>();

        // Find all gem-related lookups
        var gemLookups = auction.NBTLookups
            .Where(n => n.NBTKey != null &&
                       (n.NBTKey.KeyName.Contains("PERFECT") ||
                        n.NBTKey.KeyName.Contains("FLAWLESS") ||
                        n.NBTKey.KeyName.Contains("FINE") ||
                        n.NBTKey.KeyName.Contains("ROUGH")))
            .Where(n => n.NBTValue?.Value != null)
            .ToList();

        foreach (var gem in gemLookups)
        {
            if (gem.NBTKey?.KeyName is not string gemName || string.IsNullOrEmpty(gem.NBTValue?.Value))
                continue;

            var gemType = gem.NBTValue.Value;

            // Extract quality from key name
            string quality = "Rough";
            if (gemName.Contains("PERFECT")) quality = "Perfect";
            else if (gemName.Contains("FLAWLESS")) quality = "Flawless";
            else if (gemName.Contains("FINE")) quality = "Fine";

            // Extract slot type from key name
            var slotType = gemName.Replace("PERFECT_", "").Replace("FLAWLESS_", "").Replace("FINE_", "").Replace("ROUGH_", "");

            properties.Add(new ItemProperty
            {
                Name = $"{quality} {TagToName(slotType)}",
                Value = TagToName(gemType),
                Importance = quality == "Perfect" ? 15 : quality == "Flawless" ? 13 : 10,
                Category = "Gemstone"
            });
        }

        return properties;
    }

    /// <summary>
    /// Gets formatted enchantment properties.
    /// </summary>
    private List<ItemProperty> GetEnchantmentProperties(Auction auction)
    {
        var properties = new List<ItemProperty>();

        if (auction.Enchantments == null) return properties;

        var isBook = auction.Tag == "ENCHANTED_BOOK";

        foreach (var ench in auction.Enchantments)
        {
            // Show all enchantments on books, only valuable ones on other items
            if (!isBook && !IsValuableEnchantment(ench.Type, ench.Level))
                continue;

            var enchName = TagToName(ench.Type.ToString());
            var importance = 2 + ench.Level;

            // Boost importance for ultimate enchants
            if (IsUltimateEnchant(ench.Type))
            {
                importance += 5;
            }

            // Reduce importance for infinite quiver
            if (ench.Type.ToString().ToLower().Contains("infinite_quiver"))
            {
                importance -= 3;
            }

            properties.Add(new ItemProperty
            {
                Name = enchName,
                Value = $"{ench.Level}",
                Importance = importance,
                Category = "Enchantment"
            });
        }

        return properties;
    }

    /// <summary>
    /// Determines if an enchantment is valuable enough to display.
    /// </summary>
    private bool IsValuableEnchantment(EnchantmentType type, int level)
    {
        // Always show ultimate enchants
        if (IsUltimateEnchant(type))
            return true;

        // Show high-level enchants (7+)
        if (level >= 7)
            return true;

        // Show certain valuable enchants at level 5+
        var valuableTypes = new[] {
            EnchantmentType.sharpness, EnchantmentType.protection, EnchantmentType.efficiency,
            EnchantmentType.fortune, EnchantmentType.power, EnchantmentType.first_strike,
            EnchantmentType.giant_killer, EnchantmentType.execute, EnchantmentType.lethality,
            EnchantmentType.luck, EnchantmentType.looting, EnchantmentType.scavenger,
            EnchantmentType.smite, EnchantmentType.bane_of_arthropods,
            EnchantmentType.depth_strider, EnchantmentType.feather_falling
        };

        return valuableTypes.Contains(type) && level >= 5;
    }

    /// <summary>
    /// Checks if an enchantment is an ultimate enchant.
    /// </summary>
    private bool IsUltimateEnchant(EnchantmentType type)
    {
        var typeStr = type.ToString().ToLower();
        return typeStr.StartsWith("ultimate_");
    }

    /// <summary>
    /// Checks if an item tag represents a pet.
    /// </summary>
    private bool IsPet(string tag)
    {
        return tag == "PET" || tag.StartsWith("PET_");
    }

    /// <summary>
    /// Calculates pet level from experience.
    /// Simplified version - in reality this uses pet-specific XP tables.
    /// </summary>
    private int CalculatePetLevel(double exp)
    {
        // This is a simplified calculation - real implementation uses pet-specific XP tables
        // For now, we'll use a rough approximation
        if (exp < 100) return 1;
        if (exp < 1000) return (int)(exp / 100) + 1;
        if (exp < 10000) return (int)(exp / 500) + 10;
        if (exp < 50000) return (int)(exp / 1000) + 25;
        if (exp < 100000) return (int)(exp / 2000) + 45;
        return 100; // Max level
    }

    /// <summary>
    /// Converts a tag to a readable name (e.g., "ASPECT_OF_THE_END" -> "Aspect of the End").
    /// Based on Coflnet.Sky.Core.ItemDetails.TagToName.
    /// </summary>
    public static string TagToName(string tag)
    {
        if (string.IsNullOrEmpty(tag) || tag.Length <= 2)
            return tag;

        var split = tag.ToLower().Split('_');
        var result = "";

        foreach (var item in split)
        {
            if (item == "of" || item == "the" || item == "a" || item == "an" || item == "and")
                result += " " + item;
            else
                result += " " + char.ToUpper(item[0]) + item.Substring(1);
        }

        return result.Trim();
    }
}

/// <summary>
/// Represents a formatted item property for display.
/// </summary>
public class ItemProperty
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int Importance { get; set; }
    public string Category { get; set; } = string.Empty; // "Basic", "Enhancement", "Enchantment", "Pet", "Gemstone", "Price"
    /// <summary>
    /// Optional item tag for linkable sub-items (e.g., pet skins, held items, ability scrolls).
    /// When set, the frontend can link to /item/{ItemTag}.
    /// </summary>
    public string? ItemTag { get; set; }
}
