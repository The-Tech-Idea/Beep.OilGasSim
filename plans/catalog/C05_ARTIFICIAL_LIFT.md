# C05 — Artificial Lift

**Catalogue sheet** · chain position: keeps dying wells flowing ([04](../design/04_MATERIAL_AND_FLOW.md) stage 2) · phases: R7
**Authoring spec** for the content files of [10](../design/10_CONTENT_AND_UNITS.md) §2 — every row below becomes a `well-component`, `facility-unit`, `pipe-spec` or `information-source` entry with `requiresTech`/`availableFromEra` ([07](../design/07_TECHNOLOGY.md) §4b). Gates from [TECH_TREE](TECH_TREE.md). Eras E1 1950s–60s · E2 70s–80s · E3 90s–2000s · E4 2010s+ ([18](../design/18_GAME_MODES.md) §3.5). Capex bands relative ($–$$$$$).

The flagship tier family — the worked ladder of [07](../design/07_TECHNOLOGY.md) §4b.3.

| Equipment (visible to the player) | Tier ladder | Tech gate | Era | Capex | Install (operation · duration) | What the datasheet changes |
|---|---|---|---|---|---|---|
| Rod pump | beam gen-1 → long-stroke | Rod pump | E1→E2 | $ | workover · 4 d | Displacement cap; depth range |
| Gas lift valves & mandrels | one | Gas lift | E1 | $ | workover · 3 d | Column density above valve; needs compressed gas ([C08](C08_GAS_PROCESSING.md)) |
| ESP | A → B → C (gas-handler) → D (PM) | ESP (C: High-temp/gassy · D: PM-motor) | E2→E4 | $$→$$$ | workover · 6–8 d | Head curve = **the flow**; gas/temp envelope; power draw; failure profile per tier |
| PCP | one → high-temp elastomer | PCP | E2→E3 | $$ | workover · 4 d | Viscous/sandy envelope |
| Velocity string | one | — | E2 | $ | workover · 2 d | Gas-well liquid loading remedy |

**Couplings & notes**
- ESP fleets drive the **power balance** ([R8](../phases/R8_FACILITIES.md) §2.5) — tier D exists to cut that draw.
- Tier C/D carry **early-generation failure profiles** — proven-vs-new is the DDV8 decision.
