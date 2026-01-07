export interface Auction {
    uuid: string;
    uid?: number;
    itemName: string;
    tag: string;
    tier: string;
    reforge: string;
    price: number;
    startingBid: number;
    highestBidAmount: number;
    bin: boolean;
    start: string;
    end: string;
    fetchedAt: string;
    auctioneerId?: string;
    count: number;
    category?: string;
    anvilUses?: number;
    itemCreatedAt?: string;
    enchantments?: Enchantment[];
    flatenedNBTJson?: string;
    texture?: string;
}

export interface Enchantment {
    name: string;
    level: number;
}

export interface Stats {
    totalAuctions: number;
    binAuctions: number;
    recentAuctions: number;
    uniqueItemTags: number;
    oldestFetch: string;
    newestFetch: string;
    timestamp: string;
}

export interface TagCount {
    tag: string;
    count: number;
}
