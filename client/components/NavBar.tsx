'use client';

import Link from 'next/link';
import { Navbar, Container, Nav, Form, Button } from 'react-bootstrap';
import { useState } from 'react';
import { useRouter } from 'next/navigation';

export default function NavBar() {
    const [searchTerm, setSearchTerm] = useState('');
    const router = useRouter();

    const handleSearch = (e: React.FormEvent) => {
        e.preventDefault();
        if (searchTerm.trim()) {
            router.push(`/search?q=${encodeURIComponent(searchTerm)}`);
        }
    };

    return (
        <Navbar bg="dark" variant="dark" expand="lg" className="mb-4 shadow-sm" style={{ backdropFilter: 'blur(10px)', backgroundColor: 'rgba(33, 37, 41, 0.85)' }}>
            <Container>
                <Navbar.Brand as={Link} href="/" className="fw-bold text-primary">SkyFlipperSolo</Navbar.Brand>
                <Navbar.Toggle aria-controls="basic-navbar-nav" />
                <Navbar.Collapse id="basic-navbar-nav">
                    <Nav className="me-auto">
                        <Nav.Link as={Link} href="/">Home</Nav.Link>
                        <Nav.Link as={Link} href="/search">All Auctions</Nav.Link>
                    </Nav>
                    <Form className="d-flex" onSubmit={handleSearch}>
                        <Form.Control
                            type="search"
                            placeholder="Search items..."
                            className="me-2 bg-dark text-light border-secondary"
                            aria-label="Search"
                            value={searchTerm}
                            onChange={(e) => setSearchTerm(e.target.value)}
                        />
                        <Button variant="outline-primary" type="submit">Search</Button>
                    </Form>
                </Navbar.Collapse>
            </Container>
        </Navbar>
    );
}
