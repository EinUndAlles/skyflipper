# Active Context

## Focus
Close remaining P1/P3 parity gaps (AveragePrice CacheKey length, debug endpoints, Prometheus, regression fixtures).

## What Exists
- Coflnet-style reference-auction engine implemented.
- NBT parsing aligned to reference.
- Enchant list aligned to `dev/Data/Flipper/Constants.cs`.
- Price history aggregation and flip detection aligned.
- NBTLookup storage now uses KeyId/ValueId only.
- NBT SQL filtering extracted to `NbtQueryHelper`.
- NBT key/value ID resolver centralized in `NbtLookupResolver`.

## Current Risks
- `AveragePrice.CacheKey` length is still `varchar(200)` (risk of truncation).
- `/flips` hydration warnings may still exist; consider no-SSR wrapper if recurring.
- Real-data regression fixtures are limited; capturing live mismatches would improve confidence.

## Next Steps
- Expand `AveragePrice.CacheKey` length.
- Add debug endpoints for flip reference inspection.
- Add Prometheus metrics for runtime monitoring.
- Expand parity tests with live-data fixtures.
