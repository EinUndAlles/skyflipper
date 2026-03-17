'use client';

import { use, useEffect, useState } from 'react';
import { api, getItemImageUrl, getHeadImageUrl } from '@/api/ApiHelper';
import { getRarityColor, getRarityName } from '@/utils/rarity';
import { AuctionWithProperties } from '@/types';
import { Container, Row, Col, Card, Badge, Spinner, Button } from 'react-bootstrap';
import Image from 'next/image';
import Link from 'next/link';
import { formatDistanceToNow, format } from 'date-fns';
import { toast } from '@/components/ToastProvider';
import PriceHistoryChart from '@/components/PriceHistoryChart';
import ItemLoreCard from '@/components/ItemLoreCard/ItemLoreCard';

export default function AuctionDetailPage({ params }: { params: Promise<{ uuid: string }> }) {
    const { uuid } = use(params);
    const [auctionData, setAuctionData] = useState<AuctionWithProperties | null>(null);
    const [loading, setLoading] = useState(true);
    const [sellerName, setSellerName] = useState<string>('');

    const handleCopyUUID = async () => {
        if (!auctionData) return;
        const text = `/viewauction ${auctionData.uuid}`;
        try {
            await navigator.clipboard.writeText(text);
            toast.success('Copied to clipboard!');
        } catch (error) {
            toast.error('Failed to copy to clipboard');
        }
    };

    useEffect(() => {
        const fetchAuction = async () => {
            try {
                const data = await api.getAuction(uuid);
                setAuctionData(data);

                // Fetch seller name if auctioneerId exists
                if (data.auctioneerId) {
                    const name = await api.getPlayerName(data.auctioneerId);
                    setSellerName(name);
                }
            } catch (e) {
                console.error(e);
                toast.error('Failed to load auction details');
            } finally {
                setLoading(false);
            }
        };
        fetchAuction();
    }, [uuid]);

    if (loading) {
        return <div className="text-center mt-5"><Spinner animation="border" variant="primary" /></div>;
    }

    if (!auctionData) {
        return <div className="text-center mt-5"><h3>Auction not found</h3></div>;
    }

    const imageUrl = getItemImageUrl(auctionData.tag, 'default', auctionData.texture);
    const sellerImage = auctionData.auctioneerId ? getHeadImageUrl(auctionData.auctioneerId) : '';
    const rarityColor = getRarityColor(auctionData.tier);
    const rarityName = getRarityName(auctionData.tier);

    return (
        <Container className="py-5">
            <Link href={`/item/${auctionData.tag}`} className="btn btn-outline-secondary mb-4">&larr; Back to {auctionData.itemName}</Link>

            <Row className="gy-4">
                {/* Left: Item Preview with Lore Card - Single unified box */}
                <Col lg={5}>
                    <div className="sticky-top" style={{ top: '20px' }}>
                        <ItemLoreCard 
                            auction={auctionData} 
                            imageUrl={imageUrl}
                            rarityColor={rarityColor}
                        />
                    </div>
                </Col>

                {/* Right: Auction Details */}
                <Col lg={7}>
                    {/* Price & Status Card */}
                    <Card className="bg-dark border-secondary text-white mb-4">
                        <Card.Header className="bg-secondary bg-opacity-10 py-3 d-flex justify-content-between align-items-center">
                            <h4 className="m-0">Auction</h4>
                            <div className="d-flex gap-2 align-items-center">
                                {auctionData.bin && <Badge bg="success" className="fs-6">BIN</Badge>}
                                <Button
                                    variant="outline-light"
                                    size="sm"
                                    onClick={handleCopyUUID}
                                    title="Copy /viewauction command"
                                >
                                    📋 Copy
                                </Button>
                            </div>
                        </Card.Header>
                        <Card.Body>
                            {/* Price Row */}
                            <Row className="mb-4 text-center">
                                <Col>
                                    <div className="small text-muted text-uppercase">Price</div>
                                    <h2 className="text-warning fw-bold mb-0" style={{ fontFamily: 'monospace' }}>
                                        {(auctionData.highestBidAmount || auctionData.startingBid).toLocaleString()}
                                    </h2>
                                    <div className="text-muted small">coins</div>
                                </Col>
                                <Col>
                                    <div className="small text-muted text-uppercase">Seller</div>
                                    <div className="d-flex align-items-center justify-content-center mt-2">
                                        {sellerImage && <Image src={sellerImage} width={24} height={24} className="rounded me-2" alt="Seller" unoptimized style={{ imageRendering: 'pixelated' }} />}
                                        <span className="fw-500">{sellerName || (auctionData.auctioneerId ? 'Loading...' : 'Unknown')}</span>
                                    </div>
                                </Col>
                                <Col>
                                    <div className="small text-muted text-uppercase">Ends</div>
                                    <div className="fw-bold mt-2">
                                        {new Date(auctionData.end) > new Date() ? formatDistanceToNow(new Date(auctionData.end)) : 'Expired'}
                                    </div>
                                </Col>
                            </Row>

                            {/* Auction Metadata */}
                            <div className="border-top border-secondary pt-3">
                                <Row className="g-3 small">
                                    {/* Only show Starting Bid and Highest Bid for non-BIN auctions */}
                                    {!auctionData.bin && (
                                        <>
                                            <Col xs={6}>
                                                <span className="text-muted">Starting Bid:</span>
                                                <span className="float-end">{auctionData.startingBid.toLocaleString()}</span>
                                            </Col>
                                            {auctionData.highestBidAmount > 0 && (
                                                <Col xs={6}>
                                                    <span className="text-muted">Highest Bid:</span>
                                                    <span className="float-end text-warning">{auctionData.highestBidAmount.toLocaleString()}</span>
                                                </Col>
                                            )}
                                        </>
                                    )}
                                    <Col xs={6}>
                                        <span className="text-muted">Started:</span>
                                        <span className="float-end">{format(new Date(auctionData.start), 'MMM d, h:mm a')}</span>
                                    </Col>
                                    <Col xs={6}>
                                        <span className="text-muted">Ends:</span>
                                        <span className="float-end">{format(new Date(auctionData.end), 'MMM d, h:mm a')}</span>
                                    </Col>
                                    {auctionData.itemCreatedAt && (
                                        <Col xs={6}>
                                            <span className="text-muted">Item Created:</span>
                                            <span className="float-end">{format(new Date(auctionData.itemCreatedAt), 'MMM d, yyyy')}</span>
                                        </Col>
                                    )}
                                    {auctionData.anvilUses !== undefined && auctionData.anvilUses > 0 && (
                                        <Col xs={6}>
                                            <span className="text-muted">Anvil Uses:</span>
                                            <span className="float-end">{auctionData.anvilUses}</span>
                                        </Col>
                                    )}
                                </Row>
                            </div>
                        </Card.Body>
                    </Card>

                    {/* Bids Card */}
                    {auctionData.bids && auctionData.bids.length > 0 && (
                        <Card className="bg-dark border-secondary text-white mb-4">
                            <Card.Header className="bg-secondary bg-opacity-10 py-2">
                                <h5 className="m-0">Bids ({auctionData.bids.length})</h5>
                            </Card.Header>
                            <Card.Body className="p-0">
                                <div style={{ maxHeight: '200px', overflowY: 'auto' }}>
                                    {auctionData.bids
                                        .sort((a, b) => b.amount - a.amount)
                                        .map((bid, i) => (
                                            <div 
                                                key={i} 
                                                className={`d-flex justify-content-between align-items-center p-2 ${i === 0 ? 'bg-success bg-opacity-10' : ''}`}
                                                style={{ borderBottom: '1px solid #333' }}
                                            >
                                                <span className="font-monospace small text-muted">{bid.bidderId.substring(0, 8)}...</span>
                                                <span className={i === 0 ? 'text-success fw-bold' : 'text-warning'}>{bid.amount.toLocaleString()}</span>
                                                <span className="small text-muted">{format(new Date(bid.timestamp), 'h:mm a')}</span>
                                            </div>
                                        ))}
                                </div>
                            </Card.Body>
                        </Card>
                    )}

                    {/* Price History Chart */}
                    <Card className="bg-dark border-secondary text-white">
                        <Card.Header className="bg-secondary bg-opacity-10 py-2">
                            <h5 className="m-0">Price History</h5>
                        </Card.Header>
                        <Card.Body>
                            <PriceHistoryChart itemTag={auctionData.tag} height={250} />
                        </Card.Body>
                    </Card>
                </Col>
            </Row>
        </Container>
    );
}
