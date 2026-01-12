'use client';

import Link from 'next/link';
import Image from 'next/image';
import { AuctionWithProperties } from '@/types';
import { getRarityColor, getRarityName } from '@/utils/rarity';
import { getItemImageUrl } from '@/api/ApiHelper';
import styles from './ItemLoreCard.module.css';

interface ItemLoreCardProps {
    auction: AuctionWithProperties;
    imageUrl?: string;
    rarityColor?: string;
}

// Map tier to CSS class
function getRarityClass(tier: string): string {
    const tierUpper = tier?.toUpperCase() || '';
    switch (tierUpper) {
        case 'COMMON': return styles.rarityCommon;
        case 'UNCOMMON': return styles.rarityUncommon;
        case 'RARE': return styles.rarityRare;
        case 'EPIC': return styles.rarityEpic;
        case 'LEGENDARY': return styles.rarityLegendary;
        case 'MYTHIC': return styles.rarityMythic;
        case 'DIVINE': return styles.rarityDivine;
        case 'SPECIAL': return styles.raritySpecial;
        case 'VERY_SPECIAL': return styles.rarityVerySpecial;
        default: return styles.rarityCommon;
    }
}

// Get item type text (e.g., "RARE SWORD")
function getItemType(auction: AuctionWithProperties): string {
    const rarity = getRarityName(auction.tier).toUpperCase();
    const category = auction.category?.toUpperCase() || '';
    
    // Map category to display name
    const categoryMap: Record<string, string> = {
        'WEAPON': 'SWORD',
        'ARMOR': 'ARMOR',
        'ACCESSORIES': 'ACCESSORY',
        'CONSUMABLES': 'CONSUMABLE',
        'BLOCKS': 'BLOCK',
        'MISC': '',
    };
    
    const displayCategory = categoryMap[category] || category;
    return displayCategory ? `${rarity} ${displayCategory}` : rarity;
}

/**
 * Minecraft-style tooltip card showing item image, name, and properties.
 */
export default function ItemLoreCard({ auction, imageUrl, rarityColor: propRarityColor }: ItemLoreCardProps) {
    const rarityColor = propRarityColor || getRarityColor(auction.tier);
    const rarityName = getRarityName(auction.tier);
    const itemImageUrl = imageUrl || getItemImageUrl(auction.tag, 'default', auction.texture);
    const rarityClass = getRarityClass(auction.tier);

    // Filter out properties we don't want to show
    const displayProperties = auction.properties?.filter(p => 
        p.name !== 'Buy It Now' && 
        p.name !== 'Stack Size'
    ) || [];

    // Check if this is a pet
    const isPet = auction.tag === 'PET' || auction.tag?.startsWith('PET_');

    return (
        <div className={styles.loreCard}>
            {/* Item Image */}
            <div className={styles.imageContainer}>
                <Image
                    src={itemImageUrl}
                    alt={auction.itemName}
                    width={96}
                    height={96}
                    style={{ imageRendering: 'pixelated' }}
                    className={styles.itemImage}
                    unoptimized
                />
            </div>

            {/* Item Name with rarity color */}
            <h2 className={`${styles.itemName} ${rarityClass}`}>
                {auction.itemName}
            </h2>

            <hr className={styles.divider} />

            {/* Rarity */}
            <div className={styles.propertyRow}>
                <span className={styles.propertyLabel}>Rarity</span>
                <span className={`${styles.propertyValue} ${rarityClass}`} style={{ fontWeight: 'bold' }}>
                    {rarityName}
                </span>
            </div>

            {/* Reforge - only for non-pets with actual reforge */}
            {!isPet && auction.reforge && auction.reforge !== '0' && auction.reforge !== 'None' && (
                <div className={styles.propertyRow}>
                    <span className={styles.propertyLabel}>Reforge</span>
                    <span className={`${styles.propertyValue} ${styles.colorLightPurple}`}>{auction.reforge}</span>
                </div>
            )}

            {/* Count - if stacked */}
            {auction.count > 1 && (
                <div className={styles.propertyRow}>
                    <span className={styles.propertyLabel}>Count</span>
                    <span className={`${styles.propertyValue} ${styles.colorGreen}`}>x{auction.count}</span>
                </div>
            )}

            {/* All properties from backend */}
            {displayProperties.length > 0 && (
                <>
                    <hr className={styles.divider} />
                    {displayProperties.map((prop, i) => (
                        <div key={i} className={styles.propertyRow}>
                            {prop.itemTag ? (
                                // Linkable property with icon
                                <Link href={`/item/${prop.itemTag}`} className={styles.propertyLink} style={{ width: '100%', justifyContent: 'space-between' }}>
                                    <span style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                        <Image
                                            src={getItemImageUrl(prop.itemTag)}
                                            alt={prop.value}
                                            width={20}
                                            height={20}
                                            className={styles.propertyIcon}
                                            unoptimized
                                        />
                                        <span className={styles.propertyLabel}>{prop.name}</span>
                                    </span>
                                    <span className={styles.propertyValueLink}>{prop.value}</span>
                                </Link>
                            ) : (
                                // Regular property
                                <>
                                    <span className={styles.propertyLabel}>{prop.name}</span>
                                    <span className={`${styles.propertyValue} ${getPropertyColor(prop)}`}>{prop.value}</span>
                                </>
                            )}
                        </div>
                    ))}
                </>
            )}

            {/* Item type line at bottom */}
            <div className={`${styles.itemTypeLine} ${rarityClass}`}>
                {getItemType(auction)}
            </div>
        </div>
    );
}

// Helper to get color class based on property
function getPropertyColor(prop: { name: string; value: string; category: string }): string {
    // Enchantments are blue
    if (prop.category === 'Enchantment') return styles.colorBlue;
    // Pet properties
    if (prop.category === 'Pet') return styles.colorGreen;
    // Enhancements (HPB, recomb, etc)
    if (prop.category === 'Enhancement') return styles.colorGold;
    // Gemstones
    if (prop.category === 'Gemstone') return styles.colorLightPurple;
    // Price-related
    if (prop.category === 'Price') return styles.colorYellow;
    // Default white
    return styles.colorWhite;
}
