// Tier enum mapping from backend (Enums.cs)
export enum Tier {
    UNKNOWN = 0,
    COMMON = 1,
    UNCOMMON = 2,
    RARE = 3,
    EPIC = 4,
    LEGENDARY = 5,
    MYTHIC = 6,
    DIVINE = 7,
    SPECIAL = 8,
    VERY_SPECIAL = 9,
    ULTIMATE = 10,
    ADMIN = 11
}

// Map string tier names to enum values
const TIER_STRING_TO_ENUM: Record<string, Tier> = {
    'UNKNOWN': Tier.UNKNOWN,
    'COMMON': Tier.COMMON,
    'UNCOMMON': Tier.UNCOMMON,
    'RARE': Tier.RARE,
    'EPIC': Tier.EPIC,
    'LEGENDARY': Tier.LEGENDARY,
    'MYTHIC': Tier.MYTHIC,
    'DIVINE': Tier.DIVINE,
    'SPECIAL': Tier.SPECIAL,
    'VERY_SPECIAL': Tier.VERY_SPECIAL,
    'ULTIMATE': Tier.ULTIMATE,
    'ADMIN': Tier.ADMIN
};

// Rarity color mapping (matching Hypixel Skyblock colors)
export const RARITY_COLORS: Record<number, string> = {
    [Tier.UNKNOWN]: '#AAAAAA',
    [Tier.COMMON]: '#FFFFFF',
    [Tier.UNCOMMON]: '#55FF55',
    [Tier.RARE]: '#5555FF',
    [Tier.EPIC]: '#AA00AA',
    [Tier.LEGENDARY]: '#FFAA00',
    [Tier.MYTHIC]: '#FF55FF',
    [Tier.DIVINE]: '#55FFFF',
    [Tier.SPECIAL]: '#FF5555',
    [Tier.VERY_SPECIAL]: '#FF5555',
    [Tier.ULTIMATE]: '#AA0000',
    [Tier.ADMIN]: '#AA0000'
};

// Rarity name mapping
export const RARITY_NAMES: Record<number, string> = {
    [Tier.UNKNOWN]: 'Unknown',
    [Tier.COMMON]: 'Common',
    [Tier.UNCOMMON]: 'Uncommon',
    [Tier.RARE]: 'Rare',
    [Tier.EPIC]: 'Epic',
    [Tier.LEGENDARY]: 'Legendary',
    [Tier.MYTHIC]: 'Mythic',
    [Tier.DIVINE]: 'Divine',
    [Tier.SPECIAL]: 'Special',
    [Tier.VERY_SPECIAL]: 'Very Special',
    [Tier.ULTIMATE]: 'Ultimate',
    [Tier.ADMIN]: 'Admin'
};

/**
 * Convert tier (string or number) to numeric enum value
 */
function tierToNumber(tier: string | number): number {
    if (typeof tier === 'number') {
        return tier;
    }
    // If it's a string like "LEGENDARY", map it to the enum value
    const upperTier = tier.toUpperCase();
    return TIER_STRING_TO_ENUM[upperTier] ?? Tier.UNKNOWN;
}

/**
 * Get rarity color for a tier value
 */
export function getRarityColor(tier: string | number): string {
    const tierNum = tierToNumber(tier);
    return RARITY_COLORS[tierNum] || RARITY_COLORS[Tier.COMMON];
}

/**
 * Get rarity name for a tier value
 */
export function getRarityName(tier: string | number): string {
    const tierNum = tierToNumber(tier);
    return RARITY_NAMES[tierNum] || RARITY_NAMES[Tier.UNKNOWN];
}

/**
 * Get CSS style properties for tier coloring
 * Matches reference project's getStyleForTier function
 */
export function getTierStyle(tier?: string | number): React.CSSProperties {
    const color = tier !== undefined ? getRarityColor(tier) : RARITY_COLORS[Tier.COMMON];
    return {
        color,
        fontFamily: 'monospace',
        fontWeight: 'bold'
    };
}

