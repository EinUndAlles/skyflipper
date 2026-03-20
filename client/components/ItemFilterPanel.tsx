'use client';

import { useState, useEffect, useMemo } from 'react';
import { Card, Form, Button, Row, Col, Spinner, Badge } from 'react-bootstrap';
import { ItemFilter, FilterOptions, FilterType, FilterTypeHelper } from '@/types/filters';

interface Props {
    onFilterChange?: (filter: ItemFilter) => void;
    filters?: FilterOptions[];
    defaultFilter?: ItemFilter;
    ignoreURL?: boolean;
}

const FILTER_HINTS: Record<string, string> = {
    PetLevel: 'Level of the pet. Use ranges like 1-200 or >100.',
    PetExp: 'Pet experience. Supports ranges (e.g. >1000000).',
    Candy: 'Candy count. 0 means not present.',
    HotPotatoCount: 'Hot potato/fuming count. Supports ranges.',
    EndBefore: 'Show auctions ending before this date/time.',
    EndAfter: 'Show auctions ending after this date/time.',
    ItemCreatedBefore: 'Show items created before this date/time.',
    ItemCreatedAfter: 'Show items created after this date/time.'
};

// Group filters by category for better UX with 345+ filters
function categorizeFilter(name: string): string {
    const n = name.toLowerCase();
    if (n.includes('enchant') || n === 'ultimate_duplex' || n === 'ultimate_reiterate' || n === 'pristine' || n === 'prismatic' || n === 'gravity' || n === 'drain') return 'Enchants';
    if (n.includes('rune')) return 'Runes';
    if (n.includes('gem') || n.includes('gemtype')) return 'Gems';
    if (n.includes('attr') || n === 'lifeline' || n === 'veteran' || n === 'mana_pool' || n === 'dominance' || n === 'vitality' || n === 'speed' || n === 'combo') return 'Attributes';
    if (n.includes('kill') || n.includes('killed') || n.includes('handles') || n.includes('consumer') || n.includes('runic')) return 'Kills';
    if (n.includes('skin') || n.includes('pet')) return 'Pets & Skins';
    if (n.includes('color') || n.includes('dye') || n.includes('exotic') || n.includes('fairy') || n.includes('crystal')) return 'Colors';
    if (n.includes('drill') || n.includes('part') || n.includes('tuned') || n.includes('power_ability')) return 'Drills';
    if (n.includes('mined') || n.includes('farmed') || n.includes('blocks') || n.includes('cultivating') || n.includes('logs') || n.includes('axe') || n.includes('absorb')) return 'Counters';
    if (n.includes('cake') || n.includes('party') || n.includes('seller') || n.includes('captured') || n.includes('edition')) return 'Misc';
    if (n.includes('raffle') || n.includes('chimera') || n.includes('collected') || n.includes('jyrre') || n.includes('intelligence') || n.includes('thunder') || n.includes('pickonimbus') || n.includes('mana_dis')) return 'Stats';
    if (n.includes('shiny') || n.includes('peace') || n.includes('singularity') || n.includes('model') || n.includes('clean') || n.includes('sold') || n.includes('everything')) return 'Flags';
    if (n.includes('candy') || n.includes('tier') || n.includes('uid') || n.includes('tag') || n.includes('item_id') || n.includes('name') || n.includes('powder') || n.includes('growth') || n.includes('bass') || n.includes('jalapeno') || n.includes('plarvoid') || n.includes('dungeon')) return 'Item';
    if (n.includes('price') || n.includes('cost')) return 'Pricing';
    return 'Core';
}

const CATEGORY_ORDER = ['Core', 'Enchants', 'Pets & Skins', 'Gems', 'Attributes', 'Kills', 'Counters', 'Stats', 'Colors', 'Runes', 'Drills', 'Flags', 'Item', 'Misc', 'Pricing'];

function normalizeFilterType(type: FilterOptions['type'], longType?: string): FilterType {
    if (typeof type === 'number') return type as FilterType;

    const numeric = Number(type);
    if (!Number.isNaN(numeric) && Number.isFinite(numeric)) {
        return numeric as FilterType;
    }

    // Backend may serialize enums as strings, use longType/type name fallback.
    const value = (longType || type || '').toString();
    let flags = 0;
    if (value.includes('EQUAL')) flags |= FilterType.EQUAL;
    if (value.includes('HIGHER')) flags |= FilterType.HIGHER;
    if (value.includes('LOWER')) flags |= FilterType.LOWER;
    if (value.includes('DATE')) flags |= FilterType.DATE;
    if (value.includes('NUMERICAL')) flags |= FilterType.NUMERICAL;
    if (value.includes('RANGE')) flags |= FilterType.RANGE;
    if (value.includes('TEXT')) flags |= FilterType.TEXT;
    if (value.includes('SIMPLE')) flags |= FilterType.SIMPLE;
    if (value.includes('BOOLEAN')) flags |= FilterType.BOOLEAN;
    if (value.includes('AppliedItem')) flags |= FilterType.AppliedItem;
    return flags as FilterType;
}

