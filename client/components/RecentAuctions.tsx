'use client';

import { useEffect, useState } from 'react';
import { Card, Nav, Row, Col, Spinner, Button } from 'react-bootstrap';
import { api, getItemImageUrl, getHeadImageUrl } from '@/api/ApiHelper';
import { ItemFilter } from '@/types/priceHistory';

interface RecentAuctionsProps {
    itemTag: string;
    itemFilter?: ItemFilter;
}

// Format large numbers with K, M, B suffixes
const formatPrice = (price: number | null | undefined): string => {
    if (price === null || price === undefined) return 'N/A';
    if (price >= 1_000_000_000) return `${(price / 1_000_000_000).toFixed(2)}B`;
    if (price >= 1_000_000) return `${(price / 1_000_000).toFixed(2)}M`;
    if (price >= 1_000) return `${(price / 1_000).toFixed(1)}K`;
    return price.toLocaleString();
};

// Format time remaining
const formatTimeAgo = (date: string | Date): string => {
    const now = new Date();
    const then = new Date(date);
    const seconds = Math.floor((now.getTime() - then.getTime()) / 1000);
    
    if (seconds < 60) return `${seconds}s ago`;
    if (seconds < 3600) return `${Math.floor(seconds / 60)}m ago`;
    if (seconds < 86400) return `${Math.floor(seconds / 3600)}h ago`;
    return `${Math.floor(seconds / 86400)}d ago`;
};

const formatTimeRemaining = (seconds: number): string => {
    if (seconds <= 0) return 'Ended';
    if (seconds < 60) return `${Math.floor(seconds)}s`;
    if (seconds < 3600) return `${Math.floor(seconds / 60)}m`;
    if (seconds < 86400) return `${Math.floor(seconds / 3600)}h`;
    return `${Math.floor(seconds / 86400)}d`;
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
        default: return '#FFFFFF';
    }
};

type TabType = 'active' | 'sold';
type SortType = 'price' | 'price_desc' | 'ending';

