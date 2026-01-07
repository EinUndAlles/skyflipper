'use client';

import { useEffect, useState } from 'react';
import { api } from '@/api/ApiHelper';
import { Stats, TagCount } from '@/types';
import { Row, Col, Card, Container, Spinner, Badge } from 'react-bootstrap';
import Link from 'next/link';

export default function Home() {
  const [stats, setStats] = useState<Stats | null>(null);
  const [topTags, setTopTags] = useState<TagCount[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchData = async () => {
      try {
        const [statsData, tagsData] = await Promise.all([
          api.getStats(),
          api.getTopTags(20)
        ]);
        setStats(statsData);
        setTopTags(tagsData);
      } catch (e) {
        console.error("Failed to fetch data", e);
      } finally {
        setLoading(false);
      }
    };
    fetchData();
  }, []);

  if (loading) {
    return <div className="text-center mt-5"><Spinner animation="border" variant="primary" /></div>;
  }

  return (
    <main>
      <div className="text-center mb-5">
        <h1 className="display-4 fw-bold mb-3">SkyFlipperSolo</h1>
        <p className="lead text-muted">Advanced Hypixel Skyblock Auction Tracker</p>
      </div>

      {stats && (
        <Row className="mb-5 g-4">
          <Col md={3}>
            <Card className="text-center bg-dark border-secondary h-100">
              <Card.Body>
                <h2 className="display-6 fw-bold text-success">{stats.totalAuctions.toLocaleString()}</h2>
                <div className="text-muted">Total Auctions</div>
              </Card.Body>
            </Card>
          </Col>
          <Col md={3}>
            <Card className="text-center bg-dark border-secondary h-100">
              <Card.Body>
                <h2 className="display-6 fw-bold text-info">{stats.binAuctions.toLocaleString()}</h2>
                <div className="text-muted">BIN Auctions</div>
              </Card.Body>
            </Card>
          </Col>
          <Col md={3}>
            <Card className="text-center bg-dark border-secondary h-100">
              <Card.Body>
                <h2 className="display-6 fw-bold text-warning">{stats.uniqueItemTags.toLocaleString()}</h2>
                <div className="text-muted">Unique Items</div>
              </Card.Body>
            </Card>
          </Col>
          <Col md={3}>
            <Card className="text-center bg-dark border-secondary h-100">
              <Card.Body>
                <h2 className="display-6 fw-bold text-danger">{stats.recentAuctions.toLocaleString()}</h2>
                <div className="text-muted">Recent (5m)</div>
              </Card.Body>
            </Card>
          </Col>
        </Row>
      )}

      <h3 className="mb-4 border-bottom border-secondary pb-2">Top Items</h3>
      <div className="d-flex flex-wrap gap-2 justify-content-center">
        {topTags.map((tag) => (
          <Link key={tag.tag} href={`/search?q=${tag.tag}`}>
            <Badge
              bg="secondary"
              className="p-3 text-decoration-none cursor-pointer hover-shadow"
              style={{ cursor: 'pointer', fontSize: '1rem' }}
            >
              {tag.tag} <span className="opacity-50 ms-2">({tag.count})</span>
            </Badge>
          </Link>
        ))}
      </div>
    </main>
  );
}
