'use client';

import { useSearchParams, useRouter, usePathname } from 'next/navigation';
import { useEffect, useState, Suspense, useMemo, useCallback } from 'react';
import { api } from '@/api/ApiHelper';
import { Auction } from '@/types';
import AuctionCard from '@/components/AuctionCard';
import { Container, Row, Col, Spinner, Alert, Form, ButtonGroup, Button, Card } from 'react-bootstrap';
import { toast } from '@/components/ToastProvider';

type SortOption = 'price_asc' | 'price_desc' | 'ending_soon' | 'newest';
type RarityFilter = '' | 'COMMON' | 'UNCOMMON' | 'RARE' | 'EPIC' | 'LEGENDARY' | 'MYTHIC' | 'SPECIAL';

function SearchContent() {
    const searchParams = useSearchParams();
    const router = useRouter();
    const pathname = usePathname();
    
    const q = searchParams.get('q') || '';
    const initialSort = (searchParams.get('sort') as SortOption) || 'price_asc';
    const initialRarity = (searchParams.get('rarity') as RarityFilter) || '';
    const initialBinOnly = searchParams.get('bin') !== 'false';
    const initialMinPrice = searchParams.get('minPrice') || '';
    const initialMaxPrice = searchParams.get('maxPrice') || '';
    
    const [auctions, setAuctions] = useState<Auction[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    
    // Filter state
    const [sortBy, setSortBy] = useState<SortOption>(initialSort);
    const [rarityFilter, setRarityFilter] = useState<RarityFilter>(initialRarity);
    const [binOnly, setBinOnly] = useState(initialBinOnly);
    const [minPrice, setMinPrice] = useState(initialMinPrice);
    const [maxPrice, setMaxPrice] = useState(initialMaxPrice);
    
    // Update URL with filters
    const updateUrl = useCallback((updates: Record<string, string>) => {
        const params = new URLSearchParams(searchParams.toString());
        Object.entries(updates).forEach(([key, value]) => {
            if (value) {
                params.set(key, value);
            } else {
                params.delete(key);
            }
        });
        router.replace(`${pathname}?${params.toString()}`, { scroll: false });
    }, [pathname, router, searchParams]);

    useEffect(() => {
        const fetchAuctions = async () => {
            setLoading(true);
            setError('');
            try {
                let results: Auction[] = [];
                if (q) {
                    results = await api.getAuctionsByTag(
                        q, 
                        200, 
                        undefined, 
                        binOnly, 
                        false,
                        undefined,
                        undefined,
                        undefined,
                        undefined,
                        minPrice ? parseInt(minPrice) : undefined,
                        maxPrice ? parseInt(maxPrice) : undefined
                    );
                } else {
                    results = await api.getRecentAuctions(200);
                }
                setAuctions(results);
            } catch (err) {
                console.error(err);
                toast.error('Failed to fetch auctions');
                setError('Failed to fetch auctions. Please try again.');
            } finally {
                setLoading(false);
            }
        };

        fetchAuctions();
    }, [q, binOnly, minPrice, maxPrice]);

    // Apply client-side filtering and sorting
    const filteredAndSortedAuctions = useMemo(() => {
        let result = [...auctions];
        
        // Apply rarity filter
        if (rarityFilter) {
            result = result.filter(a => a.tier === rarityFilter);
        }
        
        // Apply sorting
        switch (sortBy) {
            case 'price_asc':
                result.sort((a, b) => a.price - b.price);
                break;
            case 'price_desc':
                result.sort((a, b) => b.price - a.price);
                break;
            case 'ending_soon':
                result.sort((a, b) => new Date(a.end).getTime() - new Date(b.end).getTime());
                break;
            case 'newest':
                result.sort((a, b) => new Date(b.fetchedAt || b.end).getTime() - new Date(a.fetchedAt || a.end).getTime());
                break;
        }
        
        return result;
    }, [auctions, rarityFilter, sortBy]);

    const handleSortChange = (newSort: SortOption) => {
        setSortBy(newSort);
        updateUrl({ sort: newSort });
    };

    const handleRarityChange = (newRarity: RarityFilter) => {
        setRarityFilter(newRarity);
        updateUrl({ rarity: newRarity });
    };

    const handleBinToggle = () => {
        const newBin = !binOnly;
        setBinOnly(newBin);
        updateUrl({ bin: newBin ? '' : 'false' });
    };

    const handlePriceFilter = () => {
        updateUrl({ minPrice, maxPrice });
    };

    const clearFilters = () => {
        setSortBy('price_asc');
        setRarityFilter('');
        setBinOnly(true);
        setMinPrice('');
        setMaxPrice('');
        router.replace(q ? `${pathname}?q=${q}` : pathname, { scroll: false });
    };

    const hasActiveFilters = rarityFilter || !binOnly || minPrice || maxPrice || sortBy !== 'price_asc';

    return (
        <Container className="py-4">
            <h2 className="mb-4 text-light">
                {q ? `Results for "${q}"` : 'Browse Auctions'}
                <span className="text-secondary ms-2" style={{ fontSize: '0.6em' }}>
                    ({filteredAndSortedAuctions.length} found)
                </span>
            </h2>

            {/* Filters Card */}
            <Card className="bg-dark border-secondary mb-4">
                <Card.Body>
                    <Row className="g-3 align-items-end">
                        {/* Sort */}
                        <Col xs={12} md={3}>
                            <Form.Label className="text-light small mb-1">Sort By</Form.Label>
                            <Form.Select 
                                size="sm"
                                className="bg-dark text-light border-secondary"
                                value={sortBy}
                                onChange={(e) => handleSortChange(e.target.value as SortOption)}
                            >
                                <option value="price_asc">Price: Low to High</option>
                                <option value="price_desc">Price: High to Low</option>
                                <option value="ending_soon">Ending Soon</option>
                                <option value="newest">Newest First</option>
                            </Form.Select>
                        </Col>

                        {/* Rarity Filter */}
                        <Col xs={12} md={3}>
                            <Form.Label className="text-light small mb-1">Rarity</Form.Label>
                            <Form.Select 
                                size="sm"
                                className="bg-dark text-light border-secondary"
                                value={rarityFilter}
                                onChange={(e) => handleRarityChange(e.target.value as RarityFilter)}
                            >
                                <option value="">All Rarities</option>
                                <option value="COMMON">Common</option>
                                <option value="UNCOMMON">Uncommon</option>
                                <option value="RARE">Rare</option>
                                <option value="EPIC">Epic</option>
                                <option value="LEGENDARY">Legendary</option>
                                <option value="MYTHIC">Mythic</option>
                                <option value="SPECIAL">Special</option>
                            </Form.Select>
                        </Col>

                        {/* Price Range */}
                        <Col xs={6} md={2}>
                            <Form.Label className="text-light small mb-1">Min Price</Form.Label>
                            <Form.Control
                                type="number"
                                size="sm"
                                className="bg-dark text-light border-secondary"
                                placeholder="0"
                                value={minPrice}
                                onChange={(e) => setMinPrice(e.target.value)}
                                onBlur={handlePriceFilter}
                                onKeyDown={(e) => e.key === 'Enter' && handlePriceFilter()}
                            />
                        </Col>
                        <Col xs={6} md={2}>
                            <Form.Label className="text-light small mb-1">Max Price</Form.Label>
                            <Form.Control
                                type="number"
                                size="sm"
                                className="bg-dark text-light border-secondary"
                                placeholder="Any"
                                value={maxPrice}
                                onChange={(e) => setMaxPrice(e.target.value)}
                                onBlur={handlePriceFilter}
                                onKeyDown={(e) => e.key === 'Enter' && handlePriceFilter()}
                            />
                        </Col>

                        {/* BIN Only & Clear */}
                        <Col xs={12} md={2}>
                            <div className="d-flex gap-2">
                                <Button
                                    size="sm"
                                    variant={binOnly ? 'success' : 'outline-secondary'}
                                    onClick={handleBinToggle}
                                    className="flex-grow-1"
                                >
                                    BIN Only
                                </Button>
                                {hasActiveFilters && (
                                    <Button
                                        size="sm"
                                        variant="outline-danger"
                                        onClick={clearFilters}
                                        title="Clear all filters"
                                    >
                                        ✕
                                    </Button>
                                )}
                            </div>
                        </Col>
                    </Row>
                </Card.Body>
            </Card>

            {loading && (
                <div className="text-center py-5">
                    <Spinner animation="border" variant="primary" />
                    <p className="text-light mt-2">Loading auctions...</p>
                </div>
            )}

            {error && <Alert variant="danger">{error}</Alert>}

            {!loading && !error && filteredAndSortedAuctions.length === 0 && (
                <Alert variant="info" className="bg-dark text-light border-secondary">
                    No auctions found matching your criteria.
                    {hasActiveFilters && (
                        <Button variant="link" className="p-0 ms-2" onClick={clearFilters}>
                            Clear filters
                        </Button>
                    )}
                </Alert>
            )}

            {!loading && !error && filteredAndSortedAuctions.length > 0 && (
                <Row xs={1} md={2} lg={3} xl={4} className="g-4">
                    {filteredAndSortedAuctions.map((auction) => (
                        <Col key={auction.uuid}>
                            <AuctionCard auction={auction} />
                        </Col>
                    ))}
                </Row>
            )}
        </Container>
    );
}

export default function SearchPage() {
    return (
        <Suspense fallback={
            <Container className="text-center py-5">
                <Spinner animation="border" variant="primary" />
                <p className="text-light mt-2">Loading...</p>
            </Container>
        }>
            <SearchContent />
        </Suspense>
    );
}
