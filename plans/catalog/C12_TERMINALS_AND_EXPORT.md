# C12 — Terminals & Export

**Catalogue sheet** · chain position: the revenue end ([04](../design/04_MATERIAL_AND_FLOW.md) stage 10) · phases: R11
**Authoring spec** for the content files of [10](../design/10_CONTENT_AND_UNITS.md) §2 — every row below becomes a `well-component`, `facility-unit`, `pipe-spec` or `information-source` entry with `requiresTech`/`availableFromEra` ([07](../design/07_TECHNOLOGY.md) §4b). Gates from [TECH_TREE](TECH_TREE.md). Eras E1 1950s–60s · E2 70s–80s · E3 90s–2000s · E4 2010s+ ([18](../design/18_GAME_MODES.md) §3.5). Capex bands relative ($–$$$$$).

Where the rhythm of tankers meets the arithmetic of ullage.

| Equipment (visible to the player) | Tier ladder | Tech gate | Era | Capex | Install (operation · duration) | What the datasheet changes |
|---|---|---|---|---|---|---|
| Terminal tank farm | size ladder | — | E1 | $$$$ | construction · years | Export buffer; parcel building |
| Fixed berth + loading arms | rate tiers | — | E1 | $$$$ | construction | Loading rate; laytime exposure |
| SPM buoy | one | Marine loading | E2 | $$$ | construction | Export **without a harbour** — opens shallow coasts; weather-sensitive |
| Custody metering skid | meter tiers | ([C10](C10_STORAGE_AND_METERING.md)) | E1→E3 | $$ | construction | The revenue event's accuracy |
| LNG jetty & loading | expansion | LNG liquefaction | E3–E4 | $$$$$ | construction | Marine gas export |
| Heavy-lift / decom spread | service | Heavy lift | E3 | $$$$ rented | decom op · months | Offshore abandonment executable at all |

**Couplings & notes**
- Berth occupancy + storm closures drive SC8; port **water depth** caps tanker class → parcel economics ([13](../design/13_ENVIRONMENT.md) §3.5).
- Third-party terminals (H10) are the rent-vs-build alternative to every row here.
