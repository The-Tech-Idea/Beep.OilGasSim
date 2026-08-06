# C16 — Terrain Classes

**Catalogue sheet** · world surface ([SDD-010](../sdd/SDD-010_WORLD_GENERATION.md) §3 terrain step) · phases: R15 (generation), R3 (loading)
**Authoring spec** for `terrain-class` content entries (a plain `ContentDefinition` kind — terrain is a **world fact, not an unlockable**: no tier ladder, no `requiresTech`, no `Fits`). The world generator assigns one class per cell from the `(height, slope, climate)` formation table below; the class datasheet is where terrain touches gameplay. `GeneratedTerrain.Classes` carries these ids; `ClassByCell` indexes them.

**Sea is not a class**: water is elevation < 0 in the shared heightfield (bathymetry — [SDD-010](../sdd/SDD-010_WORLD_GENERATION.md) §3 hydrology). Offshore behaviour comes from `WaterDepthClass` on `AccessRequirements`, not from a terrain entry.

## Shipped classes

| Class id | Forms where (height · slope · climate) | Constr. cost × | Transport cost × (A*) | Buildable | Rig access | Seismic ops | Settlement weight | Notes |
|---|---|---|---|---|---|---|---|---|
| `plains` | low · flat · any | 1.0 | 1.0 | yes | standard | standard crew | high (arable) | The baseline every factor is relative to |
| `hills` | mid · moderate · any | 1.4 | 1.8 | yes | standard | standard crew | mid | Roads wind: transport × exceeds construction × |
| `mountain` | high · steep · any | 3.5 | 6.0 | yes | heli-supported | mountain crew | low | The expensive-twice cell: capex AND every resupply |
| `desert` | low–mid · flat · arid | 1.2 | 1.3 | yes | standard + water logistics | standard crew | very low | Cheap to build, hostile to crews: water/heat couple to HSE and standby |
| `rock-plateau` | mid · flat · arid/any | 1.8 | 1.5 | yes | standard | hard-rock crew | very low | Good foundations, brutal seismic coupling |
| `swamp` | low · flat · wet | 2.8 | 4.5 | piled only | marsh/barge | swamp buggy crew | very low | Everything on piles; spill sensitivity couples to [SensitivityZone](../sdd/SDD-010_WORLD_GENERATION.md) density |

## Datasheet block (closed, kind-specific — SDD-004 §6 rule)

```json
{ "kind": "terrain-class", "id": "mountain",
  "formation": { "heightBand": "high", "slopeBand": "steep", "climateBands": ["any"] },
  "constructionCostFactor": 3.5,
  "transportCostFactor": 6.0,
  "buildable": true,
  "rigAccess": "heli-supported",
  "seismicOps": "mountain-crew",
  "settlementWeight": 0.1,
  "arableWeight": 0.0 }
```

`formation` bands name rows of the world-template's height/slope/climate cut
tables — the template owns the numbers; the class owns the identity and its
gameplay factors. One cell, one class, deterministically.

**Couplings & notes**
- `rigAccess`/`seismicOps` name **crew/rig classes that are themselves gated
  equipment** ([C01](C01_EXPLORATION_AND_SURVEYS.md) survey crews,
  [C02](C02_DRILLING_AND_RIGS.md) rig classes) — the terrain entry is never
  gated, but operating *on* it can be: a mountain cell without the heli-support
  tech is a `GateFail` naming the missing rig class, not a hidden cost bump.
- `constructionCostFactor` multiplies facility/road capex at siting;
  `transportCostFactor` is the A* edge cost ([SDD-010](../sdd/SDD-010_WORLD_GENERATION.md) §3 transport) —
  both flow into the cost-by-cause audit, so "why was this pad so expensive?"
  answers itself.
- Climate is orthogonal: these factors never encode weather. A desert cell in a
  monsoon region gets the region's severity curve on top ([SDD-016](../sdd/SDD-016_ENVIRONMENT_RUNTIME.md) §1).
- Mods add classes (tundra, jungle, permafrost) as new entries plus world-template
  cut-table rows — zero engine code, the [23 §5](../design/23_FUNCTION_MATRIX.md) moddability promise.
