# SkyFlipperSolo Full Ecosystem Parity Review

**Date:** 2026-03-19
**Reviewer:** Senior Code Review & Architecture Agent
**Scope:** Full parity audit of SkyFlipperSolo against the entire Coflnet SkyBlock ecosystem

---

## A. Workspace Inventory — Coflnet Root Folders

| Folder | Role | Relevance to SkyFlipperSolo |
|--------|------|-----------------------------|
| `dev/` | Core shared library: DB models (SaveAuction, NBT, HypixelContext), NBT parsing, Kafka, Redis, ItemDetails, Constants | **CRITICAL** — source of truth for all flip logic |
| `SkyFlipper/` | Flip detection engine: FlipperEngine.cs, Kafka consumer, reference auction selection | **CRITICAL** — primary algorithm reference |
| `SkyUpdater/` | Data ingestion from Hypixel API → Kafka topics | **HIGH** — SkyFlipperSolo merges this into AuctionFetcherService |
| `SkyIndexer/` | Kafka consumer → MariaDB indexer, lifecycle management | **HIGH** — SkyFlipperSolo merges this into FlipperService + DB layer |
| `SkyApi/` | REST API gateway with rate limiting, premium features | **HIGH** — endpoint contract reference |
| `SkyBackendForFrontend/` | Shared backend library: FlipperService, PricesService, SettingsService, SearchService | **MEDIUM** — API surface/contract reference |
| `SkyCommands/` | WebSocket command service for real-time flip subscriptions | **MEDIUM** — real-time protocol reference |
| `SkyFilter/` | 118+ filter implementations (FilterEngine, FilterDictonary) | **LOW-MEDIUM** — advanced filtering reference |
| `SkyFlipTracker/` | Tracks flip success rates, monitors which flips actually sold | **LOW** — feedback loop not replicated |
| `SkyItems/` | Item metadata service (names, tiers, categories) | **LOW** — partially covered by ItemDetailsService |
| `SkyCrafts/` | Crafting recipe data, forge costs | **LOW** — not relevant to flip detection |
| `SkyModCommands/` | Minecraft mod integration | **LOW** — not relevant |
| `SkyMcConnect/` | Account verification linking | **LOW** — not relevant |
| `hypixel-react/` | React frontend (Next.js 15, Material-UI, React Query) | **HIGH** — UI/UX pattern reference |

### Infrastructure (from root `docker-compose.yml`)

The Coflnet stack uses: MariaDB + Redis + Kafka + Zookeeper + ScyllaDB + MongoDB + MinIO + Jaeger.

SkyFlipperSolo uses: PostgreSQL only + in-memory cache + SignalR. No Kafka, no Redis, no ScyllaDB.

---

## B. Executive Summary

### How close is SkyFlipperSolo to the full Coflnet stack?

SkyFlipperSolo is a **monolithic, single-process rewrite** of the Coflnet flip-detection pipeline. It consolidates ~10 microservices into one .NET process + one Next.js frontend. The core flip-detection algorithm has been ported with high fidelity (reference-auction selection, anti-manipulation, weighted median, special-item matching).

**Estimated overall parity: ~85%**

The biggest strengths are in the flip-detection engine itself — the ReferenceAuctionService, CacheKeyService, and NbtParserService closely mirror the Coflnet reference. The biggest gaps are in **infrastructure, real-time architecture, advanced API features, and operational tooling**.

### Biggest Parity Gaps

