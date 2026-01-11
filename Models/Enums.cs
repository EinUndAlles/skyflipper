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

/// <summary>
/// Represents the status of an auction.
/// </summary>
public enum AuctionStatus
{
    ACTIVE,
    SOLD,
    EXPIRED
}

/// <summary>
/// Represents the granularity of price data.
/// FifteenMinute: For high-volume items needing faster price updates
/// Hourly: Standard granularity (7-day retention)
/// Daily: Long-term aggregates (indefinite retention)
/// </summary>
public enum PriceGranularity
{
    FifteenMinute,
    Hourly,
    Daily
}
