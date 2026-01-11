import { useRouter } from 'next/navigation';
import { ItemFilter } from '@/types/filters';

export function getItemFilterFromUrl(): ItemFilter {
    if (typeof window === 'undefined') return {};

    const params = new URLSearchParams(window.location.search);
    const filter: ItemFilter = {};

    params.forEach((value, key) => {
        filter[key] = value;
    });

    return filter;
}

export function setFilterIntoUrlParams(
    router: ReturnType<typeof useRouter>,
    pathname: string,
    itemFilter: ItemFilter
) {
    const params = new URLSearchParams();

    Object.entries(itemFilter).forEach(([key, value]) => {
        if (value && value !== '') {
            params.set(key, value);
        }
    });

    router.replace(`${pathname}?${params.toString()}`);
}
