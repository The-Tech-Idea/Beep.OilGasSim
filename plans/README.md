# OGSim — design workspace

A ground-up oil & gas company simulation engine. **Exploration → appraisal →
development → production → processing → transport → export.**

> **Nothing in this folder is code, and no code exists yet — by instruction.**
> The design is settled on paper first: concept matrix, domain model, system
> diagrams, simulation models, then a phased build plan. Code begins only after
> these are approved.

> **The previous engine (`OGGame.Core`) is not an input.** It is not referenced,
> ported, or consulted here. This design starts from the domain and from
> industry data standards.

## Reading order

| # | Document | What it settles |
|---|---|---|
| 0 | [design/00_VISION.md](design/00_VISION.md) | What the game is, the player fantasy, the fun/realism contract |
| 1 | [design/01_CONCEPT_MATRIX.md](design/01_CONCEPT_MATRIX.md) | Every concept ↔ real-world entity ↔ standard ↔ contract ↔ model ↔ player verb |
| 2 | [design/02_DOMAIN_MODEL.md](design/02_DOMAIN_MODEL.md) | The entities and their relationships, aligned to PPDM |
| 3 | [design/03_ARCHITECTURE.md](design/03_ARCHITECTURE.md) | Contracts, plugin composition, the kernel, module map |
| 4 | [design/04_MATERIAL_AND_FLOW.md](design/04_MATERIAL_AND_FLOW.md) | **One flow engine**: material from reservoir to export berth |
| 5 | [design/05_SIMULATION_MODELS.md](design/05_SIMULATION_MODELS.md) | The maths, per stage, with the fidelity dial |
| 6 | [design/06_WORLD_AND_EXPLORATION.md](design/06_WORLD_AND_EXPLORATION.md) | Map, basins, seismic, discovery, the exploration loop |
| 7 | [design/07_TECHNOLOGY.md](design/07_TECHNOLOGY.md) | Technology tree and how tech changes the physics |
| 8 | [design/08_ECONOMICS.md](design/08_ECONOMICS.md) | Fiscal regimes, markets, contracts, decommissioning |
| 9 | [design/09_DIAGNOSTICS.md](design/09_DIAGNOSTICS.md) | Log, audit and failure policy — a first-class subsystem |
| 10 | [design/10_CONTENT_AND_UNITS.md](design/10_CONTENT_AND_UNITS.md) | Data-driven content, units of measure, validation |
| 11 | [design/11_PERSISTENCE.md](design/11_PERSISTENCE.md) | Save/load, determinism, versioning |
| 12 | [design/12_VERIFICATION.md](design/12_VERIFICATION.md) | How each phase proves itself |
| 13 | [design/13_ENVIRONMENT.md](design/13_ENVIRONMENT.md) | The operating environment: terrain, climate, access, weather — and how each changes every stage |
| 14 | [design/14_HSE.md](design/14_HSE.md) | Health, safety and environment as a discipline: barriers, leading indicators, ESG |
| 15 | [design/15_TIME_AND_EXECUTION.md](design/15_TIME_AND_EXECUTION.md) | **How the sim runs** — turn-based engine, real-time-with-pause game, sub-tick resolution |
| 16 | [design/16_EVENT_MATRIX.md](design/16_EVENT_MATRIX.md) | Every event: trigger, payload, severity, auto-pause, consumer |
| 17 | [design/17_CROSS_IMPACT_MATRIX.md](design/17_CROSS_IMPACT_MATRIX.md) | **How everything affects everything** — the matrix, the couplings, the feedback loops |
| 18 | [design/18_GAME_MODES.md](design/18_GAME_MODES.md) | Objectives, missions, challenges, scenarios, campaign — one system, five modes |
| 19 | [design/19_GLOSSARY.md](design/19_GLOSSARY.md) | Naming discipline — one term, one meaning, one contract |
| 20 | [design/20_PLAYER_DECISIONS.md](design/20_PLAYER_DECISIONS.md) | Every decision the player makes, and the four-part test each must pass |
| 21 | [design/21_INTEGRATION.md](design/21_INTEGRATION.md) | **Time × events × cross-impact** — propagation delays, loop periods, tick-stage event map, alerts as loop-entry detectors |
| 22 | [design/22_DESIGN_COHERENCE.md](design/22_DESIGN_COHERENCE.md) | **How the documents affect each other** — change-impact map, rule registry, identifier scheme, coherence log |
| 23 | [design/23_FUNCTION_MATRIX.md](design/23_FUNCTION_MATRIX.md) | **The contract function matrix** — every C# contract: function → SDD → implementing phase → consumer stage → test pin, plus the dependency and two-pipeline mermaid graphs |
| — | [MASTER_TRACKER.md](MASTER_TRACKER.md) | Phase status and execution order — the single source of truth |
| — | [phases/](phases/) | One design document per build phase (R1–R25) |
| — | [sdd/](sdd/) | **Software design documents** — signatures and algorithms; foundation SDDs before code, per-phase SDDs rolling ([SDD_INDEX](sdd/SDD_INDEX.md)) |
| — | [catalog/](catalog/) | **Equipment & tech catalogue** — one sheet per station (visible equipment, tiers, gates, costs) + the [TECH_TREE](catalog/TECH_TREE.md) gate registry ([CATALOG_INDEX](catalog/CATALOG_INDEX.md)) |
| — | [research/](research/) | Standards and domain notes the design draws on |

