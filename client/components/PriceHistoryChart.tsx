'use client';

import { useEffect, useState, useRef } from 'react';
import { Spinner, Alert, ButtonGroup, Button } from 'react-bootstrap';
import ReactECharts from 'echarts-for-react';
import { api } from '@/api/ApiHelper';
import { ItemPrice, DateRange, ItemFilter } from '@/types/priceHistory';

interface PriceHistoryChartProps {
    itemTag: string;
    itemFilter?: ItemFilter;
    height?: number;
    onRangeChange?: (range: DateRange) => void;
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

// Format date for tooltip
const formatDateTime = (date: Date): string => {
    return date.toLocaleString('en-US', { 
        month: 'short', 
        day: 'numeric',
        year: 'numeric',
        hour: 'numeric',
        minute: '2-digit'
    });
};

// Date range options
const dateRanges: { value: DateRange; label: string }[] = [
    { value: 'day', label: '1 Day' },
    { value: 'week', label: '1 Week' },
    { value: 'month', label: '1 Month' },
];

// LocalStorage key for legend selection
const LEGEND_STORAGE_KEY = 'priceGraphLegendSelection';

// Get default legend selection from localStorage
const getDefaultLegendSelection = (): Record<string, boolean> => {
    if (typeof window === 'undefined') {
        return { Price: true, Min: true, Max: false, Volume: false };
    }
    try {
        const saved = localStorage.getItem(LEGEND_STORAGE_KEY);
        if (saved) {
            return JSON.parse(saved);
        }
    } catch (e) {
        // Ignore parse errors
    }
    return { Price: true, Min: true, Max: false, Volume: false };
};

export default function PriceHistoryChart({
    itemTag,
    itemFilter,
    height = 350,
    onRangeChange
}: PriceHistoryChartProps) {
    const [priceData, setPriceData] = useState<ItemPrice[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [dateRange, setDateRange] = useState<DateRange>('week');
    const chartRef = useRef<ReactECharts>(null);

    // Fetch price data when tag, range, or filters change
    useEffect(() => {
        const fetchPriceHistory = async () => {
            try {
                setLoading(true);
                setError(null);
                const data = await api.getItemPrices(itemTag, dateRange, itemFilter);
                setPriceData(data);
            } catch (err: any) {
                console.error('Failed to load price history:', err);
                if (err.response?.status === 404) {
                    setError('No price data available for this item');
                } else {
                    setError('Failed to load price history');
                }
                setPriceData([]);
            } finally {
                setLoading(false);
            }
        };

        if (itemTag) {
            fetchPriceHistory();
        }
    }, [itemTag, dateRange, itemFilter]);

    // Handle date range change
    const handleRangeChange = (range: DateRange) => {
        setDateRange(range);
        onRangeChange?.(range);
    };

    // Build chart options
    const getChartOptions = () => {
        if (priceData.length === 0) {
            return {};
        }

        const defaultSelection = getDefaultLegendSelection();
        
        // Prepare x-axis data as formatted strings
        const xAxisData = priceData.map(item => {
            const date = item.time instanceof Date ? item.time : new Date(item.time);
            if (dateRange === 'day') {
                return date.toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' });
            }
            return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
        });

        return {
            backgroundColor: 'transparent',
            legend: {
                data: ['Price', 'Min', 'Max', 'Volume'],
                selected: defaultSelection,
                textStyle: { color: '#aaa' },
                top: 0,
                left: 'center'
            },
            tooltip: {
                trigger: 'axis',
                backgroundColor: 'rgba(30, 30, 30, 0.95)',
                borderColor: '#444',
                textStyle: { color: '#fff' },
                formatter: (params: any) => {
                    if (!params || params.length === 0) return '';
                    const dataIndex = params[0].dataIndex;
                    const item = priceData[dataIndex];
                    if (!item) return '';
                    
                    const date = item.time instanceof Date ? item.time : new Date(item.time);
                    let html = `<div style="font-weight: bold; margin-bottom: 5px;">${formatDateTime(date)}</div>`;
                    
                    params.forEach((param: any) => {
                        if (param.value !== undefined) {
                            const color = param.color;
                            const value = param.seriesName === 'Volume' 
                                ? param.value.toLocaleString()
                                : formatPrice(param.value);
                            html += `<div style="display: flex; justify-content: space-between; gap: 20px;">
                                <span><span style="display: inline-block; width: 10px; height: 10px; background: ${color}; border-radius: 50%; margin-right: 5px;"></span>${param.seriesName}</span>
                                <span style="font-weight: bold;">${value}</span>
                            </div>`;
                        }
                    });
                    
                    return html;
                }
            },
            grid: {
                left: '60',
                right: '60',
                top: '40',
                bottom: '60'
            },
            xAxis: {
                type: 'category',
                data: xAxisData,
                axisLabel: {
                    color: '#aaa',
                },
                axisLine: { lineStyle: { color: '#444' } },
                axisTick: { lineStyle: { color: '#444' } }
            },
            yAxis: [
                {
                    type: 'value',
                    name: 'Price',
                    nameTextStyle: { color: '#aaa' },
                    axisLabel: {
                        color: '#aaa',
                        formatter: (value: number) => formatPrice(value)
                    },
                    axisLine: { lineStyle: { color: '#444' } },
                    splitLine: { lineStyle: { color: '#333' } }
                },
                {
                    type: 'value',
                    name: 'Volume',
                    nameTextStyle: { color: '#aaa' },
                    position: 'right',
                    axisLabel: {
                        color: '#aaa',
                        formatter: (value: number) => formatPrice(value)
                    },
                    axisLine: { lineStyle: { color: '#444' } },
                    splitLine: { show: false }
                }
            ],
            dataZoom: [
                {
                    type: 'inside',
                    start: 0,
                    end: 100
                },
                {
                    type: 'slider',
                    start: 0,
                    end: 100,
                    height: 20,
                    bottom: 10,
                    borderColor: '#444',
                    backgroundColor: '#1a1a1a',
                    fillerColor: 'rgba(80, 80, 80, 0.3)',
                    handleStyle: { color: '#666' },
                    textStyle: { color: '#aaa' }
                }
            ],
            series: [
                {
                    name: 'Price',
                    type: 'line',
                    data: priceData.map(item => item.avg),
                    smooth: true,
                    lineStyle: { color: '#22A7F0', width: 2 },
                    itemStyle: { color: '#22A7F0' },
                    areaStyle: {
                        color: {
                            type: 'linear',
                            x: 0, y: 0, x2: 0, y2: 1,
                            colorStops: [
                                { offset: 0, color: 'rgba(34, 167, 240, 0.3)' },
                                { offset: 1, color: 'rgba(34, 167, 240, 0)' }
                            ]
                        }
                    },
                    symbol: 'circle',
                    symbolSize: 4,
                    showSymbol: false
                },
                {
                    name: 'Min',
                    type: 'line',
                    data: priceData.map(item => item.min),
                    smooth: true,
                    lineStyle: { color: '#228B22', width: 1.5 },
                    itemStyle: { color: '#228B22' },
                    symbol: 'circle',
                    symbolSize: 3,
                    showSymbol: false
                },
                {
                    name: 'Max',
                    type: 'line',
                    data: priceData.map(item => item.max),
                    smooth: true,
                    lineStyle: { color: '#B22222', width: 1.5 },
                    itemStyle: { color: '#B22222' },
                    symbol: 'circle',
                    symbolSize: 3,
                    showSymbol: false
                },
                {
                    name: 'Volume',
                    type: 'bar',
                    yAxisIndex: 1,
                    data: priceData.map(item => item.volume),
                    barWidth: '60%',
                    itemStyle: { 
                        color: 'rgba(108, 117, 125, 0.5)',
                        borderRadius: [2, 2, 0, 0]
                    }
                }
            ]
        };
    };

    // Handle legend selection change - persist to localStorage
    const onChartEvents = {
        legendselectchanged: (params: any) => {
            try {
                localStorage.setItem(LEGEND_STORAGE_KEY, JSON.stringify(params.selected));
            } catch (e) {
                // Ignore storage errors
            }
        }
    };

    // Calculate summary stats
    const getSummaryStats = () => {
        if (priceData.length === 0) return null;
        
        const avgPrice = priceData.reduce((sum, p) => sum + p.avg, 0) / priceData.length;
        const minPrice = Math.min(...priceData.map(p => p.min));
        const maxPrice = Math.max(...priceData.map(p => p.max));
        const totalVolume = priceData.reduce((sum, p) => sum + p.volume, 0);
        
        // Calculate price change
        const firstPrice = priceData[0]?.avg || 0;
        const lastPrice = priceData[priceData.length - 1]?.avg || 0;
        const priceChange = firstPrice > 0 ? ((lastPrice - firstPrice) / firstPrice) * 100 : 0;
        
        return { avgPrice, minPrice, maxPrice, totalVolume, priceChange };
    };

    const stats = getSummaryStats();

    if (loading) {
        return (
            <div className="d-flex justify-content-center align-items-center bg-dark rounded border border-secondary" style={{ height }}>
                <Spinner animation="border" variant="primary" size="sm" />
                <span className="ms-2 text-light">Loading price history...</span>
            </div>
        );
    }

    if (error || priceData.length === 0) {
        return (
            <div className="bg-dark rounded border border-secondary p-3">
                {/* Header with range selector even when no data */}
                <div className="d-flex justify-content-between align-items-center mb-3 flex-wrap gap-2">
                    <h5 className="mb-0 text-light">Price History</h5>
                    <ButtonGroup size="sm">
                        {dateRanges.map(range => (
                            <Button
                                key={range.value}
                                variant={dateRange === range.value ? 'primary' : 'outline-secondary'}
                                onClick={() => handleRangeChange(range.value)}
                            >
                                {range.label}
                            </Button>
                        ))}
                    </ButtonGroup>
                </div>
                <div className="text-center py-5">
                    <p className="text-secondary mb-0">{error || 'No price history available for this item.'}</p>
                    <small className="text-muted">Price data is collected from sold auctions over time.</small>
                </div>
            </div>
        );
    }

    return (
        <div className="bg-dark rounded border border-secondary p-3">
            {/* Header with controls */}
            <div className="d-flex justify-content-between align-items-center mb-2 flex-wrap gap-2">
                <h5 className="mb-0 text-light">
                    Price History
                    {stats && (
                        <span 
                            className={`ms-2 badge ${stats.priceChange >= 0 ? 'bg-success' : 'bg-danger'}`}
                        >
                            {stats.priceChange >= 0 ? '+' : ''}{stats.priceChange.toFixed(1)}%
                        </span>
                    )}
                </h5>
                
                {/* Time range selector */}
                <ButtonGroup size="sm">
                    {dateRanges.map(range => (
                        <Button
                            key={range.value}
                            variant={dateRange === range.value ? 'primary' : 'outline-secondary'}
                            onClick={() => handleRangeChange(range.value)}
                        >
                            {range.label}
                        </Button>
                    ))}
                </ButtonGroup>
            </div>

            {/* Chart - click legend items to toggle Price/Min/Max/Volume */}
            <ReactECharts
                ref={chartRef}
                option={getChartOptions()}
                style={{ height: height }}
                opts={{ renderer: 'canvas' }}
                onEvents={onChartEvents}
            />

            {/* Summary stats */}
            {stats && (
                <div className="d-flex justify-content-around mt-3 pt-3 border-top border-secondary text-center flex-wrap gap-2">
                    <div>
                        <small className="text-secondary d-block">Avg Price</small>
                        <span className="text-info fw-bold">{formatPrice(stats.avgPrice)}</span>
                    </div>
                    <div>
                        <small className="text-secondary d-block">Lowest</small>
                        <span className="text-success fw-bold">{formatPrice(stats.minPrice)}</span>
                    </div>
                    <div>
                        <small className="text-secondary d-block">Highest</small>
                        <span className="text-danger fw-bold">{formatPrice(stats.maxPrice)}</span>
                    </div>
                    <div>
                        <small className="text-secondary d-block">Volume</small>
                        <span className="text-warning fw-bold">{stats.totalVolume.toLocaleString()}</span>
                    </div>
                </div>
            )}
        </div>
    );
}
