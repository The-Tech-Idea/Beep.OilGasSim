# C01 — Exploration & Surveys

**Catalogue sheet** · chain position: before the chain — information acquisition ([06](../design/06_WORLD_AND_EXPLORATION.md) §3) · phases: R14
**Authoring spec** for the content files of [10](../design/10_CONTENT_AND_UNITS.md) §2 — every row below becomes a `well-component`, `facility-unit`, `pipe-spec` or `information-source` entry with `requiresTech`/`availableFromEra` ([07](../design/07_TECHNOLOGY.md) §4b). Gates from [TECH_TREE](TECH_TREE.md). Eras E1 1950s–60s · E2 70s–80s · E3 90s–2000s · E4 2010s+ ([18](../design/18_GAME_MODES.md) §3.5). Capex bands relative ($–$$$$$).

The kit that buys variance reduction — and, at the tier boundaries, buys **detectability classes** (D0–D3, [06](../design/06_WORLD_AND_EXPLORATION.md) §2.3).

| Equipment (visible to the player) | Tier ladder | Tech gate | Era | Capex | Install (operation · duration) | What the datasheet changes |
|---|---|---|---|---|---|---|
| Gravity / magnetic survey kit | one | — | E1 | $$ | survey op · weeks | Basin shape, depth to basement priors |
| 2-D seismic crew | land · marine | 2-D seismic | E1 | $$$ | survey op · months | Structure priors; **opens D0** |
| 3-D seismic crew | streamer → node array | 3-D seismic (node array: tier) | E2→E4 | $$$$–$$$$$ | survey op · months | Trap geometry sharp; **opens D1**; node array: better in obstructed areas |
| Seismic attribute workstation | one | Seismic attributes | E3 | $$ | interpretation op · weeks | **Opens D2** on existing 3-D data |
| PSDM reprocessing | service | Pre-stack depth migration | E3 | $$$ | reprocessing op · months — **no field work** | **Opens D3** on data already shot |
| Wireline logging unit | suite gen-1 → gen-2 | — | E1→E3 | $$ | per-well · days | Porosity/saturation error model per suite |
| Coring kit | one | — | E1 | $$ | per-well · days | Best point accuracy |
| Well test spread | surface → downhole gauges | — | E1→E3 | $$$ | per-well · days | Permeability/skin error; **flares during test** |
| Build-up gauge | mechanical → digital quartz | — | E1→E3 | $ | shut-in survey · days | Reservoir pressure accuracy — the p/Z point quality |

**Couplings & notes**
- Marine crews need a **weather window** ([13](../design/13_ENVIRONMENT.md) §3.1); jungle/swamp multiplies land-crew cost.
- Re-screening held acreage with a new tier prices as a survey; **re-processing owned data is far cheaper** ([06](../design/06_WORLD_AND_EXPLORATION.md) §2.3a).
- Well tests defer production and flare gas — their cost is partly physical.
