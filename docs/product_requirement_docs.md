# SkyFlipperSolo PRD

## Purpose
Provide a self-hosted recreation of Coflnet's SkyBlock flipping stack with near-parity behavior for auction ingestion, NBT parsing, cache-key/reference matching, price aggregation, flip detection, and API/frontend contracts.

## Goals
- Match Coflnet flip accuracy (target ~99.5%).
- Ingest auctions and lifecycle status correctly (active/sold/expired).
- Normalize NBT and cache keys to Coflnet rules.
- Provide compatible API payloads for Coflnet-style frontend.
- Support BIN and non-BIN flip detection with reference-auction valuation.

## Non-Goals
- Feature divergence from Coflnet behavior.
- Heavy UI redesign beyond contract parity.
- External hosted services beyond local deployment.

## Core Requirements
- Poll Hypixel auctions and auctions_ended APIs.
- Persist auctions with NBT lookups and bids.
- Maintain sold price and sold timestamps for aggregation.
- Compute price history (15m/hourly/daily) with anti-manipulation.
- Evaluate flips using reference-auction selection + weighted median.
- Provide REST + SignalR contracts for flips and auctions.

## Parity Targets
- Cache key generation identical to Coflnet reference.
- NBT flattening and matching rules for special items.
- Anti-manipulation dedupe rules in reference selection and aggregation.
- Compatibility aliases in flip payloads.

## Reference Sources
- Coflnet core: `C:\Users\floor\Documents\coding\skyblock\dev`
- Flip engine: `C:\Users\floor\Documents\coding\skyblock\SkyFlipper\Flipper\FlippingEngine.cs`