1. **No Kafka/message-bus architecture** — Coflnet uses Kafka for decoupled event streaming; SkyFlipperSolo uses in-process Channels
2. **No Redis caching layer** — Coflnet caches reference sets in Redis; SkyFlipperSolo uses IMemoryCache (lost on restart)
3. **No Prometheus metrics** — Coflnet has extensive operational metrics; SkyFlipperSolo has zero
4. **No debug API endpoints** — Coflnet exposes `/flip/{uuid}/based`, `/flip/{uuid}/cache` for debugging
5. **Limited advanced filtering** — Coflnet has 118+ filter types; SkyFlipperSolo has basic stars/rarity/reforge/enchantment
6. **No flip success tracking** — SkyFlipTracker monitors actual sale outcomes; SkyFlipperSolo doesn't
7. **No premium/user system** — Coflnet has subscription tiers and user auth; SkyFlipperSolo is open
8. **Enchant list divergence** — Constants.cs has ~50 relevant enchants vs SkyFlipperSolo's ~45; some are missing
9. **No rate limiting on Hypixel API calls** — Coflnet has distributed rate control; SkyFlipperSolo polls independently
10. **CacheKey format is subtly different** — Reference uses `String.Concat(Dictionary)` which produces `[key, value]` pairs; SkyFlipperSolo normalizes with sorted keys and range bucketing

### Biggest Correctness Risks

1. **CacheKey divergence** — The reference `GetCacheKey()` concatenates `FlatenedNBT` directly via `String.Concat(auction.FlatenedNBT.Where(...))` which produces `[key1, value1][key2, value2]` format. SkyFlipperSolo's `BuildNbtString()` does the same format BUT sorts keys alphabetically and applies range normalization. While this improves matching, it can cause the key to **diverge from Coflnet's exact key**, making pre-aggregated AveragePrice data from Coflnet incompatible.
2. **`ShouldPetItemMatch` difference** — Reference checks `flatNbt.ContainsKey("exp")` as a guard; SkyFlipperSolo doesn't, allowing pet items to match even without exp data.
3. **`SelectBestEnchant` ordering** — Reference uses a hand-coded `WorthOrder`/`WorthOrderLevels` priority list (100+ entries); SkyFlipperSolo uses `OrderByDescending(Level).ThenBy(Type)`, which can pick a different "best" enchant.
4. **Reference timestamp logic** — Reference uses `a.End` for all references; SkyFlipperSolo uses `SoldAt` for sold and `End` for active. This is actually an improvement for sold-auction accuracy but differs from reference behavior.
5. **Hit count persistence** — Coflnet uses Redis with TTL; SkyFlipperSolo uses DB rows with manual cleanup. Risk of stale hit counts if cleanup fails.

### Biggest Architectural Mismatch

| Aspect | Coflnet | SkyFlipperSolo |
|--------|---------|----------------|
| Data flow | Kafka topic → multiple consumers | Channel → single consumer |
| Reference cache | Redis (shared, persistent) | IMemoryCache (per-process, ephemeral) |
| Ingestion | Dedicated SkyUpdater microservice | AuctionFetcherService (in-process) |
| Indexing | Dedicated SkyIndexer microservice | FlipperService (in-process) |
| Flip detection | Dedicated SkyFlipper service | FlipDetectionService (in-process) |
| Real-time | WebSocket (SkyCommands) | SignalR (in-process) |
| DB | MariaDB | PostgreSQL |
| Monitoring | Prometheus + Jaeger | None |

---

## C. Gap List

### P0 — Critical (affects flip accuracy or core functionality)

