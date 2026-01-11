'use client';

import { use, useEffect, useState } from 'react';
import { api, getItemImageUrl, getHeadImageUrl } from '@/api/ApiHelper';
import { getRarityColor, getRarityName } from '@/utils/rarity';
import { Auction } from '@/types';
import { Container, Row, Col, Card, Badge, Spinner, Button, Table } from 'react-bootstrap';
import Image from 'next/image';
import Link from 'next/link';
import { formatDistanceToNow, format } from 'date-fns';
import { toast } from '@/components/ToastProvider';
import PriceHistoryChart from '@/components/PriceHistoryChart';

export default function AuctionDetailPage({ params }: { params: Promise<{ uuid: string }> }) {
    const { uuid } = use(params);
    const [auction, setAuction] = useState<Auction | null>(null);
    const [loading, setLoading] = useState(true);

    const handleCopyUUID = async () => {
        if (!auction) return;
        const text = `/viewauction ${auction.uuid}`;
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
                setAuction(data);
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

    if (!auction) {
        return <div className="text-center mt-5"><h3>Auction not found</h3></div>;
    }

    const imageUrl = getItemImageUrl(auction.tag, 'default', auction.texture);
    const sellerImage = auction.auctioneerId ? getHeadImageUrl(auction.auctioneerId) : '';
    const rarityColor = getRarityColor(auction.tier);
    const rarityName = getRarityName(auction.tier);

    // Format enchantment name
    const formatEnchantmentName = (type: string | number | undefined): string => {
        if (!type) return 'Unknown';
        
        // Convert number to string if needed
        const typeStr = String(type);
        
        return typeStr
            .replace(/_/g, ' ')
            .replace(/\b\w/g, l => l.toUpperCase())
            .replace('Ultimate ', 'Ult. '); // Shorten ultimate enchants
    };

    // Format NBT key name
    const formatNBTKey = (key: string): string => {
        return key
            .replace(/_/g, ' ')
            .replace(/\b\w/g, l => l.toUpperCase());
    };

    // Format NBT value
    const formatNBTValue = (key: string, value: number | string | undefined): string => {
        if (value === undefined || value === null) return 'N/A';
        
        // Special formatting for certain keys
        if (key === 'timestamp' || key.includes('date')) {
            return format(new Date(Number(value) * 1000), 'PPpp');
        }
        
        if (typeof value === 'number') {
            return value.toLocaleString();
        }
        
        return String(value);
    };

    return (
        <Container className="py-5">
            <Link href={`/item/${auction.tag}`} className="btn btn-outline-secondary mb-4">&larr; Back to {auction.itemName}</Link>

            <Row className="gy-4">
                {/* Left: Item Preview */}
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
                        <h2 className="mt-3 fw-bold" style={{ color: rarityColor, textShadow: `0 0 15px ${rarityColor}60` }}>
                            {auction.itemName}
                        </h2>
                        <Badge bg="dark" className="fs-6 mb-2" style={{ color: rarityColor, border: `2px solid ${rarityColor}` }}>
                            {rarityName}
                        </Badge>
                        <div className="text-muted small">{auction.tag}</div>
                        {auction.count > 1 && (
                            <Badge bg="warning" text="dark" className="mt-2">
                                ×{auction.count}
                            </Badge>
                        )}
                    </Card>
                </Col>

                {/* Right: Auction Details */}
                <Col md={8}>
                    {/* Price & Status */}
                    <Card className="bg-dark border-secondary text-white mb-4">
                        <Card.Header className="bg-secondary bg-opacity-10 py-3">
                            <h4 className="m-0">Auction Details</h4>
                        </Card.Header>
                        <Card.Body>
                            <Row className="mb-4 text-center">
                                <Col>
                                    <div className="small text-muted text-uppercase">Price</div>
                                    <h3 className="text-warning fw-bold">
                                        {(auction.highestBidAmount || auction.startingBid).toLocaleString()} Coins
                                    </h3>
                                    {auction.bin && <Badge bg="success">BIN</Badge>}
                                </Col>
                                <Col>
                                    <div className="small text-muted text-uppercase">Seller</div>
                                    <div className="d-flex align-items-center justify-content-center mt-2">
                                        {sellerImage && <Image src={sellerImage} width={24} height={24} className="rounded-circle me-2" alt="Seller" unoptimized />}
                                        <span className="fw-500 small font-monospace">{auction.auctioneerId ? auction.auctioneerId.substring(0, 8) : 'Unknown'}</span>
                                    </div>
                                </Col>
                                <Col>
                                    <div className="small text-muted text-uppercase">Ends In</div>
                                    <div className="fw-bold mt-2">
                                        {new Date(auction.end) > new Date() ? formatDistanceToNow(new Date(auction.end)) : 'Expired'}
                                    </div>
                                </Col>
                            </Row>

                            {/* Enchantments */}
                            {auction.enchantments && auction.enchantments.length > 0 && (
                                <div className="mb-4">
                                    <h5 className="border-bottom border-secondary pb-2 mb-3">Enchantments</h5>
                                    <div className="d-flex flex-wrap gap-2">
                                        {auction.enchantments.map((ench, i) => {
                                            const enchName = formatEnchantmentName(ench.type || 'unknown');
                                            const isUltimate = String(ench.type || '').startsWith('ultimate_');
                                            return (
                                                <Badge 
                                                    key={i} 
                                                    bg={isUltimate ? 'danger' : 'primary'} 
                                                    className="p-2"
                                                    style={isUltimate ? { fontWeight: 'bold' } : {}}
                                                >
                                                    {enchName} {ench.level}
                                                </Badge>
                                            );
                                        })}
                                    </div>
                                </div>
                            )}

                            {/* NBT Data */}
                            {auction.nbtLookups && auction.nbtLookups.length > 0 && (
                                <div className="mb-4">
                                    <h5 className="border-bottom border-secondary pb-2 mb-3">NBT Data</h5>
                                    <Table striped bordered hover variant="dark" responsive size="sm">
                                        <tbody>
                                            {auction.nbtLookups.map((nbt, i) => {
                                                const keyName = nbt.nbtKey?.keyName || nbt.key || 'Unknown';
                                                const value = nbt.valueNumeric ?? nbt.nbtValue?.value ?? nbt.valueString ?? 'N/A';
                                                return (
                                                    <tr key={i}>
                                                        <td className="fw-bold" style={{ width: '40%' }}>
                                                            {formatNBTKey(keyName)}
                                                        </td>
                                                        <td>{formatNBTValue(keyName, value)}</td>
                                                    </tr>
                                                );
                                            })}
                                        </tbody>
                                    </Table>
                                </div>
                            )}

                            {/* Item Metadata */}
                            <div className="mb-4">
                                <h5 className="border-bottom border-secondary pb-2 mb-3">Item Data</h5>
                                <Table striped bordered hover variant="dark" responsive>
                                    <tbody>
                                        {auction.reforge && auction.reforge !== '0' && (
                                            <tr>
                                                <td className="fw-bold">Reforge</td>
                                                <td>{auction.reforge}</td>
                                            </tr>
                                        )}
                                        {auction.anvilUses !== undefined && auction.anvilUses > 0 && (
                                            <tr>
                                                <td className="fw-bold">Anvil Uses</td>
                                                <td>{auction.anvilUses}</td>
                                            </tr>
                                        )}
                                        <tr>
                                            <td className="fw-bold">Starting Bid</td>
                                            <td>{auction.startingBid.toLocaleString()} Coins</td>
                                        </tr>
                                        {auction.highestBidAmount > 0 && (
                                            <tr>
                                                <td className="fw-bold">Highest Bid</td>
                                                <td>{auction.highestBidAmount.toLocaleString()} Coins</td>
                                            </tr>
                                        )}
                                        <tr>
                                            <td className="fw-bold">Start Time</td>
                                            <td>{format(new Date(auction.start), 'PPpp')}</td>
                                        </tr>
                                        <tr>
                                            <td className="fw-bold">End Time</td>
                                            <td>{format(new Date(auction.end), 'PPpp')}</td>
                                        </tr>
                                        {auction.itemCreatedAt && (
                                            <tr>
                                                <td className="fw-bold">Item Created</td>
                                                <td>{format(new Date(auction.itemCreatedAt), 'PPpp')}</td>
                                            </tr>
                                        )}
                                         <tr>
                                             <td className="fw-bold">UUID</td>
                                             <td>
                                                 <div className="d-flex align-items-center justify-content-between">
                                                     <span className="font-monospace small">{auction.uuid}</span>
                                                     <Button
                                                         variant="outline-secondary"
                                                         size="sm"
                                                         onClick={handleCopyUUID}
                                                         className="ms-2"
                                                         style={{ minWidth: '50px' }}
                                                     >
                                                         📋
                                                     </Button>
                                                 </div>
                                             </td>
                                         </tr>
                                    </tbody>
                                </Table>
                            </div>

                            {/* Bids */}
                            {auction.bids && auction.bids.length > 0 && (
                                <div className="mb-4">
                                    <h5 className="border-bottom border-secondary pb-2 mb-3">Bids ({auction.bids.length})</h5>
                                    <Table striped bordered hover variant="dark" responsive>
                                        <thead>
                                            <tr>
                                                <th>Bidder</th>
                                                <th>Amount</th>
                                                <th>Time</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            {auction.bids
                                                .sort((a, b) => b.amount - a.amount)
                                                .map((bid, i) => (
                                                    <tr key={i}>
                                                        <td className="font-monospace small">{bid.bidderId.substring(0, 8)}...</td>
                                                        <td className="text-warning fw-bold">{bid.amount.toLocaleString()} Coins</td>
                                                        <td className="small">{format(new Date(bid.timestamp), 'PPp')}</td>
                                                    </tr>
                                                ))}
                                        </tbody>
                                    </Table>
                                </div>
                            )}

                            {/* Price History Chart */}
                            <div className="mb-4">
                                <h5 className="border-bottom border-secondary pb-2 mb-3">Price History</h5>
                                <PriceHistoryChart itemTag={auction.tag} height={300} />
                            </div>

                        </Card.Body>
                    </Card>
                </Col>
            </Row>
        </Container>
    );
}
