# C03 — Completion & Stimulation

**Catalogue sheet** · chain position: reservoir → wellbore connection ([04](../design/04_MATERIAL_AND_FLOW.md) stage 1) · phases: R6, R12
**Authoring spec** for the content files of [10](../design/10_CONTENT_AND_UNITS.md) §2 — every row below becomes a `well-component`, `facility-unit`, `pipe-spec` or `information-source` entry with `requiresTech`/`availableFromEra` ([07](../design/07_TECHNOLOGY.md) §4b). Gates from [TECH_TREE](TECH_TREE.md). Eras E1 1950s–60s · E2 70s–80s · E3 90s–2000s · E4 2010s+ ([18](../design/18_GAME_MODES.md) §3.5). Capex bands relative ($–$$$$$).

Where skin is created and destroyed, and where the **Tight access class** unlocks.

| Equipment (visible to the player) | Tier ladder | Tech gate | Era | Capex | Install (operation · duration) | What the datasheet changes |
|---|---|---|---|---|---|---|
| Perforating guns | gen-1 → deep-penetration | — | E1→E3 | $ | within completion op | Perforation efficiency; initial skin |
| Acidising unit | one | — | E1 | $$ | workover · days | Removes damage/scale skin toward 0 |
| Frac spread | single-stage → multi-stage | Hydraulic fracturing (multi-stage: tier) | E3→E4 | $$$$ **rentable** | frac op · weeks | Negative skin −3..−6; **opens Tight class**; needs a water source |
| Sand screens / gravel pack | one | Sand control | E2 | $$ | within completion | Enables high drawdown in soft rock (SD5 expansion) |
| Smart completion | sliding sleeves + gauges | Smart completions | E3 | $$$ | completion op · days | **Zonal control without a workover** — DPR4 gets a remote lever |
| Production packers | standard → HP | — | E1→E3 | $ | within completion | Isolation rating; barrier element |

**Couplings & notes**
- Frac spreads are the canonical **service-route rental** ([07](../design/07_TECHNOLOGY.md) §4b.4).
- Multi-stage frac converts shelved Tight discoveries the day it unlocks — the era campaign's hinge ([06](../design/06_WORLD_AND_EXPLORATION.md) §2.3).
