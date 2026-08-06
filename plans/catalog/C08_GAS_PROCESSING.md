# C08 — Gas Processing

**Catalogue sheet** · chain position: stage 6 of [04](../design/04_MATERIAL_AND_FLOW.md) · phases: R9
**Authoring spec** for the content files of [10](../design/10_CONTENT_AND_UNITS.md) §2 — every row below becomes a `well-component`, `facility-unit`, `pipe-spec` or `information-source` entry with `requiresTech`/`availableFromEra` ([07](../design/07_TECHNOLOGY.md) §4b). Gates from [TECH_TREE](TECH_TREE.md). Eras E1 1950s–60s · E2 70s–80s · E3 90s–2000s · E4 2010s+ ([18](../design/18_GAME_MODES.md) §3.5). Capex bands relative ($–$$$$$).

The capital-hungry chain; every unit is a spec-gate remover.

| Equipment (visible to the player) | Tier ladder | Tech gate | Era | Capex | Install (operation · duration) | What the datasheet changes |
|---|---|---|---|---|---|---|
| Reciprocating compressor | frame sizes | Reciprocating compression | E1 | $$$ | construction · months | Ratio/stage ~3; fuel gas or power draw |
| Centrifugal compressor | frame sizes → multi-stage | Centrifugal compression | E2→E3 | $$$$ | construction | Throughput; staging as Pr falls |
| Electric-drive compression | retrofit / new | Electrification | E4 | $$$$ | construction | Fuel term → power; emissions down; **frees gas for sale** |
| Glycol (TEG) dehydrator | size ladder | Glycol dehydration | E1–E2 | $$ | construction | Water dewpoint to pipeline spec |
| Molecular sieve | one | Molecular sieve | E3 | $$$ | construction | Deep dewpoint — **required for LNG/cryo** |
| Amine sweetening unit | size ladder | Amine sweetening | E2 | $$$ | construction | H2S/CO2 to spec; **opens Sour sales**; sulphur by-product |
| Sulphur recovery unit | one | Sulphur recovery | E2 | $$$ | construction | Sulphur product instead of acid-gas flare |
| NGL plant | JT skid → cryogenic turboexpander | NGL extraction (cryo: Cryogenic recovery) | E2→E3 | $$$→$$$$$ | construction | C2+ recovery fraction; the price-spread bet |
| Flare system | tips & KO drum | — | E1 | $ | construction | Disposal of last resort; the capped path |
| Vapour recovery unit | one | Vapour recovery | E3–E4 | $$ | construction | Tank vapours → sales; methane intensity down |
| LNG train | mini → full (expansion) | LNG liquefaction | E3–E4 | $$$$$ | mega-construction · years | Marine gas exit; needs mol-sieve + cryo upstream |

**Couplings & notes**
- Heat **derates** every compressor ([13](../design/13_ENVIRONMENT.md) §3.3) — desert capacity dips each summer.
- Gas-driven frames burn the fuel term; electric-drive moves it to the power balance ([C13](C13_POWER_AND_UTILITIES.md)).
