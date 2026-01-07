'use client';

import { useSearchParams } from 'next/navigation';
import { useEffect, useState, Suspense } from 'react';
import { api } from '@/api/ApiHelper';
import { Auction } from '@/types';
import AuctionCard from '@/components/AuctionCard';
import { Container, Row, Col, Spinner, Alert } from 'react-bootstrap';

function SearchContent() {
    const searchParams = useSearchParams();
    const q = searchParams.get('q');
    const [auctions, setAuctions] = useState<Auction[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');

    useEffect(() => {
        const fetchAuctions = async () => {
            setLoading(true);
            setError('');
            try {
                let results = [];
                if (q) {
                    results = await api.getAuctionsByTag(q);
                } else {
                    results = await api.getRecentAuctions();
                }
                setAuctions(results);
            } catch (err) {
                console.error(err);
                setError('Failed to fetch auctions. Please try again.');
            } finally {
                setLoading(false);
            }
        };

        fetchAuctions();
    }, [q]);

    return (
        <Container>
            <h2 className="mb-4">{q ? `Results for "${q}"` : 'Recent Auctions'}</h2>

            {loading && <div className="text-center"><Spinner animation="border" variant="primary" /></div>}

            {error && <Alert variant="danger">{error}</Alert>}

            {!loading && !error && auctions.length === 0 && (
                <Alert variant="info">No auctions found.</Alert>
            )}

            <Row xs={1} md={2} lg={3} xl={4} className="g-4">
                {auctions.map((auction) => (
                    <Col key={auction.uuid}>
                        <AuctionCard auction={auction} />
                    </Col>
                ))}
            </Row>
        </Container>
    );
}

export default function SearchPage() {
    return (
        <Suspense fallback={<div className="text-center mt-5"><Spinner animation="border" /></div>}>
            <SearchContent />
        </Suspense>
    );
}
