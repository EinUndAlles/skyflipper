# Item Filters

This documentation describes all available filters for SkyFlipperSolo auction tracker.

## Auction Filters

### Stars
- **Type**: `NUMERICAL`, `RANGE`
- **Description**: Star level of dungeon items
- **Format**: "min-max" for range (e.g., "3-5"), single number for specific level
- **Options**: 0-5

### Rarity
- **Type**: `EQUAL`
- **Description**: Item rarity/tier
- **Options**: COMMON, UNCOMMON, RARE, EPIC, LEGENDARY, MYTHIC, SPECIAL

### Reforge
- **Type**: `EQUAL`
- **Description**: Applied reforge to item
- **Options**: All reforge values from Reforge enum

### Enchantment
- **Type**: `EQUAL`
- **Description**: Applied enchantment to item
- **Options**: All enchantment types from EnchantmentType enum
- **Grouped Filters**: When Enchantment is selected, EnchantLvl filter is automatically enabled

### Bin
- **Type**: `BOOLEAN`
- **Description**: Buy It Now auction
- **Options**: true, false

### MinPrice / MaxPrice
- **Type**: `NUMERICAL`, `RANGE`
- **Description**: Price range in coins
- **Format**: "min-max" for range, number for single value

## Usage Notes

1. **Add filters by clicking "+ Add Filters"** button and typing in the search box to find and select from dropdown
2. **Filters are automatically persisted** in URL and localStorage
3. **Grouped filters** (Enchantment + EnchantLvl) are auto-enabled together for convenience
4. **Remove filters** using the ✕ button next to each active filter
5. **Apply filters** by clicking "Apply Filters" button after configuring your desired filters
6. **Validation**: Invalid filter values show error messages - fix before applying
7. **URL Sharing**: Filter state is reflected in URL parameters, making filter links shareable
8. **Persistence**: Last used filter is saved to localStorage and restored on page load
