# Technical

## Stack
- Backend: ASP.NET Core 8, EF Core, PostgreSQL, SignalR
- Frontend: Next.js (App Router), TypeScript, React Bootstrap

## Services Registration
- See `Program.cs` for DI and hosted services registration.
- All background services run continuously on startup.

## Runtime
- API base: `http://localhost:5135`
- SignalR: `http://localhost:5135/hubs/flips`
- Frontend dev server: `http://localhost:3000`

## Data
- EF Core migrations auto-apply on startup.
- Core entities: `Auction`, `AveragePrice`, `FlipOpportunity`, `NBTLookup`.
- `SoldAt` and `SoldPrice` are required for accurate history/valuation.

## Parity References
- Coflnet core: `C:\Users\floor\Documents\coding\skyblock\dev`
- Flip engine: `C:\Users\floor\Documents\coding\skyblock\SkyFlipper\Flipper\FlippingEngine.cs`

## Parity Notes
- Overall parity ~85% per `FULL_PARITY_REVIEW.md`.
- P0 gaps remain: SelectBestEnchant ordering, SQL-level NBT filtering, pet exp guard.
