# SkyFlipperSolo Parity Session Report

This document summarizes the major parity work completed on `feature/auction-detail-ui` while aligning SkyFlipperSolo with Coflnet/SkyFlipper behavior, contracts, and runtime characteristics.

It covers:

- the commit history produced during this parity push
- the architectural changes behind those commits
- the parity goals each change was meant to address
- the current known blockers and follow-up areas

## Scope

The work in this session focused on:

- auction ingestion and lifecycle correctness
- sold auction handling and reference usability
- NBT lookup/schema parity
- API/data-contract parity with Coflnet-style frontend expectations
- reference-auction-based flip valuation instead of aggregate-only pricing
- parity verification via automated tests
- real-data runtime validation and bug fixes
- frontend synchronization with backend changes

## High-Level Outcome

SkyFlipperSolo moved from a mostly aggregate-driven flipper into a much closer Coflnet-style private deployment with:

- live reference-auction matching
- Coflnet-style anti-manipulation
- weighted median valuation
- special-case NBT matching for many high-impact item classes
- tighter lifecycle correctness for active and sold auctions
- significantly better frontend/backend contract alignment
- automated parity coverage over the translated engine

## Commit Timeline

### 1. Initial ingestion and detail parity

#### `a6c1d15` `feat: improve auction detail parity and ingestion fixes`

Main outcomes:

- enabled non-BIN ingestion so bid-flip logic could operate on real non-BIN auctions
- fixed sold timestamp handling to use Hypixel ended timestamps instead of `DateTime.UtcNow`
- corrected compile/runtime issues around auction detail/controller contract mismatches
- improved auction detail parity groundwork

Parity impact:

- fixed a core `P0` blocker where non-BIN flips were impossible because non-BIN auctions were being skipped
- corrected sold time windows for history and pricing logic

#### `a8130e0` `fix: align history contracts and auction refresh parity`

Main outcomes:

- active auctions are refreshed/upserted instead of treated as insert-only
- existing auctions now get mutable fields and bids refreshed
- `NBTLookup` uniqueness parity work added to schema/model path
- price-history endpoints began returning a Coflnet-style wrapped contract

Parity impact:

- addressed the major ingestion/lifecycle gap where existing auctions were becoming stale
- started aligning backend responses with frontend/Coflnet expectations

### 2. Frontend contract alignment

#### `19f0b8d` `fix: update frontend for price history contract`

Main outcomes:

- frontend `getItemPrices()` adapted to wrapped history responses
- shared history types updated

Parity impact:

- removed a backend/frontend mismatch introduced by contract normalization

#### `547f77c` `feat: align auction preview payloads`

Main outcomes:

- active/sold auction previews now expose Coflnet-style aliases like `uuid`, `price`, `end`, `seller`, `playerName`
- existing UI-friendly fields were preserved

Parity impact:

- improved frontend compatibility without breaking current consumers

#### `b0c55d8` `feat: align lowest bin and flip payloads`

Main outcomes:

- lowest BIN response moved to a more stable typed shape
- flip DTOs now expose compatibility aliases like `uuid`, `price`, `targetPrice`, `profit`, `profitPercent`, `end`

Parity impact:

- reduced contract drift for common frontend integrations

### 3. Database/schema correctness and robustness

#### `ce92cf8` `chore: add nbt lookup uniqueness migration`

Main outcomes:

- added the migration that enforces one `NBTLookup` row per `AuctionId + KeyId`

Parity impact:

- brought the NBT lookup table closer to the reference schema
- removed duplicate-row risks that could skew filtering and key generation

#### `065d291` `fix: scope auction dedupe to fetch cycle`

Main outcomes:

- dedupe tracking in fetch flow was reduced from a long-lived in-memory cache to a fetch-cycle scope

Parity impact:

- improved ingestion correctness and reduced restart/memory-related dedupe artifacts

#### `2520607` `fix: widen flip hit count cache key`

Main outcomes:

