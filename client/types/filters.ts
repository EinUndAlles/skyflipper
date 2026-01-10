export enum FilterType {
    EQUAL = 1,
    HIGHER = 2,
    LOWER = 4,
    DATE = 8,
    NUMERICAL = 16,
    RANGE = 32,
    PLAYER = 64,
    SIMPLE = 128,
    BOOLEAN = 256,
    PLAYER_WITH_RANK = 512,
    SHOW_ICON = 1024
}

export interface FilterOptions {
    name: string;
    type: FilterType;
    options: string[];
    description?: string;
}

export interface ItemFilter {
    [key: string]: string;
}
