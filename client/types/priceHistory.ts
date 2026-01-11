// Coflnet API price data format
export interface ItemPrice {
    min: number;
    max: number;
    avg: number;
    volume: number;
    time: Date | string;
}

// Date ranges supported by Coflnet API
export type DateRange = 'day' | 'week' | 'month' | 'year' | 'full';

// Filter object passed to price API
export interface ItemFilter {
    [key: string]: string;
}

// Legacy types (for our own backend - can remove later)
export interface PriceDataPoint {
    timestamp: string;
    min: number;
    max: number;
    avg: number;
    median: number;
    volume: number;
}

export interface PriceHistorySummary {
    totalVolume: number;
    avgMedian: number;
    priceChange: number;
    trend: 'increasing' | 'decreasing' | 'stable';
    lowestMin: number;
    highestMax: number;
}

export interface PriceHistoryResponse {
    itemTag: string;
    granularity: 'hourly' | 'daily';
    data: PriceDataPoint[];
    summary: PriceHistorySummary | null;
}
