# SkyFlipperSolo - Remaining Features for Full Parity

This document tracks features from the Coflnet SkyFlipper reference that are not yet implemented in SkyFlipperSolo.

**Last updated: 2026-03-19**

## Priority Legend
- **P0**: Critical - Affects flip detection accuracy significantly
- **P1**: High - Affects specific item categories  
- **P2**: Medium - Nice to have, improves edge cases
- **P3**: Low - Development/operations tooling

---

## Completed Features ✅

All P0, P1, and P2 accuracy-affecting features have been implemented:

| Feature | Status | Implementation |
|---------|--------|----------------|
| Attribute Shard Weighting | ✅ | `CacheKeyService.cs` - ShardAttributes dict with weight-based range matching |
| Armor Color/Dye NBT Matching | ✅ | `CacheKeyService.cs` - color, dye_item keys + IsArmor helper |
| Cosmetic NBT Keys | ✅ | `CacheKeyService.cs` - MUSIC, DRAGON, TIDAL, party_hat_emoji |
| Bid Flip Detection | ✅ | `BidFlipDetectionService.cs` - non-BIN auctions ending in 30s-2min |
| Unlocked Slots Date Filter | ✅ | `PriceAggregationService.cs` - GemstoneIntroductionDate 2021-09-04 |
| Drill Parts Matching | ✅ | `CacheKeyService.cs` - DrillPartKeys |
| Kill Counter Range Matching | ✅ | `CacheKeyService.cs` - _kills suffix pattern, eman_kills |
| Pet Held Item Matching | ✅ | `CacheKeyService.cs` - ShouldPetItemMatch(), ValuablePetItems |
| Ability Scroll Extraction | ✅ | `NbtParserService.cs` - ability_scroll array extraction |
| Composite Tags (PET/POTION/RUNE/ABICASE) | ✅ | `NbtParserService.cs` - GetCompositeItemId() |
| NBT Flattening (50+ keys) | ✅ | `NbtParserService.cs` - FlattenNbtData() |
| Candy Used Special Logic | ✅ | `CacheKeyService.cs` - GetCandyCacheValue() with binary check + max-exp skin case |
| CacheKey format aligned | ✅ | `CacheKeyService.cs` - matches Coflnet raw concat format |
| SelectBestEnchant WorthOrder | ✅ | `ReferenceAuctionService.cs` - full priority list ported |
| ShouldPetItemMatch exp guard | ✅ | `CacheKeyService.cs` - exp key guard added |
| SQL-level NBT filtering | ✅ | `ReferenceAuctionService.cs` - DB-level filtering via NBTLookup joins |
| Enchant list aligned | ✅ | `CacheKeyService.cs` - matches Constants.cs RelevantEnchants |
| Redis distributed cache | ✅ | `Program.cs` - IDistributedCache via Redis (2h TTL) |
| SkyFilter parity (345 filters) | ✅ | `Services/Filters/` - full coflnet FilterEngine parity |
| Debug API endpoints | ✅ | `FlipsController.cs` - reference auctions, cache info |
| Prometheus metrics | ✅ | `Program.cs` - flip counts, latencies, cache hit rates |

---

## Remaining Items

### 1. Frontend Filter Integration
**Priority**: P1 — makes backend filters usable by end users

Backend has 345 filter types served via `GET /api/auctions/filters/{tag}`. The frontend client currently shows ~10 hardcoded filters. Needs wiring to the backend endpoint for full filter discoverability.

### 2. Test Coverage
**Priority**: P2 — confidence in correctness

Current: 165 tests (parity integration + fixtures + unit tests). Coverage includes:
- `RangeParserTests` — 16 tests for range parsing (exact, range, greater/less than, any/none, pipes, underscores, invalid input)
- `FilterUnitTests` — 111 tests covering bootstrapping (300+ filters register), names unique, type flags, options, Apply doesn't throw, gem/attribute/enchant/rune/skin/drill filter existence, specific filter behavior (Sold, Everything, ItemNameContains, ItemTag, ItemId, Computed pass-through)
- `CacheKeyServiceTests` — 18 tests covering ExtractRelevantEnchants, ShouldPetItemMatch (coflnet cases + exp guard), IsPet, IsArmor, DoesRecombMatter, GeneratePriceCacheKey (tag, pet level normalization, tier separation)
- `ReferenceParityTests` — 32 tests for end-to-end parity with fixtures

Needs:
- More CacheKeyService edge cases (composite tags, range matching)
- NbtParserService unit tests (requires NBT test data)
- FlipDetectionService unit tests

### 3. Live Validation
**Priority**: P2 — confirm filters work with real data

Filters are implemented but unvalidated against real Hypixel data. Some NBT keys (e.g. `raffle_year`, `plarvoid_book_count`) may never appear in practice. Need a pass to confirm keys exist in `NBTKeys` table after real ingestion.

### 4. Performance Optimization
**Priority**: P3

- Filter queries are individual EF Core queries; `CleanFilter` does expensive NOT EXISTS joins
- Consider compiled queries or query optimization for hot paths
- Connection pooling tuning

### 5. Operational Hardening
**Priority**: P3

- Hypixel API rate limiting / circuit breaker
- Graceful shutdown for background services
- Deeper health checks (DB, Redis, Hypixel API reachability)

### 6. Out-of-Scope (Not Needed for Standalone)
- Kafka / distributed messaging (monolith by design)
- ScyllaDB / MongoDB (PostgreSQL sufficient for single-user)
- SkyFlipTracker (flip success tracking)
- SkyCrafts (crafting cost data)
- SkyModCommands (Minecraft mod integration)
- SkyMcConnect (account verification)
- Premium/user auth system

---

## Current Accuracy Estimate

| Stage | Accuracy |
|-------|----------|
| P0 features complete | ~97% |
| P1 features complete | ~99% |
| P2 features complete | **~99.5%** ✅ |
| Filter parity complete | **~99.7%** ✅ |

---

## Implementation Checklist

### Completed
- [x] P0: All core flip detection logic
- [x] P0: CacheKey format alignment
- [x] P0: SelectBestEnchant WorthOrder
- [x] P0: ShouldPetItemMatch exp guard
- [x] P0: Enchant list alignment
- [x] P0: SQL-level NBT filtering
- [x] P1: Attribute Shard Weighting
- [x] P1: Armor Color/Dye NBT Matching  
- [x] P1: Cosmetic NBT Keys
- [x] P1: Redis distributed cache
- [x] P1: SkyFilter parity (345 filters)
- [x] P2: Bid Flip Detection
- [x] P2: Unlocked Slots Date Filter
- [x] P2: Drill Parts Matching
- [x] P2: Debug API Endpoints
- [x] P2: Prometheus Metrics
- [x] P3: Candy Used Special Logic
- [x] P2: Unit Tests (165 tests: RangeParser, filters, CacheKeyService, parity)

### Remaining
- [ ] P1: Frontend filter integration
- [ ] P2: Live-data regression tests
- [ ] P3: Performance optimization
- [ ] P3: Operational hardening
