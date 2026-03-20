# Active Context

## Focus
Next parity work: advanced filters (SkyFilter parity) and runtime hardening.

## What Exists
- Coflnet-style reference-auction engine implemented.
- NBT parsing aligned to reference.
- Enchant list aligned to `dev/Data/Flipper/Constants.cs`.
- Price history aggregation and flip detection aligned.
- NBTLookup storage now uses KeyId/ValueId only.
- NBT SQL filtering extracted to `NbtQueryHelper`.
- NBT key/value ID resolver centralized in `NbtLookupResolver`.

## Current Risks
- `/flips` hydration warnings may still exist; consider no-SSR wrapper if recurring.
- Real-data regression fixtures are limited; capturing live mismatches would improve confidence.

## Next Steps
- Implement SkyFilter parity (advanced filtering types).
- Expand parity tests with live-data fixtures.
