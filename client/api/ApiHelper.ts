import axios from 'axios';
import { Auction, Stats, TagCount } from '../types';

const API_BASE_URL = 'http://localhost:5135/api';

// Image URL construction similar to hypixel-react
export const getItemImageUrl = (tag: string, type: 'default' | 'vanilla' = 'default', texture?: string | null): string => {
    if (texture) {
        // Check if it's a value (decoded property or texture ID) or full URL
        let textureId = texture;
        if (texture.startsWith('http')) {
            // Extract ID from http://textures.minecraft.net/texture/<ID>
            const parts = texture.split('/');
            textureId = parts[parts.length - 1];
        }

        // Use mc-heads.net to render the 3D head
        return `https://mc-heads.net/head/${textureId}`;
    }

    // Fallback: Use sky.coflnet.com tag-based icon for items without texture
    if (!tag) {
        // Ultimate fallback: minecraft item frame icon
        return 'https://mc-heads.net/head/eyJ0ZXh0dXJlcyI6eyJTS0lOIjp7InVybCI6Imh0dHA6Ly90ZXh0dXJlcy5taW5lY3JhZnQubmV0L3RleHR1cmUvODI2MDVkNmMyZjU1NjFhNTU2OGM5ZGQ1ZWJjMWI2NTU1YjRhZWE2OGJlNDkwMmYwMjhlZDM3NTk5MDc2In19fQ';
    }

    return `https://sky.coflnet.com/static/icon/${tag}${type === 'vanilla' ? '/vanilla' : ''}`;
};

export const getHeadImageUrl = (uuid: string): string => {
    return `https://mc-heads.net/avatar/${uuid}`;
}

export const api = {
    getStats: async (): Promise<Stats> => {
        const response = await axios.get<Stats>(`${API_BASE_URL}/auctions/stats`);
        return response.data;
    },

    getRecentAuctions: async (limit: number = 50, offset: number = 0): Promise<Auction[]> => {
        const response = await axios.get<Auction[]>(`${API_BASE_URL}/auctions/recent`, {
            params: { limit, offset }
        });
        return response.data;
    },

    getAuctionsByTag: async (
        tag: string,
        limit: number = 200,
        filter?: string,
        binOnly: boolean = true,
        showEnded: boolean = false,
        minStars?: number,
        maxStars?: number,
        enchantment?: string,
        minEnchantLevel?: number,
        minPrice?: number,
        maxPrice?: number
    ): Promise<Auction[]> => {
        const params: any = { limit, binOnly, showEnded };
        if (filter) params.filter = filter;
        if (minStars !== undefined) params.minStars = minStars;
        if (maxStars !== undefined) params.maxStars = maxStars;
        if (enchantment) params.enchantment = enchantment;
        if (minEnchantLevel !== undefined) params.minEnchantLevel = minEnchantLevel;
        if (minPrice !== undefined) params.minPrice = minPrice;
        if (maxPrice !== undefined) params.maxPrice = maxPrice;

        const response = await axios.get<Auction[]>(`${API_BASE_URL}/auctions/by-tag/${tag}`, {
            params
        });
        return response.data;
    },

    getTopTags: async (limit: number = 20): Promise<TagCount[]> => {
        const response = await axios.get<TagCount[]>(`${API_BASE_URL}/auctions/tags/popular`, {
            params: { limit }
        });
        return response.data;
    },

    getAuction: async (uuid: string): Promise<Auction> => {
        const response = await axios.get<Auction>(`${API_BASE_URL}/auctions/${uuid}`);
        return response.data;
    },

    searchItems: async (query: string, limit: number = 10): Promise<{ itemName: string, tag: string, tier: string, texture?: string, filter?: string }[]> => {
        const response = await axios.get(`${API_BASE_URL}/auctions/search`, {
            params: { query, limit }
        });
        return response.data;
    },

    getFilters: async (tag: string): Promise<any[]> => {
        const response = await axios.get(`${API_BASE_URL}/auctions/filters/${tag}`);
        return response.data;
    }
};