- widened `FlipHitCounts.CacheKey` from `varchar(200)` to `text`
- added and applied EF migration `20260319001638_WidenFlipHitCountCacheKey`

Parity impact:

- prevented live parity cache keys from crashing bid-flip detection
- preserved exact cache key semantics instead of truncating them

### 4. NBT handling and aggregation correctness

#### `b19771f` `fix: harden nbt parsing null handling`

Main outcomes:

- added defensive null handling in NBT parsing and property selection
- removed brittle assumptions around missing or malformed nodes

Parity impact:

- improved runtime reliability on real Hypixel edge-case data

#### `1a6f33f` `fix: align price aggregation semantics`

Main outcomes:

- hourly/15m aggregation now keeps real `ItemTag` alongside cache key
- daily rollups use volume-weighted calculations instead of flat averaging of hourly medians
- history queries now prefer exact `ItemTag` matching and only fall back to cache-key prefix compatibility

Parity impact:

- moved history semantics closer to Coflnet’s item/time-bucketed `AveragePrice` behavior
- reduced distortion from naive rollups

### 5. Port of the Coflnet-style flip engine

#### `9f3365a` `feat: port cofllnet reference flip engine`

Main outcomes:

- added [ReferenceAuctionService.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Services/ReferenceAuctionService.cs)
- flips now use dynamic reference-auction selection instead of relying primarily on aggregate tables
- ported:
  - reference caching behavior
  - dynamic widening/narrowing reference windows
  - item-specific selection logic
  - anti-manipulation filtering
  - weighted median valuation

Services switched over:

- [FlipDetectionService.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Services/FlipDetectionService.cs)
- [BidFlipDetectionService.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Services/BidFlipDetectionService.cs)

Parity impact:

- this was the largest architectural parity jump of the session
- replaced the biggest mismatch between SkyFlipperSolo and Coflnet: aggregate-first valuation

### 6. Automated parity harness and test coverage

#### `9dce9a0` `test: add flip parity harness`

Main outcomes:

- introduced `SkyFlipperSolo.Tests`
- added baseline parity tests for translated reference logic

Parity impact:

- created a repeatable way to check whether our engine behaves like Coflnet for concrete item cases

#### `a399dc2` `test: tighten reference parity coverage`

Main outcomes:

- debugged the first failing selection case
- improved visibility into reference selection stages
- clarified that one perceived mismatch was actually expected fallback behavior

Parity impact:

- reduced uncertainty in the translated selection pipeline

#### `b2b098b` `test: cover pet and midas parity cases`

Covered:

- valuable pet item matching
- Midas-specific range semantics

#### `758e437` `test: cover final destination and dyed armor parity`

Covered:

- `eman_kills` / Final Destination range matching
- dyed armor `color` and `dye_item`

#### `16dbc28` `test: cover drill and special nbt parity`

Covered:

- drill parts
- ability scrolls
- attribute gear
- cake soul `captured_player`

#### `b4b07e3` `test: cover anti manipulation parity`

Covered:

- seller dedupe
- buyer dedupe
- UID dedupe
- back-and-forth trading removal

#### `59f5ce7` `test: cover flip valuation parity`

Covered:

- hit-count decay
- auction-heavy BIN halving behavior
- low-value extra margin logic
- stack-count penalty

Parity impact across test commits:

- matcher/select logic is now covered for many high-impact Coflnet item classes
- anti-manipulation and valuation rules are verified by automated tests instead of assumption

### 7. Real-data runtime fixes

#### `2a2775e` `fix: unlock real-data reference valuation`

Main outcomes:

- sold auctions now copy final sale price into `HighestBidAmount`
- sold auctions now generate usable winning-bid records from ended data when possible
- sold reference selection now uses `SoldAt` instead of original `End`
- aggregation buckets sold auctions using `SoldAt`
- added live debug endpoints in [TestController.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Controllers/TestController.cs)

Parity impact:

- synthetic tests were passing before this, but live data showed sold auctions were still unusable as references
- this commit made real sold references actually participate in valuation

