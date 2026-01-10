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
    nbtLookups?: NBTLookup[];
    texture?: string;
    bids?: Bid[];
}

export interface Enchantment {
    type: string | number; // Enchantment type enum value (e.g., "sharpness", "protection") or number
    level: number;
}

export interface NBTLookup {
    id: number;
    keyId?: number;
    key?: string;
    valueNumeric?: number;
    valueString?: string;
    valueId?: number;
    nbtKey?: NBTKey;
    nbtValue?: NBTValue;
}

export interface NBTKey {
    id: number;
    keyName: string;
}

export interface NBTValue {
    id: number;
    value: string;
}

export interface Bid {
    id: number;
    bidderId: string;
    amount: number;
    timestamp: string;
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
