# Tasks Plan

## Current Status
- Overall parity ~85% per `FULL_PARITY_REVIEW.md` (P0 gaps remain).
- Reference-auction engine and cache-key parity mostly implemented.
- Enchant list aligned to `dev/Data/Flipper/Constants.cs` (P0 item resolved).
- API contracts aligned to Coflnet-style payloads (core endpoints).

## Remaining Tasks
1. Align CacheKey format with Coflnet raw concat (P0). (done)
2. Fix `ShouldPetItemMatch` exp guard (P0). (done)
3. Port `SelectBestEnchant` WorthOrder/WorthOrderLevels (P0). (done)
4. Move NBT filtering to SQL-level in `ReferenceAuctionService` (P1). (done)
5. Add debug endpoints in `FlipsController` (P3).
6. Add Prometheus metrics for flip detection loop (P3).
7. Capture and add real-data regression fixtures to tests (P3).
8. Optional: frontend polish / no-SSR wrapper for `/flips` if hydration warnings persist.

## Completed Highlights
- Sold auction ingestion uses `SoldAt` + `SoldPrice`.
- Price aggregation uses sold-time bucketing and volume-weighted daily rollups.
- Anti-manipulation dedupe implemented in reference selection and aggregation.
