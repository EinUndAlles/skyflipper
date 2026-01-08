-- Add Status column to Auctions table
-- Default 0 = ACTIVE

ALTER TABLE "Auctions" 
ADD COLUMN "Status" INTEGER NOT NULL DEFAULT 0;

-- Update existing WasSold=true records to Status=1 (SOLD) if WasSold column exists
-- This is optional, can be run if the WasSold column still exists
-- UPDATE "Auctions" SET "Status" = 1 WHERE "WasSold" = true;

-- Drop WasSold column if it exists
-- ALTER TABLE "Auctions" DROP COLUMN IF EXISTS "WasSold";