| # | Title | Category | Impact | Our File | Reference File |
|---|-------|----------|--------|----------|----------------|
| 1 | Enchant list divergence from Constants.cs | incorrect behavior | Missing enchants: `critical(7)`, `angler(7)`, `spiked_hook(7)`, `caster(7)`, `magnet(7)`, `luck_of_the_sea(7)`, `thunderlord(7)`, `lethality(7)`, `thunderbolt(8)`, `infinite_quiver(11)`, `feather_falling(11)`, `ultimate_wise(4)`, `smoldering(1)`, `strong_mana(5)`, `hardened_mana(5)`, `mana_vampire(4)`, `ferocious_mana(2)`, `charm(4)`, `cayenne(5)`, `green_thumb(1)`, `prosperity(1)`, `tabasco(3)`, `fire_aspect(3)`, `pesterminator(1)`, `ultimate_refrigerate(1)`, `paleontologist(1)`, `ice_cold(1)`, `toxophilite(1)`, `lapidary(2)`, `replenish(1)`, `quick_bite(1)`, `absorb(1)`, `forest_pledge(4)`, `raspiration(4)`, `scuba(3)`, `delicate(5)`, `quantum(5)`, `small_brain(5)` | `Services/CacheKeyService.cs:RelevantEnchants` | `dev/Data/Flipper/Constants.cs:RelevantEnchants` |
| 2 | `SelectBestEnchant` uses wrong priority ordering | incorrect behavior | Could select wrong "best" enchant in reduced-mode matching, causing reference set divergence | `Services/ReferenceAuctionService.cs:SelectBestEnchant` | `dev/Data/Flipper/Constants.cs:SelectBest + WorthOrder` |
| 3 | `ShouldPetItemMatch` missing exp guard | incorrect behavior | Pet items could match even when exp data is missing from flatNBT | `Services/CacheKeyService.cs:ShouldPetItemMatch` | `SkyFlipper/Flipper/FlippingEngine.cs:ShouldPetItemMatch` |
| 4 | CacheKey format divergence from Coflnet | contract mismatch | Cache keys sort NBT keys alphabetically + apply range normalization. Coflnet does NOT sort and uses raw values. This means pre-existing Coflnet AveragePrice data won't match SkyFlipperSolo keys. | `Services/CacheKeyService.cs:BuildNbtString` | `SkyFlipper/Flipper/FlippingEngine.cs:GetCacheKey` |
| 5 | Reference `GetSelect` doesn't use DB-level NBT filtering | incorrect behavior | Reference uses `AddNBTSelect`/`AddNbtRangeSelect` to filter at SQL level via NBTLookup joins. SkyFlipperSolo fetches rough candidates then filters in-memory. Much slower with large datasets. | `Services/ReferenceAuctionService.cs:GetSelect` | `SkyFlipper/Flipper/FlippingEngine.cs:GetSelect` |

### P1 — High (affects specific item categories or operational correctness)

| # | Title | Category | Impact | Our File | Reference File |
|---|-------|----------|--------|----------|----------------|
| 6 | No Prometheus metrics | operational/runtime gap | Cannot monitor flip rates, latencies, cache hit rates in production | `Program.cs` | `SkyFlipper/Flipper/FlippingEngine.cs:41-56` |
| 7 | No debug API endpoints | operational/runtime gap | Cannot inspect what references were used for a flip, cache key contents, or invalidate cache | `Controllers/FlipsController.cs` | `ApiController.cs` (Coflnet) |
| 8 | IMemoryCache vs Redis for reference sets | performance/scaling risk | Reference sets lost on restart; not shared across instances; limited by process memory | `Services/ReferenceAuctionService.cs` | `SkyFlipper/Flipper/FlippingEngine.cs:GetRelevantAuctionsCache` |
| 9 | No Hypixel API rate limiting coordination | operational/runtime gap | Multiple independent background services poll the same API without coordination | `Services/AuctionFetcherService.cs` + `Services/SoldAuctionService.cs` | `SkyUpdater/` |
| 10 | HitCount CacheKey column `text` but AveragePrice.CacheKey still `varchar(200)` | schema mismatch | Long cache keys from range normalization could truncate in AveragePrice table | `Models/AveragePrice.cs` | — |
| 11 | Missing `VeryValuableEnchant` dictionary | missing microservice feature | Reference has a separate dict for enchants worth 20M+ used in premium features; not replicated | — | `dev/Data/Flipper/Constants.cs:VeryValuableEnchant` |
| 12 | No ShardNames dictionary | missing microservice feature | Reference maps shard display names to tag names for attribute shard pricing | — | `dev/Data/Flipper/Constants.cs:ShardNames` |
| 13 | `DoesRecombMatter` has wrong endings list | incorrect behavior | Reference has `INFINI_VACUUM`, `POWER_ORB`, `GRIFFIN_UPGRADE_STONE_EPIC`; SkyFlipperSolo has these but different C# array syntax vs reference | `Services/CacheKeyService.cs:DoesRecombMatter` | `dev/Data/Flipper/Constants.cs:DoesRecombMatter` |
| 14 | `AveragePrice.CacheKey` max length 200 may truncate | schema mismatch | Range-normalized keys with many NBT entries can exceed 200 chars | `Models/AveragePrice.cs` | — |

