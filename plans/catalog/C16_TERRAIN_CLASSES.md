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

## Amendment — the datasheet schema, pinned (found building `TerrainClassContentKind`)

The block above shows one worked example and left three things a loader has to
decide silently: whether `heightBand`/`slopeBand` are single values or lists,
what `buildable` holds beyond a bare `true`, and how a generator picks one
class when more than one row's bands could match a cell. Pinned here rather
than guessed by the implementation (F-1).

- **`heightBand`, `slopeBand` and `climateBands` are all JSON arrays**, from
  three closed vocabularies — `heightBand`: `low`/`mid`/`high`;
  `slopeBand`: `flat`/`moderate`/`steep`; `climateBands`: `arid`/`wet`/`any`.
  The worked example's singular `"heightBand": "high"` is corrected to
  `["high"]` — arrays throughout, the same convention `prerequisites` and
  `routes` already use elsewhere in this content pipeline, so a class whose
  physical range spans two bands (desert is `low–mid`) has somewhere to say
  so without a second schema.
- **`buildable` is a three-state string, not a bool**: `"yes"`, `"piled-only"`,
  `"no"` — the table's own Buildable column already has three values
  (`yes` ×5, `piled only` ×1) and a bare `true`/`false` cannot hold the
  middle one. The worked example's `"buildable": true` is corrected to
  `"buildable": "yes"`.
- **`rigAccess` and `seismicOps` are free-text strings, not `ContentId`s.**
  They name a crew or rig CLASS ([C01](C01_EXPLORATION_AND_SURVEYS.md),
  [C02](C02_DRILLING_AND_RIGS.md)) for a human or a future gate to read, and
  the table's own values (`standard + water logistics`, `marsh/barge`) use
  characters `ContentId`'s kebab-case charset forbids. No catalog of rig/crew
  classes exists as loadable content yet, so there is nothing to validate
  against — this field is descriptive until one does.

### How the shipped generator picks a class, until a world-template exists

`BasinWorldGenerator` classifies by **height and slope only** — never
`climateBands` — because no per-cell climate or aridity signal is generated
anywhere in this composition (finding 242): `IWorldSink.AddClimateRegion` is
never called, and the composed engine runs one hand-authored climate
everywhere regardless of location. Inventing a throwaway noise field just to
feed terrain classification would create a second, unrelated climate signal
that a real climate generator would later have to reconcile with or replace —
the exact "two owners of one fact" shape finding 242 already found once in
this same area. So the four classes whose `climateBands` includes `"any"`
(plains, hills, mountain, rock-plateau) are reachable; **desert and swamp are
not**, until real per-cell climate exists to be their other input. Both still
ship as validated content — the vocabulary is loaded and checked against every
row above, the same way sixty-five technology nodes ship with `Effects: []`
before anything needs one (SDD-005's R20d.10e amendment).

The (height × slope) grid is filled **exhaustively** by the generator's own
hand-authored cut table (analogous to `Defaults.Eras` — content-shaped, not
yet content-driven, pending the world-template system §6's mods note above
already anticipates), so every land cell reaches exactly one class:

| | Flat | Moderate | Steep |
|---|---|---|---|
| **Low** | `plains` | `hills` | `hills` |
| **Mid** | `rock-plateau` | `hills` | `mountain` |
| **High** | `rock-plateau` | `mountain` | `mountain` |

This is a widening of two rows' own declared bands, both cited here so the
JSON and this table never drift apart: `rock-plateau`'s `heightBand` is
`["mid", "high"]` rather than `["mid"]` alone (a flat plateau at altitude is
still a rock-plateau, not a new class), and `mountain`'s `slopeBand` is
`["moderate", "steep"]` rather than `["steep"]` alone (the moderate shoulder of
a high massif is still mountain terrain). Sea (elevation below zero) is never
classified — [C16](#c16---terrain-classes)'s own rule, "sea is not a class,"
holds structurally: `GeneratedTerrain.ClassByCell` carries `-1` for a sea cell,
never an index into `Classes`.
