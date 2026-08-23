> Source read in full: `src/OGSim.Composition/Modules.cs`.
> Part of the module review requested 2026-08-23. Nothing in the engine was
> changed to produce this — it records what is there.


# 06 — facilities

`internal sealed class FacilitiesModule(FacilityLadders ladders, ContentId startingState)`

## Manifest

| | |
|---|---|
| **provides** | `ISeparationModel`, `IHydraulicModel`, `SurfaceChain`, `FacilityLadders`, `PlantBuilder` |
| **requires** | `IFluidPropertyModel`, `IFlowElementRegistry` |
| **ownsState** | `facilities.units` |
| **stages** | *(none)* |

## Compose

1. `FixedEfficiencySeparationModel()` → `ISeparationModel`
2. `LiquidHydraulicModel(Density.FromSpecificGravity(0.85), new Viscosity(3e-3), new Length(0.0))` → `IHydraulicModel`
3. `PlantBuilder(ladders, separation, fluid, hydraulics, network)`
4. `new SurfaceChain()` — empty
5. **The condition** — see below
6. `Own(new FacilitiesState(chain, ladders, works))`, `Provide(ladders)`, `Provide(chain)`

## `SurfaceChain` — functions and properties

Thirteen nullable sockets, every one optional:

`Manifold` · `Flowline` · `Separator` · `Custody` · `Treater` · `GasPlant` ·
`Flare` · `Disposal` · `Intake` · `Tank` · `OffSpecSink` · `Compressor` ·
`PumpStation`

| Member | What it does |
|---|---|
| `Install(T built)` × 13 | One overload per element type, so a caller cannot install a separator into the tank slot |
| `Wire(IFlowElementRegistry)` | Makes the twelve edges, **each exactly once**, when its second end appears |
| `Slots` | `Manifold?.Slots ?? 0` — honestly zero with no header |
| `MeteredPoints` | `Custody is null ? [] : [Custody.Id]` |
| `NameOf(element)` | "separator", "custody-meter", "gathering-3", … |
| `TheField` (static) | `EntityRef(Field, 1)` — what an order names when there is no element to name |
| `NothingToUpgrade(named)` (static) | The refusal every install returns on bare ground |

The twelve edges, in order: manifold→flowline, flowline→separator,
separator·liquid→pump station, pump station→treater, treater→custody,
separator·gas→compressor, compressor→gas plant, gas plant·reject→flare,
separator·water→disposal, intake→disposal·import, custody·on-spec→tank,
custody·reject→off-spec sink.

`Edge` refuses to connect unless **both** ends exist, and a `_wired[12]` flag
array makes calling `Wire` after every install safe.

## Dependencies and conditions it decides for itself

**This module holds the clearest example in the engine.**

| Where | Condition |
|---|---|
| `Compose` | `if (startingState == Defaults.OpeningPosition) works.Commission(chain); else if (startingState != Defaults.BareGround) throw InvariantFault(...)` |

A module branching on a content id to decide whether the company owns a plant.
The refusal on an unknown value is right; the branch itself is what a dependency
manager should own.

## Static numbers found

`Density.FromSpecificGravity(0.85)` and `new Viscosity(3e-3)` inline in
`Compose`.

## Content and Defaults consumed

`content/facilities/*.json` — 19 rungs across 8 ladders, all read.
`Defaults.OpeningPosition`, `Defaults.BareGround`, `Defaults.FirstGatheringLine`,
and through `PlantBuilder.Commission` the thirteen element ids plus
`MaterialCount`, `SalesSpec`, `MeasureStream`, `WaterOrdinal`,
`SurfaceOilDensity`, `GasCompressibilityFactor`, `FlareCapacity`,
`FlareCombustionEfficiency`, `Disposal`, `DisposalPressure`,
`DisposalFormationPressure`, `Flowline`, `FlowlineRating`, `SurfaceAmbient`.
