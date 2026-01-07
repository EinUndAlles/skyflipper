import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  images: {
    remotePatterns: [
      {
        protocol: 'https',
        hostname: 'sky.coflnet.com',
        port: '',
        pathname: '/static/icon/**',
      },
      {
        protocol: 'https',
        hostname: 'mc-heads.net', // hypixel-react uses this too sometimes
        port: '',
        pathname: '/**',
      },
    ],
  },
};

export default nextConfig;
