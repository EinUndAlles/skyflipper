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
- Metrics: `http://localhost:5135/metrics`

## NBT Storage
- Legacy `NBTLookups.Key` and `NBTLookups.ValueString` removed.
- NBT lookups now use `KeyId` and `ValueId` exclusively.
- SQL-level filtering extracted into `Services/NbtQueryHelper.cs`.
- Key/value ID lookup centralized in `Services/NbtLookupResolver.cs`.

## Data
- EF Core migrations auto-apply on startup.
- Core entities: `Auction`, `AveragePrice`, `FlipOpportunity`, `NBTLookup`.
- `SoldAt` and `SoldPrice` are required for accurate history/valuation.

## Parity References
- Coflnet core: `C:\Users\floor\Documents\coding\skyblock\dev`
- Flip engine: `C:\Users\floor\Documents\coding\skyblock\SkyFlipper\Flipper\FlippingEngine.cs`

## Parity Notes
- Overall parity ~90% based on latest review.
- P0 correctness gaps closed (enchants, cache key format, pet exp guard, SelectBestEnchant ordering, SQL-level NBT filtering).
- Remaining: operational tooling (Prometheus/debug endpoints) and intentional monolith architecture.
