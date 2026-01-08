-- Test script to verify Phase 1 & 2 implementation
-- Run with: dotnet run --project test-script.csproj

-- 1. Check if new tables exist
SELECT table_name FROM information_schema.tables 
WHERE table_schema = 'public' 
ORDER BY table_name;

-- 2. Check column exists in Auctions
SELECT column_name, data_type 
FROM information_schema.columns 
WHERE table_name = 'Auctions' 
AND column_name = 'SoldPrice';

-- 3. Check enchantments count (should be > 0 if enchantments are being saved)
SELECT COUNT(*) as enchantment_count FROM "Enchantments";

-- 4. Sample enchantments with auction info
SELECT 
    e."Type",
    e."Level",
    a."ItemName",
    a."Tag"
FROM "Enchantments" e
JOIN "Auctions" a ON e."AuctionId" = a."Id"
LIMIT 10;

-- 5. Check auction status distribution
SELECT 
    "Status",
    COUNT(*) as count
FROM "Auctions"
GROUP BY "Status";

-- 6. Check for sold auctions with SoldPrice
SELECT COUNT(*) as sold_with_price
FROM "Auctions"
WHERE "Status" = 1 AND "SoldPrice" IS NOT NULL;

-- 7. Check ItemPriceHistory table
SELECT COUNT(*) as price_history_count FROM "ItemPriceHistory";

-- 8. Check FlipOpportunities table
SELECT COUNT(*) as flip_count FROM "FlipOpportunities";

-- 9. Sample active BIN auctions (candidates for flip detection)
SELECT 
    "ItemName",
    "Tag",
    "StartingBid",
    "Bin",
    "Status"
FROM "Auctions"
WHERE "Bin" = true AND "Status" = 0
LIMIT 5;

-- 10. Check indexes
SELECT 
    tablename,
    indexname,
    indexdef
FROM pg_indexes
WHERE tablename IN ('Auctions', 'ItemPriceHistory', 'FlipOpportunities', 'Enchantments')
ORDER BY tablename, indexname;
