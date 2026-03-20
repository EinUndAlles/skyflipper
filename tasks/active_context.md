# Active Context

## Focus
Filter parity: implementing SkyFilter filters to match coflnet's filter surface area.

## What Exists
- Coflnet-style reference-auction engine implemented.
- NBT parsing aligned to reference.
- Enchant list aligned to `dev/Data/Flipper/Constants.cs`.
- Price history aggregation and flip detection aligned.
- NBTLookup storage now uses KeyId/ValueId only.
- NBT SQL filtering extracted to `NbtQueryHelper`.
- NBT key/value ID resolver centralized in `NbtLookupResolver`.
- Redis distributed cache for reference lookups (2h TTL).
- Test fixtures for pets, drills, attributes; all 32 tests passing.

## Filter Parity Status
- **Implemented: ~109 of ~175 coflnet filters (~62%)**
- Batch 1: Core — Bin, Stars, Rarity, Reforge, StartingBid, HighestBid, Count, Enchantment, EnchantLvl
- Batch 2: Equipment — HotPotatoCount, ArtOfTheWar, FarmingForDummies, Recombobulated, Ethermerge, AbilityScroll, Skin, WinningBid, Edition, CapturedPlayer, EndBefore/After, ItemCreatedBefore/After
- Batch 3: Pet — PetLevel, PetItem, PetSkin, PetExp
- Batch 4: Color — Color, HexColorList, ExoticColor, DyeItem
- Batch 5: Slots/Gems/Attributes — UnlockedSlots, UnlockedSlotsMatch, HasAttribute, GemFilter (×35), GemTypeFilter (×12), PerfectGemsCount, FlawlessGemsCount
- Batch 6: Kills/Stats — ZombieKills, SpiderKills, EmanKills, ExpertiseKills, RaiderKills, SwordKills, BloodGodKills, BlazeKills, YogsKilled, BlazeConsumer, RunicKills, HandlesFound, BaseStatBoost, ManaDisintegrator, FarmedCultivating, MinedCrops, BlocksBroken, ThunderCharge, CollectedCoins, ChimeraFound, PickonimbusDurability, IntelligenceEarned, RaffleWin, RaffleYear, IntelligenceBonus

## Current Risks
- `/flips` hydration warnings may still exist; consider no-SSR wrapper if recurring.
- Real-data regression fixtures are limited; capturing live mismatches would improve confidence.

## Next Steps
- Batch 7: Rune filters (5), Item-specific skins (8), Bool/flag filters (7), Drill/equipment (5).
- Batch 8+: Enchant aliases + per-enchant loop, per-attribute level loop, misc string filters.
