'use client';

import { use, useEffect, useState } from 'react';
import { api, getItemImageUrl, getHeadImageUrl } from '@/api/ApiHelper';
import { getRarityColor, getRarityName } from '@/utils/rarity';
import { Auction } from '@/types';
import { Container, Row, Col, Card, Badge, Spinner, Button, Table } from 'react-bootstrap';
import Image from 'next/image';
import Link from 'next/link';
import { formatDistanceToNow, format } from 'date-fns';

export default function AuctionDetailPage({ params }: { params: Promise<{ uuid: string }> }) {
    const { uuid } = use(params);
    const [auction, setAuction] = useState<Auction | null>(null);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        const fetchAuction = async () => {
            try {
                const data = await api.getAuction(uuid);
                setAuction(data);
            } catch (e) {
                console.error(e);
            } finally {
                setLoading(false);
            }
        };
        fetchAuction();
    }, [uuid]);

    if (loading) { // Should use a proper skeleton loader ideally
        return <div className="text-center mt-5"><Spinner animation="border" variant="primary" /></div>;
    }

    if (!auction) {
        return <div className="text-center mt-5"><h3>Auction not found</h3></div>;
    }

    const imageUrl = getItemImageUrl(auction.tag);
    const sellerImage = auction.auctioneerId ? getHeadImageUrl(auction.auctioneerId) : '';
    const rarityColor = getRarityColor(auction.tier);
    const rarityName = getRarityName(auction.tier);

    return (
        <Container className="py-5">
            <Link href="/search" className="btn btn-outline-secondary mb-4">&larr; Back to Search</Link>

            <Row className="gy-4">
                <Col md={4} className="text-center">
                    <Card className="bg-dark border-secondary p-4 shadow-lg sticky-top" style={{ top: '20px' }}>
                        <div style={{ position: 'relative', width: '100%', height: '256px' }}>
                            <Image
                                src={imageUrl}
                                alt={auction.itemName}
                                fill
                                style={{ objectFit: 'contain', imageRendering: 'pixelated' }}
                                className="drop-shadow-lg"
                                unoptimized
                            />
                        </div>
                        <h2 className="mt-3 fw-bold" style={{ color: rarityColor, textShadow: `0 0 15px ${rarityColor}60` }}>{auction.itemName}</h2>
                        <Badge bg="dark" className="fs-6 mb-2" style={{ color: rarityColor, borderColor: rarityColor, border: '2px solid' }}>{rarityName}</Badge>
                        <div className="text-muted small">{auction.tag}</div>
                    </Card>
                </Col>

                <Col md={8}>
                    <Card className="bg-dark border-secondary text-white mb-4">
                        <Card.Header className="bg-secondary bg-opacity-10 py-3">
                            <h4 className="m-0">Auction Details</h4>
                        </Card.Header>
                        <Card.Body>
                            <Row className="mb-4 text-center">
                                <Col>
                                    <div className="small text-muted text-uppercase ls-1">Price</div>
                                    <h3 className="text-warning fw-bold">{(auction.highestBidAmount || auction.startingBid).toLocaleString()} Coins</h3>
                                    {auction.bin && <Badge bg="success">BIN</Badge>}
                                </Col>
                                <Col>
                                    <div className="small text-muted text-uppercase ls-1">Seller</div>
                                    <div className="d-flex align-items-center justify-content-center mt-2">
                                        {sellerImage && <Image src={sellerImage} width={24} height={24} className="rounded-circle me-2" alt="Seller" unoptimized />}
                                        <span className="fw-500">{auction.auctioneerId || 'Unknown'}</span>
                                    </div>
                                </Col>
                                <Col>
                                    <div className="small text-muted text-uppercase ls-1">Ends In</div>
                                    <div className="fw-bold mt-2">{new Date(auction.end) > new Date() ? formatDistanceToNow(new Date(auction.end)) : 'Expired'}</div>
                                </Col>
                            </Row>

                            {auction.enchantments && auction.enchantments.length > 0 && (
                                <div className="mb-4">
                                    <h5 className="border-bottom border-secondary pb-2 mb-3">Enchantments</h5>
                                    <div className="d-flex flex-wrap gap-2">
                                        {auction.enchantments.map((ench, i) => (
                                            <Badge key={i} bg="primary" className="p-2">
                                                {ench.name} {ench.level}
                                            </Badge>
                                        ))}
                                    </div>
                                </div>
                            )}

                            <div className="mb-4">
                                <h5 className="border-bottom border-secondary pb-2 mb-3">Item Data</h5>
                                <Table striped bordered hover variant="dark" responsive>
                                    <tbody>
                                        <tr><td>Reforge</td><td>{auction.reforge}</td></tr>
                                        <tr><td>Count</td><td>{auction.count}</td></tr>
                                        <tr><td>Start Time</td><td>{format(new Date(auction.start), 'PPpp')}</td></tr>
                                        <tr><td>UUID</td><td className="font-monospace small">{auction.uuid}</td></tr>
                                    </tbody>
                                </Table>
                            </div>

                            {/* Placeholder for Price Graph */}
                            <div className="p-4 border border-secondary border-dashed rounded text-center text-muted bg-black bg-opacity-25">
                                <h5 className="mb-3">Price History</h5>
                                <p>Price graph visualization coming soon...</p>
                            </div>

                        </Card.Body>
                    </Card>
                </Col>
            </Row>
        </Container>
    );
}
