'use client';

import { Card, Badge, Button } from 'react-bootstrap';
import { useState, useEffect } from 'react';
import { Auction } from '@/types';
import { getItemImageUrl } from '@/api/ApiHelper';
import { getRarityColor, getRarityName } from '@/utils/rarity';
import Image from 'next/image';
import Link from 'next/link';


interface Props {
    auction: Auction;
}

export default function AuctionCard({ auction }: Props) {
    const [timeLeft, setTimeLeft] = useState('');

    useEffect(() => {
        const updateTime = () => {
            const now = new Date();
            const endDate = new Date(auction.end);
            const diff = endDate.getTime() - now.getTime();

            if (diff <= 0) {
                setTimeLeft('Ended');
                return;
            }

            const days = Math.floor(diff / (1000 * 60 * 60 * 24));
            const hours = Math.floor((diff % (1000 * 60 * 60 * 24)) / (1000 * 60 * 60));
            const minutes = Math.floor((diff % (1000 * 60 * 60)) / (1000 * 60));
            const seconds = Math.floor((diff % (1000 * 60)) / 1000);

            const parts = [];
            if (days > 0) parts.push(`${days}d`);
            if (hours > 0 || days > 0) parts.push(`${hours}h`);
            parts.push(`${minutes}m`);
            parts.push(`${seconds}s`);

            setTimeLeft(parts.join(' '));
        };

        updateTime();
        const interval = setInterval(updateTime, 1000);
        return () => clearInterval(interval);
    }, [auction.end]);

    const imageUrl = getItemImageUrl(auction.tag, 'default', auction.texture);

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
                <Badge bg="dark" className="mb-3" style={{ color: rarityColor, borderColor: rarityColor, border: '1px solid' }}>
                    {rarityName}
                </Badge>

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