### P2 — Medium (edge cases, nice-to-haves)

| # | Title | Category | Impact | Our File | Reference File |
|---|-------|----------|--------|----------|----------------|
| 15 | No flip success tracking (SkyFlipTracker) | missing microservice feature | Cannot validate whether detected flips actually sold at predicted price | — | `SkyFlipTracker/` |
| 16 | No advanced filtering (118+ types from SkyFilter) | missing microservice feature | Users can't filter by gems, pet level ranges, custom NBT properties | `Controllers/AuctionsController.cs:ApplyFilters` | `SkyFilter/Core/FilterEngine.cs` |
| 17 | No WebSocket-based real-time (SkyCommands protocol) | missing microservice feature | Coflnet mod connects via WebSocket; SkyFlipperSolo only has SignalR | `Hubs/FlipHub.cs` | `SkyCommands/Socket/Server.cs` |
| 18 | No item metadata enrichment from SkyItems | missing microservice feature | Item category, NPC price, bazaar price not available | `Services/ItemDetailsService.cs` | `SkyItems/` |
| 19 | No crafting cost data (SkyCrafts) | missing microservice feature | Cannot factor crafting costs into flip valuation | — | `SkyCrafts/` |
| 20 | Frontend uses React Bootstrap, reference uses Material-UI | contract mismatch | UI component patterns diverge from Coflnet frontend | `client/` | `hypixel-react/` |
| 21 | Price history API shape differs from Coflnet | contract mismatch | Coflnet returns `{ prices: [{timestamp, min, max, avg, volume}] }`; SkyFlipperSolo wraps in `{ filterable, bazaar, filters, prices }` — actually aligned now | `Controllers/AuctionsController.cs` | `SkyApi/Controllers/PricesController.cs` |
| 22 | No player name caching from Coflnet PlayerName API | data-flow mismatch | SkyFlipperSolo calls Hypixel API directly per-request; Coflnet uses a dedicated PlayerName service | `Controllers/AuctionsController.cs:GetPlayerName` | `dev/` PlayerName client |
| 23 | No ScyllaDB/Redis for high-volume data | performance/scaling risk | All data in PostgreSQL; may not scale past ~100k auctions/day efficiently | `Data/AppDbContext.cs` | `docker-compose.yml` |
| 24 | Back-forth trading detection has counter bug | incorrect behavior | `counter` starts at 1 and increments in UID dedup, but `counter > 2` check after UID dedup may not correctly detect UID-less items | `Services/ReferenceAuctionService.cs:ApplyAntiMarketManipulation` | `SkyFlipper/Flipper/FlippingEngine.cs:ApplyAntiMarketManipulation` |

---

## D. Focus-Area Scorecard (0–100)

| Area | Score | Notes |
|------|-------|-------|
| **Data Ingestion & Lifecycle** | 85 | HTTP polling replaces Kafka; upsert logic for active auctions; sold tracking via auctions_ended API; lifecycle cleanup. Missing: Kafka durability, distributed ingestion. |
| **Auction Fetching/Deduping/Lifecycle Transitions** | 80 | Cycle-scoped dedup is good; sold-at timestamp handling correct; lifecycle integrity checks in place. Missing: distributed dedup coordination. |
| **NBT Parsing & Normalization** | 90 | Comprehensive FlattenNbtData with 50+ keys; composite tag generation for pets/potions/runes/abicase; proper gem/attribute/rune extraction. Minor differences in edge cases. |
| **Cache Key Generation / Price Matching** | 75 | Format diverges from reference (sorted keys + range normalization vs raw concat); enchant list incomplete; SelectBestEnchant ordering wrong. |
| **Price History Aggregation** | 85 | 15-min/hourly/daily granularity; anti-manipulation before aggregation; gem value subtraction; volume-weighted daily rollups. Missing: distributed aggregation. |
| **Flip Detection Accuracy** | 88 | Reference-auction valuation ported; anti-manipulation; weighted median; hit count decay; BIN halving; count penalty. Enchant list gaps reduce accuracy for some items. |
| **API Parity & Data Contracts** | 80 | Most REST endpoints covered; price history wrapped response; flip DTO aliases; lowest BIN endpoint. Missing: debug endpoints, advanced filtering, rate limiting. |
| **Frontend Parity / Data Dependencies** | 75 | SignalR live flips; price history charts; auction detail page. Missing: advanced filtering UI, item comparison, bazaar data, crafting costs. |
| **Data Retention & Performance** | 70 | 30-day auction retention; 7-day hourly retention; 2-day 15-min retention. No Redis, no ScyllaDB, no distributed cache. PostgreSQL-only may struggle at scale. |
| **Microservice Architecture Parity** | 30 | Intentionally monolithic. Covers: Updater + Indexer + Flipper + API + Frontend in one process. Missing: Kafka, Redis, ScyllaDB, WebSocket commands, filter engine, flip tracker, items service, crafts service, mod commands. |

