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
- **Implemented: ~345 coflnet filter registrations (~100%)**
- Batch 1: Core — Bin, Stars, Rarity, Reforge, StartingBid, HighestBid, Count, Enchantment, EnchantLvl
- Batch 2: Equipment — HotPotatoCount, ArtOfTheWar, FarmingForDummies, Recombobulated, Ethermerge, AbilityScroll, Skin, WinningBid, Edition, CapturedPlayer, EndBefore/After, ItemCreatedBefore/After
- Batch 3: Pet — PetLevel, PetItem, PetSkin, PetExp
- Batch 4: Color — Color, HexColorList, ExoticColor, DyeItem
- Batch 5: Slots/Gems/Attributes — UnlockedSlots, UnlockedSlotsMatch, HasAttribute, GemFilter (×35), GemTypeFilter (×12), PerfectGemsCount, FlawlessGemsCount
- Batch 6: Kills/Stats — ZombieKills, SpiderKills, EmanKills, ExpertiseKills, RaiderKills, SwordKills, BloodGodKills, BlazeKills, YogsKilled, BlazeConsumer, RunicKills, HandlesFound, BaseStatBoost, ManaDisintegrator, FarmedCultivating, MinedCrops, BlocksBroken, ThunderCharge, CollectedCoins, ChimeraFound, PickonimbusDurability, IntelligenceEarned, RaffleWin, RaffleYear, IntelligenceBonus
- Batch 7: Runes — MusicRune, EnchantRune, TidalRune, EndRune
- Batch 7: Skins — DragonArmor, ReaperMask, SnowSuite, TarantulaHelmet, FrozenBlaze, PerfectHelmet, DiversMask, ShadowAssassin
- Batch 7: Bool/Flag — IsShiny, ArtOfPeace, WoodSingularity, Model, Sold, Clean
- Batch 7: Drill/Equipment — DrillPartEngine, DrillPartFuelTank, DrillPartUpgradeModule, PowerAbilityScroll, TunedTransmission
- Batch 8: Per-attribute level — 36 attribute filters (attr.lifeline through attr.magic_find) + vitality alias
- Batch 8: Misc string — Seller, CakeOwner, CakeYear, PartyHatYear, PartyHatColor, PartyHatEmoji, FairyColor, CrystalColor
- Batch 9: Enchant — EnchantBaseFilter (8 aliases + ~153 per-enchant loop)
- Batch 9: Remaining — Candy, Jalapeno, BassWeight, ItemTier, PowderCoating, GrowthStages, UId, CrabHatColor, TalismanEnrichment, DungeonSkillReq, RodHook/Line/Sinker, LogsCut, AbsorbLogs, AxeBoosters, PlarvoidBook, ItemId, ItemTag, Everything, ItemNameContains, JyrreMax, SecondEnchant/SecondEnchantLvl, NoOtherValuableEnchants, PricePerLevel, PricePerUnit, CostPerExpPlusBase
- Fixed: PartyHatYearFilter changed to EQUAL type; CakeYearFilter key fixed to `new_years_cake`

## Current Risks
- `/flips` hydration warnings may still exist; consider no-SSR wrapper if recurring.
- Real-data regression fixtures are limited; capturing live mismatches would improve confidence.

## Next Steps
- Full coflnet filter parity achieved. Focus shifts to testing with live data and frontend integration.
