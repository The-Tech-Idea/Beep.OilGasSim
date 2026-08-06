# C09 — Water Handling & Injection

**Catalogue sheet** · chain position: stage 7 of [04](../design/04_MATERIAL_AND_FLOW.md) · phases: R10
**Authoring spec** for the content files of [10](../design/10_CONTENT_AND_UNITS.md) §2 — every row below becomes a `well-component`, `facility-unit`, `pipe-spec` or `information-source` entry with `requiresTech`/`availableFromEra` ([07](../design/07_TECHNOLOGY.md) §4b). Gates from [TECH_TREE](TECH_TREE.md). Eras E1 1950s–60s · E2 70s–80s · E3 90s–2000s · E4 2010s+ ([18](../design/18_GAME_MODES.md) §3.5). Capex bands relative ($–$$$$$).

The late-game's biggest equipment bill.

| Equipment (visible to the player) | Tier ladder | Tech gate | Era | Capex | Install (operation · duration) | What the datasheet changes |
|---|---|---|---|---|---|---|
| Skim tank | size ladder | — | E1 | $$ | construction | Bulk oil-in-water removal |
| Hydrocyclone bank | size ladder | Produced-water treating | E3 | $$ | construction | Compact deep cleanup to discharge/injection spec |
| Polishing filters | one | — | E2 | $ | construction | Injectivity protection (solids) |
| Injection pump station | pressure tiers | Waterflood | E2 | $$$ | construction | Injection rate vs compartment pressure |
| Disposal wellhead & tree | one | — | E1 | $$ | completion op | Injectivity; the INV-checked disposal path |
| Water-source infrastructure | intake / source wells | — | E1 | $$ | construction | Make-up water for voidage replacement — early flood needs more than produced water |

**Couplings & notes**
- Injectivity **declines** (R10-V4) — remediation is a recurring workover, not a one-time build.
- Disposal volume drives induced seismicity risk (R23.9); the sensitivity map prices your options.
