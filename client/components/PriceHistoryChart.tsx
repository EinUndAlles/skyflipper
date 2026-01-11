'use client';

import { useEffect, useState } from 'react';
import { Spinner, Alert, ButtonGroup, Button, Badge } from 'react-bootstrap';
import {
    Area,
    XAxis,
    YAxis,
    CartesianGrid,
    Tooltip,
    ResponsiveContainer,
    Bar,
    ComposedChart,
    Legend
} from 'recharts';
import { api } from '@/api/ApiHelper';
import { PriceHistoryResponse } from '@/types/priceHistory';

interface PriceHistoryChartProps {
    itemTag: string;
    defaultDays?: number;
    defaultGranularity?: 'hourly' | 'daily';
    height?: number;
}

// Format large numbers (1M, 1B, etc.)
const formatPrice = (value: number): string => {
    if (value >= 1_000_000_000) {
        return `${(value / 1_000_000_000).toFixed(1)}B`;
    }
    if (value >= 1_000_000) {
        return `${(value / 1_000_000).toFixed(1)}M`;
    }
    if (value >= 1_000) {
        return `${(value / 1_000).toFixed(1)}K`;
    }
    return value.toFixed(0);
};

// Format date for x-axis
const formatDate = (timestamp: string, granularity: 'hourly' | 'daily'): string => {
    const date = new Date(timestamp);
    if (granularity === 'hourly') {
        return date.toLocaleString('en-US', { month: 'short', day: 'numeric', hour: 'numeric' });
    }
    return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
};

// Custom tooltip component
const CustomTooltip = ({ active, payload, label }: any) => {
    if (active && payload && payload.length) {
        const data = payload[0].payload;
        return (
            <div className="bg-dark border border-secondary rounded p-2" style={{ minWidth: '180px' }}>
                <p className="mb-1 text-light fw-bold">
                    {new Date(label).toLocaleDateString('en-US', { 
                        month: 'short', 
                        day: 'numeric',
                        year: 'numeric'
                    })}
                </p>
                <p className="mb-1 text-warning">Median: {formatPrice(data.median)}</p>
                <p className="mb-1 text-info">Avg: {formatPrice(data.avg)}</p>
                <p className="mb-1 text-success">Min: {formatPrice(data.min)}</p>
                <p className="mb-1 text-danger">Max: {formatPrice(data.max)}</p>
                <p className="mb-0 text-secondary">Volume: {data.volume}</p>
            </div>
        );
    }
    return null;
};

