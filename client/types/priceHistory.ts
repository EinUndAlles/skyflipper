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