export default function RecentAuctions({ itemTag, itemFilter }: RecentAuctionsProps) {
    const [activeTab, setActiveTab] = useState<TabType>('active');
    const [sort, setSort] = useState<SortType>('price');
    const [loading, setLoading] = useState(true);
    const [auctions, setAuctions] = useState<any[]>([]);
    const [total, setTotal] = useState(0);
    const [page, setPage] = useState(0);
    const [hasMore, setHasMore] = useState(false);

    useEffect(() => {
        const fetchData = async () => {
            setLoading(true);
            try {
                if (activeTab === 'active') {
                    const data = await api.getActiveAuctions(itemTag, sort, page, 12, itemFilter);
                    setAuctions(prev => page === 0 ? data.auctions : [...prev, ...data.auctions]);
                    setTotal(data.total);
                    setHasMore(data.hasMore);
                } else {
                    const data = await api.getSoldAuctions(itemTag, page, 12, itemFilter);
                    setAuctions(prev => page === 0 ? data.auctions : [...prev, ...data.auctions]);
                    setTotal(data.total);
                    setHasMore(data.hasMore);
                }
            } catch (err) {
                console.error('Failed to load auctions:', err);
            } finally {
                setLoading(false);
            }
        };

        fetchData();
    }, [itemTag, itemFilter, activeTab, sort, page]);

    // Reset when tab or sort changes
    useEffect(() => {
        setPage(0);
        setAuctions([]);
    }, [activeTab, sort, itemFilter]);

    const handleTabChange = (tab: TabType) => {
        setActiveTab(tab);
    };

    const handleSortChange = (newSort: SortType) => {
        setSort(newSort);
    };

    const loadMore = () => {
        setPage(prev => prev + 1);
    };

    return (
        <Card bg="dark" text="light" className="border-secondary">
            <Card.Header className="d-flex justify-content-between align-items-center">
                <Nav variant="tabs" className="border-0">
                    <Nav.Item>
                        <Nav.Link 
                            active={activeTab === 'active'}
                            onClick={() => handleTabChange('active')}
                            className={activeTab === 'active' ? 'bg-primary text-white' : 'text-light'}
                        >
                            Active ({activeTab === 'active' ? total : '...'})
                        </Nav.Link>
                    </Nav.Item>
                    <Nav.Item>
                        <Nav.Link 
                            active={activeTab === 'sold'}
                            onClick={() => handleTabChange('sold')}
                            className={activeTab === 'sold' ? 'bg-primary text-white' : 'text-light'}
                        >
                            Sold ({activeTab === 'sold' ? total : '...'})
                        </Nav.Link>
                    </Nav.Item>
                </Nav>

                {activeTab === 'active' && (
                    <div className="d-flex gap-1">
                        <Button 
                            size="sm" 
                            variant={sort === 'price' ? 'primary' : 'outline-secondary'}
                            onClick={() => handleSortChange('price')}
                        >
                            Lowest
                        </Button>
                        <Button 
                            size="sm" 
                            variant={sort === 'price_desc' ? 'primary' : 'outline-secondary'}
                            onClick={() => handleSortChange('price_desc')}
                        >
                            Highest
                        </Button>
                        <Button 
                            size="sm" 
                            variant={sort === 'ending' ? 'primary' : 'outline-secondary'}
                            onClick={() => handleSortChange('ending')}
                        >
                            Ending
                        </Button>
                    </div>
                )}
            </Card.Header>

            <Card.Body style={{ maxHeight: '400px', overflowY: 'auto' }}>
                {loading && auctions.length === 0 ? (
                    <div className="d-flex justify-content-center py-4">
                        <Spinner animation="border" size="sm" />
                    </div>
                ) : auctions.length === 0 ? (
                    <div className="text-center text-muted py-4">
                        No {activeTab} auctions found
                    </div>
                ) : (
                    <>
                        <Row xs={1} md={2} lg={3} className="g-2">
                            {auctions.map((auction, idx) => (
                                <Col key={`${auction.uuid}-${idx}`}>
                                    <Card 
                                        bg="secondary" 
                                        className="h-100 border-0"
                                        style={{ cursor: 'pointer' }}
                                        onClick={() => window.location.href = `/auction/${auction.uuid}`}
                                    >
                                        <Card.Body className="p-2 d-flex align-items-center">
                                            {/* Item Icon */}
                                            <div style={{ width: '40px', height: '40px' }} className="me-2 flex-shrink-0">
                                                {/* eslint-disable-next-line @next/next/no-img-element */}
                                                <img
                                                    src={getItemImageUrl(auction.tag, 'default', auction.texture)}
                                                    alt={auction.itemName}
                                                    className="w-100 h-100"
                                                    style={{ imageRendering: 'pixelated', objectFit: 'contain' }}
                                                />
                                            </div>

                                            {/* Auction Info */}
                                            <div className="flex-grow-1 min-w-0">
                                                <div 
                                                    className="text-truncate small fw-bold"
                                                    style={{ color: getTierColor(auction.tier) }}
                                                >
                                                    {auction.itemName}
                                                </div>
                                                <div className="d-flex justify-content-between align-items-center">
                                                    <span className="text-warning fw-bold">
                                                        {formatPrice(auction.price)}
                                                    </span>
                                                    <span className="text-muted small">
                                                        {activeTab === 'active' 
                                                            ? formatTimeRemaining(auction.timeRemaining)
                                                            : formatTimeAgo(auction.soldAt)
                                                        }
                                                    </span>
                                                </div>
                                            </div>

                                            {/* BIN Badge */}
                                            {auction.bin && (
                                                <span className="badge bg-info ms-1">BIN</span>
                                            )}
                                        </Card.Body>
                                    </Card>
                                </Col>
                            ))}
                        </Row>

                        {hasMore && (
                            <div className="text-center mt-3">
                                <Button 
                                    variant="outline-primary" 
                                    size="sm"
                                    onClick={loadMore}
                                    disabled={loading}
                                >
                                    {loading ? <Spinner animation="border" size="sm" /> : 'Load More'}
                                </Button>
                            </div>
                        )}
                    </>
                )}
            </Card.Body>
        </Card>
    );
}
