'use client';

import React, { useEffect, useState, useRef, useCallback } from 'react';
import { Container, Row, Col, Card, Badge, Button, Spinner, Alert } from 'react-bootstrap';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import { FlipNotification } from '@/types/flip';
import { api, getItemImageUrl } from '@/api/ApiHelper';
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
            const existingIndex = prev.findIndex(f => f.auctionUuid === flip.auctionUuid);
            if (existingIndex >= 0) {
                const updated = [...prev];
                updated[existingIndex] = { ...updated[existingIndex], ...flip, status: 'ACTIVE' };
                return updated.sort((a, b) => b.estimatedProfit - a.estimatedProfit);
            }

            const newFlips = [{ ...flip, status: 'ACTIVE' as const }, ...prev];
            // Keep sorted by profit
            return newFlips.sort((a, b) => b.estimatedProfit - a.estimatedProfit);
        });

        sendNotification(flip);
    }, [sendNotification]);

    // Handle full update
    const handleFlipsUpdated = useCallback((updatedFlips: FlipNotification[]) => {
        setFlips(prev => {
            const byUuid = new Map<string, FlipNotification>();

            for (const existing of prev) {
                byUuid.set(existing.auctionUuid, existing);
            }

            for (const incoming of updatedFlips) {
                const existing = byUuid.get(incoming.auctionUuid);
                byUuid.set(incoming.auctionUuid, {
                    ...existing,
                    ...incoming,
                    status: existing?.status && existing.status !== 'ACTIVE' ? existing.status : 'ACTIVE'
                });
            }

            return Array.from(byUuid.values()).sort((a, b) => {
                const aClosed = a.status && a.status !== 'ACTIVE' ? 1 : 0;
                const bClosed = b.status && b.status !== 'ACTIVE' ? 1 : 0;
                if (aClosed !== bClosed) {
                    return aClosed - bClosed;
                }
                return b.estimatedProfit - a.estimatedProfit;
            });
        });
    }, []);

    // Handle auction sold/expired - keep card but mark status
    const handleAuctionStatusChanged = useCallback((update: { auctionUuid: string; status: 'SOLD' | 'EXPIRED' | 'ACTIVE' }) => {
        setFlips(prev => prev.map(f =>
            f.auctionUuid === update.auctionUuid
                ? { ...f, status: update.status }
                : f));
    }, []);

    // Connect to SignalR
    useEffect(() => {
        const connect = async () => {
            setConnectionState('Connecting');

            try {
                const initialFlips = await api.getFlips();
                handleFlipsUpdated(initialFlips);

                const connection = new HubConnectionBuilder()
                    .withUrl('http://localhost:5135/hubs/flips')
                    .withAutomaticReconnect()
                    .configureLogging(LogLevel.Information)
                    .build();

                connection.on('FlipsUpdated', handleFlipsUpdated);
                connection.on('NewFlip', handleNewFlip);
                connection.on('AuctionStatusChanged', handleAuctionStatusChanged);

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
                connectionRef.current.off('AuctionStatusChanged');
                connectionRef.current.stop();
            }
        };
    }, [handleFlipsUpdated, handleNewFlip, handleAuctionStatusChanged]);

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
                        <div className="h-100 d-flex flex-column" style={{
                            backgroundColor: '#100010',
                            border: '2px solid #28007d',
                            borderRadius: '4px',
                            padding: '12px',
                            color: '#ffffff',
                            fontFamily: 'Consolas, monospace',
                            boxShadow: '0 4px 8px rgba(0,0,0,0.5)'
                        }}>
                            {/* Header: Item Image and Name */}
                            <div className="text-center mb-3">
                                <div className="d-flex justify-content-center mb-2" style={{ height: '64px' }}>
                                    <img
                                        src={getItemImageUrl(flip.itemTag)}
                                        alt={flip.itemName}
                                        width={64}
                                        height={64}
                                        style={{ objectFit: 'contain', imageRendering: 'pixelated' }}
                                    />
                                </div>
                                <div style={{ 
                                    color: '#55FFFF', // Default rarity color (Aqua)
                                    fontWeight: 'bold', 
                                    fontSize: '1.1rem',
                                    textShadow: '2px 2px 0px #000'
                                }}>
                                    {flip.itemName}
                                </div>
                            </div>

                            {/* BIN Badge */}
                            <div className="mb-3">
                                <div className="d-flex gap-2">
                                    <span style={{ 
                                        backgroundColor: '#ffaa00', 
                                        color: '#000', 
                                        fontWeight: 'bold', 
                                        padding: '2px 6px', 
                                        borderRadius: '2px',
                                        fontSize: '0.8rem',
                                        textTransform: 'uppercase'
                                    }}>
                                        BIN
                                    </span>
                                    {flip.status && flip.status !== 'ACTIVE' && (
                                        <span style={{
                                            backgroundColor: flip.status === 'SOLD' ? '#55aa55' : '#777777',
                                            color: '#ffffff',
                                            fontWeight: 'bold',
                                            padding: '2px 6px',
                                            borderRadius: '2px',
                                            fontSize: '0.8rem',
                                            textTransform: 'uppercase'
                                        }}>
                                            {flip.status}
                                        </span>
                                    )}
                                </div>
                            </div>

                            {/* Stats */}
                            <div className="flex-grow-1">
                                <div className="mb-3">
                                    <div style={{ color: '#aaaaaa', fontSize: '0.9rem', marginBottom: '2px' }}>Price:</div>
                                    <div style={{ color: '#ffaa00', fontSize: '1.1rem' }}>{formatCoins(flip.currentPrice)} Coins</div>
                                </div>

                                <div className="mb-3">
                                    <div style={{ color: '#aaaaaa', fontSize: '0.9rem', marginBottom: '2px' }}>Target price:</div>
                                    <div style={{ color: '#ffaa00', fontSize: '1.1rem' }}>{formatCoins(flip.medianPrice)} Coins</div>
                                </div>

                                <div className="mb-3">
                                    <div style={{ color: '#aaaaaa', fontSize: '0.9rem', marginBottom: '2px' }}>Estimated Profit:</div>
                                    <div style={{ color: '#55ff55', fontSize: '1.1rem' }}>
                                        +{formatCoins(flip.estimatedProfit)} Coins ({flip.profitMarginPercent.toFixed(0)}%)
                                    </div>
                                </div>

                                {/* Placeholders */}
                                <div className="mb-3">
                                    <div style={{ color: '#aaaaaa', fontSize: '0.9rem', marginBottom: '2px' }}>Lowest BIN:</div>
                                    <div style={{ color: '#ffaa00' }}>{formatCoins(flip.medianPrice)} Coins</div>
                                </div>

                                <div className="mb-3">
                                    <div style={{ color: '#aaaaaa', fontSize: '0.9rem', marginBottom: '2px' }}>Seller:</div>
                                    <div style={{ color: '#ffffff' }}>{flip.seller || '---'}</div>
                                </div>

                                <div className="mb-3">
                                    <div style={{ color: '#aaaaaa', fontSize: '0.9rem', marginBottom: '2px' }}>Volume:</div>
                                    <div style={{ color: '#ffffff' }}>{flip.volume ?? '---'}</div>
                                </div>
                            </div>

                            {/* Actions */}
                            <div className="mt-2">
                                <Link 
                                    href={`/auction/${flip.auctionUuid}`} 
                                    className="btn w-100 mb-2"
                                    style={{
                                        backgroundColor: '#3f3f3f',
                                        border: '2px solid #000',
                                        color: '#ffffff',
                                        fontWeight: 'bold',
                                        borderRadius: '0',
                                        textTransform: 'uppercase',
                                        boxShadow: 'inset 0 2px 0 rgba(255,255,255,0.1), 0 4px 0 #1a1a1a'
                                    }}
                                >
                                    SNIPE
                                </Link>
                                <Button
                                    variant="link"
                                    className="w-100 text-decoration-none p-0"
                                    style={{ color: '#aaaaaa', fontSize: '0.85rem' }}
                                    onClick={() => {
                                        navigator.clipboard.writeText(`/viewauction ${flip.auctionUuid}`);
                                        toast.success('Command copied!');
                                    }}
                                >
                                    [Copy /viewauction]
                                </Button>
                            </div>
                        </div>
                    </Col>
                ))}
            </Row>
        </Container>
    );
}
