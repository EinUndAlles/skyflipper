'use client';

import { useEffect, useState, use, useMemo, useCallback } from 'react';
import { useSearchParams, useRouter, usePathname } from 'next/navigation';
import { Container, Row, Col, Spinner, Alert } from 'react-bootstrap';
import { api, getItemImageUrl } from '@/api/ApiHelper';
import { Auction } from '@/types';
import AuctionCard from '@/components/AuctionCard';
import ItemFilterPanel from '@/components/ItemFilterPanel';
import PriceHistoryChart from '@/components/PriceHistoryChart';
import PriceSummary from '@/components/PriceSummary';
import RecentAuctions from '@/components/RecentAuctions';
import RelatedItems from '@/components/RelatedItems';
import { ItemFilter, FilterOptions } from '@/types/filters';
import { ItemFilter as PriceItemFilter } from '@/types/priceHistory';
import { toast } from '@/components/ToastProvider';

// Encode filters to base64 for URL
const encodeFilters = (filters: ItemFilter): string => {
    if (Object.keys(filters).length === 0) return '';
    try {
        return btoa(JSON.stringify(filters));
    } catch {
        return '';
    }
};

// Decode filters from base64 URL param
const decodeFilters = (encoded: string | null): ItemFilter => {
    if (!encoded) return {};
    try {
        return JSON.parse(atob(encoded));
    } catch {
        return {};
    }
};

interface ItemPageProps {
    params: Promise<{ tag: string }>;
    filters?: FilterOptions[];
}

// Helper to get tier color
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
        default: return '#FFFFFF';
    }
};

export default function ItemPage({ params, filters }: ItemPageProps) {
    const resolvedParams = use(params);
    const tag = resolvedParams.tag;
    const searchParams = useSearchParams();
    const router = useRouter();
    const pathname = usePathname();
    const nameFilter = searchParams.get('filter') || undefined;
    
    // Initialize filters from URL
    const urlFilters = searchParams.get('itemFilter');
    const initialFilters = useMemo(() => decodeFilters(urlFilters), [urlFilters]);

    const [auctions, setAuctions] = useState<Auction[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    const [activeFilters, setActiveFilters] = useState<ItemFilter>(initialFilters);
    const [filterOptions, setFilterOptions] = useState<FilterOptions[]>([]);
    
    // Sync filters to URL when they change
    const updateUrlWithFilters = useCallback((newFilters: ItemFilter) => {
        const params = new URLSearchParams(searchParams.toString());
        const encoded = encodeFilters(newFilters);
        
        if (encoded) {
            params.set('itemFilter', encoded);
        } else {
            params.delete('itemFilter');
        }
        
        const newUrl = `${pathname}?${params.toString()}`;
        router.replace(newUrl, { scroll: false });
    }, [pathname, router, searchParams]);

    const handleFiltersChange = useCallback((newFilters: ItemFilter) => {
        console.log('Filters changed:', newFilters);
        setActiveFilters(newFilters);
        updateUrlWithFilters(newFilters);
    }, [updateUrlWithFilters]);

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
    }, [tag, nameFilter]);

    // Convert UI filters to Coflnet API filter format for price chart
    const priceChartFilter = useMemo((): PriceItemFilter => {
        const filter: PriceItemFilter = {};
        
        // Map our filter keys to Coflnet's expected format
        if (activeFilters.Rarity) {
            filter.Rarity = activeFilters.Rarity;
        }
        if (activeFilters.Reforge) {
            filter.Reforge = activeFilters.Reforge;
        }
        if (activeFilters.Enchantment) {
            filter.Enchantment = activeFilters.Enchantment;
        }
        // Pet-specific: pass the name filter for pet level filtering
        if (nameFilter) {
            // Coflnet uses PetItem for pet name filtering
            filter.PetItem = nameFilter;
        }
        
        return filter;
    }, [activeFilters, nameFilter]);

    // Client-side filter application
    const filteredAuctions = useMemo(() => {
        let result = [...auctions];

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
    const displayName = nameFilter ? `${nameFilter} Pet` : (item?.itemName || tag);
    const tierColor = item ? getTierColor(item.tier) : '#FFFFFF';

    return (
        <Container className="py-4">
            {/* ===== SECTION 1: Item Header (Icon + Title) ===== */}
            <div className="d-flex align-items-center mb-4 p-3 bg-dark rounded shadow-sm border border-secondary" 
                 style={{ backdropFilter: 'blur(5px)', backgroundColor: 'rgba(33, 37, 41, 0.9)' }}>
                <div style={{ width: '64px', height: '64px', position: 'relative' }} className="me-3">
                    {/* eslint-disable-next-line @next/next/no-img-element */}
                    <img
                        src={item ? getItemImageUrl(item.tag, 'default', item.texture) : getItemImageUrl(tag, 'default')}
                        alt={displayName}
                        className="w-100 h-100 object-fit-contain"
                        style={{ imageRendering: 'pixelated' }}
                    />
                </div>
                <div>
                    <h2 className="mb-0" style={{
                        color: tierColor,
                        textShadow: '2px 2px 4px rgba(0,0,0,0.5)'
                    }}>
                        {displayName}
                    </h2>
                    {item && (
                        <small className="text-secondary">{item.tier}</small>
                    )}
                </div>
            </div>

            {/* ===== SECTION 2: Price Summary (Lowest BIN, Avg, Median) ===== */}
            <PriceSummary itemTag={tag} itemFilter={priceChartFilter} />

            {/* ===== SECTION 3: Price History Chart ===== */}
            <div className="mb-4">
                <PriceHistoryChart itemTag={tag} itemFilter={priceChartFilter} />
            </div>

            {/* ===== SECTION 4: Recent/Active Auctions ===== */}
            <div className="mb-4">
                <RecentAuctions itemTag={tag} itemFilter={priceChartFilter} />
            </div>

            {/* ===== SECTION 5: Related Items ===== */}
            <RelatedItems itemTag={tag} />

            {/* ===== SECTION 6: Auctions Section ===== */}
            <div className="mb-3">
                <h4 className="text-light mb-3">
                    Auctions
                    <span className="text-secondary ms-2" style={{ fontSize: '0.8em' }}>
                        ({filteredAuctions.length} found)
                    </span>
                </h4>
                
                {/* Filters */}
                <ItemFilterPanel
                    defaultFilter={activeFilters}
                    onFilterChange={handleFiltersChange}
                    filters={filterOptions}
                />
            </div>

            {/* Auction Cards Grid */}
            {filteredAuctions.length === 0 ? (
                <Alert variant="info" className="bg-dark text-light border-secondary">
                    No active auctions found for this item.
                </Alert>
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
