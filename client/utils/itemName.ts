/**
 * Utility functions for cleaning and formatting Skyblock item names
 */

// Known Skyblock reforge prefixes
const REFORGES = new Set([
    // Sword/Melee reforges
    'Epic', 'Fair', 'Fast', 'Gentle', 'Heroic', 'Legendary', 'Odd', 'Sharp',
    'Spicy', 'Coldfused', 'Dirty', 'Fabled', 'Gilded', 'Suspicious', 'Warped',
    'Withered', 'Bulky', 'Fanged', 'Hasty',
    // Bow reforges
    'Awkward', 'Deadly', 'Fine', 'Grand', 'Neat', 'Rapid', 'Rich', 'Unreal',
    'Precise', 'Spiritual', 'Headstrong',
    // Armor reforges
    'Clean', 'Fierce', 'Heavy', 'Light', 'Mythic', 'Pure', 'Smart', 'Titanic',
    'Wise', 'Perfect', 'Necrotic', 'Ancient', 'Spiked', 'Renowned', 'Cubic',
    'Reinforced', 'Loving', 'Ridiculous', 'Giant', 'Submerged', 'Jaded', 'Bustling',
    // Accessory reforges
    'Bizarre', 'Itchy', 'Ominous', 'Pleasant', 'Pretty', 'Shiny', 'Simple',
    'Strange', 'Sweet', 'Vivid', 'Godly', 'Demonic', 'Forceful', 'Hurtful',
    'Keen', 'Strong', 'Superior', 'Unpleasant', 'Zealous', 'Silky', 'Bloody',
    'Shaded', 'Aote_Stone', // Special reforges
    // Tool reforges
    'Fleet', 'Heated', 'Magnetic', 'Mithraic', 'Refined', 'Stellar', 'Fruitful',
    'Moil', 'Toil', 'Blessed', 'Bountiful', 'Auspicious', 'Salty', 'Treacherous',
    'Stiff', 'Lucky', 'Very', 'Highly', 'Extremely', 'Not So', 'Absolutely',
    // Fishing rod reforges
    'Pitchin', 'Glistening', 'Chomp',
    // Mining reforges
    'Fortunate', 'Great', 'Rugged', 'Lush', 'Green Thumb',
    // Other common ones
    'Mossy', 'Festive', 'Snowy',
]);

// Star symbols used in Skyblock item names
const STAR_SYMBOLS = /[✪✫⚚➊➋➌➍➎✦✧⍟]/g;

// Pet level pattern: [Lvl 1] or [Lvl 100] etc.
const PET_LEVEL_PATTERN = /^\[Lvl \d+\]\s*/i;

// Skin pattern for pets: some pets have skin indicators
const SKIN_PATTERN = /\s*✦\s*$/;

/**
 * Clean an item name by removing:
 * - Star symbols (upgrade stars, master stars)
 * - Reforge prefixes
 * - Pet level indicators
 * - Extra whitespace
 */
export function cleanItemName(name: string | undefined | null): string {
    if (!name) return '';
    
    let cleaned = name;
    
    // Remove star symbols
    cleaned = cleaned.replace(STAR_SYMBOLS, '');
    
    // Remove pet level prefix
    cleaned = cleaned.replace(PET_LEVEL_PATTERN, '');
    
    // Remove skin indicator
    cleaned = cleaned.replace(SKIN_PATTERN, '');
    
    // Trim and normalize whitespace
    cleaned = cleaned.trim().replace(/\s+/g, ' ');
    
    // Remove reforge prefix (first word if it's a known reforge)
    const words = cleaned.split(' ');
    if (words.length > 1) {
        const firstWord = words[0];
        // Check if first word is a reforge (case-insensitive)
        if (REFORGES.has(firstWord) || REFORGES.has(capitalizeFirst(firstWord))) {
            cleaned = words.slice(1).join(' ');
        }
    }
    
    return cleaned.trim();
}

/**
 * Convert an item tag to a readable name
 * e.g., "ASPECT_OF_THE_END" -> "Aspect of the End"
 * e.g., "HYPERION" -> "Hyperion"
 */
export function tagToName(tag: string): string {
    if (!tag) return '';
    
    // Special case mappings for common items
    const specialMappings: Record<string, string> = {
        'AOTE': 'Aspect of the End',
        'AOTD': 'Aspect of the Dragons',
        'AOTS': 'Axe of the Shredded',
        'AOTV': 'Aspect of the Void',
        'JERRY': 'Jerry',
    };
    
    if (specialMappings[tag]) {
        return specialMappings[tag];
    }
    
    // Replace underscores with spaces, then title case
    return tag
        .split('_')
        .map((word, index) => {
            const lower = word.toLowerCase();
            // Keep small words lowercase (except first word)
            if (index > 0 && ['of', 'the', 'a', 'an', 'and', 'or', 'in', 'on', 'at', 'to', 'for'].includes(lower)) {
                return lower;
            }
            return capitalizeFirst(lower);
        })
        .join(' ');
}

/**
 * Get a clean display name for an item, preferring cleaned itemName over tag conversion
 */
export function getDisplayName(itemName: string | undefined | null, tag: string): string {
    const cleaned = cleanItemName(itemName);
    if (cleaned) {
        return cleaned;
    }
    return tagToName(tag);
}

/**
 * Capitalize the first letter of a string
 */
function capitalizeFirst(str: string): string {
    if (!str) return '';
    return str.charAt(0).toUpperCase() + str.slice(1);
}
