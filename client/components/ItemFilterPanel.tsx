'use client';

import { useState, useEffect, useRef } from 'react';
import { Card, Form, Button, Badge } from 'react-bootstrap';
import { useRouter, usePathname, useSearchParams } from 'next/navigation';
import FilterElement from './filter-elements/EqualFilterElement';
import NumberRangeFilterElement from './filter-elements/NumberRangeFilterElement';
import BooleanFilterElement from './filter-elements/BooleanFilterElement';
import NumericalFilterElement from './filter-elements/NumericalFilterElement';
import { toast } from './ToastProvider';
import { ItemFilter, FilterOptions } from '@/types/filters';
import { FilterType, FilterTypeHelper } from '@/types/filters';

interface Props {
    onFilterChange?: (filter: ItemFilter) => void;
    filters?: FilterOptions[];
    defaultFilter?: ItemFilter;
    ignoreURL?: boolean;
}

// Grouped filters - when one is selected, auto-select related ones
const GROUPED_FILTERS = [
    ['Enchantment', 'EnchantLvl'],
    ['SecondEnchantment', 'SecondEnchantLvl']
];

export default function ItemFilterPanel({ onFilterChange, filters, defaultFilter, ignoreURL }: Props) {
    const router = useRouter();
    const pathname = usePathname();
    const searchParams = useSearchParams();

    const [itemFilter, _setItemFilter] = useState<ItemFilter>({});
    const [selectedFilters, setSelectedFilters] = useState<string[]>([]);
    const [expanded, setExpanded] = useState(false);
    const [invalidFilters, setInvalidFilters] = useState<Set<string>>(new Set());
    const typeaheadRef = useRef<any>(null);

    // Initialize filters from URL or localStorage
    useEffect(() => {
        if (filters && filters.length > 0) {
            initFilter();
        }
    }, [filters]);

    function initFilter() {
        if (ignoreURL && !defaultFilter) {
            return;
        }

        let initialFilters: ItemFilter = defaultFilter || {};
        
        // Try URL first
        if (!ignoreURL) {
            const urlParams = new URLSearchParams(window.location.search);
            urlParams.forEach((value, key) => {
                initialFilters[key] = value;
            });
        }
        
        // Fallback to localStorage if URL empty
        if (Object.keys(initialFilters).length === 0) {
            const saved = localStorage.getItem('LAST_USED_FILTER');
            if (saved) {
                initialFilters = JSON.parse(saved);
                // Only keep filters that exist in available options
                if (filters) {
                    const validKeys = new Set(filters.map(f => f.name));
                    Object.keys(initialFilters).forEach(key => {
                        if (!validKeys.has(key)) {
                            delete initialFilters[key];
                        }
                    });
                }
            }
        }

        _setItemFilter(initialFilters);
        onFilterChange?.(initialFilters);
        
        if (Object.keys(itemFilter).length > 0) {
            setExpanded(true);
            Object.keys(itemFilter).forEach(name => {
                if (!filters?.find(f => f.name === name)) {
                    delete itemFilter[name];
                    return;
                }
                enableFilter(name);
                getGroupedFilters(name).forEach(filter => enableFilter(filter));
            });
            setItemFilter(itemFilter);
            onFilterChange(itemFilter);
        }
    }

    function getGroupedFilters(filterName: string): string[] {
        for (const group of GROUPED_FILTERS) {
            const idx = group.indexOf(filterName);
            if (idx !== -1) {
                const groupToEnable = group.filter(f => f !== filterName);
                return groupToEnable;
            }
        }
        return [];
    }

    function enableFilter(filterName: string, filterValue?: string) => {
        if (selectedFilters.some(n => n === filterName)) {
            return;
        }

        selectedFilters = [...selectedFilters, filterName];
        setSelectedFilters(selectedFilters);

        if (itemFilter[filterName] === undefined && !filterValue) {
            itemFilter[filterName] = getDefaultValue(filterName);
        }
        if (itemFilter[filterName] === undefined && filterValue) {
            itemFilter[filterName] = filterValue;
        }
        
        updateURL();
        setItemFilter(itemFilter);
        onFilterChange(itemFilter);
    }

    function removeFilter(filterName: string) => {
        if (invalidFilters.has(filterName)) {
            const newInvalidFilters = new Set(invalidFilters);
            newInvalidFilters.delete(filterName);
            setInvalidFilters(newInvalidFilters);
        }
        delete itemFilter[filterName];
        setItemFilter({ ...itemFilter });
        updateURL();
        onFilterChange(itemFilter);
        
        // Remove grouped filters
        getGroupedFilters(filterName).forEach(filter => removeFilter(filter));
        
        const newSelected = selectedFilters.filter(f => f !== filterName);
        setSelectedFilters(newSelected);
    }

    let addFilter = ([selected]: FilterOptions[]) => {
        if (!selected) {
            return;
        }

        enableFilter(selected.name);
        getGroupedFilters(selected.name).forEach(filter => enableFilter(filter));
        typeaheadRef.current?.clear();
    };

    let onFilterClose = () => {
        setSelectedFilters([]);
        setExpanded(false);
        setItemFilter({});
        updateURL();
        onFilterChange({});
    };

    function onFilterRemoveClick(filterName: string) => {
        if (invalidFilters.has(filterName)) {
            const newInvalidFilters = new Set(invalidFilters);
            newInvalidFilters.delete(filterName);
            setInvalidFilters(newInvalidFilters);
        }
        removeFilter(filterName);
        getGroupedFilters(filterName).forEach(filter => removeFilter(filter));
    }

    const updateURL = () => {
        if (ignoreURL) {
            return;
        }

        const params = new URLSearchParams();
        Object.entries(itemFilter).forEach(([key, value]) => {
            if (value && value !== '') {
                params.set(key, value);
            }
        });
        
        router.replace(`${pathname}?${params.toString()}`);
    };

    function onFilterChange(filter: ItemFilter) {
        let filterCopy = { ...filter };
        
        let valid = true;
        Object.keys(filterCopy).forEach(key => {
            if (!checkForValidGroupedFilter(key, filterCopy)) {
                valid = false;
                return;
            }
        });

        if (!valid) {
            return;
        }

        setItemFilter(filterCopy);
        if (!ignoreURL) {
            localStorage.setItem('LAST_USED_FILTER', JSON.stringify(filterCopy));
        }
        if (onFilterChange) {
            Object.keys(filterCopy).forEach(key => {
                if (filterCopy[key] === '' || filterCopy[key] === null) {
                    delete filterCopy[key];
                }
            });
            onFilterChange(filterCopy);
        }
    }

    function checkForValidGroupedFilter(filterName: string, filter: ItemFilter): boolean {
        let groupFilters = getGroupedFilters(filterName);
        
        let invalid = false;
        groupFilters.forEach(name => {
            if (filter[name] === undefined || filter[name] === null) {
                invalid = true;
            }
        });
        
        return !invalid;
    }

    function setInvalidFilters(newInvalidFilters: Set<string>) {
        if (onFilterChange) {
            onFilterChange(newInvalidFilters.size === 0);
        }
        _setInvalidFilters(newInvalidFilters);
    }

    function getDefaultValue(filterName: string): string {
        const options = filters?.find(f => f.name === filterName);
        let defaultValue: any = '';
        
        if (options && options.options[0] !== null && options.options[0] !== undefined) {
            if ((FilterTypeHelper.HasFlag(options.type, FilterType.EQUAL) && FilterTypeHelper.HasFlag(options.type, FilterType.SIMPLE)) || FilterTypeHelper.HasFlag(options.type, FilterType.BOOLEAN)) {
                defaultValue = options.options[0];
                if (options.name === 'Everything') {
                    defaultValue = 'true';
                }
            }
        }
        
        if (filterName === 'Color') {
            defaultValue = '#000000';
        }
        
        return defaultValue;
    }

    const sortedFilters = useMemo(() => {
        if (!filters) {
            return [];
        }
        
        // Sort alphabetically (similar to hypixel-react for now)
        return filters.sort((a, b) => {
            if (a.name.toLowerCase() < b.name.toLowerCase()) return -1;
            if (a.name.toLowerCase() > b.name.toLowerCase()) return 1;
            return 0;
        });
    }, [filters]);

    if (!expanded) {
        return (
            <div>
                <Button 
                    variant="outline-secondary" 
                    onClick={() => setExpanded(true)}
                    className="w-100"
                >
                    + Add Filters
                </Button>
            </div>
        );
    }

    return (
        <Card className="bg-dark text-light border-secondary">
            <Card.Header className="d-flex justify-content-between align-items-center">
                <span className="fw-bold">Filters</span>
                <Button variant="link" size="sm" onClick={() => setExpanded(false)}>
                    ✕
                </Button>
            </Card.Header>
            <Card.Body>
                {!filters ? (
                    <Spinner animation="border" role="status" variant="primary" />
                ) : (
                    <div className="mb-3">
                        <Form className="mb-3">
                            <Typeahead
                                id="add-filter-typeahead"
                                placeholder="Add filter"
                                onChange={addFilter}
                                options={sortedFilters}
                                labelKey={(options: FilterOptions) => options.name}
                                filterBy={(options: FilterOptions, props) => {
                                    const search = (props.text || '').toLowerCase();
                                    return options.name.toLowerCase().includes(search);
                                }}
                                ref={typeaheadRef}
                            />
                        </Form>
                        
                        <div className="d-flex flex-wrap gap-2 mt-3">
                            {selectedFilters.map(filterName => {
                                const options = filters?.find(f => f.name === filterName);
                                if (!options) {
                                    return null;
                                }
                                
                                const defaultValue = getDefaultValue(filterName);
                                if (itemFilter[filterName]) {
                                    // Override with current value if set
                                    defaultValue = itemFilter[filterName];
                                }
                                
                                return (
                                    <div key={filterName} className="filter-element-wrapper">
                                        <FilterElement
                                            options={options}
                                            defaultValue={defaultValue}
                                            onFilterChange={(newFilter) => {
                                                const newFilter = { ...itemFilter };
                                                newFilter[filterName] = newFilter[filterName];
                                                setItemFilter(newFilter);
                                                updateURL();
                                                onFilterChange(newFilter);
                                            }}
                                            onIsValidChange={(isValid) => {
                                                const newInvalid = new Set(invalidFilters);
                                                if (isValid) {
                                                    newInvalid.delete(filterName);
                                                } else {
                                                    newInvalid.add(filterName);
                                                }
                                                setInvalidFilters(newInvalid);
                                            }}
                                        />
                                        <Button 
                                            variant="outline-danger" 
                                            size="sm"
                                            onClick={() => onFilterRemoveClick(filterName)}
                                            className="remove-filter-btn"
                                        >
                                            ✕
                                        </Button>
                                    </div>
                                );
                            })}
                        </div>
                    </div>
                )}
            </Card.Body>
            {!ignoreURL && (
                <div className="mt-3">
                    <Button variant="danger" onClick={onFilterClose()}>
                        Close
                    </Button>
                </div>
            )}
        </Card>
    );
}
