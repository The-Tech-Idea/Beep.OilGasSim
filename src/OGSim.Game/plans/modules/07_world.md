> Source read in full: `src/OGSim.Composition/Modules.cs`.
> Part of the module review requested 2026-08-23. Nothing in the engine was
> changed to produce this — it records what is there.


# 07 — world

`internal sealed class WorldModule(IReadOnlyList<TerrainClassDefinition> terrainClasses, ContentId climateId, IReadOnlyList<FluidSystemDefinition> fluidSystems)`

## Manifest

| | |
|---|---|
| **provides** | `IWorldGenerator`, `WorldState` |
| **requires** | *(none)* |
| **ownsState** | `world.decisions` |
| **stages** | *(none)* — world generation runs once, at tick zero, not in the loop |

## Compose

```
Provide<IWorldGenerator>(new BasinWorldGenerator(terrainClasses, climateId, fluidSystems))
var world = new WorldState();   // EMPTY
Own(world)  Provide(world)
```

`WorldState` is composed **empty** and filled once by generation before the
first tick — because the field reads it (a well tied in has to know where its
prospect is) and a module cannot depend on something built after composition
finished.

## Functions and properties

| Member | What it does |
|---|---|
| `Chart(...)` | Called only when a survey is shot — this is what makes the map dark |
| `Shoot(block, risks)` | A block survey; records the block as shot |
| `WasShot(block)` | Saved, so surveys are not repeatable-for-free after a reload |
| `BlockAt` / `BlockCount` / `CentreOf` | The 4×4 = 16-block licence grid |
| `DeclareKnownField(field, volume)` | A scenario-declared field at D0 subtlety — **no exploration risk to be wrong about** |
| `ProspectFor(field)` | The prospect a declared field sits in |
| `HeaderAt(site)` | **Write-once** (`_header ??= site`) — where the header went is a decision, not a function of the seed |
| `DistanceToHeaderOf(...)` | Prices every later well's gathering line |

`world.decisions` is a saved key because of `HeaderAt`: unsaved, a reloaded
campaign re-sited the header at whichever field it next tied in.

## Dependencies and conditions it decides for itself

**None in `Compose`.**

## Content and Defaults consumed

`content/terrain-classes/*.json` — 6 files, each with construction and transport
cost factors, buildability, rig access, seismic ops, settlement and arable
weights.
`content/fluid-systems/*.json` — the generator **throws** if this list is empty.
`Defaults.Climate.Id` only; the profile itself goes to **environment**.
