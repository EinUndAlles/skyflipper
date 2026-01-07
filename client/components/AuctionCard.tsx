'use client';

import { Card, Badge, Button } from 'react-bootstrap';
import { Auction } from '@/types';
import { getItemImageUrl } from '@/api/ApiHelper';
import { getRarityColor, getRarityName } from '@/utils/rarity';
import Image from 'next/image';
import Link from 'next/link';
import { formatDistanceToNow } from 'date-fns';

interface Props {
    auction: Auction;
}

export default function AuctionCard({ auction }: Props) {
    const imageUrl = getItemImageUrl(auction.tag);
    const timeLeft = new Date(auction.end) > new Date()
        ? formatDistanceToNow(new Date(auction.end), { addSuffix: true })
        : 'Ended';

    // Format price with commas
    const price = auction.price.toLocaleString();

    // Get rarity color and name
    const rarityColor = getRarityColor(auction.tier);
    const rarityName = getRarityName(auction.tier);

    return (
        <Card className="h-100 shadow-sm border-0" style={{ backgroundColor: '#1e1e1e', color: '#fff' }}>
            <Card.Body className="d-flex flex-column text-center align-items-center">
                <div style={{ position: 'relative', width: '96px', height: '96px', marginBottom: '1rem' }}>
                    <Image
                        src={imageUrl}
                        alt={auction.itemName}
                        fill
                        style={{ objectFit: 'contain', imageRendering: 'pixelated' }}
                        sizes="96px"
                        unoptimized
                    />
                </div>
                <Card.Title
                    className="h5 mb-1"
                    style={{ color: rarityColor, fontWeight: 'bold', textShadow: `0 0 10px ${rarityColor}40` }}
                >
                    {auction.itemName}
                </Card.Title>
                <Badge bg="dark" className="mb-2" style={{ color: rarityColor, borderColor: rarityColor, border: '1px solid' }}>
                    {rarityName}
                </Badge>
                <Card.Text className="text-muted small mb-3">{auction.tag}</Card.Text>

                <div className="mt-auto w-100">
                    <h4 className="fw-bold mb-3 text-warning">{price} Coins</h4>
                    <div className="d-flex justify-content-between align-items-center small text-secondary mb-3">
                        <span>{auction.bin ? 'BIN' : 'Auction'}</span>
                        <span>{timeLeft}</span>
                    </div>
                    <Link href={`/auction/${auction.uuid}`}>
                        <Button variant="outline-light" className="w-100">View Details</Button>
                    </Link>
                </div>
            </Card.Body>
        </Card>
    );
}
