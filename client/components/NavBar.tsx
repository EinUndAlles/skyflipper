// eslint-disable-next-line @typescript-eslint/ban-ts-comment
// @ts-nocheck
'use client';

import Link from 'next/link';
import { Navbar, Container, Nav, Form, ListGroup, Badge } from 'react-bootstrap';
import { useState, useEffect, useRef } from 'react';
import { useRouter } from 'next/navigation';
import { api, getItemImageUrl } from '../api/ApiHelper';
import { getTierStyle } from '../utils/rarity';
import { toast } from '@/components/ToastProvider';

export default function NavBar() {
    const [searchTerm, setSearchTerm] = useState('');
    const [searchResults, setSearchResults] = useState<{ itemName: string, tag: string, tier: string, texture?: string, filter?: string, isDuplicate?: boolean }[]>([]);
    const [showDropdown, setShowDropdown] = useState(false);
    const [selectedIndex, setSelectedIndex] = useState(0);
    const wrapperRef = useRef<HTMLDivElement>(null);
    const router = useRouter();

    useEffect(() => {
        const fetchResults = async () => {
            if (searchTerm.length >= 2) {
                try {
                    const results = await api.searchItems(searchTerm);

                    // Deduplicate items by name - keep only first occurrence
                    const seen = new Set<string>();
                    const deduplicatedResults = results
                        .map((item, index) => {
                            const isDuplicate = results.findIndex((r) =>
                                r.itemName === item.itemName
                            ) !== index;
                            return { ...item, isDuplicate };
                        })
                        .filter((item) => {
                            // Only show first occurrence of each item name
                            if (seen.has(item.itemName)) {
                                return false;
                            }
                            seen.add(item.itemName);
                            return true;
                        });

                    setSearchResults(deduplicatedResults);
                    setShowDropdown(true);
                    setSelectedIndex(0); // Reset selection on new results
                } catch (error) {
                    console.error("Search failed", error);
                    toast.error('Failed to search items');
                }
            } else {
                setSearchResults([]);
                setShowDropdown(false);
            }
        };

        const timeoutId = setTimeout(fetchResults, 300); // 300ms debounce
        return () => clearTimeout(timeoutId);
    }, [searchTerm]);

    // Close dropdown when clicking outside
    useEffect(() => {
        function handleClickOutside(event: MouseEvent) {
            if (wrapperRef.current && !wrapperRef.current.contains(event.target as Node)) {
                setShowDropdown(false);
            }
        }
        document.addEventListener("mousedown", handleClickOutside);
        return () => document.removeEventListener("mousedown", handleClickOutside);
    }, [wrapperRef]);

    const handleSearchSubmit = (e: React.FormEvent) => {
        e.preventDefault();
        if (searchResults.length > 0) {
            handleItemClick(searchResults[selectedIndex].tag, searchResults[selectedIndex].filter);
        }
    };

    const handleItemClick = (tag: string, filter?: string) => {
        setSearchTerm('');
        setShowDropdown(false);
        setSelectedIndex(0);
        // Include filter as query param if provided (for pets)
        const url = filter ? `/item/${tag}?filter=${encodeURIComponent(filter)}` : `/item/${tag}`;
        router.push(url);
    };

    const handleKeyDown = (e: React.KeyboardEvent) => {
        if (!showDropdown || searchResults.length === 0) return;

        switch (e.key) {
            case 'ArrowDown':
                e.preventDefault();
                setSelectedIndex(prev => (prev < searchResults.length - 1 ? prev + 1 : prev));
                break;
            case 'ArrowUp':
                e.preventDefault();
                setSelectedIndex(prev => (prev > 0 ? prev - 1 : prev));
                break;
            case 'Escape':
                e.preventDefault();
                setShowDropdown(false);
                setSelectedIndex(0);
                break;
        }
    };

    return (
        <Navbar bg="dark" variant="dark" expand="lg" className="mb-4 shadow-sm" style={{ backdropFilter: 'blur(10px)', backgroundColor: 'rgba(33, 37, 41, 0.85)', position: 'relative', zIndex: 1050 }}>
            <Container>
                <Link href="/" className="navbar-brand fw-bold text-primary">SkyFlipperSolo</Link>
                <Navbar.Toggle aria-controls="basic-navbar-nav" />
                <Navbar.Collapse id="basic-navbar-nav">
<Nav className="me-auto">
                        <Link href="/" className="nav-link">Home</Link>
                        <Link href="/flips" className="nav-link">Live Flips</Link>
                        {/* <Link href="/search" className="nav-link">All Auctions</Link> */}
                    </Nav>

                    <div className="position-relative" style={{ width: '100%', maxWidth: '400px' }} ref={wrapperRef}>
                        <Form className="d-flex" onSubmit={handleSearchSubmit}>
                            <Form.Control
                                type="search"
                                placeholder="Search items..."
                                className="bg-dark text-light border-secondary"
                                aria-label="Search"
                                value={searchTerm}
                                onChange={(e) => setSearchTerm(e.target.value)}
                                onFocus={() => searchTerm.length >= 2 && setShowDropdown(true)}
                                onKeyDown={handleKeyDown}
                            />
                        </Form>

                        {showDropdown && searchResults.length > 0 && (
                            <ListGroup className="position-absolute w-100 mt-1 shadow-lg border-secondary" style={{ zIndex: 9999, maxHeight: '400px', overflowY: 'auto' }}>
                                {searchResults.map((item, index) => {
                                    const isSelected = index === selectedIndex;
                                    const itemStyle = getTierStyle(item.tier);

                                    return (
                                        <ListGroup.Item
                                            key={`${item.tag}-${item.itemName}-${index}`}
                                            action
                                            onClick={() => handleItemClick(item.tag, item.filter)}
                                            className="bg-dark text-light border-secondary d-flex align-items-center justify-content-between"
                                            style={{
                                                cursor: 'pointer',
                                                backgroundColor: isSelected ? '#333333' : undefined
                                            }}
                                        >
                                            <div className="d-flex align-items-center flex-grow-1">
                                                <div style={{ width: '32px', height: '32px', marginRight: '10px' }}>
                                                    {/* eslint-disable-next-line @next/next/no-img-element */}
                                                    <img
                                                        src={getItemImageUrl(item.tag, 'default', item.texture)}
                                                        alt={item.itemName}
                                                        className="w-100 h-100 object-fit-contain"
                                                        style={{ imageRendering: 'pixelated' }}
                                                    />
                                                </div>
                                                <div className="d-flex flex-column">
                                                    <span className="fw-bold" style={itemStyle}>
                                                        {item.itemName}
                                                    </span>
                                                </div>
                                            </div>
                                        </ListGroup.Item>
                                    );
                                })}
                            </ListGroup>
                        )}
                    </div>
                </Navbar.Collapse>
            </Container>
        </Navbar>
    );
}
