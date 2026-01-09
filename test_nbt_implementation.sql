-- NBT Implementation Test Queries
-- Run these to verify all features are working correctly

-- ===== 1. Check table counts =====
SELECT 'Auctions' as Table_Name, COUNT(*) as Count FROM "Auctions"
UNION ALL
SELECT 'NbtData', COUNT(*) FROM "NbtData"
UNION ALL
SELECT 'NBTLookups', COUNT(*) FROM "NBTLookups"
UNION ALL
SELECT 'AveragePrices', COUNT(*) FROM "AveragePrices"
UNION ALL
SELECT 'FlipOpportunities', COUNT(*) FROM "FlipOpportunities"
ORDER BY Table_Name;

-- ===== 2. Verify NBT data is attached to auctions =====
SELECT 
    COUNT(*) as AuctionsWithNBT,
    (SELECT COUNT(*) FROM "Auctions") as TotalAuctions,
    ROUND(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM "Auctions"), 2) as PercentageWithNBT
FROM "Auctions" 
WHERE "NbtDataId" IS NOT NULL;

-- ===== 3. Check NBT lookup distribution =====
SELECT 
    "Key",
    COUNT(*) as Count,
    COUNT(DISTINCT "AuctionId") as UniqueAuctions
FROM "NBTLookups"
GROUP BY "Key"
ORDER BY Count DESC
LIMIT 15;

-- ===== 4. Find 5-star dungeon items (NBT lookup test) =====
SELECT 
    a."ItemName",
    a."Tag",
    l."ValueNumeric" as Stars,
    a."HighestBidAmount" as Price
FROM "Auctions" a
JOIN "NBTLookups" l ON a."Id" = l."AuctionId"
WHERE l."Key" = 'dungeon_item_level' AND l."ValueNumeric" = 5
LIMIT 10;

-- ===== 5. Find pets with skins =====
SELECT 
    a."ItemName",
    a."Tag",
    l."ValueString" as Skin
FROM "Auctions" a
JOIN "NBTLookups" l ON a."Id" = l."AuctionId"
WHERE l."Key" = 'skin'
LIMIT 10;

-- ===== 6. Check item counts (stacked items) =====
SELECT 
    "ItemName",
    "Tag",
    "Count",
    "HighestBidAmount" as TotalPrice,
    ROUND("HighestBidAmount"::numeric / "Count", 2) as PricePerItem
FROM "Auctions"
WHERE "Count" > 1
ORDER BY "Count" DESC
LIMIT 10;

-- ===== 7. Check price aggregation by granularity =====
SELECT 
    "Granularity",
    COUNT(*) as RecordCount,
    COUNT(DISTINCT "ItemTag") as UniqueItems,
    MIN("Timestamp") as EarliestData,
    MAX("Timestamp") as LatestData
FROM "AveragePrices"
GROUP BY "Granularity"
ORDER BY "Granularity";

-- ===== 8. Check hourly price data (last 24h) =====
SELECT 
    "ItemTag",
    "Timestamp",
    "Median",
    "Volume"
FROM "AveragePrices"
WHERE "Granularity" = 0 -- Hourly
  AND "Timestamp" > NOW() - INTERVAL '24 hours'
ORDER BY "Timestamp" DESC, "Volume" DESC
LIMIT 20;

-- ===== 9. Check flip opportunities with data sources =====
SELECT 
    "ItemName",
    "CurrentPrice",
    "MedianPrice",
    "EstimatedProfit",
    ROUND("ProfitMarginPercent", 2) as MarginPercent,
    "DataSource",
    "AuctionEnd"
FROM "FlipOpportunities"
ORDER BY "ProfitMarginPercent" DESC
LIMIT 15;

-- ===== 10. Summary statistics =====
SELECT 
    'Total Auctions' as Metric, COUNT(*)::text as Value FROM "Auctions"
UNION ALL
SELECT 'Auctions with NBT', COUNT(*)::text FROM "Auctions" WHERE "NbtDataId" IS NOT NULL
UNION ALL
SELECT 'Total NBT Lookups', COUNT(*)::text FROM "NBTLookups"
UNION ALL
SELECT 'Hourly Price Records', COUNT(*)::text FROM "AveragePrices" WHERE "Granularity" = 0
UNION ALL
SELECT 'Daily Price Records', COUNT(*)::text FROM "AveragePrices" WHERE "Granularity" = 1
UNION ALL
SELECT 'Active Flips', COUNT(*)::text FROM "FlipOpportunities"
UNION ALL
SELECT 'Sold Auctions', COUNT(*)::text FROM "Auctions" WHERE "Status" = 1;