export default function PriceHistoryChart({
    itemTag,
    defaultDays = 30,
    defaultGranularity = 'daily',
    height = 300
}: PriceHistoryChartProps) {
    const [priceData, setPriceData] = useState<PriceHistoryResponse | null>(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [days, setDays] = useState(defaultDays);
    const [granularity, setGranularity] = useState<'hourly' | 'daily'>(defaultGranularity);

    useEffect(() => {
        const fetchPriceHistory = async () => {
            try {
                setLoading(true);
                setError(null);
                const data = await api.getPriceHistory(itemTag, days, granularity);
                setPriceData(data);
            } catch (err) {
                console.error('Failed to load price history:', err);
                setError('Failed to load price history');
            } finally {
                setLoading(false);
            }
        };

        if (itemTag) {
            fetchPriceHistory();
        }
    }, [itemTag, days, granularity]);

    // Handle granularity change
    const handleGranularityChange = (newGranularity: 'hourly' | 'daily') => {
        setGranularity(newGranularity);
        // Hourly data only available for 7 days
        if (newGranularity === 'hourly' && days > 7) {
            setDays(7);
        }
    };

    if (loading) {
        return (
            <div className="d-flex justify-content-center align-items-center bg-dark rounded border border-secondary" style={{ height }}>
                <Spinner animation="border" variant="primary" size="sm" />
                <span className="ms-2 text-light">Loading price history...</span>
            </div>
        );
    }

    if (error) {
        return (
            <Alert variant="danger" className="bg-dark text-danger border-danger">
                {error}
            </Alert>
        );
    }

    if (!priceData || priceData.data.length === 0) {
        return (
            <div className="bg-dark rounded border border-secondary p-4 text-center" style={{ minHeight: height }}>
                <p className="text-secondary mb-0">No price history available for this item yet.</p>
                <small className="text-muted">Price data is collected from sold auctions over time.</small>
            </div>
        );
    }

    // Prepare chart data with formatted dates
    const chartData = priceData.data.map(point => ({
        ...point,
        formattedDate: formatDate(point.timestamp, granularity),
        granularity
    }));

    const { summary } = priceData;

    return (
        <div className="bg-dark rounded border border-secondary p-3">
            {/* Header with controls */}
            <div className="d-flex justify-content-between align-items-center mb-3 flex-wrap gap-2">
                <h5 className="mb-0 text-light">
                    Price History
                    {summary && (
                        <Badge 
                            bg={summary.trend === 'increasing' ? 'success' : summary.trend === 'decreasing' ? 'danger' : 'secondary'}
                            className="ms-2"
                        >
                            {summary.priceChange > 0 ? '+' : ''}{summary.priceChange}%
                        </Badge>
                    )}
                </h5>
                
                <div className="d-flex gap-2 flex-wrap">
                    {/* Granularity toggle */}
                    <ButtonGroup size="sm">
                        <Button
                            variant={granularity === 'hourly' ? 'primary' : 'outline-secondary'}
                            onClick={() => handleGranularityChange('hourly')}
                        >
                            Hourly
                        </Button>
                        <Button
                            variant={granularity === 'daily' ? 'primary' : 'outline-secondary'}
                            onClick={() => handleGranularityChange('daily')}
                        >
                            Daily
                        </Button>
                    </ButtonGroup>

                    {/* Time range selector */}
                    <ButtonGroup size="sm">
                        {granularity === 'hourly' ? (
                            <>
                                <Button variant={days === 1 ? 'primary' : 'outline-secondary'} onClick={() => setDays(1)}>24h</Button>
                                <Button variant={days === 3 ? 'primary' : 'outline-secondary'} onClick={() => setDays(3)}>3d</Button>
                                <Button variant={days === 7 ? 'primary' : 'outline-secondary'} onClick={() => setDays(7)}>7d</Button>
                            </>
                        ) : (
                            <>
                                <Button variant={days === 7 ? 'primary' : 'outline-secondary'} onClick={() => setDays(7)}>7d</Button>
                                <Button variant={days === 30 ? 'primary' : 'outline-secondary'} onClick={() => setDays(30)}>30d</Button>
                                <Button variant={days === 60 ? 'primary' : 'outline-secondary'} onClick={() => setDays(60)}>60d</Button>
                                <Button variant={days === 90 ? 'primary' : 'outline-secondary'} onClick={() => setDays(90)}>90d</Button>
                            </>
                        )}
                    </ButtonGroup>
                </div>
            </div>

            {/* Chart */}
            <ResponsiveContainer width="100%" height={height}>
                <ComposedChart data={chartData} margin={{ top: 10, right: 10, left: 0, bottom: 0 }}>
                    <defs>
                        <linearGradient id="colorMedian" x1="0" y1="0" x2="0" y2="1">
                            <stop offset="5%" stopColor="#ffc107" stopOpacity={0.3} />
                            <stop offset="95%" stopColor="#ffc107" stopOpacity={0} />
                        </linearGradient>
                        <linearGradient id="colorRange" x1="0" y1="0" x2="0" y2="1">
                            <stop offset="5%" stopColor="#6c757d" stopOpacity={0.2} />
                            <stop offset="95%" stopColor="#6c757d" stopOpacity={0} />
                        </linearGradient>
                    </defs>
                    <CartesianGrid strokeDasharray="3 3" stroke="#444" />
                    <XAxis 
                        dataKey="formattedDate" 
                        stroke="#aaa" 
                        tick={{ fill: '#aaa', fontSize: 11 }}
                        interval="preserveStartEnd"
                    />
                    <YAxis 
                        stroke="#aaa" 
                        tick={{ fill: '#aaa', fontSize: 11 }}
                        tickFormatter={formatPrice}
                        width={60}
                    />
                    <Tooltip content={<CustomTooltip />} />
                    <Legend />
                    
                    {/* Min-Max range area */}
                    <Area
                        type="monotone"
                        dataKey="max"
                        stroke="transparent"
                        fill="url(#colorRange)"
                        name="Max"
                    />
                    <Area
                        type="monotone"
                        dataKey="min"
                        stroke="#28a745"
                        strokeWidth={1}
                        fill="transparent"
                        name="Min"
                        strokeDasharray="3 3"
                    />
                    
                    {/* Median line (primary) */}
                    <Area
                        type="monotone"
                        dataKey="median"
                        stroke="#ffc107"
                        strokeWidth={2}
                        fill="url(#colorMedian)"
                        name="Median"
                    />

                    {/* Volume bars */}
                    <Bar 
                        dataKey="volume" 
                        fill="#6c757d" 
                        opacity={0.3} 
                        yAxisId="volume"
                        name="Volume"
                    />
                </ComposedChart>
            </ResponsiveContainer>

            {/* Summary stats */}
            {summary && (
                <div className="d-flex justify-content-around mt-3 pt-3 border-top border-secondary text-center flex-wrap gap-2">
                    <div>
                        <small className="text-secondary d-block">Avg Median</small>
                        <span className="text-warning fw-bold">{formatPrice(summary.avgMedian)}</span>
                    </div>
                    <div>
                        <small className="text-secondary d-block">Lowest</small>
                        <span className="text-success fw-bold">{formatPrice(summary.lowestMin)}</span>
                    </div>
                    <div>
                        <small className="text-secondary d-block">Highest</small>
                        <span className="text-danger fw-bold">{formatPrice(summary.highestMax)}</span>
                    </div>
                    <div>
                        <small className="text-secondary d-block">Volume</small>
                        <span className="text-info fw-bold">{summary.totalVolume.toLocaleString()}</span>
                    </div>
                </div>
            )}
        </div>
    );
}
