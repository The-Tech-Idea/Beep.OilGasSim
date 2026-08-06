# C04 — Wellbore & Tubing

**Catalogue sheet** · chain position: the conduit ([04](../design/04_MATERIAL_AND_FLOW.md) stage 2) · phases: R6
**Authoring spec** for the content files of [10](../design/10_CONTENT_AND_UNITS.md) §2 — every row below becomes a `well-component`, `facility-unit`, `pipe-spec` or `information-source` entry with `requiresTech`/`availableFromEra` ([07](../design/07_TECHNOLOGY.md) §4b). Gates from [TECH_TREE](TECH_TREE.md). Eras E1 1950s–60s · E2 70s–80s · E3 90s–2000s · E4 2010s+ ([18](../design/18_GAME_MODES.md) §3.5). Capex bands relative ($–$$$$$).

Metallurgy is the slow bet: chosen at completion, judged years later by souring.

| Equipment (visible to the player) | Tier ladder | Tech gate | Era | Capex | Install (operation · duration) | What the datasheet changes |
|---|---|---|---|---|---|---|
| Tubing | carbon steel → 13Cr → duplex | 13Cr+: Sour-service metallurgy (duplex: tier) | E1→E3 | $→$$$ ×8 | tubing op · days | H2S/CO2 service envelope; corrosion severity factor; friction (ID) |
| Subsurface safety valve | one | — | E2 | $ | with tubing | Mandatory barrier element offshore |
| Permanent downhole gauge | memory → surface-readout | Downhole gauges | E2→E3 | $ | with tubing | Continuous Pwf — belief updates without shut-ins |
| Fibre (DAS/DTS) | one | Fibre monitoring | E4 | $$ | with tubing | Inflow profile visibility; leak detection; enables premium allocation |
| Wellhead & tree | pressure rating tiers | — | E1→E3 | $$ | completion op | Pressure envelope; barrier element |

**Couplings & notes**
- Metallurgy vs **souring** ([05](../design/05_SIMULATION_MODELS.md) §8): the DHS3 decision lives in this sheet.
- Gauges partially substitute build-up surveys — continuous Pwf, but average pressure still needs shut-ins.
