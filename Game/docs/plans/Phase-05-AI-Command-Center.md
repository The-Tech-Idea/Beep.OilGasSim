# Phase 5 — AI Command Center MVP

**Goal:** Game-aware advisors without hidden data leakage.

## TODO

- [x] AiContextBuilder + AiVisibilityFilter
- [x] Advisors: Strategy, Geologist, CFO, HSE (rule-based MVP engine)
- [x] `POST /api/game-sessions/{id}/ai/ask`
- [x] `GET /api/game-sessions/{id}/ai/turn-report`
- [x] Command Center UI panel (client)
- [x] AI safety unit tests (3 tests)

## Verification

AI answers block/finance/abandonment questions; context never includes HiddenGeology.

**Note:** MVP uses `RuleBasedAdvisorEngine` — swap for LLM provider later without changing context safety layer.
