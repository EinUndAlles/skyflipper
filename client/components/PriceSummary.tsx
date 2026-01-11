'use client';

import { useEffect, useState } from 'react';
import { Row, Col, Card, Spinner } from 'react-bootstrap';
import { api } from '@/api/ApiHelper';
import { ItemFilter } from '@/types/priceHistory';

interface PriceSummaryProps {
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

export default function PriceSummary({ itemTag, itemFilter }: PriceSummaryProps) {
    const [loading, setLoading] = useState(true);
    const [lowestBin, setLowestBin] = useState<{
        lowest: number | null;
        secondLowest: number | null;
        uuid: string | null;
    } | null>(null);
    const [priceSummary, setPriceSummary] = useState<{
        min: number;
        max: number;
        avg: number;
        med: number;
        volume: number;
    } | null>(null);

    useEffect(() => {
        const fetchData = async () => {
            setLoading(true);
            try {
                const [binData, summaryData] = await Promise.all([
                    api.getLowestBin(itemTag, itemFilter),
                    api.getPriceSummary(itemTag, itemFilter)
                ]);
                setLowestBin(binData);
                setPriceSummary(summaryData);
            } catch (err) {
                console.error('Failed to load price data:', err);
            } finally {
                setLoading(false);
            }
        };

        fetchData();
    }, [itemTag, itemFilter]);

    if (loading) {
        return (
            <Card bg="dark" text="light" className="border-secondary mb-4">
                <Card.Body className="d-flex justify-content-center py-4">
                    <Spinner animation="border" size="sm" />
                </Card.Body>
            </Card>
        );
    }

    return (
        <Card bg="dark" text="light" className="border-secondary mb-4">
            <Card.Body>
                <Row>
                    {/* Lowest BIN */}
                    <Col xs={6} md={3} className="text-center border-end border-secondary">
                        <div className="text-muted small">Lowest BIN</div>
                        <div className="fs-5 fw-bold text-success">
                            {lowestBin?.lowest ? formatPrice(lowestBin.lowest) : 'N/A'}
                        </div>
                        {lowestBin?.uuid && (
                            <a 
                                href={`/auction/${lowestBin.uuid}`} 
                                className="text-info small"
                                style={{ fontSize: '0.75rem' }}
                            >
                                View Auction
                            </a>
                        )}
                    </Col>

                    {/* Average Price */}
                    <Col xs={6} md={3} className="text-center border-end border-secondary">
                        <div className="text-muted small">Avg Price (2d)</div>
                        <div className="fs-5 fw-bold text-warning">
                            {priceSummary ? formatPrice(priceSummary.avg) : 'N/A'}
                        </div>
                        <div className="text-muted" style={{ fontSize: '0.75rem' }}>
                            {priceSummary?.volume ? `${priceSummary.volume}/day` : ''}
                        </div>
                    </Col>

                    {/* Median Price */}
                    <Col xs={6} md={3} className="text-center border-end border-secondary">
                        <div className="text-muted small">Median</div>
                        <div className="fs-5 fw-bold text-info">
                            {priceSummary ? formatPrice(priceSummary.med) : 'N/A'}
                        </div>
                    </Col>

                    {/* Min/Max Range */}
                    <Col xs={6} md={3} className="text-center">
                        <div className="text-muted small">Range (2d)</div>
                        <div className="fs-6">
                            <span className="text-success">{priceSummary ? formatPrice(priceSummary.min) : 'N/A'}</span>
                            <span className="text-muted mx-1">-</span>
                            <span className="text-danger">{priceSummary ? formatPrice(priceSummary.max) : 'N/A'}</span>
                        </div>
                    </Col>
                </Row>
            </Card.Body>
        </Card>
    );
}
