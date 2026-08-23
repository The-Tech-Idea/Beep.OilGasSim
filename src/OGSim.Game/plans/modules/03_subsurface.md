> Source read in full: `src/OGSim.Composition/Modules.cs`.
> Part of the module review requested 2026-08-23. Nothing in the engine was
> changed to produce this — it records what is there.


# 03 — subsurface

`internal sealed class SubsurfaceModule()`

## Manifest

| | |
|---|---|
| **provides** | `IDriveMechanism`, `OGSim.Subsurface.SubsurfaceState` |
| **requires** | `FluidSystems`, `TickProduction` |
| **ownsState** | `subsurface.compartments` |
| **stages** | `MaterialBalance` order 0 |

It requires `FluidSystems`, **not** the single `IFluidPropertyModel`: a
compartment names its own fluid system, and this is where that name is resolved
to a model — per compartment rather than once for the field.

## Compose

1. `SolutionGasDrive()` → `Provide<IDriveMechanism>`
2. `SubsurfaceState(FluidSystems.ByContentId, drive, Defaults.SourCurve, Defaults.TheRock, Defaults.SouringReferencePpm, Defaults.MaxTickPressureDropFraction)` → **owned and provided**
3. `MaterialBalanceStage(state, () => production.Withdrawals)` at order 0

## The stage

**`MaterialBalanceStage`** — stage 6. Its whole body is
`state.CommitTick(withdrawalsForTick())`.

This is the month the reservoir actually loses pressure for what was produced.
Decline, the GOR climb past the bubble point, rising water cut, and pressure
support from an aquifer or injector all appear here.

**Solve and commit are separate stages on purpose**: stage 5 fills a shared
`TickProduction` buffer and stage 6 drains it, so **a failed solve commits
nothing**.

## Functions and properties

`SubsurfaceState` is the module's whole surface. The truth accessors are
`internal` — a player learns porosity, permeability and net pay only by paying
to measure them.

## Dependencies and conditions it decides for itself

**None in `Compose`.** No branch, no fallback.

## One dead parameter found

`Defaults.MaxTickPressureDropFraction` is validated in `SubsurfaceState`'s
constructor and threaded through `CommitTick` into
`ReservoirCompartment.CommitWithdrawal` — where **it is never read**. The limit
that actually binds is the drive's own `MaxTickVoidageFraction`. Law L3: a
member with no behaviour.

## Content and Defaults consumed

`Defaults.SourCurve`, `Defaults.TheRock`, `Defaults.SouringReferencePpm`,
`Defaults.MaxTickPressureDropFraction`. Indirectly `content/fluid-systems/`.
`content/rock-types/*.json` exists and is **not loaded** — no content kind is
registered for it.
