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

## Cache
- Reference cache uses Redis distributed cache (2h TTL).

## Filters
- FilterEngine/FilterRegistry with SkyFilter-compatible FilterType flags.
- `NbtNumberFilter` base: number range queries against `NBTLookups.ValueNumeric`.
- `NbtStringFilter` base: exact/any/none matching via `NBTLookups.ValueId` + `NBTValues`.
- `BoolNbtFilter` base: presence/absence check on `NBTLookups.KeyId`.
- `NumberFilterBase` base: abstract range filtering with `RangeParser`.
- Full coflnet registration parity implemented (~345 filter registrations).
- All filters registered in `FilterBootstrapper.RegisterCoreFilters()`.
- Applicability infrastructure:
  - `IApplicableFilter` for context-aware applicability checks.
  - `INbtFilter` to expose required NBT key.
  - `FilterApplicabilityContext` (tag, category, enchant presence, NBT keys).
  - `FilterRegistry.FiltersFor(context)` for per-tag filter selection.
- `/api/auctions/filters/{tag}` now resolves filter list by tag context rather than returning all filters.

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