### 8. Live flips backend/frontend synchronization

#### `617662f` `fix: sync live flips with backend state`

Main outcomes:

- stale ghost flips no longer linger when backend flips go to zero
- seller and reference volume are wired into flip payloads
- `ReferenceCount` persisted for flip opportunities

Parity impact:

- made the live flips page reflect actual backend truth instead of stale client state

#### `2b6ac12` `fix: align frontend with live backend contracts`

Main outcomes:

- frontend now fetches current flips on load
- history request code became more robust to wrapped responses

Parity impact:

- reduced backend/frontend drift for live operation

#### `dba2518` `feat: keep sold flips visible in live feed`

Main outcomes:

- flips are not removed immediately when auction status changes
- cards are marked `SOLD` / `EXPIRED`

Parity impact:

- improved operational usability of the live feed

#### `8f406c8` `fix: persist seen flips in live feed`

Main outcomes:

- once a flip appears, reevaluation alone does not make it vanish from the page
- cards remain available for copy/snipe workflow

Parity impact:

- improved practical usability of the flipper while keeping lifecycle information

### 9. Frontend stability and hydration/history safety

#### `2ec414b` `fix: stabilize frontend history and hydration`

Main outcomes:

- `ApiHelper` hardened for wrapped/bare-array history responses
- `PriceHistoryChart` made more defensive against malformed history payloads
- flips page hydration safety improved for browser-dependent state

Parity impact:

- ensured frontend keeps functioning with the normalized backend contracts
- reduced runtime crashes and hydration mismatch risks

## Major Parity Changes By Area

## Data Ingestion and Lifecycle

Completed:

- non-BIN auctions are now ingested
- active auctions are refreshed instead of insert-only
- sold auctions use real ended timestamps
- sold auctions now preserve a usable final value for valuation/reference logic
- fetch-cycle dedupe is less fragile

Files most impacted:

- [AuctionFetcherService.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Services/AuctionFetcherService.cs)
- [FlipperService.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Services/FlipperService.cs)
- [SoldAuctionService.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Services/SoldAuctionService.cs)
- [AuctionLifecycleService.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Services/AuctionLifecycleService.cs)

## NBT Parsing and Normalization

Completed:

- schema tightened for `NBTLookup`
- null-handling hardened in parser and property selection
- matching coverage expanded across major special-case NBT item types

Files most impacted:

- [NbtParserService.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Services/NbtParserService.cs)
- [PropertiesSelectorService.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Services/PropertiesSelectorService.cs)
- [NBTLookup.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Models/NBTLookup.cs)
- [AppDbContext.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Data/AppDbContext.cs)

## Cache Key Generation and Price Matching

Completed:

- parity cache keys now flow through the full reference-auction engine
- long cache keys no longer break hit-count persistence
- reference matching now depends on selection logic, not only broad cache-key aggregates

Files most impacted:

- [CacheKeyService.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Services/CacheKeyService.cs)
- [ReferenceAuctionService.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Services/ReferenceAuctionService.cs)
- [FlipHitCount.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Models/FlipHitCount.cs)

## Price History Aggregation

Completed:

- item-tag-aware rollups
- volume-weighted daily semantics
- sold-at-based bucketing for sold auctions
- frontend contract normalization

Files most impacted:

- [PriceAggregationService.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Services/PriceAggregationService.cs)
- [AveragePrice.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Models/AveragePrice.cs)
- [AuctionsController.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Controllers/AuctionsController.cs)

## Flip Detection Accuracy

Completed:

- BIN and bid flips now use reference-auction valuation
- anti-manipulation and weighted median are part of live runtime
- hit-count decay is covered by tests
- special-item matching has broad parity coverage

Files most impacted:

- [ReferenceAuctionService.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Services/ReferenceAuctionService.cs)
- [FlipDetectionService.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Services/FlipDetectionService.cs)
- [BidFlipDetectionService.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Services/BidFlipDetectionService.cs)

