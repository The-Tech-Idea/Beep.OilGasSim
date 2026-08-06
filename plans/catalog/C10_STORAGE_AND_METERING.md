# C10 — Storage & Metering

**Catalogue sheet** · chain position: stage 8 + custody ([04](../design/04_MATERIAL_AND_FLOW.md) stages 8, 10) · phases: R8, R11
**Authoring spec** for the content files of [10](../design/10_CONTENT_AND_UNITS.md) §2 — every row below becomes a `well-component`, `facility-unit`, `pipe-spec` or `information-source` entry with `requiresTech`/`availableFromEra` ([07](../design/07_TECHNOLOGY.md) §4b). Gates from [TECH_TREE](TECH_TREE.md). Eras E1 1950s–60s · E2 70s–80s · E3 90s–2000s · E4 2010s+ ([18](../design/18_GAME_MODES.md) §3.5). Capex bands relative ($–$$$$$).

Ullage buys you time; meters get you paid.

| Equipment (visible to the player) | Tier ladder | Tech gate | Era | Capex | Install (operation · duration) | What the datasheet changes |
|---|---|---|---|---|---|---|
| Bolted tank | size ladder | — | E1 | $$ | construction · weeks | Cheap ullage; higher vapour loss |
| Welded / floating-roof tank | size ladder | — | E2 | $$$ | construction · months | Lower losses; export-scale ullage |
| Pressure vessels / spheres | NGL service | — | E2 | $$$ | construction | NGL/condensate storage |
| LACT unit | one | — | E2 | $$ | construction | Automated custody transfer |
| Orifice metering | one | — | E1 | $ | construction | Base uncertainty |
| Turbine meter | one | — | E2 | $ | construction | Uncertainty down |
| Coriolis meter | one | Precision metering | E3 | $$ | construction | **Mass** metering — tightest custody variance |
| Vapour recovery | crossref | Vapour recovery | E3 | $$ | ([C08](C08_GAS_PROCESSING.md)) | Losses → sales |

**Couplings & notes**
- Tank ullage is the coupling that shuts in fields (FV5/SC8) — this sheet is the insurance catalogue against a late tanker.
- Meter tier sets the audited measurement tolerance at custody (FD5).
