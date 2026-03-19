# Active Context

## Focus
Parity fixes from `FULL_PARITY_REVIEW.md` with P0 accuracy gaps first.

## What Exists
- Coflnet-style reference-auction engine implemented.
- NBT parsing aligned to reference.
- Enchant list aligned to `dev/Data/Flipper/Constants.cs`.
- Price history aggregation and flip detection aligned.

## Current Risks
- CacheKey format now matches Coflnet raw concat; range normalization removed.
- SQL-level NBT filtering missing (performance at scale).
- `/flips` hydration warnings may still exist; consider no-SSR wrapper if recurring.
- Real-data regression fixtures are limited; capturing live mismatches would improve confidence.

## Next Steps
- Port `SelectBestEnchant` WorthOrder/WorthOrderLevels.
- Add SQL-level NBT filtering.
- Add debug endpoints for flip reference inspection.
- Add Prometheus metrics for runtime monitoring.
- Expand parity tests with live-data fixtures.
