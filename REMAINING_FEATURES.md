# SkyFlipperSolo - Remaining Features for Full Parity

This document tracks features from the Coflnet SkyFlipper reference that are not yet implemented in SkyFlipperSolo.

## Priority Legend
- **P0**: Critical - Affects flip detection accuracy significantly
- **P1**: High - Affects specific item categories
- **P2**: Medium - Nice to have, improves edge cases
- **P3**: Low - Development/operations tooling

---

## P1: High Priority

### 1. Attribute Shard Weighting
**Reference**: `FlippingEngine.cs` lines 67-83

Attribute-based items (Kuudra gear) have weighted attributes that affect value. Some attributes are worth more than others.

```csharp
private static readonly Dictionary<string, short> ShardAttributes = new(){
    {"mana_pool", 1},
    {"breeze", 1},
    {"speed", 2},
    {"life_regeneration", 2},
    {"fishing_experience", 2},
    {"ignition", 2},
    {"blazing_fortune", 2},
    {"double_hook", 3},
    {"mana_regeneration", 2},
    {"mending", 3},
    {"dominance", 3},
    {"magic_find", 2},
    {"veteran", 1}
};
```

**Implementation needed**:
- Add `ShardAttributes` dictionary to `CacheKeyService.cs`
- Include attribute level bucketing in cache key for attribute items
- Weight should affect how attributes are grouped (tier 3 = exact match, tier 1 = broader ranges)

**Affected items**: Crimson armor, Aurora armor, Terror armor, Fervor armor, Hollow armor, attribute shards

---

### 2. Armor Color/Dye NBT Matching
**Reference**: `FlippingEngine.cs` lines 730-734

Leather armor pieces with custom colors or dyes have different values.

```csharp
if (flatNbt.ContainsKey("color") || IsArmour(auction))
{
    select = AddNBTSelect(select, flatNbt, "color");
    select = AddNBTSelect(select, flatNbt, "dye_item");
}
```

**Implementation needed**:
- Add `color` and `dye_item` to cache key for armor items
- Check if item tag ends with `_CHESTPLATE`, `_BOOTS`, `_HELMET`, `_LEGGINGS`

**Affected items**: All dyeable armor, fairy armor, crystal armor

---

### 3. Cosmetic NBT Keys
**Reference**: `FlippingEngine.cs` lines 693-703

Special cosmetic items have unique NBT keys that affect value.

```csharp
if (flatNbt.ContainsKey("MUSIC")) select = AddNBTSelect(select, flatNbt, "MUSIC");
if (flatNbt.ContainsKey("ENCHANT")) select = AddNBTSelect(select, flatNbt, "ENCHANT");
if (flatNbt.ContainsKey("DRAGON")) select = AddNBTSelect(select, flatNbt, "DRAGON");
if (flatNbt.ContainsKey("TIDAL")) select = AddNBTSelect(select, flatNbt, "TIDAL");
if (flatNbt.ContainsKey("party_hat_emoji")) select = AddNBTSelect(select, flatNbt, "party_hat_emoji");
```

**Implementation needed**:
- Add these keys to `BuildNbtString()` in `CacheKeyService.cs`
- Include in cache key when present

**Affected items**: Music discs, party hats, dragon pets, tidal items

---

## P2: Medium Priority

### 4. Bid Flip Detection (Non-BIN Auctions)
**Reference**: `FlippingEngine.cs` `QueckActiveAuctionsForFlips()` lines 121-156

The reference checks non-BIN auctions ending soon (0.5-2 minutes) for flip opportunities.

```csharp
var toCheck = context.Auctions
    .Where(a => a.End < max && a.End > min && !a.Bin)
```

**Implementation needed**:
- New service: `BidFlipDetectionService`
- Query auctions ending in 30s-2min window
- Calculate if current highest bid + expected snipe is below median
- Different profit calculation (account for bidding wars)

**Complexity**: High - requires different algorithm and timing considerations

---

### 5. Unlocked Slots Date Filter
**Reference**: `FlippingEngine.cs` lines 748-757

Items with gemstone slots should only compare to items created after the gemstone update.

