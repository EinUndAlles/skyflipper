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
 * Get rarity color for a tier value
 */
export function getRarityColor(tier: string | number): string {
    const tierNum = typeof tier === 'string' ? parseInt(tier) : tier;
    return RARITY_COLORS[tierNum] || RARITY_COLORS[Tier.COMMON];
}

/**
 * Get rarity name for a tier value
 */
export function getRarityName(tier: string | number): string {
    const tierNum = typeof tier === 'string' ? parseInt(tier) : tier;
    return RARITY_NAMES[tierNum] || RARITY_NAMES[Tier.UNKNOWN];
}
