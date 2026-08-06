# C14 — Monitoring & Integrity

**Catalogue sheet** · chain position: cross-cutting — feeds beliefs, barriers and maintenance · phases: R14, R18, R23
**Authoring spec** for the content files of [10](../design/10_CONTENT_AND_UNITS.md) §2 — every row below becomes a `well-component`, `facility-unit`, `pipe-spec` or `information-source` entry with `requiresTech`/`availableFromEra` ([07](../design/07_TECHNOLOGY.md) §4b). Gates from [TECH_TREE](TECH_TREE.md). Eras E1 1950s–60s · E2 70s–80s · E3 90s–2000s · E4 2010s+ ([18](../design/18_GAME_MODES.md) §3.5). Capex bands relative ($–$$$$$).

The equipment that turns leading indicators from design promise into hardware.

| Equipment (visible to the player) | Tier ladder | Tech gate | Era | Capex | Install (operation · duration) | What the datasheet changes |
|---|---|---|---|---|---|---|
| SCADA / telemetry | one | Telemetry / SCADA | E2 | $$$ | rollout op · months | Remote readings & chokes; alert latency down |
| Condition monitoring kit | vibration/temp per asset class | Condition monitoring | E3 | $$ | install per asset | **Enables condition-based maintenance** (R18 §2.4) |
| Predictive analytics | one | Predictive maintenance | E4 | $$$ | rollout | Failure hazard visibility horizon extends |
| Intelligent pigs | crossref | Inline inspection | E3 | $$ | ([C11](C11_PIPELINES_AND_STATIONS.md)) | Pipe-wall belief; barrier evidence |
| Corrosion coupons & probes | one | — | E1 | $ | routine op | Cheap corrosion-rate belief |
| LDAR kit | handheld → continuous | Leak detection | E4 | $$ | survey op / rollout | **Methane intensity measured** — the pays-for-itself mechanic (HS7) |
| Gas & fire detection | per facility | — | E2 | $$ | construction | Mitigating barrier hardware (14 §2) |

**Couplings & notes**
- This sheet is [14](../design/14_HSE.md)'s leading indicators **as purchasable objects** — barrier strength derives from what you bought and maintained.
- Monitoring tiers gate maintenance strategies; without them condition-based is not selectable (R18).