```csharp
private static readonly DateTime UnlockedIntroduction = new DateTime(2021, 9, 4);

if (canHaveGemstones || flatNbt.ContainsKey("unlocked_slots"))
{
    select = AddNBTSelect(select, flatNbt, "unlocked_slots");
    select = select.Where(a => a.ItemCreatedAt > UnlockedIntroduction);
}
```

**Implementation needed**:
- Track `ItemCreatedAt` on auctions (from NBT `timestamp` field)
- Filter price references by creation date for gemstone items

**Affected items**: All items with unlockable gemstone slots

---

### 6. Drill Parts Matching
**Reference**: `FlippingEngine.cs` lines 714-719

Drills have modular parts that affect value.

```csharp
if (auction.Tag.Contains("_DRILL"))
{
    select = AddNBTSelect(select, flatNbt, "drill_part_engine");
    select = AddNBTSelect(select, flatNbt, "drill_part_fuel_tank");
    select = AddNBTSelect(select, flatNbt, "drill_part_upgrade_module");
}
```

**Implementation needed**:
- Add drill part keys to cache key for drill items
- Check if tag contains `_DRILL`

**Affected items**: All drills (Mithril Drill, Titanium Drill, etc.)

---

## P3: Low Priority (Dev/Ops Tooling)

### 7. Debug API Endpoints
**Reference**: `ApiController.cs`

Endpoints for debugging flip detection:

| Endpoint | Purpose |
|----------|---------|
| `GET /flip/{uuid}/based` | Get reference auctions used for pricing |
| `GET /flip/{uuid}/cache` | Get cache info (hitCount, key, queryTime, estimate) |
| `DELETE /flip/{uuid}` | Invalidate cache for an auction |
| `GET /status` | Health check with last live probe time |

**Implementation needed**:
- Add new controller or extend `FlipsController`
- Expose cache key generation for debugging
- Add cache invalidation endpoint

---

### 8. Prometheus Metrics
**Reference**: `FlippingEngine.cs` lines 41-56

Operational metrics for monitoring:

```csharp
Prometheus.Counter foundFlipCount = Prometheus.Metrics.CreateCounter("flips_found", "Number of flips found");
Prometheus.Counter alreadySold = Prometheus.Metrics.CreateCounter("already_sold_flips", "Flips already sold");
Prometheus.Histogram time = Prometheus.Metrics.CreateHistogram("time_to_find_flip", "Time to find flip");
Prometheus.Histogram runtroughTime = Prometheus.Metrics.CreateHistogram("sky_flipper_auction_to_send_flip_seconds", "...");
```

**Implementation needed**:
- Add `prometheus-net.AspNetCore` NuGet package
- Add metrics to `FlipDetectionService`
- Expose `/metrics` endpoint

---

### 9. Candy Used Special Logic
**Reference**: `FlippingEngine.cs` `AddCandySelect()` lines 850-862

Special handling for pet candy:
- If pet has max exp (>24M) and has a skin, filter differently
- Binary check: candy used > 0 vs = 0

```csharp
if (flatNbt.TryGetValue("exp", out string expString) && double.TryParse(expString, out double exp) 
    && exp > 24_000_000 && flatNbt.ContainsKey("skin"))
{
    // Special skin filtering
}
if (val > 0)
    return select.Where(a => a.NBTLookup.Where(n => n.KeyId == keyId && n.Value > 0).Any());
return select.Where(a => a.NBTLookup.Where(n => n.KeyId == keyId && n.Value == 0).Any());
```

**Implementation needed**:
- Update `BuildNbtString()` to handle candy as binary (0 vs >0)
- Add max-exp + skin special case

---

## Implementation Checklist

- [ ] P1: Attribute Shard Weighting
- [ ] P1: Armor Color/Dye NBT Matching  
- [ ] P1: Cosmetic NBT Keys (MUSIC, DRAGON, TIDAL, party_hat_emoji)
- [ ] P2: Bid Flip Detection
- [ ] P2: Unlocked Slots Date Filter
- [ ] P2: Drill Parts Matching
- [ ] P3: Debug API Endpoints
- [ ] P3: Prometheus Metrics
- [ ] P3: Candy Used Special Logic

---

## Current Accuracy Estimate

With all P0 features implemented: **~97%**

After P1 features: **~99%**

After P2 features: **~99.5%**

The remaining 0.5% are edge cases and items with very low volume where exact matching is difficult.
