> Source read in full: `src/OGSim.Composition/Modules.cs`, plus the types this
> module composes. Part of the module review requested 2026-08-23. Nothing in the
> engine was changed to produce this.


# 09 — information

`internal sealed class InformationModule()`

## Manifest

| | |
|---|---|
| **provides** | `IBeliefStore`, `IObservationModel`, `ObservationSampler`, `ProspectRisks`, `RivalRoster` |
| **requires** | `IAuditTrail`, `IRandomSource`, `SimulationClock`, `WorldState`, `SubsurfaceState` |
| **ownsState** | `information.beliefs`, `information.prospect-risk`, `company.rivals` |
| **stages** | `Company` order 5 |

It owns a key in the `company.` namespace (`company.rivals`) despite being the
information module.

## Compose

1. `ISimulationClock clock = Require<SimulationClock>()` — held as the interface
   so this module **cannot move the clock** even by mistake
2. `BeliefStore(audit, Defaults.SigmaFloorFor, () => clock.Date)` — owned, provided
3. `RegionalObservationModel(world)` → `IObservationModel`
4. `ProspectRisks(Defaults.ExplorationPrior)` — owned, provided
5. `ObservationSampler(model, exploration stream, measurement stream, audit)`
6. `RivalRoster()` — **composed empty**, owned, provided
7. `RivalExplorationStage(...)` at order 5

## Why the clock matters here

`AsOf` is one of five fields a belief projects. This once passed a literal epoch,
so every belief claimed to have been learned in January 1965 — including ones a
company spent forty years buying. Nothing caught it because a constant is
perfectly self-consistent.

## The stage

**`RivalExplorationStage`** — stage 11, order 5. Every six ticks each rival
drills the prospect it values highest; the result is published into the
**player's** belief store as a widened observation. A rival that gets there first
also removes the prospect from the pool.

## Functions and properties

**`RivalRoster`** (`RivalExploration.cs`)

| Member | |
|---|---|
| `Rivals` | the roster, sealed after generation |
| `HasExplored(prospect)` / `MarkExplored(prospect)` | who got there first |
| `SealRoster(rivals)` | filled by `EngineBuilder.SealRivals`, not by `Compose` |
| `Key` = `company.rivals`, `SchemaVersion` 1 | |

`RivalExplorationStage.Execute` short-circuits on an empty roster, so a build
with `RivalCount = 0` costs nothing.

**`ProspectRisks`** — five Beta factors per prospect, `Source`/`Reservoir`/`Seal`
shared across a play, so **one dry hole re-prices every prospect on the same
system**.

## Dependencies and conditions it decides for itself

**None in `Compose`.** The roster being empty is a composed fact, not a branch.

## Content and Defaults consumed

`Defaults.SigmaFloorFor`, `DriftPerYearFor`, `ExplorationPrior`,
`TrapConfidenceOf`, `Rivals` (three personalities),
`RivalExplorationCadenceTicks`, `RivalMinimumCapacityCubicMetres`,
`RivalCapacityBeliefSigma`, `RivalDiscoverySigma`, `RivalDisclosureExtraSigma`,
`RivalDisclosureKind`, `RivalValueRecoveryFactor`.
**No content file** — there is no `information-source` content kind.
