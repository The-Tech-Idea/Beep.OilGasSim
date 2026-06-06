# Phase 1 — Core Domain & Simulation Prototype

**Goal:** Server resolves exploration turns without UI.

## TODO

- [ ] Domain entities + enums (partial classes)
- [ ] `GameplayModeProfile`, `BalanceProfile`, scenario DTOs
- [ ] `IContentLoader` — load JSON from `Game/content/`
- [ ] Desert Frontier: 20 blocks with hidden geology
- [ ] `ITurnEngine` + resolution pipeline shell
- [ ] `IActionValidator`
- [ ] `IAuctionResolver`
- [ ] `IExplorationResolver` (study, 2D seismic, exploration well)
- [ ] `IGameRandomFactory` deterministic seeds
- [ ] `GameSessionService` — create, join, submit actions, commit
- [ ] REST: `POST/GET /api/game-sessions`, scenarios, game-modes
- [ ] Tests: exploration chance, dry hole/discovery, turn smoke

## Target Files

| Area | Path |
|------|------|
| Domain | `Game/src/Beep.OilGasSim.Domain/**` |
| Simulation | `Game/src/Beep.OilGasSim.Simulation/TurnEngine/` |
| Application | `Game/src/Beep.OilGasSim.Application/GameSessions/` |
| Content | `Game/content/scenarios/desert-frontier.json` |
| Tests | `Game/src/Beep.OilGasSim.Tests/Simulation/` |

## Verification

Automated test runs 5+ turns with bid, study, drill; outcomes are deterministic given seed.
