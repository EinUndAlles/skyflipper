export enum FilterType {
    EQUAL = 1,
    HIGHER = 2,
    LOWER = 4,
    DATE = 8,
    NUMERICAL = 16,
    RANGE = 32,
    TEXT = 64,
    SIMPLE = 128,
    BOOLEAN = 256,
    PLAYER_WITH_RANK = 512,
    AppliedItem = 1024
}

export class FilterTypeHelper {
    public static HasFlag(full: FilterType | null, flag: FilterType): boolean {
        if (full === null) return false;
        return (full & flag) === flag;
    }
}

export interface FilterOptions {
    name: string;
    type: FilterType | string;
    longType?: string;
    options: string[];
    description?: string;
}

export interface ItemFilter {
    [key: string]: string;
}
