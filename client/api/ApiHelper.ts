import axios from 'axios';
import { Auction, Stats, TagCount } from '../types';

const API_BASE_URL = 'http://localhost:5135/api';

// Image URL construction similar to hypixel-react
export const getItemImageUrl = (tag: string, type: 'default' | 'vanilla' = 'default'): string => {
    if (!tag) return '';
    // Use sky.coflnet.com as per hypixel-react
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

    getAuctionsByTag: async (tag: string, limit: number = 50): Promise<Auction[]> => {
        const response = await axios.get<Auction[]>(`${API_BASE_URL}/auctions/by-tag/${tag}`, {
            params: { limit }
        });
        return response.data;
    },

    getTopTags: async (limit: number = 20): Promise<TagCount[]> => {
        const response = await axios.get<TagCount[]>(`${API_BASE_URL}/auctions/top-tags`, {
            params: { limit }
        });
        return response.data;
    },

    getAuction: async (uuid: string): Promise<Auction> => {
        const response = await axios.get<Auction>(`${API_BASE_URL}/auctions/${uuid}`);
        return response.data;
    }
};
