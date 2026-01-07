# SkyFlipperSolo

A modern Hypixel Skyblock auction tracker with a Next.js frontend and .NET backend.

## Features

- **Real-time Auction Tracking**: Automatically fetches and stores Hypixel Skyblock auction data
- **Modern UI**: Beautiful Next.js frontend with rarity-based coloring
- **Search & Filter**: Search auctions by item tag
- **Auction Details**: View comprehensive item information including enchantments
- **Statistics Dashboard**: Track total auctions, BIN auctions, and trending items

## Tech Stack

### Backend (.NET 8)
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL
- Background services for auction fetching

### Frontend (Next.js)
- Next.js 16 with App Router
- TypeScript
- React Bootstrap
- Axios for API calls

## Getting Started

### Prerequisites
- .NET 8 SDK
- Node.js 18+
- PostgreSQL

### Backend Setup

1. Navigate to the project directory:
   ```bash
   cd SkyFlipperSolo
   ```

2. Update the connection string in `appsettings.json` if needed

3. Run the backend:
   ```bash
   dotnet run
   ```

The API will be available at `http://localhost:5135`

### Frontend Setup

1. Navigate to the client directory:
   ```bash
   cd client
   ```

2. Install dependencies:
   ```bash
   npm install
   ```

3. Run the development server:
   ```bash
   npm run dev
   ```

The frontend will be available at `http://localhost:3000`

## Features

- **Rarity-Based Coloring**: Items are colored by rarity (Legendary, Epic, Rare, etc.)
- **Live Stats**: Real-time auction statistics
- **Search**: Find auctions by item tag
- **Detailed Views**: Comprehensive auction information with enchantments

## License

MIT