## API Parity and Data Contracts

Completed:

- wrapped price-history responses
- better lowest-BIN contract
- better flip payload aliases
- auction preview contract alignment

Files most impacted:

- [AuctionsController.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Controllers/AuctionsController.cs)
- [FlipsController.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Controllers/FlipsController.cs)
- [ApiHelper.ts](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/client/api/ApiHelper.ts)

## Frontend Parity and Operational UX

Completed:

- frontend now consumes wrapped history data
- flips page syncs with backend state on load and through SignalR
- sold/expired flips remain visible
- seen flips persist instead of disappearing on reevaluation
- hydration/history robustness improved

Files most impacted:

- [page.tsx](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/client/app/flips/page.tsx)
- [PriceHistoryChart.tsx](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/client/components/PriceHistoryChart.tsx)
- [ApiHelper.ts](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/client/api/ApiHelper.ts)
- [flip.ts](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/client/types/flip.ts)

## Testing and Verification

Completed:

- automated parity harness added
- parity tests expanded across:
  - pets
  - Midas
  - Final Destination
  - dyed armor
  - drills
  - ability scrolls
  - attributes
  - cake souls
  - anti-manipulation
  - valuation rules

Files most impacted:

- [ReferenceParityTests.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/SkyFlipperSolo.Tests/ReferenceParityTests.cs)
- [SkyFlipperSolo.Tests.csproj](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/SkyFlipperSolo.Tests/SkyFlipperSolo.Tests.csproj)

## Real Runtime Validation Summary

Key live-runtime findings during this session:

- price history backend data was valid once sold bucketing and contracts were corrected
- reference valuation initially passed synthetic tests but failed on real sold data until sold price/bid handling was corrected
- live reference sets began appearing for items like `KAT_FLOWER`
- stale frontend flip cards were misleading until backend sync/status handling was tightened
- parity cache keys exceeded old hit-count column limits and required a schema widening

## Remaining Known Issues / Follow-Up Areas

These are the main remaining areas to continue improving:

1. Full `/flips` hydration stability
   - browser-side mismatch is reduced but may still need a no-SSR route wrapper if the page keeps warning

2. Longer live-history accumulation
   - a wiped dev DB needs more sold history before many items produce strong reference sets consistently

3. More real-data regression fixtures
   - synthetic parity coverage is strong, but more captured live examples would improve confidence further

4. Continued frontend polish
   - some UX pieces are now correct but still utilitarian rather than production-polished

## Suggested Next Steps

1. Let ingestion continue building sold-history depth.
2. Capture real examples where SkyFlipperSolo and Coflnet disagree on flip/no-flip.
3. Add those as regression fixtures to `ReferenceParityTests`.
4. If `/flips` still throws hydration warnings, move the route to a client-only no-SSR wrapper.
5. Re-run parity scorecards after a longer live-data window.

## Quick Reference: Most Important Files Changed

- [ReferenceAuctionService.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Services/ReferenceAuctionService.cs)
- [FlipDetectionService.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Services/FlipDetectionService.cs)
- [BidFlipDetectionService.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Services/BidFlipDetectionService.cs)
- [AuctionFetcherService.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Services/AuctionFetcherService.cs)
- [FlipperService.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Services/FlipperService.cs)
- [SoldAuctionService.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Services/SoldAuctionService.cs)
- [AuctionLifecycleService.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Services/AuctionLifecycleService.cs)
- [PriceAggregationService.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Services/PriceAggregationService.cs)
- [AuctionsController.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Controllers/AuctionsController.cs)
- [FlipsController.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/Controllers/FlipsController.cs)
- [ApiHelper.ts](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/client/api/ApiHelper.ts)
- [page.tsx](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/client/app/flips/page.tsx)
- [ReferenceParityTests.cs](C:/Users/floor/Documents/coding/skyblock/SkyFlipperSolo/SkyFlipperSolo.Tests/ReferenceParityTests.cs)

