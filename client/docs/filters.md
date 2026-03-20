# Item Filters

This documentation describes all available filters for SkyFlipperSolo auction tracker.

## Auction Filters

| Filter | Type | Description | Options |
|--------|------|-------------|---------|
| Stars | NUMERICAL, RANGE | Star level of dungeon items (upgrade_level / dungeon_item_level) | 0-5 |
| Rarity | EQUAL | Item rarity/tier | COMMON, UNCOMMON, RARE, EPIC, LEGENDARY, MYTHIC, SPECIAL |
| Reforge | EQUAL | Applied reforge | All Reforge enum values |
| Bin | BOOLEAN | Buy It Now auction | true, false |
| StartingBid | NUMERICAL, RANGE | Starting bid price in coins | number range |
| HighestBid | NUMERICAL, RANGE | Highest bid amount | number range |
| Count | NUMERICAL, RANGE | Item stack count | 1-64 |
| WinningBid | NUMERICAL, RANGE | Midas winning bid amount | number range |

## Enchantment Filters

| Filter | Type | Description | Options |
|--------|------|-------------|---------|
| Enchantment | EQUAL | Applied enchantment type | All EnchantmentType values |
| EnchantLvl | NUMERICAL, RANGE | Enchantment level | 1-10 |

## Equipment Filters

| Filter | Type | Description | Options |
|--------|------|-------------|---------|
| HotPotatoCount | NUMERICAL, RANGE | Hot potato book count | 0-15 |
| ArtOfTheWar | BOOLEAN | Art of War applied | yes, no |
| FarmingForDummies | NUMERICAL, RANGE | Farming for Dummies count | 0-5 |
| Recombobulated | BOOLEAN | Recombobulated | yes, no |
| Ethermerge | BOOLEAN | Ethermerge applied | yes, no |
| AbilityScroll | EQUAL | Ability scroll applied | Any, None, specific scroll |

## Time Filters

| Filter | Type | Description | Format |
|--------|------|-------------|--------|
| EndBefore | DATE | Auction ends before | ISO datetime |
| EndAfter | DATE | Auction ends after | ISO datetime |
| ItemCreatedBefore | DATE | Item created before | ISO datetime |
| ItemCreatedAfter | DATE | Item created after | ISO datetime |

## Pet Filters

| Filter | Type | Description | Options |
|--------|------|-------------|---------|
| PetLevel | NUMERICAL, RANGE | Pet level from item name | 1-200 |
| PetItem | EQUAL | Held pet item | Any, None, NOT_TIER_BOOST, specific items |
| PetSkin | EQUAL | Pet skin | Any, None, specific skins |
| PetExp | NUMERICAL, RANGE | Pet experience | 0-max |

## Color Filters

| Filter | Type | Description | Options |
|--------|------|-------------|---------|
| Color | EQUAL | Item color (leather/leather armor) | hex color values |
| HexColorList | RANGE | Color list filter | hex lists |
| ExoticColor | EQUAL | Exotic color (fairy/crystal) | Fairy, Crystal, combined |
| DyeItem | EQUAL | Dye applied | Any, None, specific dyes |

## Slot / Gem / Attribute Filters

| Filter | Type | Description | Options |
|--------|------|-------------|---------|
| UnlockedSlots | NUMERICAL, RANGE | Count of unlocked gem slots | 0-5 |
| UnlockedSlotsMatch | EQUAL | Exact unlocked slot match | Any, None, specific slot |
| HasAttribute | BOOLEAN | Any Crimson Isle attribute present | true, false |
| PerfectGemsCount | NUMERICAL, RANGE | Count of perfect gems | 0-5 |
| FlawlessGemsCount | NUMERICAL, RANGE | Count of flawless gems | 0-5 |
| {gem}{i}Gem | EQUAL | Gem quality per slot (e.g. ruby0Gem, combat0Gem) | Any, None, PERFECT, FLAWLESS, etc. |
| {group}{i}GemType | EQUAL | Gem type per group slot (e.g. combat0GemType) | Any, None, JASPER, RUBY, etc. |