export default function ItemFilterPanel({ onFilterChange, filters, defaultFilter, ignoreURL }: Props) {
    const [itemFilter, setItemFilter] = useState<ItemFilter>(defaultFilter || {});
    const [appliedFilter, setAppliedFilter] = useState<ItemFilter>(defaultFilter || {});
    const [expanded, setExpanded] = useState(false);
    const [selectedFilters, setSelectedFilters] = useState<string[]>([]);
    const [searchQuery, setSearchQuery] = useState('');
    
    // Initialize selectedFilters based on defaultFilter keys
    useEffect(() => {
        if (defaultFilter) {
            const filterKeys = Object.keys(defaultFilter);
            if (filterKeys.length > 0) {
                setSelectedFilters(filterKeys);
                setExpanded(true); // Auto-expand if there are default filters
            }
            setItemFilter(defaultFilter);
            setAppliedFilter(defaultFilter);
        }
    }, [defaultFilter]);

    const handleFilterChange = (filterName: string, value: string) => {
        const newFilter = { ...itemFilter, [filterName]: value };
        if (!value) {
            delete newFilter[filterName];
        }
        setItemFilter(newFilter);
    };

    const applyFilters = () => {
        setAppliedFilter(itemFilter);
        onFilterChange?.(itemFilter);
    };

    const addFilter = (filterName: string) => {
        if (!selectedFilters.includes(filterName)) {
            setSelectedFilters([...selectedFilters, filterName]);
        }
    };

    const removeFilter = (filterName: string) => {
        const newSelected = selectedFilters.filter(f => f !== filterName);
        setSelectedFilters(newSelected);

        const newFilter = { ...itemFilter };
        delete newFilter[filterName];
        setItemFilter(newFilter);
    };

    const clearAllFilters = () => {
        setSelectedFilters([]);
        setItemFilter({});
        setAppliedFilter({});
        onFilterChange?.({});
    };

    const hasPendingChanges = JSON.stringify(itemFilter) !== JSON.stringify(appliedFilter);

    const getNumericPlaceholder = (filterName: string) => {
        switch (filterName) {
            case 'PetLevel':
                return '1-200, >100';
            case 'Candy':
                return '0, 1-10';
            case 'HotPotatoCount':
                return '0-15';
            case 'PetExp':
                return '>1000000';
            default:
                return '5, 5-10, >5';
        }
    };

    const availableFilters = useMemo(() => {
        if (!filters) return [];
        return filters.filter(f => !selectedFilters.includes(f.name));
    }, [filters, selectedFilters]);

    // Filter and group available filters by search query
    const groupedFilters = useMemo(() => {
        const filtered = searchQuery
            ? availableFilters.filter(f =>
                f.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
                f.name.replace(/_/g, ' ').toLowerCase().includes(searchQuery.toLowerCase())
            )
            : availableFilters;

        const groups: Record<string, FilterOptions[]> = {};
        for (const f of filtered) {
            const cat = categorizeFilter(f.name);
            if (!groups[cat]) groups[cat] = [];
            groups[cat].push(f);
        }
        return groups;
    }, [availableFilters, searchQuery]);

    if (!expanded) {
        return (
            <div className="mb-3">
                <Button
                    variant="outline-secondary"
                    onClick={() => setExpanded(true)}
                    className="w-100"
                >
                    + Add Filters {selectedFilters.length > 0 && `(${selectedFilters.length} active)`}
                </Button>
            </div>
        );
    }

    return (
        <Card className="bg-dark text-light border-secondary mb-3">
            <Card.Header className="d-flex justify-content-between align-items-center">
                <span className="fw-bold">Filters {selectedFilters.length > 0 && <Badge bg="primary">{selectedFilters.length}</Badge>}</span>
                <Button variant="link" size="sm" className="text-light" onClick={() => setExpanded(false)}>
                    ✕
                </Button>
            </Card.Header>
            <Card.Body>
                {!filters || filters.length === 0 ? (
                    <div className="text-center">
                        <Spinner animation="border" role="status" variant="primary" size="sm" />
                        <span className="ms-2">Loading filters...</span>
                    </div>
                ) : (
                    <>
                        {/* Search + add filter */}
                        <Row className="mb-3">
                            <Col>
                                <Form.Control
                                    type="text"
                                    placeholder="Search filters..."
                                    className="bg-dark text-light border-secondary mb-2"
                                    value={searchQuery}
                                    onChange={(e) => setSearchQuery(e.target.value)}
                                />
                                <div style={{ maxHeight: '300px', overflowY: 'auto' }} className="border border-secondary rounded p-2">
                                    {CATEGORY_ORDER.map(cat => {
                                        const items = groupedFilters[cat];
                                        if (!items || items.length === 0) return null;
                                        return (
                                            <div key={cat} className="mb-2">
                                                <div className="text-muted small fw-bold mb-1">{cat} ({items.length})</div>
                                                <div className="d-flex flex-wrap gap-1">
                                                    {items.slice(0, 20).map(f => (
                                                        <Button
                                                            key={f.name}
                                                            variant="outline-secondary"
                                                            size="sm"
                                                            className="py-0 px-2"
                                                            style={{ fontSize: '0.75rem' }}
                                                            onClick={() => addFilter(f.name)}
                                                        >
                                                            + {f.name.replace(/_/g, ' ')}
                                                        </Button>
                                                    ))}
                                                    {items.length > 20 && (
                                                        <span className="text-muted small">+{items.length - 20} more...</span>
                                                    )}
                                                </div>
                                            </div>
                                        );
                                    })}
                                    {Object.keys(groupedFilters).length === 0 && (
                                        <div className="text-muted text-center py-2">No matching filters</div>
                                    )}
                                </div>
                            </Col>
                        </Row>

                        {/* Active filters */}
                        <div className="d-flex flex-wrap gap-2">
                            {selectedFilters.map(filterName => {
                                const filterOption = filters?.find(f => f.name === filterName);
                                if (!filterOption) return null;

                                return (
                                    <div key={filterName} className="d-flex align-items-center gap-1 bg-secondary rounded p-2">
                                        <span
                                            className="small text-light me-1"
                                            style={{ whiteSpace: 'nowrap' }}
                                            title={FILTER_HINTS[filterName] || ''}
                                        >
                                            {filterName.replace(/_/g, ' ')}:
                                        </span>

                                        {(() => {
                                            const ft = normalizeFilterType(filterOption.type, filterOption.longType);

                                            if (FilterTypeHelper.HasFlag(ft, FilterType.BOOLEAN)) {
                                                return (
                                            <Form.Check
                                                type="checkbox"
                                                checked={itemFilter[filterName] === 'true'}
                                                onChange={(e) => handleFilterChange(filterName, e.target.checked ? 'true' : '')}
                                            />
                                                );
                                            }

                                            if (FilterTypeHelper.HasFlag(ft, FilterType.DATE)) {
                                                return (
                                            <Form.Control
                                                type="datetime-local"
                                                size="sm"
                                                style={{ width: '190px' }}
                                                className="bg-dark text-light border-secondary"
                                                value={itemFilter[filterName] || ''}
                                                onChange={(e) => handleFilterChange(filterName, e.target.value)}
                                            />
                                                );
                                            }

                                            if (FilterTypeHelper.HasFlag(ft, FilterType.NUMERICAL)) {
                                                return (
                                            <div className="d-flex gap-1 align-items-center">
                                                <Form.Control
                                                    type="text"
                                                    size="sm"
                                                    style={{ width: '130px' }}
                                                    className="bg-dark text-light border-secondary"
                                                    value={itemFilter[filterName] || ''}
                                                    onChange={(e) => handleFilterChange(filterName, e.target.value)}
                                                    placeholder={getNumericPlaceholder(filterName)}
                                                />
                                                <span className="text-muted" style={{ fontSize: '0.75rem' }}>#</span>
                                            </div>
                                                );
                                            }

                                            return (
                                            <Form.Select
                                                size="sm"
                                                style={{ width: '150px' }}
                                                className="bg-dark text-light border-secondary"
                                                value={itemFilter[filterName] || ''}
                                                onChange={(e) => handleFilterChange(filterName, e.target.value)}
                                            >
                                                <option value="">Select...</option>
                                                {filterOption.options.map(opt => (
                                                    <option key={opt} value={opt}>
                                                        {opt.replace(/_/g, ' ').toLowerCase().replace(/\b\w/g, c => c.toUpperCase())}
                                                    </option>
                                                ))}
                                            </Form.Select>
                                            );
                                        })()}

                                        <Button
                                            variant="outline-danger"
                                            size="sm"
                                            className="p-0 px-1"
                                            onClick={() => removeFilter(filterName)}
                                        >
                                            ✕
                                        </Button>
                                    </div>
                                );
                            })}
                        </div>

                        {selectedFilters.length > 0 && (
                            <div className="mt-3 d-flex gap-2">
                                <Button
                                    variant={hasPendingChanges ? 'primary' : 'outline-primary'}
                                    size="sm"
                                    onClick={applyFilters}
                                    disabled={!hasPendingChanges}
                                >
                                    Apply Filters
                                </Button>
                                <Button
                                    variant="outline-danger"
                                    size="sm"
                                    onClick={clearAllFilters}
                                >
                                    Clear All Filters
                                </Button>
                            </div>
                        )}
                    </>
                )}
            </Card.Body>
        </Card>
    );
}
