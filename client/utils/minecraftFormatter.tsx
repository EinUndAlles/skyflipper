import { CSSProperties, cloneElement, type JSX } from 'react';

/**
 * Minecraft color code style map.
 * Maps § color codes to CSS styles.
 */
const MINECRAFT_STYLE_MAP: Record<string, CSSProperties> = {
    '0': { color: '#000000' },
    '1': { color: '#0000aa' },
    '2': { color: '#00aa00' },
    '3': { color: '#00aaaa' },
    '4': { color: '#aa0000' },
    '5': { color: '#aa00aa' },
    '6': { color: '#ffaa00' },  // Gold
    '7': { color: '#aaaaaa' },  // Gray
    '8': { color: '#555555' },  // Dark Gray
    '9': { color: '#5555ff' },  // Blue
    'a': { color: '#55ff55' },  // Green
    'b': { color: '#55ffff' },  // Aqua
    'c': { color: '#FF5555' },  // Red
    'd': { color: '#FF55FF' },  // Light Purple
    'e': { color: '#FFFF55' },  // Yellow
    'f': { color: '#FFFFFF' },  // White
    'l': { fontWeight: 'bold' },
    'n': { textDecorationLine: 'underline', textDecorationSkip: 'spaces' },
    'o': { fontStyle: 'italic' },
    'm': { textDecoration: 'line-through', textDecorationSkip: 'spaces' },
    'r': { textDecoration: 'none', textDecorationLine: 'none', fontWeight: 'normal', fontStyle: 'normal', color: '#FFFFFF' }
};

/**
 * Converts Minecraft § color codes to styled React elements.
 * Example: "§6Gold §lBold" -> <span><span style={{color:'#ffaa00'}}>Gold </span><span style={{fontWeight:'bold'}}>Bold</span></span>
 */
export function getMinecraftColorCodedElement(text: string = '', autoFormat = true): JSX.Element {
    const splits = text.split('§');
    const elements: JSX.Element[] = [];
    let currentStyle: CSSProperties = {};

    splits.forEach((split, i) => {
        if (i === 0) {
            if (split !== '') {
                elements.push(<span key={i}>{split}</span>);
            }
            return;
        }
        
        const code = split.substring(0, 1);
        let content = split.substring(1);
        
        if (autoFormat) {
            content = convertTagToName(content);
        }

        // Get new style, use reset if unknown code
        const newStyle = MINECRAFT_STYLE_MAP[code] || MINECRAFT_STYLE_MAP['r'];
        currentStyle = { ...currentStyle, ...newStyle };
        
        elements.push(
            <span key={i} style={currentStyle}>
                {content}
            </span>
        );
    });

    // Handle line breaks
    const withBreaks = elements.map((element, idx) => {
        if (element.type === 'span' && typeof element.props.children === 'string') {
            const text = element.props.children;
            if (text.includes('\n')) {
                const parts = text.split('\n');
                return cloneElement(
                    element,
                    { key: idx },
                    <>
                        {parts.map((part: string, i: number) => (
                            <span key={i}>
                                {part}
                                {i < parts.length - 1 && <br />}
                            </span>
                        ))}
                    </>
                );
            }
        }
        return element;
    });

    return <span>{withBreaks}</span>;
}

/**
 * Removes Minecraft § color codes from text.
 */
export function removeMinecraftColorCoding(text: string = ''): string {
    return text.replace(/§[0-9a-fk-or]/gi, '');
}

/**
 * Converts a tag (e.g. WOODEN_AXE) to a readable name (e.g. Wooden Axe).
 */
export function convertTagToName(itemTag?: string): string {
    if (!itemTag) return '';

    // Special case for PET_SKIN
    if (itemTag === 'PET_SKIN') {
        return 'Pet Skin (unapplied)';
    }

    const exceptions = ['of', 'the', 'a', 'an', 'and'];

    function capitalizeWords(text: string): string {
        return text.replace(/\w\S*/g, (txt) => {
            if (exceptions.includes(txt.toLowerCase())) {
                return txt.toLowerCase();
            }
            return txt.charAt(0).toUpperCase() + txt.slice(1).toLowerCase();
        });
    }

    let formatted = itemTag.replace(/_/g, ' ').toLowerCase();
    formatted = capitalizeWords(formatted);

    // Special formatting
    formatted = formatted.replace('Pet Item', '');
    if (formatted.startsWith('Pet ')) {
        formatted = formatted.replace('Pet ', '') + ' Pet';
    }
    if (formatted.startsWith('Ring ')) {
        formatted = formatted.replace('Ring ', '') + ' Ring';
    }

    return formatted.trim();
}

/**
 * Formats a number with thousands separators.
 */
export function formatNumber(num?: number): string {
    if (!num) return '0';
    return num.toLocaleString();
}

/**
 * Formats a price with shorthand (e.g., 1.5M, 2.3B).
 */
export function formatPriceShort(num: number, decimals: number = 1): string {
    const multMap = [
        { mult: 1e12, suffix: 'T' },
        { mult: 1e9, suffix: 'B' },
        { mult: 1e6, suffix: 'M' },
        { mult: 1e3, suffix: 'k' },
        { mult: 1, suffix: '' }
    ];
    
    let multIndex = multMap.findIndex(m => num >= m.mult);
    if (multIndex === -1) multIndex = multMap.length - 1;
    
    // Handle edge case where rounding would push to next tier
    if (multIndex !== 0 && Math.round(num / multMap[multIndex].mult) === 1000) {
        multIndex -= 1;
    }

    const mult = multMap[multIndex];
    return (num / mult.mult).toFixed(decimals) + mult.suffix;
}

/**
 * Formats dungeon stars with proper coloring.
 * Normal stars are gold, master stars are red.
 */
export function formatDungeonStars(name: string, dungeonItemLevel?: number): JSX.Element {
    const yellowStarStyle: CSSProperties = { color: '#ffaa00', fontWeight: 'normal' };
    const redStarStyle: CSSProperties = { color: '#FF5555', fontWeight: 'normal' };
    
    const stars = name.match(/✪.*/);
    if (!stars || stars.length === 0) {
        return <span>{name}</span>;
    }

    const masterStars = Math.max((dungeonItemLevel || 0) - 5, 0);
    const starsString = stars[0];
    const itemName = name.split(starsString)[0];
    const totalStars = starsString.length;

    return (
        <span>
            {itemName}
            <span style={yellowStarStyle}>{'✪'.repeat(Math.max(0, totalStars - masterStars))}</span>
            {masterStars > 0 && <span style={redStarStyle}>{'✪'.repeat(masterStars)}</span>}
        </span>
    );
}

/**
 * Formats an enchantment name for display.
 */
export function formatEnchantmentName(enchantType: string): string {
    // Handle ultimate enchant prefix
    const isUltimate = enchantType.toLowerCase().startsWith('ultimate_');
    const baseName = convertTagToName(enchantType);
    
    if (isUltimate) {
        return baseName.replace('Ultimate ', '');
    }
    return baseName;
}

/**
 * Get enchantment color based on type.
 */
export function getEnchantmentColor(enchantType: string): string {
    const type = enchantType.toLowerCase();
    if (type.startsWith('ultimate_')) return '#FF55FF'; // Light purple for ultimate
    return '#5555FF'; // Blue for normal enchants
}
