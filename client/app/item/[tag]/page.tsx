'use client';

import { useEffect, useState, use } from 'react';
import { useSearchParams } from 'next/navigation';
import { Container, Row, Col, Spinner, Alert } from 'react-bootstrap';
import { api, getItemImageUrl } from '@/api/ApiHelper';
import { Auction } from '@/types';
import AuctionCard from '@/components/AuctionCard';
import ItemFilterPanel, { ItemFilters } from '@/components/ItemFilterPanel';
import { toast } from '@/components/ToastProvider';

interface ItemPageProps {
    params: Promise<{ tag: string }>;
}

export default function ItemPage({ params }: ItemPageProps) {
    const resolvedParams = use(params);
    const tag = resolvedParams.tag;
    const searchParams = useSearchParams();
    const nameFilter = searchParams.get('filter') || undefined;

    const [auctions, setAuctions] = useState<Auction[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    const [filters, setFilters] = useState<ItemFilters>({});

    useEffect(() => {
        const fetchAuctions = async () => {
            try {
                setLoading(true);
                const data = await api.getAuctionsByTag(
                    tag,
                    200,
                    nameFilter,
                    filters.binOnly !== false, // Default to true
                    filters.showEnded || false,
                    filters.minStars,
                    filters.maxStars,
                    filters.enchantment,
                    filters.minEnchantLevel,
                    filters.minPrice,
                    filters.maxPrice
                );
                setAuctions(data);
            } catch (err) {
                console.error(err);
                toast.error('Failed to load auctions');
                setError('Failed to load auctions.');
            } finally {
                setLoading(false);
            }
        };

        if (tag) {
            fetchAuctions();
        }
    }, [tag, nameFilter, filters]);

    const getFilteredAuctions = () => {
        const sorted = [...auctions];
        const sortBy = filters.sortBy || 'lowest';

        switch (sortBy) {
            case 'lowest':
                sorted.sort((a, b) => a.price - b.price);
                break;
            case 'highest':
                sorted.sort((a, b) => b.price - a.price);
                break;
            case 'ending':
                sorted.sort((a, b) => new Date(a.end).getTime() - new Date(b.end).getTime());
                break;
        }
        return sorted;
    };

    if (loading) return (
        <Container className="d-flex justify-content-center align-items-center" style={{ minHeight: '60vh' }}>
            <Spinner animation="border" role="status" variant="primary">
                <span className="visually-hidden">Loading...</span>
            </Spinner>
        </Container>
    );

    if (error) return (
        <Container className="mt-4">
            <Alert variant="danger">{error}</Alert>
        </Container>
    );

    const filteredAuctions = getFilteredAuctions();
    // Get representative item for header
    const item = auctions.length > 0 ? auctions[0] : null;

    return (
        <Container className="py-4">
            {item ? (
                <div className="d-flex align-items-center mb-4 p-3 bg-dark rounded shadow-sm border border-secondary" style={{ backdropFilter: 'blur(5px)', backgroundColor: 'rgba(33, 37, 41, 0.9)' }}>
                    <div style={{ width: '64px', height: '64px', position: 'relative' }} className="me-3">
                        {/* eslint-disable-next-line @next/next/no-img-element */}
                        <img
                            src={getItemImageUrl(item.tag, 'default', item.texture)}
                            alt={item.itemName}
                            className="w-100 h-100 object-fit-contain"
                            style={{ imageRendering: 'pixelated' }}
                        />
                    </div>
                    <div>
                        <h2 className="mb-0 text-white" style={{
                            color: item.tier === 'LEGENDARY' ? '#FFAA00' :
                                item.tier === 'EPIC' ? '#AA00AA' :
                                    item.tier === 'RARE' ? '#5555FF' :
                                        item.tier === 'UNCOMMON' ? '#55FF55' :
                                            item.tier === 'COMMON' ? '#FFFFFF' : '#FFFFFF',
                            textShadow: '2px 2px 4px rgba(0,0,0,0.5)'
                        }}>
                            {nameFilter ? `${nameFilter} Pet` : item.itemName}
                        </h2>
                    </div>
                </div>
            ) : (
                <div className="d-flex align-items-center mb-4 text-white">
                    <h2>{tag}</h2>
                </div>
            )}

            <ItemFilterPanel
                initialFilters={filters}
                onFilterChange={(newFilters) => setFilters(newFilters)}
            />

            {filteredAuctions.length === 0 ? (
                <Alert variant="info" className="bg-dark text-light border-secondary">No active auctions found for this item.</Alert>
            ) : (
                <Row xs={1} md={2} lg={3} xl={4} className="g-4">
                    {filteredAuctions.map(auction => (
                        <Col key={auction.uuid}>
                            <AuctionCard auction={auction} />
                        </Col>
                    ))}
                </Row>
            )}
        </Container>
    );
}
