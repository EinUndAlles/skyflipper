'use client';

import { useState, useEffect } from 'react';
import { Card, Form, Button, Row, Col, Collapse } from 'react-bootstrap';
import { useRouter, usePathname, useSearchParams } from 'next/navigation';

export interface ItemFilters {
    minStars?: number;
    maxStars?: number;
    enchantment?: string;
    minEnchantLevel?: number;
    minPrice?: number;
    maxPrice?: number;
    binOnly?: boolean;
    showEnded?: boolean;
    sortBy?: 'lowest' | 'highest' | 'ending';
}

interface Props {
    onFilterChange?: (filters: ItemFilters) => void;
    initialFilters?: ItemFilters;
}

export default function ItemFilterPanel({ onFilterChange, initialFilters }: Props) {
    const router = useRouter();
    const pathname = usePathname();
    const searchParams = useSearchParams();

    const [isOpen, setIsOpen] = useState(false);
    // Local state for form inputs (changes on every keystroke)
    const [localFilters, setLocalFilters] = useState<ItemFilters>(initialFilters || {
        binOnly: true,
        showEnded: false,
        sortBy: 'lowest'
    });
    // Applied filters (only updates when "Apply" is clicked)
    const [appliedFilters, setAppliedFilters] = useState<ItemFilters>(initialFilters || {
        binOnly: true,
        showEnded: false,
        sortBy: 'lowest'
    });

    // Initialize filters from URL params on mount
    useEffect(() => {
        const urlFilters: ItemFilters = {};

        const minStars = searchParams.get('minStars');
        const maxStars = searchParams.get('maxStars');
        const enchantment = searchParams.get('enchantment');
        const minEnchantLevel = searchParams.get('minEnchantLevel');
        const minPrice = searchParams.get('minPrice');
        const maxPrice = searchParams.get('maxPrice');
        const binOnly = searchParams.get('binOnly');
        const showEnded = searchParams.get('showEnded');
        const sortBy = searchParams.get('sortBy');

        if (minStars) urlFilters.minStars = parseInt(minStars);
        if (maxStars) urlFilters.maxStars = parseInt(maxStars);
        if (enchantment) urlFilters.enchantment = enchantment;
        if (minEnchantLevel) urlFilters.minEnchantLevel = parseInt(minEnchantLevel);
        if (minPrice) urlFilters.minPrice = parseInt(minPrice);
        if (maxPrice) urlFilters.maxPrice = parseInt(maxPrice);
        if (binOnly !== null) urlFilters.binOnly = binOnly !== 'false';
        if (showEnded !== null) urlFilters.showEnded = showEnded === 'true';
        if (sortBy) urlFilters.sortBy = sortBy as 'lowest' | 'highest' | 'ending';

        if (Object.keys(urlFilters).length > 0) {
            const merged = { ...localFilters, ...urlFilters };
            setLocalFilters(merged);
            setAppliedFilters(merged);
            setIsOpen(true);
        }
    }, []);

    const applyFilters = () => {
        setAppliedFilters(localFilters);

        // Update URL params
        const params = new URLSearchParams();
        if (localFilters.minStars) params.set('minStars', localFilters.minStars.toString());
        if (localFilters.maxStars) params.set('maxStars', localFilters.maxStars.toString());
        if (localFilters.enchantment) params.set('enchantment', localFilters.enchantment);
        if (localFilters.minEnchantLevel) params.set('minEnchantLevel', localFilters.minEnchantLevel.toString());
        if (localFilters.minPrice) params.set('minPrice', localFilters.minPrice.toString());
        if (localFilters.maxPrice) params.set('maxPrice', localFilters.maxPrice.toString());
        if (localFilters.binOnly !== undefined) params.set('binOnly', localFilters.binOnly.toString());
        if (localFilters.showEnded !== undefined) params.set('showEnded', localFilters.showEnded.toString());
        if (localFilters.sortBy) params.set('sortBy', localFilters.sortBy);

        // Preserve existing params like 'filter' for pets
        const existingFilter = searchParams.get('filter');
        if (existingFilter) params.set('filter', existingFilter);

        router.push(`${pathname}?${params.toString()}`);

        // Notify parent component
        if (onFilterChange) {
            onFilterChange(localFilters);
        }
    };

    const clearFilters = () => {
        const cleared: ItemFilters = {
            binOnly: true,
            showEnded: false,
            sortBy: 'lowest'
        };
        setLocalFilters(cleared);
        setAppliedFilters(cleared);

        // Keep only the 'filter' param for pets
        const params = new URLSearchParams();
        const existingFilter = searchParams.get('filter');
        if (existingFilter) params.set('filter', existingFilter);

        router.push(`${pathname}?${params.toString()}`);
        if (onFilterChange) {
            onFilterChange(cleared);
        }
    };

    const hasActiveFilters = !!(
        localFilters.minStars ||
        localFilters.maxStars ||
        localFilters.enchantment ||
        localFilters.minEnchantLevel ||
        localFilters.minPrice ||
        localFilters.maxPrice
    );

    const hasUnappliedChanges = JSON.stringify(localFilters) !== JSON.stringify(appliedFilters);

    return (
        <div className="mb-4">
            {!isOpen ? (
                <Button
                    variant="outline-secondary"
                    onClick={() => setIsOpen(true)}
                    className="w-100"
                >
                    + Add Filters
                </Button>
            ) : (
                <Card className="bg-dark text-light border-secondary">
                    <Card.Header className="d-flex justify-content-between align-items-center">
                        <span className="fw-bold">Filters</span>
                        <Button
                            variant="link"
                            size="sm"
                            className="text-secondary"
                            onClick={() => setIsOpen(false)}
                        >
                            ✕
                        </Button>
                    </Card.Header>
                    <Card.Body>
                        <Row className="g-3">
                            {/* Star Level Filter */}
                            <Col md={6}>
                                <Form.Group>
                                    <Form.Label className="text-secondary small">Min Stars</Form.Label>
                                    <Form.Control
                                        type="number"
                                        min="0"
                                        max="5"
                                        className="bg-dark text-light border-secondary"
                                        placeholder="0"
                                        value={localFilters.minStars || ''}
                                        onChange={(e) => setLocalFilters({
                                            ...localFilters,
                                            minStars: e.target.value ? parseInt(e.target.value) : undefined
                                        })}
                                    />
                                </Form.Group>
                            </Col>
                            <Col md={6}>
                                <Form.Group>
                                    <Form.Label className="text-secondary small">Max Stars</Form.Label>
                                    <Form.Control
                                        type="number"
                                        min="0"
                                        max="5"
                                        className="bg-dark text-light border-secondary"
                                        placeholder="5"
                                        value={localFilters.maxStars || ''}
                                        onChange={(e) => setLocalFilters({
                                            ...localFilters,
                                            maxStars: e.target.value ? parseInt(e.target.value) : undefined
                                        })}
                                    />
                                </Form.Group>
                            </Col>

                            {/* Enchantment Filter */}
                            <Col md={8}>
                                <Form.Group>
                                    <Form.Label className="text-secondary small">Enchantment</Form.Label>
                                    <Form.Control
                                        type="text"
                                        className="bg-dark text-light border-secondary"
                                        placeholder="e.g., sharpness, protection"
                                        value={localFilters.enchantment || ''}
                                        onChange={(e) => setLocalFilters({ 
                                            ...localFilters,
                                            enchantment: e.target.value || undefined 
                                        })}
                                    />
                                </Form.Group>
                            </Col>
                            <Col md={4}>
                                <Form.Group>
                                    <Form.Label className="text-secondary small">Min Level</Form.Label>
                                    <Form.Control
                                        type="number"
                                        min="1"
                                        className="bg-dark text-light border-secondary"
                                        placeholder="1"
                                        value={localFilters.minEnchantLevel || ''}
                                        onChange={(e) => setLocalFilters({
                                            ...localFilters,
                                            minEnchantLevel: e.target.value ? parseInt(e.target.value) : undefined
                                        })}
                                        disabled={!localFilters.enchantment}
                                    />
                                </Form.Group>
                            </Col>

                            {/* Price Range Filter */}
                            <Col md={6}>
                                <Form.Group>
                                    <Form.Label className="text-secondary small">Min Price (coins)</Form.Label>
                                    <Form.Control
                                        type="number"
                                        className="bg-dark text-light border-secondary"
                                        placeholder="0"
                                        value={localFilters.minPrice || ''}
                                        onChange={(e) => setLocalFilters({
                                            ...localFilters,
                                            minPrice: e.target.value ? parseInt(e.target.value) : undefined
                                        })}
                                    />
                                </Form.Group>
                            </Col>
                            <Col md={6}>
                                <Form.Group>
                                    <Form.Label className="text-secondary small">Max Price (coins)</Form.Label>
                                    <Form.Control
                                        type="number"
                                        className="bg-dark text-light border-secondary"
                                        placeholder="Unlimited"
                                        value={localFilters.maxPrice || ''}
                                        onChange={(e) => setLocalFilters({
                                            ...localFilters,
                                            maxPrice: e.target.value ? parseInt(e.target.value) : undefined
                                        })}
                                    />
                                </Form.Group>
                            </Col>

                            {/* Auction Type & Sorting */}
                            <Col md={6}>
                                <Form.Group>
                                    <Form.Label className="text-secondary small">Auction Type</Form.Label>
                                    <Form.Select
                                        className="bg-dark text-light border-secondary"
                                        value={localFilters.binOnly === undefined ? 'all' : (localFilters.binOnly ? 'bin' : 'auction')}
                                        onChange={(e) => setLocalFilters({
                                            ...localFilters,
                                            binOnly: e.target.value === 'all' ? undefined : e.target.value === 'bin'
                                        })}
                                    >
                                        <option value="all">All</option>
                                        <option value="bin">BIN Only</option>
                                        <option value="auction">Auction Only</option>
                                    </Form.Select>
                                </Form.Group>
                            </Col>
                            <Col md={6}>
                                <Form.Group>
                                    <Form.Label className="text-secondary small">Sort By</Form.Label>
                                    <Form.Select
                                        className="bg-dark text-light border-secondary"
                                        value={localFilters.sortBy || 'lowest'}
                                        onChange={(e) => setLocalFilters({
                                            ...localFilters,
                                            sortBy: e.target.value as 'lowest' | 'highest' | 'ending'
                                        })}
                                    >
                                        <option value="lowest">Lowest Price</option>
                                        <option value="highest">Highest Price</option>
                                        <option value="ending">Ending Soon</option>
                                    </Form.Select>
                                </Form.Group>
                            </Col>

                            {/* Show Ended Toggle */}
                            <Col xs={12}>
                                <Form.Check
                                    type="checkbox"
                                    label="Show Ended Auctions"
                                    className="text-light"
                                    checked={localFilters.showEnded || false}
                                    onChange={(e) => setLocalFilters({ 
                                        ...localFilters,
                                        showEnded: e.target.checked 
                                    })}
                                />
                            </Col>
                        </Row>

                        <div className="mt-3 d-flex gap-2">
                            <Button
                                variant="primary"
                                onClick={applyFilters}
                                disabled={!hasUnappliedChanges}
                            >
                                Apply Filters
                            </Button>
                            {hasActiveFilters && (
                                <Button
                                    variant="outline-danger"
                                    size="sm"
                                    onClick={clearFilters}
                                >
                                    Clear All
                                </Button>
                            )}
                        </div>
                    </Card.Body>
                </Card>
            )}
        </div>
    );
}