Gem slots: RUBY, JASPER, JADE, TOPAZ, AMETHYST, AMBER, SAPPHIRE, OPAL, PERIDOT, AQUAMARINE, CITRINE (×2 each + AMETHYST_2).
Gem groups: COMBAT, OFFENSIVE, DEFENSIVE, MINING_, UNIVERSAL, CHISEL (×2 each).

## Kills / Counter Filters

| Filter | Type | NBT Key | Description |
|--------|------|---------|-------------|
| ZombieKills | NUMERICAL, RANGE | zombie_kills | Zombie kills count |
| SpiderKills | NUMERICAL, RANGE | spider_kills | Spider kills count |
| EmanKills | NUMERICAL, RANGE | eman_kills | Enderman kills count |
| ExpertiseKills | NUMERICAL, RANGE | expertise_kills | Expertise kills count |
| RaiderKills | NUMERICAL, RANGE | raider_kills | Raider kills count |
| SwordKills | NUMERICAL, RANGE | sword_kills | Sword kills count |
| BloodGodKills | NUMERICAL, RANGE | blood_god_kills | Blood God kills count |
| BlazeKills | NUMERICAL, RANGE | blaze_kills | Blaze kills (Bulwark) |
| YogsKilled | NUMERICAL, RANGE | yogsKilled | Yogs killed count |
| BlazeConsumer | NUMERICAL, RANGE | blaze_consumer | Blaze kills count |
| RunicKills | NUMERICAL, RANGE | runic_kills | Runic mob kills count |
| HandlesFound | NUMERICAL, RANGE | handles_found | Handles found count |

## Stat / Counter Filters

| Filter | Type | NBT Key | Description |
|--------|------|---------|-------------|
| BaseStatBoost | NUMERICAL, RANGE | baseStatBoostPercentage | Base stat boost % |
| ManaDisintegrator | NUMERICAL, RANGE | mana_disintegrator_count | Mana disintegrator count |
| FarmedCultivating | NUMERICAL, RANGE | farmed_cultivating | Cultivating farming count |
| MinedCrops | NUMERICAL, RANGE | mined_crops | Mined crops count |
| BlocksBroken | NUMERICAL, RANGE | blocksBroken | Blocks broken count |
| ThunderCharge | NUMERICAL, RANGE | thunder_charge | Thunder charge accumulated |
| CollectedCoins | NUMERICAL, RANGE | collected_coins | Crown of Avarice coins |
| ChimeraFound | NUMERICAL, RANGE | chimera_found | Ultimate Chimera found |
| PickonimbusDurability | NUMERICAL, RANGE | pickonimbus_durability | Pickonimbus durability |
| IntelligenceEarned | NUMERICAL, RANGE | intelligence_earned | Wizard Wand intelligence |
| RaffleWinCount | NUMERICAL, RANGE | raffle_win | Raffle roll value |
| RaffleYearCount | NUMERICAL, RANGE | raffle_year | Raffle century |
| IntelligenceBonus | NUMERICAL, RANGE | bottle_of_jyrre_seconds | Bottle of Jyrre bonus (input in hours) |

## Skin / Misc Filters

| Filter | Type | Description | Options |
|--------|------|-------------|---------|
| Skin | EQUAL | Generic item skin | Any, None, specific skin values |
| CapturedPlayer | EQUAL | Captured player (Trophy fish) | player name |
| Edition | NUMERICAL, RANGE | Edition number | number range |

## Usage Notes

1. **Add filters** by clicking "+ Add Filters" button and typing in the search box.
2. **Filters persist** in URL and localStorage.
3. **Grouped filters** (Enchantment + EnchantLvl) auto-enable together.
4. **Remove filters** using the ✕ button next to each active filter.
5. **Apply filters** by clicking "Apply Filters" button.
6. **Range format**: "min-max" for range, "<value" or ">value" for thresholds.
7. **URL Sharing**: Filter state is in URL parameters for shareable links.
