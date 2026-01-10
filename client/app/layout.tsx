import 'bootstrap/dist/css/bootstrap.min.css';
import 'react-toastify/dist/ReactToastify.css';
import type { Metadata } from "next";
import { Inter } from "next/font/google";
import "./globals.css";
import NavBar from '@/components/NavBar';
import { Container } from 'react-bootstrap';
import ToastProvider from '@/components/ToastProvider';

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
        <ToastProvider>
          <NavBar />
          <Container>
            {children}
          </Container>
        </ToastProvider>
      </body>
    </html>
  );
}
