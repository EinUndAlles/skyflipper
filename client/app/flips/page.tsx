'use client';

import React, { useEffect, useState, useRef, useCallback } from 'react';
import { Container, Row, Col, Card, Badge, Button, Spinner, Alert } from 'react-bootstrap';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import { FlipNotification } from '@/types/flip';
import { getItemImageUrl } from '@/api/ApiHelper';
import { toast } from '@/components/ToastProvider';
import Link from 'next/link';

export default function FlipsPage() {
    const [flips, setFlips] = useState<FlipNotification[]>([]);
    const [connectionState, setConnectionState] = useState<string>('Disconnected');
    const [notificationPermission, setNotificationPermission] = useState<NotificationPermission>(() => {
        if (typeof window !== 'undefined' && 'Notification' in window) {
            return Notification.permission;
        }
        return 'default';
    });
    const connectionRef = useRef<HubConnection | null>(null);
    const isUnmountingRef = useRef(false); // Track intentional unmount

    const requestNotificationPermission = async () => {
        if (!('Notification' in window)) {
            toast.error('This browser does not support desktop notifications');
            return;
        }

        try {
            const permission = await Notification.requestPermission();
            setNotificationPermission(permission);
            if (permission === 'granted') {
                toast.success('Notifications enabled!');
                new Notification('SkyFlipper', { body: 'Notifications enabled successfully!' });
            }
        } catch (err) {
            console.error('Error requesting notification permission:', err);
        }
    };

    const sendNotification = useCallback((flip: FlipNotification) => {
        if (notificationPermission === 'granted') {
            const n = new Notification(`New Flip: ${flip.itemName}`, {
                body: `Profit: ${flip.estimatedProfit.toLocaleString()} coins (${flip.profitMarginPercent.toFixed(1)}%)`,
                icon: getItemImageUrl(flip.itemTag)
            });
            n.onclick = () => {
                window.open(`/auction/${flip.auctionUuid}`, '_blank');
            };
        }
    }, [notificationPermission]);

    // Handle incoming new flip
    const handleNewFlip = useCallback((flip: FlipNotification) => {
        setFlips(prev => {
            const exists = prev.some(f => f.auctionUuid === flip.auctionUuid);
            if (exists) return prev;

            const newFlips = [flip, ...prev];
            // Keep sorted by profit
            return newFlips.sort((a, b) => b.estimatedProfit - a.estimatedProfit);
        });

        sendNotification(flip);
    }, [sendNotification]);

    // Handle full update
    const handleFlipsUpdated = useCallback((updatedFlips: FlipNotification[]) => {
        setFlips(updatedFlips.sort((a, b) => b.estimatedProfit - a.estimatedProfit));
    }, []);

    // Handle auction sold/expired - remove from list
    const handleAuctionSold = useCallback((auctionUuid: string) => {
        setFlips(prev => prev.filter(f => f.auctionUuid !== auctionUuid));
    }, []);

    // Connect to SignalR
    useEffect(() => {
        const connect = async () => {
            setConnectionState('Connecting');

            try {
                const connection = new HubConnectionBuilder()
                    .withUrl('http://localhost:5135/hubs/flips')
                    .withAutomaticReconnect()
                    .configureLogging(LogLevel.Information)
                    .build();

                connection.on('FlipsUpdated', handleFlipsUpdated);
                connection.on('NewFlip', handleNewFlip);
                connection.on('AuctionSold', handleAuctionSold);

                connection.onreconnecting(() => {
                    setConnectionState('Reconnecting');
                    toast.warning('Connection lost, reconnecting...');
                });

                connection.onreconnected(() => {
                    setConnectionState('Connected');
                    toast.success('Reconnected to live flips');
                    connection.invoke('SubscribeToFlips').catch(err => console.error(err));
                });

                connection.onclose(() => {
                    // Only show error toast if this wasn't an intentional disconnect
                    if (!isUnmountingRef.current) {
                        setConnectionState('Disconnected');
                        toast.error('Connection to live flips lost');
                    }
                });

                await connection.start();
                setConnectionState('Connected');
                // toast.success('Connected to live flips');

                // Subscribe after connection
                await connection.invoke('SubscribeToFlips');

                connectionRef.current = connection;
            } catch (err) {
                console.error('SignalR Connection Error:', err);
                setConnectionState('Disconnected');
                toast.error('Failed to connect to live flips service');
            }
        };

        connect();

        return () => {
            isUnmountingRef.current = true; // Mark as intentional unmount
            if (connectionRef.current) {
                connectionRef.current.off('FlipsUpdated');
                connectionRef.current.off('NewFlip');
                connectionRef.current.off('AuctionSold');
                connectionRef.current.stop();
            }
        };
    }, [handleFlipsUpdated, handleNewFlip, handleAuctionSold]);

    // Helper for badge color based on profit
    const getProfitBadgeVariant = (profit: number) => {
        if (profit > 10000000) return 'danger'; // 10m+
        if (profit > 1000000) return 'warning'; // 1m+
        if (profit > 100000) return 'success'; // 100k+
        return 'primary';
    };

    // Format currency
    const formatCoins = (amount: number) => {
        return amount.toLocaleString(undefined, { maximumFractionDigits: 0 });
    };

    // Format time
    const formatTime = (dateStr: string) => {
        const date = new Date(dateStr);
        return date.toLocaleTimeString();
    };

    return (
        <Container fluid className="py-4">
            <div className="d-flex justify-content-between align-items-center mb-4">
                <div>
                    <h1 className="display-5 fw-bold mb-0">Live Flips</h1>
                    <p className="text-muted mb-0">Real-time auction flipping opportunities</p>
                </div>

                <div className="d-flex gap-3 align-items-center">
                    {/* Notification Permission Button */}
                    {notificationPermission !== 'granted' && (
                        <Button
                            variant="outline-info"
                            size="sm"
                            onClick={requestNotificationPermission}
                        >
                            <i className="bi bi-bell-fill me-2"></i>
                            Enable Notifications
                        </Button>
                    )}

                    {/* Connection Status Badge */}
                    <Badge
                        bg={
                            connectionState === 'Connected' ? 'success' :
                                connectionState === 'Connecting' || connectionState === 'Reconnecting' ? 'warning' : 'danger'
                        }
                        className="p-2 px-3"
                        style={{ fontSize: '0.9rem' }}
                    >
                        {connectionState === 'Connecting' || connectionState === 'Reconnecting' ? (
                            <Spinner animation="border" size="sm" className="me-2" />
                        ) : null}
                        {connectionState}
                    </Badge>
                </div>
            </div>

            {/* Empty State */}
            {flips.length === 0 && connectionState === 'Connected' && (
                <Alert variant="info" className="text-center py-5 bg-dark border-secondary text-light">
                    <Spinner animation="grow" variant="info" className="mb-3" />
                    <h4>Scanning for flips...</h4>
                    <p className="mb-0">Waiting for profitable auctions to appear. Make sure the backend sniper is running.</p>
                </Alert>
            )}

            {/* Disconnected State */}
            {connectionState === 'Disconnected' && (
                <Alert variant="danger" className="text-center bg-dark border-danger text-danger">
                    <h4>Disconnected</h4>
                    <p className="mb-2">Connection to the flip server has been lost. Please refresh the page or check if the server is running.</p>
                    <Button variant="outline-danger" size="sm" onClick={() => window.location.reload()}>
                        Reload Page
                    </Button>
                </Alert>
            )}

            <Row className="g-4">
                {flips.map((flip) => (
                    <Col key={flip.auctionUuid} xs={12} md={6} lg={4} xl={3}>
                        <Card className="h-100 bg-dark border-secondary text-light shadow-sm hover-shadow transition-all">
                            <Card.Header className="d-flex justify-content-between align-items-center border-secondary bg-dark bg-opacity-50">
                                <Badge bg="secondary" className="text-truncate" style={{ maxWidth: '120px' }}>
                                    {flip.itemTag}
                                </Badge>
                                <small className="text-muted" title={`Detected at ${new Date(flip.detectedAt).toLocaleString()}`}>
                                    {formatTime(flip.detectedAt)}
                                </small>
                            </Card.Header>

                            <Card.Body>
                                <div className="d-flex align-items-center mb-3">
                                    <div className="flex-shrink-0 me-3">
                                        <img
                                            src={getItemImageUrl(flip.itemTag)}
                                            alt={flip.itemName}
                                            width={64}
                                            height={64}
                                            className="rounded"
                                            style={{ objectFit: 'contain', backgroundColor: '#2a2a2a' }}
                                        />
                                    </div>
                                    <div className="flex-grow-1 min-w-0">
                                        <h5 className="card-title text-truncate mb-1" title={flip.itemName}>
                                            {flip.itemName}
                                        </h5>
                                        <div className="d-flex align-items-center gap-2 flex-wrap">
                                            <Badge bg="info" pill>{flip.dataSource}</Badge>
                                            {flip.valueBreakdown && (
                                                <Badge bg="warning" text="dark" pill title={flip.valueBreakdown}>
                                                    <i className="bi bi-gem me-1"></i>
                                                    Value Added
                                                </Badge>
                                            )}
                                        </div>
                                    </div>
                                </div>

                                {flip.valueBreakdown && (
                                    <div className="alert alert-secondary py-1 px-2 mb-3 small">
                                        <i className="bi bi-info-circle me-1"></i>
                                        {flip.valueBreakdown}
                                    </div>
                                )}

                                <div className="p-3 rounded bg-secondary bg-opacity-10 mb-3">
                                    <div className="d-flex justify-content-between mb-2">
                                        <span className="text-muted">Buy Price:</span>
                                        <span className="fw-bold text-light">{formatCoins(flip.currentPrice)}</span>
                                    </div>
                                    <div className="d-flex justify-content-between mb-2">
                                        <span className="text-muted">Median:</span>
                                        <span className="text-light">{formatCoins(flip.medianPrice)}</span>
                                    </div>
                                    <div className="border-top border-secondary my-2 pt-2 d-flex justify-content-between align-items-center">
                                        <span className="text-success fw-bold">Profit:</span>
                                        <div className="text-end">
                                            <div className={`fw-bold text-${getProfitBadgeVariant(flip.estimatedProfit)}`}>
                                                +{formatCoins(flip.estimatedProfit)}
                                            </div>
                                            <small className={`text-${getProfitBadgeVariant(flip.estimatedProfit)}`}>
                                                {flip.profitMarginPercent.toFixed(1)}% margin
                                            </small>
                                        </div>
                                    </div>
                                </div>

                                <div className="d-flex gap-2">
                                    <Link href={`/auction/${flip.auctionUuid}`} className="btn btn-primary flex-grow-1">
                                        View Auction
                                    </Link>
                                    <Button
                                        variant="outline-light"
                                        title="Copy /viewauction command"
                                        onClick={() => {
                                            navigator.clipboard.writeText(`/viewauction ${flip.auctionUuid}`);
                                            toast.success('Command copied!');
                                        }}
                                    >
                                        <i className="bi bi-clipboard"></i>
                                    </Button>
                                </div>
                            </Card.Body>
                        </Card>
                    </Col>
                ))}
            </Row>
        </Container>
    );
}
