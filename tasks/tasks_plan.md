# Tasks Plan

## Current Status
- Overall parity ~90% per `FULL_PARITY_REVIEW.md` (P0 gaps closed).
- Reference-auction engine and cache-key parity implemented.
- Enchant list aligned to `dev/Data/Flipper/Constants.cs`.
- API contracts aligned to Coflnet-style payloads.

## Completed Tasks
1. Align CacheKey format with Coflnet raw concat. ✅
2. Fix `ShouldPetItemMatch` exp guard. ✅
3. Port `SelectBestEnchant` WorthOrder/WorthOrderLevels. ✅
4. Move NBT filtering to SQL-level in `ReferenceAuctionService`. ✅
5. Fix `AveragePrice.CacheKey` length. ✅
6. Add debug endpoints in `FlipsController`. ✅
7. Add Prometheus metrics for flip detection loop. ✅
8. Capture and add real-data regression fixtures to tests. ✅
9. Replace NBTLookup legacy Key/ValueString with KeyId/ValueId. ✅
10. Extract NBT SQL filtering to helper. ✅
11. Centralize NBT key/value ID resolver. ✅
12. Replace IMemoryCache with Redis distributed cache for references. ✅
13. Port SkyFilter filters — Batches 1-7 complete (~134/175 filters, ~77%). ✅

## Filter Parity Tracker
| Batch | Category | Filters | Status |
|-------|----------|---------|--------|
| 1 | Core | Bin, Stars, Rarity, Reforge, StartingBid, HighestBid, Count, Enchantment, EnchantLvl | ✅ |
| 2 | Equipment | HotPotatoCount, ArtOfTheWar, FarmingForDummies, Recombobulated, Ethermerge, AbilityScroll, Skin, WinningBid, Edition, CapturedPlayer, EndBefore/After, ItemCreatedBefore/After | ✅ |
| 3 | Pet | PetLevel, PetItem, PetSkin, PetExp | ✅ |
| 4 | Color | Color, HexColorList, ExoticColor, DyeItem | ✅ |
| 5 | Slots/Gems/Attrs | UnlockedSlots, UnlockedSlotsMatch, HasAttribute, GemFilter(×35), GemTypeFilter(×12), PerfectGemsCount, FlawlessGemsCount | ✅ |
| 6 | Kills/Stats | 12 kills + 12 stat counters + IntelligenceBonus | ✅ |
| 7 | Runes/Skins/Bool/Drill | Rune(4), Skin(8), Bool/Flag(6), Drill(5) — 23 filters | ✅ |
| 8 | Enchant aliases + loop | EnchantBaseFilter aliases, per-enchant-type loop | 🔄 Next |
| 9 | Per-attribute level | Dynamic attr.* level filters | 📋 Planned |
| 10 | Misc string | seller, cake_owner, party_hat_*, etc. | 📋 Planned |

## Completed Highlights
- Sold auction ingestion uses `SoldAt` + `SoldPrice`.
- Price aggregation uses sold-time bucketing and volume-weighted daily rollups.
- Anti-manipulation dedupe implemented in reference selection and aggregation.
- NbtNumberFilter supports constructor-based PropName to reduce boilerplate.