---

## E. Microservice Responsibilities Matrix

| Coflnet Project | What It Does | SkyFlipperSolo Coverage | Status |
|-----------------|-------------|------------------------|--------|
| **dev/** | Core models, NBT, DB context, Constants | Auction.cs, AppDbContext.cs, CacheKeyService.cs, NbtParserService.cs | **Partial** — models ported, Constants incomplete (enchant list gaps) |
| **SkyFlipper/** | Flip detection engine | ReferenceAuctionService.cs, FlipDetectionService.cs, BidFlipDetectionService.cs | **Strong** — algorithm ported with high fidelity |
| **SkyUpdater/** | Hypixel API → Kafka ingestion | AuctionFetcherService.cs, SoldAuctionService.cs | **Partial** — HTTP polling replaces Kafka; no distributed coordination |
| **SkyIndexer/** | Kafka → DB indexing | FlipperService.cs (combined with parsing) | **Partial** — combined into monolith |
| **SkyApi/** | REST API | AuctionsController.cs, FlipsController.cs | **Partial** — core endpoints, missing debug/rate-limit/filter endpoints |
| **SkyBackendForFrontend/** | Shared backend logic | Embedded in services | **Partial** — logic duplicated inline |
| **SkyCommands/** | WebSocket real-time | FlipHub.cs (SignalR) | **Partial** — different protocol (SignalR vs WebSocket commands) |
| **SkyFilter/** | 118+ filters | AuctionsController.cs:ApplyFilters (basic) | **Minimal** — only rarity/reforge/bin/name/pet filters |
| **SkyFlipTracker/** | Flip success tracking | — | **Missing** |
| **SkyItems/** | Item metadata | ItemDetailsService.cs | **Partial** — basic tag/name/tier tracking |
| **SkyCrafts/** | Crafting recipes/costs | — | **Missing** |
| **SkyModCommands/** | Minecraft mod | — | **Missing** (not needed for web deployment) |
| **SkyMcConnect/** | Account verification | — | **Missing** (not needed for standalone) |
| **hypixel-react/** | React frontend | client/ (Next.js + React Bootstrap) | **Partial** — basic pages, missing advanced features |

---

## F. Code-Verified Parity Notes

### Already Strong Parity

1. **Reference auction selection windowing** — `ReferenceAuctionService.cs` correctly implements the 2h → 1.5d → 8d expansion with reduced-mode fallback. Matches `FlippingEngine.cs:GetRelevantAuctions` closely.

2. **Anti-market manipulation** — Both seller dedup (lowest price), buyer dedup (first per buyer), UID dedup, and back-forth trading detection are implemented. The logic flow matches reference.

3. **Weighted median** — `GetWeightedMedianAsync` implements full-time median (skip n/2) and short-term median (top 3, skip 1), with the 25th-percentile override for high-volume items (>10 refs, all <20h old).

4. **Special item NBT matching** — Midas (winning_bid + additional_coins range), Final Destination (eman_kills range), drill parts, cake soul (captured_player), ability scrolls, armor color/dye, cosmetic keys — all present and correct.

5. **Pet matching** — Pet level normalization (last digit → underscore), held item matching with valuable-items list, candy binary check with max-exp-skin exception — all ported.

6. **BIN halving** — When non-BIN refs outnumber BIN refs 2:1, median is halved. Correctly implemented.

7. **Hit count decay** — `Math.Pow(1.05, hitCount)` reduces recommended buy-under. Matches reference.

8. **Count penalty** — Stack count > 1 applies 0.9 multiplier to target price. Matches reference.

9. **Component/gem value** — `ComponentValueService` handles gemstone value separately, subtracted from reference prices. Correct approach.

10. **Sold auction handling** — `SoldAuctionService` fetches from `auctions_ended`, extracts real timestamps, copies `SoldPrice` into `HighestBidAmount`, creates winning bid records. Strong parity.

### Only Approximate / Divergent

1. **CacheKey generation** — The format `[key, value]` is matched, but keys are sorted alphabetically and range-normalized. Reference does NOT sort and uses raw values. This means SkyFlipperSolo keys will differ from Coflnet keys for the same auction.

2. **Enchant relevance** — ~35 enchants from `Constants.cs:RelevantEnchants` are missing from `CacheKeyService.cs:RelevantEnchants`. Items with these enchants will not be properly matched in reference selection.

3. **`SelectBestEnchant`** — Reference uses a 100+ entry priority list (`WorthOrder`/`WorthOrderLevels`). SkyFlipperSolo uses simple `OrderByDescending(Level).ThenBy(Type)`. Different "best" enchant selection in reduced mode.

4. **NBT filtering in SQL** — Reference builds IQueryable with `AddNBTSelect`/`AddNbtRangeSelect` that filter at database level via NBTLookup joins. SkyFlipperSolo fetches 8x the rough limit and filters in-memory. Much less efficient at scale.

5. **Reference caching** — Reference uses Redis with 2h TTL + refresh logic. SkyFlipperSolo uses `IMemoryCache` with 2h TTL. Lost on restart, not shared.

6. **`DoesRecombMatter` endings** — Reference has exact array `["CLOAK","NECKLACE","BELT","GLOVES","BRACELET","HOE","PICKAXE","GAUNTLET","WAND","ROD","DRILL","INFINI_VACUUM","POWER_ORB","GRIFFIN_UPGRADE_STONE_EPIC"]`. SkyFlipperSolo has the same items but uses LINQ `Any(e => tag.Contains(e))` which could false-positive on partial tag matches.

---

## G. Recommended Action Plan

### High Value / Low Effort

1. **Complete the enchant list** — Copy all entries from `Constants.cs:RelevantEnchants` into `CacheKeyService.cs:RelevantEnchants`. ~35 missing entries.
   - File: `Services/CacheKeyService.cs`
   - Reference: `dev/Data/Flipper/Constants.cs`

2. **Fix `ShouldPetItemMatch` guard** — Add `flatNbt.ContainsKey("exp")` check matching reference.
   - File: `Services/CacheKeyService.cs:ShouldPetItemMatch`

3. **Widen `AveragePrice.CacheKey` to `text`** — Match the `FlipHitCount.CacheKey` migration.
   - File: `Models/AveragePrice.cs`
   - Create migration

4. **Add Prometheus metrics** — Add `prometheus-net.AspNetCore` and instrument FlipDetectionService.
   - File: `Program.cs`, `Services/FlipDetectionService.cs`

5. **Add debug API endpoints** — Expose reference-auction data, cache key contents, cache invalidation.
   - File: `Controllers/FlipsController.cs`

### High Value / Medium Effort

6. **Port `SelectBestEnchant` with WorthOrder** — Copy the 100+ entry priority list from Constants.cs.
   - File: `Services/ReferenceAuctionService.cs:SelectBestEnchant`
   - Reference: `dev/Data/Flipper/Constants.cs:WorthOrder/WorthOrderLevels`

7. **Move NBT filtering to SQL level** — Rewrite `GetSelect` to build IQueryable with NBTLookup joins instead of in-memory filtering.
   - File: `Services/ReferenceAuctionService.cs:GetSelect`
   - Reference: `SkyFlipper/Flipper/FlippingEngine.cs:GetSelect`

8. **Align CacheKey format with Coflnet** — Stop sorting NBT keys, use raw Dictionary iteration order. This is critical for cross-compatibility.
   - File: `Services/CacheKeyService.cs:BuildNbtString`

9. **Add Redis for reference caching** — Replace IMemoryCache with IDistributedCache backed by Redis. Enables persistence across restarts and multi-instance sharing.
   - File: `Services/ReferenceAuctionService.cs`
   - File: `Program.cs`

10. **Add basic advanced filtering** — Port the most common SkyFilter types (stars range, enchant filter, price range) to ApplyFilters.
    - File: `Controllers/AuctionsController.cs:ApplyFilters`
    - Reference: `SkyFilter/Filters/`

### Major Architectural Follow-ups

11. **Kafka integration** — For production deployment, decouple ingestion from processing via Kafka topics. This enables horizontal scaling and fault tolerance.

12. **ScyllaDB for flip tracking** — Port SkyFlipTracker's approach to track flip outcomes and feed back into accuracy improvements.

13. **Distributed rate limiting** — Coordinate Hypixel API calls across services to avoid IP bans.

14. **WebSocket command protocol** — Implement SkyCommands-compatible WebSocket protocol for Minecraft mod compatibility.

### Docs/Context Follow-ups

15. **Update REMAINING_FEATURES.md** — The file claims all P0/P1/P2 features are done; this review found P0 gaps (enchant list, CacheKey format, SQL filtering).

16. **Update README.md** — Reflect the actual parity state and architectural differences from Coflnet.

---

## H. Known Blockers

1. **CacheKey format incompatibility** — If you ever need to import Coflnet's AveragePrice data, the sorted-key + range-normalized format will produce different keys. This is a fundamental compatibility blocker.

2. **SQL filtering performance** — The in-memory filtering approach in `GetSelect` will degrade with large reference sets (>100k sold auctions). The reference uses SQL-level NBT filtering for this reason.

3. **No persistent reference cache** — IMemoryCache loses all cached reference sets on restart. A cold start requires re-querying all references from DB, which is slow.

4. **Enchant accuracy gap** — Items with missing enchants from the list (e.g., `smoldering`, `quantum`, `replenish`) will not be properly matched, causing false-positive or missed flips.

5. **No operational monitoring** — Without Prometheus metrics, production debugging of flip detection issues is extremely difficult.

---

## Appendix: Files Inspected

### SkyFlipperSolo (Our Project)
- `README.md`
- `PARITY_SESSION_REPORT.md`
- `REMAINING_FEATURES.md`
- `.cursor/rules/error-documentation.mdc`
- `.cursor/rules/lessons-learned.mdc`
- `Program.cs`
- `Data/AppDbContext.cs`
- `Services/ReferenceAuctionService.cs`
- `Services/FlipDetectionService.cs`
- `Services/BidFlipDetectionService.cs`
- `Services/CacheKeyService.cs`
- `Services/NbtParserService.cs`
- `Services/PriceAggregationService.cs`
- `Services/AuctionFetcherService.cs`
- `Services/FlipperService.cs`
- `Services/SoldAuctionService.cs`
- `Services/AuctionLifecycleService.cs`
- `Controllers/AuctionsController.cs`
- `Controllers/FlipsController.cs`
- `Models/Auction.cs`
- `Models/AveragePrice.cs`
- `client/api/ApiHelper.ts`
- `client/app/flips/page.tsx`
- `SkyFlipperSolo.Tests/ReferenceParityTests.cs`

### Coflnet Reference (Root Workspace)
- `dev/Data/NBT.cs` (1277 lines)
- `dev/Data/Flipper/Constants.cs` (552 lines)
- `SkyFlipper/Flipper/FlippingEngine.cs` (1039 lines)
- `docker-compose.yml`
- All 14 project directories (classified via exploration)

---

*Report generated 2026-03-19. This is a living document; update as parity work progresses.*
