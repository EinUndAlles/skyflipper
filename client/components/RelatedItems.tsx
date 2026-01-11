'use client';

import { useEffect, useState } from 'react';
import { Row, Col, Card, Spinner } from 'react-bootstrap';
import Link from 'next/link';
import { api, getItemImageUrl } from '@/api/ApiHelper';

interface RelatedItem {
    tag: string;
    itemName: string;
    tier: string;
    category: string;
    texture: string | null;
    lowestPrice: number;
    count: number;
}

interface RelatedItemsProps {
    itemTag: string;
    limit?: number;
}

// Format large numbers (1M, 1B, etc.)
const formatPrice = (value: number): string => {
    if (value >= 1_000_000_000) {
        return `${(value / 1_000_000_000).toFixed(1)}B`;
    }
    if (value >= 1_000_000) {
        return `${(value / 1_000_000).toFixed(1)}M`;
    }
    if (value >= 1_000) {
        return `${(value / 1_000).toFixed(1)}K`;
    }
    return value.toFixed(0);
};

// Get tier color
const getTierColor = (tier: string): string => {
    switch (tier) {
        case 'LEGENDARY': return '#FFAA00';
        case 'EPIC': return '#AA00AA';
        case 'RARE': return '#5555FF';
        case 'UNCOMMON': return '#55FF55';
        case 'COMMON': return '#FFFFFF';
        case 'MYTHIC': return '#FF55FF';
        case 'DIVINE': return '#55FFFF';
        case 'SPECIAL': return '#FF5555';
        case 'VERY_SPECIAL': return '#FF5555';
        default: return '#AAAAAA';
    }
};

// Clean item name (remove stars, reforge, etc.)
const cleanItemName = (name: string): string => {
    // Remove star symbols
    let clean = name.replace(/[✪✫⚚➊➋➌➍➎➏➐➑➒]+/g, '').trim();
    return clean;
};

export default function RelatedItems({ itemTag, limit = 8 }: RelatedItemsProps) {
    const [items, setItems] = useState<RelatedItem[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        const fetchRelatedItems = async () => {
            try {
                setLoading(true);
                setError(null);
                const data = await api.getRelatedItems(itemTag, limit);
                setItems(data);
            } catch (err: any) {
                console.error('Failed to load related items:', err);
                setError('Failed to load related items');
                setItems([]);
            } finally {
                setLoading(false);
            }
        };

        if (itemTag) {
            fetchRelatedItems();
        }
    }, [itemTag, limit]);

    // Don't show section if no related items
    if (!loading && items.length === 0) {
        return null;
    }

    return (
        <div className="bg-dark rounded border border-secondary p-3 mb-4">
            <h5 className="text-light mb-3">Related Items</h5>
            
            {loading ? (
                <div className="d-flex justify-content-center py-4">
                    <Spinner animation="border" variant="primary" size="sm" />
                    <span className="ms-2 text-light">Loading related items...</span>
                </div>
            ) : error ? (
                <div className="text-secondary text-center py-3">
                    {error}
                </div>
            ) : (
                <Row xs={2} sm={3} md={4} lg={4} className="g-2">
                    {items.map(item => (
                        <Col key={item.tag}>
                            <Link href={`/item/${item.tag}`} className="text-decoration-none">
                                <Card className="bg-secondary bg-opacity-25 border-secondary h-100 hover-card">
                                    <Card.Body className="p-2 d-flex align-items-center">
                                        {/* Item icon */}
                                        <div style={{ width: '32px', height: '32px', flexShrink: 0 }} className="me-2">
                                            {/* eslint-disable-next-line @next/next/no-img-element */}
                                            <img
                                                src={getItemImageUrl(item.tag, 'default', item.texture)}
                                                alt={item.itemName}
                                                className="w-100 h-100 object-fit-contain"
                                                style={{ imageRendering: 'pixelated' }}
                                            />
                                        </div>
                                        
                                        {/* Item info */}
                                        <div className="flex-grow-1 overflow-hidden">
                                            <div 
                                                className="text-truncate small fw-bold"
                                                style={{ color: getTierColor(item.tier) }}
                                                title={cleanItemName(item.itemName)}
                                            >
                                                {cleanItemName(item.itemName)}
                                            </div>
                                            <div className="d-flex justify-content-between align-items-center">
                                                <small className="text-warning">
                                                    {formatPrice(item.lowestPrice)}
                                                </small>
                                                <small className="text-muted">
                                                    {item.count} listed
                                                </small>
                                            </div>
                                        </div>
                                    </Card.Body>
                                </Card>
                            </Link>
                        </Col>
                    ))}
                </Row>
            )}
            
            <style jsx global>{`
                .hover-card {
                    transition: transform 0.15s ease, border-color 0.15s ease;
                }
                .hover-card:hover {
                    transform: translateY(-2px);
                    border-color: #6c757d !important;
                }
            `}</style>
        </div>
    );
}
