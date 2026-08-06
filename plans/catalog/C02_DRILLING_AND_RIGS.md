# C02 — Drilling & Rigs

**Catalogue sheet** · chain position: creates wellbores ([02](../design/02_DOMAIN_MODEL.md) §3) · phases: R12
**Authoring spec** for the content files of [10](../design/10_CONTENT_AND_UNITS.md) §2 — every row below becomes a `well-component`, `facility-unit`, `pipe-spec` or `information-source` entry with `requiresTech`/`availableFromEra` ([07](../design/07_TECHNOLOGY.md) §4b). Gates from [TECH_TREE](TECH_TREE.md). Eras E1 1950s–60s · E2 70s–80s · E3 90s–2000s · E4 2010s+ ([18](../design/18_GAME_MODES.md) §3.5). Capex bands relative ($–$$$$$).

Rigs are contracted equipment tiers; the depth/water-depth/pressure **access classes** live here.

| Equipment (visible to the player) | Tier ladder | Tech gate | Era | Capex | Install (operation · duration) | What the datasheet changes |
|---|---|---|---|---|---|---|
| Land rig | L1 1.5 km → L2 3 km → L3 5 km | L3: Deep drilling | E1→E2 | $$$/mo day-rate | mobilise op · weeks | Depth envelope; day rate; move time |
| Jack-up rig | ≤120 m water | Offshore operations | E2 | $$$$/mo | mobilise · weeks | **Opens shallow-water class** (EV2 scope) |
| Semi-sub / DP drillship | expansion tiers | Deepwater operations | E3–E4 | $$$$$/mo | mobilise · months | **Deepwater class** — flagged expansion |
| Directional package | MWD → rotary steerable | Directional (RSS: tier) | E2→E3 | $$ | rented into the drill op | Trajectory control; horizontal reach |
| MPD package | one | Managed pressure drilling | E3 | $$$ | rented | **Opens HPHT class**; blowout threat rate down |
| HPHT wellhead & BOP stack | rating tiers | Managed pressure drilling | E3 | $$$ | with the rig | Pressure/temperature envelope |
| Winterisation package | one | Arctic operations | E2 | $$$ | refit op · weeks | Rig usable in arctic window ([13](../design/13_ENVIRONMENT.md)) |
| Casing & cementing | standard | — | E1 | $$ per string | within drill op | Integrity baseline; barrier element |

**Couplings & notes**
- Rig class validation is at **scheduling** ([07](../design/07_TECHNOLOGY.md) §2c) — a jack-up commanded into deep water is rejected naming the class.
- Day rates ride the cost-inflation cycle (ED4): booms price rigs out exactly when you want them.
