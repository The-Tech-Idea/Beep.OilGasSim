# C13 — Power & Utilities

**Catalogue sheet** · chain position: cross-cutting — tick stage 4 power balance · phases: R8
**Authoring spec** for the content files of [10](../design/10_CONTENT_AND_UNITS.md) §2 — every row below becomes a `well-component`, `facility-unit`, `pipe-spec` or `information-source` entry with `requiresTech`/`availableFromEra` ([07](../design/07_TECHNOLOGY.md) §4b). Gates from [TECH_TREE](TECH_TREE.md). Eras E1 1950s–60s · E2 70s–80s · E3 90s–2000s · E4 2010s+ ([18](../design/18_GAME_MODES.md) §3.5). Capex bands relative ($–$$$$$).

Nothing runs without it; everything competes for it.

| Equipment (visible to the player) | Tier ladder | Tech gate | Era | Capex | Install (operation · duration) | What the datasheet changes |
|---|---|---|---|---|---|---|
| Diesel genset | size ladder | — | E1 | $$ | construction · weeks | Baseline power; diesel OPEX; emissions |
| Gas genset | size ladder | — | E1–E2 | $$ | construction | Burns the fuel term; cheap where gas is stranded |
| Gas turbine | size ladder | Gas turbines | E2 | $$$ | construction | Field-scale power; heat-derated ([13](../design/13_ENVIRONMENT.md)) |
| Grid tie | one | — (env: grid reach) | E2 | $$$ | construction | Power without fuel gas; **frees gas for sale** |
| Electrification package | per-facility retrofit | Electrification | E4 | $$$$ | construction | Fuel term → grid; emissions down; ESG channel |
| Waste-heat recovery | one | Waste-heat recovery | E3 | $$ | construction | Treater/stabiliser heat from exhaust — fuel term down |
| Fuel-gas conditioning skid | one | — | E2 | $ | construction | Lets raw gas fuel turbines cleanly |
| Accommodation camp | tent → modular → permanent | — | E1→E2 | $$ | construction · weeks | Crew rotation cost ↓, fatigue factor ↓ at remote sites — the DHS-relevant lever |
| Warehouse & spares stock | one | — | E1 | $$ | construction | Spares lead time ↓ → repair durations ↓ ([SDD-007](../sdd/SDD-007_OPERATIONS_ENGINE.md) standby) |
| Operations base / workshop | one | — | E2 | $$ | construction | Local workover capability: intervention mobilisation ↓ |
| Helipad / airstrip | one | — | E2 | $$$ | construction | Adds an access mode ([13](../design/13_ENVIRONMENT.md)); emergency response time ↓ |

**Couplings & notes**
- Declared duty feeds the stage-4 balance ([R8](../phases/R8_FACILITIES.md) §2.5) — an ESP fleet without this sheet's rows takes the field down.
- Every gas-burning row appears in the conservation fuel term and the emissions ledger.
