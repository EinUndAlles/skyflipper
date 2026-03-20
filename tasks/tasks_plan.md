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
5. Fix `AveragePrice.CacheKey` length (P1). (done)
6. Add debug endpoints in `FlipsController` (P3). (done)
7. Add Prometheus metrics for flip detection loop (P3). (done)
8. Capture and add real-data regression fixtures to tests (P3). (done)
9. Replace NBTLookup legacy Key/ValueString with KeyId/ValueId (done).
10. Extract NBT SQL filtering to helper (done).
11. Centralize NBT key/value ID resolver (done).
12. Replace IMemoryCache with Redis distributed cache for references (done).
8. Optional: frontend polish / no-SSR wrapper for `/flips` if hydration warnings persist.

## Completed Highlights
- Sold auction ingestion uses `SoldAt` + `SoldPrice`.
- Price aggregation uses sold-time bucketing and volume-weighted daily rollups.
- Anti-manipulation dedupe implemented in reference selection and aggregation.
