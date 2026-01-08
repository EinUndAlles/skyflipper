# Testing Plan for Phase 1 & 2

## 1. Database Verification ✓
- [ ] Verify `ItemPriceHistory` table exists
- [ ] Verify `FlipOpportunities` table exists
- [ ] Verify `SoldPrice` column added to `Auctions`
- [ ] Verify new indexes were created

## 2. Backend Service Startup ✓
- [ ] Restart backend (`dotnet run`)
- [ ] Verify AuctionFetcherService starts
- [ ] Verify FlipperService starts
- [ ] Verify SoldAuctionService starts
- [ ] Verify PriceAggregationService starts
- [ ] Verify FlipDetectionService starts
- [ ] Check for any startup errors

## 3. Enchantment Storage ✓
- [ ] Wait for auctions to be fetched
- [ ] Query database for enchantments
- [ ] Verify count > 0 (enchantments are being saved)

## 4. Sold Auction Tracking ✓
- [ ] Wait 60 seconds for SoldAuctionService to run
- [ ] Check logs for "Marked X auctions as SOLD"
- [ ] Query database for SOLD auctions with SoldPrice

## 5. Price Aggregation ✓
- [ ] Wait for some sold auctions to accumulate
- [ ] Check logs for price aggregation (runs hourly, or trigger manually)
- [ ] Query `ItemPriceHistory` table for data

## 6. Flip Detection ✓
- [ ] Wait 2-3 minutes for services to collect data
- [ ] Check logs for "Detected X flips"
- [ ] Query `FlipOpportunities` table

## 7. API Endpoint Testing ✓
- [ ] Test `GET /api/flips`
- [ ] Test `GET /api/flips/stats`
- [ ] Test `GET /api/flips/history/{tag}` with a valid tag
- [ ] Verify response formats

## 8. Data Flow Validation ✓
- [ ] Verify auctions → enchantments relationship
- [ ] Verify sold auctions → price history flow
- [ ] Verify price history → flip detection flow
