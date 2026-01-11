// Types for flip notifications from SignalR
export interface FlipNotification {
    auctionUuid: string;
    itemTag: string;
    itemName: string;
    currentPrice: number;
    medianPrice: number;
    estimatedProfit: number;
    profitMarginPercent: number;
    detectedAt: string;
    auctionEnd: string;
    dataSource: string;
    valueBreakdown?: string;
}
