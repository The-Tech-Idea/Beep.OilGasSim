# Phase 4 — Gameplay Mode Support

**Goal:** Fun Mode + Balanced Mode as first-class profiles.

## TODO

- [x] Mode selection screen (Fun vs Balanced cards)
- [x] Wire `GameplayModeProfile` modifiers in all resolvers (already in Phase 2)
- [x] Fun: simple UI, 2 slots, 12 turns, guided recommendations
- [x] Balanced: standard UI, 3 slots, 20 turns, hedging enabled
- [x] Mode-specific action display names (`mode/modeUi.ts`)
- [x] API exposes `modeProfile` on session response
- [x] Tests: Fun vs Balanced session rules + hedging rejection in Fun

## Verification

Fun Mode feels fast/forgiving; Balanced Mode feels strategic. Same engine.
