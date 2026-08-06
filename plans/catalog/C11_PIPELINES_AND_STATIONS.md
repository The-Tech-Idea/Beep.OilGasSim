# C11 — Pipelines & Stations

**Catalogue sheet** · chain position: stage 9 of [04](../design/04_MATERIAL_AND_FLOW.md) · phases: R11
**Authoring spec** for the content files of [10](../design/10_CONTENT_AND_UNITS.md) §2 — every row below becomes a `well-component`, `facility-unit`, `pipe-spec` or `information-source` entry with `requiresTech`/`availableFromEra` ([07](../design/07_TECHNOLOGY.md) §4b). Gates from [TECH_TREE](TECH_TREE.md). Eras E1 1950s–60s · E2 70s–80s · E3 90s–2000s · E4 2010s+ ([18](../design/18_GAME_MODES.md) §3.5). Capex bands relative ($–$$$$$).

Capacity emerges from hydraulics — every row changes an input to that.

| Equipment (visible to the player) | Tier ladder | Tech gate | Era | Capex | Install (operation · duration) | What the datasheet changes |
|---|---|---|---|---|---|---|
| Line pipe | X52 → X65 → X70+coating | High-strength linepipe (per grade) | E1→E3 | $/km ladder | pipeline lay · months | Pressure rating (capacity via hydraulics); roughness; corrosion allowance |
| Pump station | power tiers | — | E1→E2 | $$$ | construction | Liquid head restored mid-line |
| Compressor station | crossref tiers | ([C08](C08_GAS_PROCESSING.md)) | E2→E4 | $$$$ | construction | Gas line inlet pressure |
| Pigging kit | utility → intelligent | Inline inspection (intelligent) | E2→E3 | $$ | pig run op · days | Wax removal; **integrity data → belief about pipe condition** |
| Insulation / burial systems | one | — | E2 | $$/km | with lay | Hydrate/wax margin; ice-scour protection |
| Inhibitor injection skid | one | — | E2 | $ | construction | Continuous hydrate/corrosion chemical OPEX |
| Drag-reducing agent skid | one | Flow improvers | E3 | $ | construction | Liquid capacity uplift without steel |

**Couplings & notes**
- Loop-vs-pump-vs-DRA is the debottlenecking trilemma the report will price for you.
- Grade ladder is the classic era story: the same corridor re-laid in X70 carries a different field.
