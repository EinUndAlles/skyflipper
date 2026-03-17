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

export interface PriceHistoryResponse {
    filterable: boolean;
    bazaar: boolean;
    filters: string[];
    prices: ItemPrice[];
}