### Quick answers

| Question | Document |
|---|---|
| Real-time or turn-based? | [15](design/15_TIME_AND_EXECUTION.md) — the engine is turn-based, the game is real-time-with-pause |
| What equipment exists, and what tech gates it? | [catalog/](catalog/CATALOG_INDEX.md) — 14 station sheets + the [tech tree](catalog/TECH_TREE.md) |
| Where do the map, basins and reservoirs come from? | [06](design/06_WORLD_AND_EXPLORATION.md) §5 — the eleven-step causal world generator; built in [R15](phases/R15_WORLD.md) |
| How does the environment affect operations? | [13](design/13_ENVIRONMENT.md) |
| Is HSE planned? | [14](design/14_HSE.md) — barrier model, leading indicators, two safety dimensions |
| How do subsystems affect each other? | [17](design/17_CROSS_IMPACT_MATRIX.md) — matrix, 30 named couplings, 9 feedback loops |
| What events exist? | [16](design/16_EVENT_MATRIX.md) |
| Challenges and missions, not just a campaign? | [18](design/18_GAME_MODES.md) — five modes on one objective system |
| Is this actually a game? | [20](design/20_PLAYER_DECISIONS.md) — 61 decisions, each passing a four-part test |
| Are we ready to write code? | [sdd/SDD_INDEX.md](sdd/SDD_INDEX.md) — after the owner gate: foundation SDDs first, then R1 |
| Is this only for engineers? | [18](design/18_GAME_MODES.md) §5b — **no**: reality levels like a flight sim. Fidelity × assists × forgiveness; the Advisor is the autopilot |
| How do time, events and coupling interact? | [21](design/21_INTEGRATION.md) — **lag is the difficulty**; slow couplings need leading indicators |
| What runs in what order inside one tick? | [03](design/03_ARCHITECTURE.md) §6 — 14 stages; event map in [21](design/21_INTEGRATION.md) §4 |
| If I change one document, what else breaks? | [22](design/22_DESIGN_COHERENCE.md) §2 — the change-impact map |
| Where is every binding rule listed? | [22](design/22_DESIGN_COHERENCE.md) §3 — L, IR, CI, INV, T, N in one registry |
| What does identifier `MB6` / `IR2` / `DHS3` mean? | [22](design/22_DESIGN_COHERENCE.md) §4 — the identifier scheme |

## Non-negotiables

These are constraints on every document and every future line of code.

1. **No stubs.** A declared member either does its job or does not exist.
2. **No fallbacks.** No collaborator has a default. Forgetting to supply one is
   a compile error, never a silently-wrong object.
3. **No legacy.** No compatibility shims, no "kept so old call sites work".
4. **Nothing is swallowed.** Every failure is logged, audited, and surfaced.
   `catch` never discards.
5. **Contract-first.** Every capability is an interface; every implementation is
   a plugin registered at composition time.
6. **One engine for the material.** Oil, gas, water and NGL move through one
   transport/processing engine, not per-fluid special cases.
7. **Standards-aligned.** Entity names and relationships follow PPDM where PPDM
   has an answer; deviations are recorded with a reason.
8. **Deterministic.** Same seed and same inputs produce the same world and the
   same numbers, on any machine.
9. **No slow trap is invisible.** Any coupling whose effect lands more than two
   years out publishes a leading indicator every tick, and every downward
   feedback loop has an entry alert that fires while at least two exits remain
   ([21](design/21_INTEGRATION.md) rules IR1–IRR4).
10. **Every crisis is explicable.** Every critical event carries a cause chain
    back to the decision that started it (rule IR6).
11. **Definition-driven, plugin-first — the moddability rule.** Every domain
    object — facility, well type, equipment tier, technology node, fiscal
    regime, environment profile, scenario — is instantiated from a JSON
    definition binding onto a contract; every behaviour is a plugin selected by
    name from content. Adding or rebalancing anything is a content edit;
    genuinely new behaviour is a new plugin plus the JSON that names it —
    **never an edit to existing engine code**. Definitions bind to closed,
    schema-validated datasheets, not untyped bags — so a modder's typo is a
    load error with a hint, not a silently ignored setting
    ([10](design/10_CONTENT_AND_UNITS.md) §1b, [SDD-004](sdd/SDD-004_CONTENT_PIPELINE.md)).
