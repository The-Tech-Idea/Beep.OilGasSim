# C07 — Separation & Oil Treating

**Catalogue sheet** · chain position: stages 4–5 of [04](../design/04_MATERIAL_AND_FLOW.md) · phases: R8
**Authoring spec** for the content files of [10](../design/10_CONTENT_AND_UNITS.md) §2 — every row below becomes a `well-component`, `facility-unit`, `pipe-spec` or `information-source` entry with `requiresTech`/`availableFromEra` ([07](../design/07_TECHNOLOGY.md) §4b). Gates from [TECH_TREE](TECH_TREE.md). Eras E1 1950s–60s · E2 70s–80s · E3 90s–2000s · E4 2010s+ ([18](../design/18_GAME_MODES.md) §3.5). Capex bands relative ($–$$$$$).

Meeting the crude spec: BS&W, salt, vapour pressure.

| Equipment (visible to the player) | Tier ladder | Tech gate | Era | Capex | Install (operation · duration) | What the datasheet changes |
|---|---|---|---|---|---|---|
| 2-phase separator | size ladder | 2-phase separation | E1 | $$ | construction · months | Gas/liquid split; dual capacities |
| 3-phase separator | size ladder | 3-phase separation | E1–E2 | $$$ | construction · months | + water leg; carry-over per size |
| Multi-stage train | 2 → 3 stages | Multi-stage separation | E2 | $$$ | construction · months | **More stock-tank oil from the same wells** — stage pressures content-tunable |
| Free-water knockout | one | — | E2 | $$ | construction | Cheap bulk water removal ahead of treating |
| Heater-treater | size ladder | — | E1 | $$ | construction | BS&W to spec; fuel draw ([04](../design/04_MATERIAL_AND_FLOW.md) §7 fuel term) |
| Desalter | one | — | E2 | $$ | construction | Salt spec for export crude |
| Stabiliser | one | — | E2 | $$$ | construction | RVP spec; recovered light ends to gas train |
| Slug catcher | one | — | E2 | $$ | construction | Absorbs multiphase-line slugs |

**Couplings & notes**
- Every unit's two capacities can be **the field bottleneck** — this sheet is what the bottleneck report tells you to buy.
- Treater/stabiliser heat = fuel gas = the conservation fuel term.
