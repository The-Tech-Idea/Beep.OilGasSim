> Source read in full: `src/OGSim.Composition/Modules.cs`.
> Part of the module review requested 2026-08-23. Nothing in the engine was
> changed to produce this — it records what is there.


# 04 — wells

`internal sealed class WellsModule()`

## Manifest

| | |
|---|---|
| **provides** | `IInflowModel`, `IOutflowModel`, `OGSim.Wells.WellsState` |
| **requires** | `IFluidPropertyModel`, `IFlowElementRegistry` |
| **ownsState** | `wells.completions` |
| **stages** | *(none)* |

## Compose

```
Provide<IInflowModel>(new CompositeInflowModel(Defaults.Inflow))
Provide<IOutflowModel>(new HydrostaticFrictionOutflowModel(
    Defaults.Tubing, Density.FromSpecificGravity(0.85), lift: null))
WellsState(Require<IFlowElementRegistry>())   -> owned and provided
```

`IFluidPropertyModel` is declared as a requirement but **never `Require<>`d in
code**. It exists purely as a composer ordering edge — which is legitimate and
is stated as the reason elsewhere in the file.

## Stages

**None.** Everything wells do per tick happens inside `FieldModule`'s stages,
which call into `WellsState`.

## Functions and properties

`WellsState` — `Open(...)` registers a completion as a network source element;
`RefreshFromReservoir(...)` pushes this month's pressure, temperature, Rs and
water cut onto each completion **together**, so a well never solves at this
month's pressure and last month's GOR; `Capture`/`Restore` carry drilled depth,
shut-in flag, skin reduction and installed lift tier across a save.

## Dependencies and conditions it decides for itself

**None.**

## Two dead registrations found

`IInflowModel` and `IOutflowModel` are provided and **required by no manifest,
and resolved by nobody**. The live per-well models are constructed directly in
`ProductionLoop.Drill` and `Defaults.CompletionFor`. Registering a neutral model
here would change nothing — which means the published contract is currently
decorative.

## Static numbers found

`Density.FromSpecificGravity(0.85)` is written inline in `Compose`, not taken
from `Defaults` or content.

## Content and Defaults consumed

`Defaults.Inflow` (permeability, net pay, drainage area, wellbore radius,
viscosity, bubble point), `Defaults.Tubing`.
`content/wells/*.json` supplies the four lift tiers but is read by
`EngineBuilder` and handed to **field**, not here.
