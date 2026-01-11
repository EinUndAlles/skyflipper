'use client';

import { use, useEffect, useState } from 'react';
import { api, getItemImageUrl, getHeadImageUrl } from '@/api/ApiHelper';
import { getRarityColor, getRarityName } from '@/utils/rarity';
import { Auction, AuctionWithProperties } from '@/types';
import { Container, Row, Col, Card, Badge, Spinner, Button, Table } from 'react-bootstrap';
import Image from 'next/image';
import Link from 'next/link';
import { formatDistanceToNow, format } from 'date-fns';
import { toast } from '@/components/ToastProvider';
import PriceHistoryChart from '@/components/PriceHistoryChart';

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

    // Get badge color for property categories
    const getPropertyBadgeColor = (category: string): string => {
        switch (category) {
            case 'Price': return 'warning';
            case 'Basic': return 'secondary';
            case 'Enhancement': return 'info';
            case 'Enchantment': return 'primary';
            case 'Pet': return 'success';
            case 'Gemstone': return 'danger';
            default: return 'dark';
        }
    };

    return (
        <Container className="py-5">
            <Link href={`/item/${auctionData.tag}`} className="btn btn-outline-secondary mb-4">&larr; Back to {auctionData.itemName}</Link>

            <Row className="gy-4">
                {/* Left: Item Preview */}
                <Col md={4} className="text-center">
                    <Card className="bg-dark border-secondary p-4 shadow-lg sticky-top" style={{ top: '20px' }}>
                        <div style={{ position: 'relative', width: '100%', height: '256px' }}>
                            <Image
                                src={imageUrl}
                                alt={auctionData.itemName}
                                fill
                                style={{ objectFit: 'contain', imageRendering: 'pixelated' }}
                                className="drop-shadow-lg"
                                unoptimized
                            />
                        </div>
                        <h2 className="mt-3 fw-bold" style={{ color: rarityColor, textShadow: `0 0 15px ${rarityColor}60` }}>
                            {auctionData.itemName}
                        </h2>
                        <Badge bg="dark" className="fs-6 mb-2" style={{ color: rarityColor, border: `2px solid ${rarityColor}` }}>
                            {rarityName}
                        </Badge>
                        <div className="text-muted small">{auctionData.tag}</div>
                        {auctionData.count > 1 && (
                            <Badge bg="warning" text="dark" className="mt-2">
                                ×{auctionData.count}
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
                                        {(auctionData.highestBidAmount || auctionData.startingBid).toLocaleString()} Coins
                                    </h3>
                                    {auctionData.bin && <Badge bg="success">BIN</Badge>}
                                </Col>
                                <Col>
                                    <div className="small text-muted text-uppercase">Seller</div>
                                    <div className="d-flex align-items-center justify-content-center mt-2">
                                        {sellerImage && <Image src={sellerImage} width={24} height={24} className="rounded-circle me-2" alt="Seller" unoptimized />}
                                        <span className="fw-500 small">{sellerName || (auctionData.auctioneerId ? 'Loading...' : 'Unknown')}</span>
                                    </div>
                                </Col>
                                <Col>
                                    <div className="small text-muted text-uppercase">Ends In</div>
                                    <div className="fw-bold mt-2">
                                        {new Date(auctionData.end) > new Date() ? formatDistanceToNow(new Date(auctionData.end)) : 'Expired'}
                                    </div>
                                </Col>
                            </Row>

                            {/* Item Properties */}
                            {auctionData.properties && auctionData.properties.length > 0 && (
                                <div className="mb-4">
                                    <h5 className="border-bottom border-secondary pb-2 mb-3">Item Properties</h5>
                                    <div className="row g-3">
                                        {/* Group properties by category */}
                                        {['Price', 'Basic', 'Enhancement', 'Enchantment', 'Pet', 'Gemstone'].map(category => {
                                            const categoryProps = auctionData.properties.filter(p => p.category === category);
                                            if (categoryProps.length === 0) return null;

                                            return (
                                                <div key={category} className="col-12">
                                                    <div className="mb-3">
                                                        <h6 className="text-muted mb-2">{category}</h6>
                                                        <div className="d-flex flex-wrap gap-2">
                                                            {categoryProps.map((prop, i) => (
                                                                <Badge
                                                                    key={i}
                                                                    bg={getPropertyBadgeColor(prop.category)}
                                                                    className="p-2"
                                                                >
                                                                    <strong>{prop.name}:</strong> {prop.value}
                                                                </Badge>
                                                            ))}
                                                        </div>
                                                    </div>
                                                </div>
                                            );
                                        })}
                                    </div>

                                    {/* Show enchantments separately if not in properties */}
                                    {auctionData.enchantments && auctionData.enchantments.length > 0 && !auctionData.properties.some(p => p.category === 'Enchantment') && (
                                        <div className="mt-3">
                                            <h6 className="text-muted mb-2">Enchantments</h6>
                                            <div className="d-flex flex-wrap gap-2">
                                                {auctionData.enchantments.map((ench, i) => {
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
                                </div>
                            )}

                            {/* Item Metadata */}
                            <div className="mb-4">
                                <h5 className="border-bottom border-secondary pb-2 mb-3">Item Data</h5>
                                <Table striped bordered hover variant="dark" responsive>
                                    <tbody>
                                        {auctionData.reforge && auctionData.reforge !== '0' && (
                                            <tr>
                                                <td className="fw-bold">Reforge</td>
                                                <td>{auctionData.reforge}</td>
                                            </tr>
                                        )}
                                        {auctionData.anvilUses !== undefined && auctionData.anvilUses > 0 && (
                                            <tr>
                                                <td className="fw-bold">Anvil Uses</td>
                                                <td>{auctionData.anvilUses}</td>
                                            </tr>
                                        )}
                                        <tr>
                                            <td className="fw-bold">Starting Bid</td>
                                            <td>{auctionData.startingBid.toLocaleString()} Coins</td>
                                        </tr>
                                        {auctionData.highestBidAmount > 0 && (
                                            <tr>
                                                <td className="fw-bold">Highest Bid</td>
                                                <td>{auctionData.highestBidAmount.toLocaleString()} Coins</td>
                                            </tr>
                                        )}
                                        <tr>
                                            <td className="fw-bold">Start Time</td>
                                            <td>{format(new Date(auctionData.start), 'PPpp')}</td>
                                        </tr>
                                        <tr>
                                            <td className="fw-bold">End Time</td>
                                            <td>{format(new Date(auctionData.end), 'PPpp')}</td>
                                        </tr>
                                        {auctionData.itemCreatedAt && (
                                            <tr>
                                                <td className="fw-bold">Item Created</td>
                                                <td>{format(new Date(auctionData.itemCreatedAt), 'PPpp')}</td>
                                            </tr>
                                        )}
                                         <tr>
                                             <td className="fw-bold">UUID</td>
                                             <td>
                                                 <div className="d-flex align-items-center justify-content-between">
                                                     <span className="font-monospace small">{auctionData.uuid}</span>
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
                            {auctionData.bids && auctionData.bids.length > 0 && (
                                <div className="mb-4">
                                    <h5 className="border-bottom border-secondary pb-2 mb-3">Bids ({auctionData.bids.length})</h5>
                                    <Table striped bordered hover variant="dark" responsive>
                                        <thead>
                                            <tr>
                                                <th>Bidder</th>
                                                <th>Amount</th>
                                                <th>Time</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            {auctionData.bids
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
                                <PriceHistoryChart itemTag={auctionData.tag} height={300} />
                            </div>

                        </Card.Body>
                    </Card>
                </Col>
            </Row>
        </Container>
    );
}
