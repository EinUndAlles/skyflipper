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
| DragonArmorSkin | EQUAL | Dragon helmet skins | Any, None, specific skin |
| ReaperMaskSkin | EQUAL | Reaper Mask skins | Any, None, specific skin |
| SnowSuiteSkin | EQUAL | Snow Suit helmet skins | Any, None, specific skin |
| TarantulaHelmetSkin | EQUAL | Tarantula Helmet skins | Any, TARANTULA_BLACK_WIDOW, None |
| FrozenBlazeSkin | EQUAL | Frozen Blaze helmet skins | Any, None, specific skin |
| PerfectHelmetSkin | EQUAL | Perfect helmet skins | Any, None, specific skin |
| DiversMaskSkin | EQUAL | Diver's Mask skins | Any, None, specific skin |
| ShadowAssassinSkin | EQUAL | Shadow Assassin helmet skins | Any, None, specific skin |

## Rune Filters

| Filter | Type | Description | Options |
|--------|------|-------------|---------|
| RUNE_MUSIC | NUMERICAL, RANGE | Music rune level | 0-3 |
| RUNE_ENCHANT | NUMERICAL, RANGE | Enchant rune level | 0-3 |
| RUNE_TIDAL | NUMERICAL, RANGE | Tidal rune level | 0-3 |
| RUNE_DRAGON | NUMERICAL, RANGE | Dragon/End rune level | 0-3 |

## Bool / Flag Filters

| Filter | Type | Description | Options |
|--------|------|-------------|---------|
| IsShiny | BOOLEAN | Is shiny item | yes, no |
| ArtOfPeace | BOOLEAN | Art of Peace applied | yes, no |
| WoodSingularity | BOOLEAN | Wood Singularity applied | yes, no |
| Model | EQUAL | Abicase model | Any, None, specific model |
| Sold | BOOLEAN | Has item sold (ended with bid) | true, false |
| Clean | BOOLEAN | Item has no modifications | yes |

## Drill / Equipment Filters

| Filter | Type | Description | Options |
|--------|------|-------------|---------|
| DrillPartEngine | EQUAL | Drill engine part | Any, None, specific part |
| DrillPartFuelTank | EQUAL | Drill fuel tank part | Any, None, specific part |
| DrillPartUpgradeModule | EQUAL | Drill upgrade module | Any, None, specific part |
| PowerAbilityScroll | EQUAL | Power ability scroll | Any, None, specific scroll |
| TunedTransmission | NUMERICAL, RANGE | Tuned transmission (AOTV) | number range |

## Attribute Level Filters

Per-attribute level filters for Crimson Isle/Kuudra attributes. All support NUMERICAL, RANGE with options 0-10.

| Filter | NBT Key | Description |
|--------|---------|-------------|
| lifeline | lifeline | Lifeline attribute |
| breeze | breeze | Breeze attribute |
| speed | speed | Speed attribute |
| experience | experience | Experience attribute |
| mana_pool | mana_pool | Mana Pool attribute |
| life_regeneration | life_regeneration | Life Regeneration attribute |
| blazing_resistance | blazing_resistance | Blazing Resistance attribute |
| arachno_resistance | arachno_resistance | Arachno Resistance attribute |
| undead_resistance | undead_resistance | Undead Resistance attribute |
| blazing_fortune | blazing_fortune | Blazing Fortune attribute |
| fishing_experience | fishing_experience | Fishing Experience attribute |
| double_hook | double_hook | Double Hook attribute |
| infection | infection | Infection attribute |
| trophy_hunter | trophy_hunter | Trophy Hunter attribute |
| fisherman | fisherman | Fisherman attribute |
| hunter | hunter | Hunter attribute |
| fishing_speed | fishing_speed | Fishing Speed attribute |
| life_recovery | life_recovery | Life Recovery attribute |
| ignition | ignition | Ignition attribute |
| combo | combo | Combo attribute |
| attack_speed | attack_speed | Attack Speed attribute |
| midas_touch | midas_touch | Midas Touch attribute |
| mana_regeneration | mana_regeneration | Mana Regeneration attribute |
| veteran | veteran | Veteran attribute |
| mending | mending | Mending attribute |
| ender_resistance | ender_resistance | Ender Resistance attribute |
| dominance | dominance | Dominance attribute |
| ender | ender | Ender attribute |
| mana_steal | mana_steal | Mana Steal attribute |
| blazing | blazing | Blazing attribute |
| elite | elite | Elite attribute |
| arachno | arachno | Arachno attribute |
| undead | undead | Undead attribute |
| warrior | warrior | Warrior attribute |
| deadeye | deadeye | Deadeye attribute |
| fortitude | fortitude | Fortitude attribute |
| magic_find | magic_find | Magic Find attribute |
| vitality | mending | Alias for mending (in-game name) |

## Misc String Filters

| Filter | Type | Description | Options |
|--------|------|-------------|---------|
| Seller | TEXT | Seller UUID or auctioneer ID | text input |
| CakeOwner | EQUAL, TEXT | Cake owner name | text input |
| CakeYear | NUMERICAL, RANGE | New Year's Cake year | 1-current |
| PartyHatYear | NUMERICAL, RANGE | Party hat year | number range |
| PartyHatColor | EQUAL | Party hat color | Any, None, specific color |
| PartyHatEmoji | EQUAL | Party hat emoji (2023) | Any, None, specific emoji |
| FairyColor | EQUAL | Fairy exotic color | fairy palette hex values |
| CrystalColor | EQUAL | Crystal exotic color | crystal palette hex values |

## Usage Notes

1. **Add filters** by clicking "+ Add Filters" button and typing in the search box.
2. **Filters persist** in URL and localStorage.
3. **Grouped filters** (Enchantment + EnchantLvl) auto-enable together.
4. **Remove filters** using the ✕ button next to each active filter.
5. **Apply filters** by clicking "Apply Filters" button.
6. **Range format**: "min-max" for range, "<value" or ">value" for thresholds.
7. **URL Sharing**: Filter state is in URL parameters for shareable links.
