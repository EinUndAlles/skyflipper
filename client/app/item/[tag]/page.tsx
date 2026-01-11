'use client';

import { useEffect, useState, use, useMemo } from 'react';
import { useSearchParams } from 'next/navigation';
import { Container, Row, Col, Spinner, Alert } from 'react-bootstrap';
import { api, getItemImageUrl } from '@/api/ApiHelper';
import { Auction } from '@/types';
import AuctionCard from '@/components/AuctionCard';
import ItemFilterPanel from '@/components/ItemFilterPanel';
import { ItemFilter, FilterOptions } from '@/types/filters';
import { toast } from '@/components/ToastProvider';

interface ItemPageProps {
    params: Promise<{ tag: string }>;
    filters?: FilterOptions[];
}

export default function ItemPage({ params, filters }: ItemPageProps) {
    const resolvedParams = use(params);
    const tag = resolvedParams.tag;
    const searchParams = useSearchParams();
    const nameFilter = searchParams.get('filter') || undefined;

    const [auctions, setAuctions] = useState<Auction[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    const [activeFilters, setActiveFilters] = useState<ItemFilter>({});
    const [filterOptions, setFilterOptions] = useState<FilterOptions[]>([]);

    const handleFiltersChange = (newFilters: ItemFilter) => {
        console.log('Filters changed:', newFilters);
        setActiveFilters(newFilters);
    };

    // Fetch filter options on mount
    useEffect(() => {
        const fetchFilters = async () => {
            try {
                const data = await api.getFilters(tag);
                setFilterOptions(data);
            } catch (err) {
                console.error('Failed to load filters:', err);
            }
        };
        if (tag) {
            fetchFilters();
        }
    }, [tag]);

    useEffect(() => {
        const fetchAuctions = async () => {
            try {
                setLoading(true);
                const data = await api.getAuctionsByTag(tag, 200, nameFilter);
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
    }, [tag, nameFilter]); // Refetch when tag or nameFilter changes

    // Client-side filter application
    const filteredAuctions = useMemo(() => {
        let result = [...auctions];
        console.log('Applying filters:', activeFilters, 'to', result.length, 'auctions');

        // BIN filter
        if (activeFilters.Bin === 'true') {
            result = result.filter(a => a.bin);
        } else if (activeFilters.Bin === 'false') {
            result = result.filter(a => !a.bin);
        }

        // Rarity filter
        if (activeFilters.Rarity) {
            result = result.filter(a => a.tier === activeFilters.Rarity);
        }

        // Min Price filter
        if (activeFilters.MinPrice) {
            const minPrice = parseInt(activeFilters.MinPrice);
            if (!isNaN(minPrice)) {
                result = result.filter(a => a.price >= minPrice);
            }
        }

        // Max Price filter
        if (activeFilters.MaxPrice) {
            const maxPrice = parseInt(activeFilters.MaxPrice);
            if (!isNaN(maxPrice)) {
                result = result.filter(a => a.price <= maxPrice);
            }
        }

        // Reforge filter
        if (activeFilters.Reforge) {
            result = result.filter(a =>
                a.reforge && a.reforge.toLowerCase() === activeFilters.Reforge.toLowerCase()
            );
        }

        // Enchantment filter
        if (activeFilters.Enchantment) {
            result = result.filter(a =>
                a.enchantments && a.enchantments.some(e => {
                    const enchType = typeof e.type === 'string' ? e.type : String(e.type);
                    return enchType.toLowerCase() === activeFilters.Enchantment.toLowerCase();
                })
            );
        }

        console.log('Filtered result:', result.length, 'auctions');
        return result;
    }, [auctions, activeFilters]);

    if (loading) {
        return (
            <Container className="d-flex justify-content-center align-items-center" style={{ minHeight: '60vh' }}>
                <Spinner animation="border" role="status" variant="primary">
                    <span className="visually-hidden">Loading...</span>
                </Spinner>
            </Container>
        );
    }

    if (error) {
        return (
            <Container className="mt-4">
                <Alert variant="danger">{error}</Alert>
            </Container>
        );
    }

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
                defaultFilter={activeFilters}
                onFilterChange={handleFiltersChange}
                filters={filterOptions}
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
