'use client';

import { useState, useEffect, useMemo } from 'react';
import { Card, Form, Button, Row, Col, Spinner } from 'react-bootstrap';
import { ItemFilter, FilterOptions, FilterType, FilterTypeHelper } from '@/types/filters';

interface Props {
    onFilterChange?: (filter: ItemFilter) => void;
    filters?: FilterOptions[];
    defaultFilter?: ItemFilter;
    ignoreURL?: boolean;
}

export default function ItemFilterPanel({ onFilterChange, filters, defaultFilter, ignoreURL }: Props) {
    const [itemFilter, setItemFilter] = useState<ItemFilter>(defaultFilter || {});
    const [expanded, setExpanded] = useState(false);
    const [selectedFilters, setSelectedFilters] = useState<string[]>([]);

    const handleFilterChange = (filterName: string, value: string) => {
        const newFilter = { ...itemFilter, [filterName]: value };
        if (!value) {
            delete newFilter[filterName];
        }
        setItemFilter(newFilter);
        onFilterChange?.(newFilter);
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
        onFilterChange?.(newFilter);
    };

    const clearAllFilters = () => {
        setSelectedFilters([]);
        setItemFilter({});
        onFilterChange?.({});
    };

    const availableFilters = useMemo(() => {
        if (!filters) return [];
        return filters.filter(f => !selectedFilters.includes(f.name));
    }, [filters, selectedFilters]);

    if (!expanded) {
        return (
            <div className="mb-3">
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
        <Card className="bg-dark text-light border-secondary mb-3">
            <Card.Header className="d-flex justify-content-between align-items-center">
                <span className="fw-bold">Filters</span>
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
                        {/* Add filter dropdown */}
                        <Row className="mb-3">
                            <Col>
                                <Form.Select
                                    className="bg-dark text-light border-secondary"
                                    value=""
                                    onChange={(e) => {
                                        if (e.target.value) {
                                            addFilter(e.target.value);
                                        }
                                    }}
                                >
                                    <option value="">+ Add filter...</option>
                                    {availableFilters.map(f => (
                                        <option key={f.name} value={f.name}>
                                            {f.name.replace(/_/g, ' ')}
                                        </option>
                                    ))}
                                </Form.Select>
                            </Col>
                        </Row>

                        {/* Active filters */}
                        <div className="d-flex flex-wrap gap-2">
                            {selectedFilters.map(filterName => {
                                const filterOption = filters?.find(f => f.name === filterName);
                                if (!filterOption) return null;

                                return (
                                    <div key={filterName} className="d-flex align-items-center gap-1 bg-secondary rounded p-2">
                                        <span className="small text-light me-1">{filterName.replace(/_/g, ' ')}:</span>

                                        {FilterTypeHelper.HasFlag(filterOption.type, FilterType.BOOLEAN) ? (
                                            <Form.Check
                                                type="checkbox"
                                                checked={itemFilter[filterName] === 'true'}
                                                onChange={(e) => handleFilterChange(filterName, e.target.checked ? 'true' : '')}
                                            />
                                        ) : FilterTypeHelper.HasFlag(filterOption.type, FilterType.NUMERICAL) ? (
                                            <Form.Control
                                                type="number"
                                                size="sm"
                                                style={{ width: '100px' }}
                                                className="bg-dark text-light border-secondary"
                                                value={itemFilter[filterName] || ''}
                                                onChange={(e) => handleFilterChange(filterName, e.target.value)}
                                                placeholder="Value"
                                            />
                                        ) : (
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
                                        )}

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
                            <div className="mt-3">
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
