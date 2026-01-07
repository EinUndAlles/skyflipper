namespace SkyFlipperSolo.Models;

/// <summary>
/// Represents the tier (rarity) of an item.
/// </summary>
public enum Tier
{
    UNKNOWN,
    COMMON,
    UNCOMMON,
    RARE,
    EPIC,
    LEGENDARY,
    MYTHIC,
    DIVINE,
    SPECIAL,
    VERY_SPECIAL,
    ULTIMATE,
    ADMIN
}

/// <summary>
/// Represents the category of an auction item.
/// </summary>
public enum Category
{
    UNKNOWN,
    WEAPON,
    ARMOR,
    ACCESSORIES,
    CONSUMABLES,
    BLOCKS,
    MISC
}
