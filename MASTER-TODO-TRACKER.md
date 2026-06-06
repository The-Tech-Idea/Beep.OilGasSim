# Beep Oil and Gas Sim — Master TODO Tracker

**Last updated:** 2026-06-05  
**Source docs:** `Documentation/BOGS-GDD-001` through `BOGS-GDD-012`, `Gameplay Mode Profiles Architecture.md`  
**Implementation root:** `Game/`

---

## Vision (MVP)

Desert Frontier scenario · Fun + Balanced modes · Server-authoritative · 20-block map · Oil only · 2–6 players · Highest company value wins.

**Core loop:** Bid → Study → Drill → Discover → Appraise → Develop → Produce → Score

**Build order (from GDD-012):** Simulation first → Solo UI → Multiplayer → AI → Polish

---

## Phase Summary

| Phase | Milestone | Status | Plan Doc |
|-------|-----------|--------|----------|
| 0 | Foundation — repo, API, DB, client shell | 🟡 In Progress | [Phase-00](Game/docs/plans/Phase-00-Foundation.md) |
| 1 | Core domain + simulation prototype | 🟡 In Progress | [Phase-01](Game/docs/plans/Phase-01-Core-Simulation.md) |
| 2 | Economy, development, production | ✅ Done | [Phase-02](Game/docs/plans/Phase-02-Lifecycle-Simulation.md) |
| 3 | Basic web game UI | ✅ Done | [Phase-03](Game/docs/plans/Phase-03-Web-UI.md) |
| 4 | Fun + Balanced gameplay modes | ✅ Done | [Phase-04](Game/docs/plans/Phase-04-Gameplay-Modes.md) |
| 5 | AI Command Center MVP | ✅ Done | [Phase-05](Game/docs/plans/Phase-05-AI-Command-Center.md) |
| 6 | Multiplayer MVP | ✅ Done | [Phase-06](Game/docs/plans/Phase-06-Multiplayer.md) |
| 7 | Polish, tutorial, balancing | ✅ Done | [Phase-07](Game/docs/plans/Phase-07-Polish-Balance.md) |

Legend: ⬜ Pending · 🟡 In Progress · ✅ Done

---

## Phase 0 — Foundation

- [x] ASP.NET Core solution structure (`Game/src/*`)
- [x] Project references (Domain → Simulation → Application → Api)
- [ ] PostgreSQL + EF Core persistence
- [x] Docker Compose scaffold (`api`, `db`)
- [x] Health endpoint
- [x] Vite + React + TypeScript client shell
- [ ] CI pipeline
- [x] Content folder layout

**Verification:** `dotnet build` succeeds; client calls `/health`.

---

## Phase 1 — Core Domain & Simulation Prototype

- [x] Domain entities (GameSession, Company, LicenseBlock, HiddenGeology, TurnAction)
- [x] GameplayModeProfile + BalanceProfile loading
- [x] Desert Frontier scenario (generator + JSON modes/balance)
- [x] TurnEngine shell + resolution order
- [x] ActionValidator
- [x] AuctionResolver (license bids)
- [x] ExplorationResolver (study, seismic, drill, dry hole/discovery)
- [x] Deterministic randomness (GameSeed → TurnSeed)
- [x] GameSessionService (create, start, submit actions, commit turn)
- [x] REST API endpoints (game-sessions, scenarios, game-modes)
- [x] Unit tests: exploration chance, turn resolution smoke

**Verification:** Backend test simulates a multi-turn match via API/tests.

---

## Phase 2 — Full Lifecycle Simulation

- [x] AppraisalResolver
- [x] DevelopmentResolver (Small/Standard/Large)
- [x] ProductionResolver (decline, uptime, OPEX, revenue)
- [x] EconomyResolver (cash flow, debt, royalty, interest)
- [x] MarketResolver (oil price, hedging)
- [x] ScoringService (company value, final score, abandonment penalty)
- [x] AbandonmentResolver + late-life triggers
- [x] Leaderboard calculation (TurnEngine)
- [x] TurnEngine wired with full GDD-002 resolution order
- [x] ActionValidator extensions (appraisal, development, optimize, abandon, debt, hedge)
- [x] Lifecycle integration test (discovery → appraise → develop → produce)
- [x] API: producing fields + final score endpoints

**Verification:** Complete lifecycle server-side: license → production → abandonment → final score. ✅ (integration test passing)

---

## Phase 3 — Basic Web Game UI

- [x] GameShell layout (GDD-010 wireframe)
- [x] Babylon.js basin map, 20 block meshes
- [x] Block selection → RightPanel
- [x] Action queue + Commit Turn
- [x] Turn results cards
- [x] Company dashboard, leaderboard
- [x] ApiClient + game state store (GameContext)

**Verification:** Solo match playable in browser (start API + `npm run dev`).

---

## Phase 4 — Gameplay Modes

- [x] Mode selection screen (Fun vs Balanced)
- [x] Mode-specific UI complexity (Simple vs Standard)
- [x] Simulation modifiers wired (chance, cost, turns, slots)
- [x] Fun Mode simplified action labels + recommended actions
- [x] Balanced Mode hedging + advanced finance panel
- [x] Backend tests for mode profiles

**Verification:** Both modes playable; Fun feels easy, Balanced feels strategic. ✅

---

## Phase 5 — AI Command Center

- [x] AiContextBuilder + visibility filter
- [x] Advisors: Strategy, Geologist, CFO, HSE
- [x] Ask AI endpoint + turn report endpoint
- [x] Command Center UI panel
- [x] AI safety tests (no hidden geology)
- [ ] AiAdvisorHub streaming (deferred)
- [ ] External LLM provider integration (deferred)

**Verification:** AI helps without exposing hidden data. ✅ (9 tests passing)

---

## Phase 6 — Multiplayer MVP

- [x] Lobby (create/join/ready/start)
- [x] SignalR GameHub (lobby, commit, resolve, chat broadcasts)
- [x] 2–6 companies, turn commit per company
- [x] Realtime turn commit status + map/actions scoped per company
- [x] Multiplayer lobby UI + join code flow
- [x] Backend tests (MultiplayerFlowTests)

**Verification:** 2–6 players complete a live match. ✅ (13 tests passing)

---

## Phase 7 — Polish & Balance

- [x] Fun Mode guided tutorial overlay (first-time flow)
- [x] Tooltips, risk badges, financial warnings
- [x] Oil price + production + company value trend charts
- [x] Automated balance simulation batch runs + MVP target tests
- [x] Turn history API (`GET /history`)

**Verification:** External testers complete Fun Mode with minimal explanation. ✅ (15 tests passing)

---

## Key Technical Decisions (from specs)

| Topic | Decision |
|-------|----------|
| Backend | ASP.NET Core, server-authoritative |
| Client | TypeScript, Vite, React, Babylon.js |
| Database | PostgreSQL + EF Core |
| First scenario | Desert Frontier (20 blocks, oil only) |
| MVP modes | Fun (12 turns, 2 slots) + Balanced (20 turns, 3 slots) |
| Turn length | 6 months per turn |
| Hidden geology | Server-only; never sent to client or AI |
| Development CAPEX | Paid immediately (MVP) |
| Debt cap | $500M (Balanced); $300M (Fun) |

---

## Current Sprint Focus

1. Phase 0 carryover — PostgreSQL persistence + CI pipeline
2. External playtest feedback
3. Optional — LLM advisor integration (Phase 5 deferred)
