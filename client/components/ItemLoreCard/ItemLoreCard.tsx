'use client';

import Link from 'next/link';
import Image from 'next/image';
import { AuctionWithProperties } from '@/types';
import { getRarityColor, getRarityName } from '@/utils/rarity';
import { getItemImageUrl } from '@/api/ApiHelper';
import styles from './ItemLoreCard.module.css';

interface ItemLoreCardProps {
    auction: AuctionWithProperties;
}

/**
 * Simple list-based item properties display, similar to Coflnet's flip card.
 * Shows all relevant properties from the backend with icons for linkable items.
 */
export default function ItemLoreCard({ auction }: ItemLoreCardProps) {
    const rarityColor = getRarityColor(auction.tier);
    const rarityName = getRarityName(auction.tier);

    // Filter out properties we don't want to show (Buy It Now is in header)
    const displayProperties = auction.properties?.filter(p => 
        p.name !== 'Buy It Now' && 
        p.name !== 'Stack Size' // Already shown elsewhere
    ) || [];

    // Check if this is a pet
    const isPet = auction.tag === 'PET' || auction.tag?.startsWith('PET_');

    return (
        <div className={styles.loreCard}>
            {/* Rarity - always show */}
            <div className={styles.propertyRow}>
                <span className={styles.propertyLabel}>Rarity:</span>
                <span className={styles.propertyValue} style={{ color: rarityColor, fontWeight: 'bold' }}>
                    {rarityName}
                </span>
            </div>

            {/* Reforge - only for non-pets with actual reforge */}
            {!isPet && auction.reforge && auction.reforge !== '0' && auction.reforge !== 'None' && (
                <div className={styles.propertyRow}>
                    <span className={styles.propertyLabel}>Reforge:</span>
                    <span className={styles.propertyValue}>{auction.reforge}</span>
                </div>
            )}

            {/* Count - if stacked */}
            {auction.count > 1 && (
                <div className={styles.propertyRow}>
                    <span className={styles.propertyLabel}>Count:</span>
                    <span className={styles.propertyValue}>×{auction.count}</span>
                </div>
            )}

            {/* All properties from backend */}
            {displayProperties.length > 0 && (
                <>
                    <hr className={styles.divider} />
                    <ul className={styles.propertyList}>
                        {displayProperties.map((prop, i) => (
                            <li key={i} className={styles.propertyItem}>
                                {prop.itemTag ? (
                                    // Linkable property with icon
                                    <Link href={`/item/${prop.itemTag}`} className={styles.propertyLink}>
                                        <Image
                                            src={getItemImageUrl(prop.itemTag)}
                                            alt={prop.value}
                                            width={16}
                                            height={16}
                                            className={styles.propertyIcon}
                                            unoptimized
                                        />
                                        <span className={styles.propertyLabel}>{prop.name}:</span>
                                        <span className={styles.propertyValueLink}>{prop.value}</span>
                                    </Link>
                                ) : (
                                    // Regular property
                                    <span>
                                        <span className={styles.propertyLabel}>{prop.name}:</span>
                                        <span className={styles.propertyValue}> {prop.value}</span>
                                    </span>
                                )}
                            </li>
                        ))}
                    </ul>
                </>
            )}
        </div>
    );
}
