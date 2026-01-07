import 'bootstrap/dist/css/bootstrap.min.css';
import type { Metadata } from "next";
import { Inter } from "next/font/google";
import "./globals.css";
import NavBar from '@/components/NavBar';
import { Container } from 'react-bootstrap';

const inter = Inter({ subsets: ["latin"] });

export const metadata: Metadata = {
  title: "SkyFlipperSolo",
  description: "Hypixel Skyblock Auction Flipper",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body className={inter.className} data-bs-theme="dark" style={{ backgroundColor: '#121212', color: '#e0e0e0', minHeight: '100vh' }}>
        <NavBar />
        <Container>
          {children}
        </Container>
      </body>
    </html>
  );
}
