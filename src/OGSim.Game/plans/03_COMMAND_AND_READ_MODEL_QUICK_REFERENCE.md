# Command and read model quick reference

This is the Godot-facing quick reference for the currently implemented
`OGSim.Composition` surface.

## 1. Engine build results

| Type | Meaning |
|---|---|
| `Built(Engine Engine)` | Engine composed successfully. |
| `BuildRefused(EngineCompositionRefused Refusal)` | Composition failed; read every problem. |

`Engine` is the concrete playable host handle.

## 2. Tick results

| Type | Meaning | Host action |
|---|---|---|
| `TickCompleted` | One month completed. | Publish new snapshot. |
| `TickAbandoned(Fault Fault)` | Tick discarded whole; previous state retained. | Show fault, continue. |
| `TickHalted(Fault Fault)` | State untrustworthy; engine stops. | Stop and report. |

## 3. Current command types

All commands live in `OGSim.Composition`.

| Command | Arguments | Game meaning |
|---|---|---|
| `DrillWellCommand` | `EntityId<IProspect> Target`, `Length TotalDepth` | Drill a prospect. |
| `WellTestCommand` | `EntityId<IReservoirCompartmentEntity> Target` | Test a compartment; refine pressure/permeability beliefs. |
| `WirelineLogCommand` | `EntityId<IReservoirCompartmentEntity> Target` | Log a well; refine porosity/permeability beliefs. |
| `CutCoreCommand` | `EntityId<IReservoirCompartmentEntity> Target` | Cut core; refine porosity/permeability beliefs. |
| `SeismicSurveyCommand` | `EntityId<IProspect> Target` | Shoot seismic; refine prospect structure/trap beliefs. |
| `SetWellChokeCommand` | `EntityId<ICompletion> Well`, `bool Open` | Open or shut in a well. |
| `InstallSeparatorCommand` | none | Debottleneck separation capacity. |
| `ExpandExportCommand` | none | Expand export capacity. |
| `AbandonWellCommand` | `EntityId<ICompletion> Well` | Abandon a completed well. |

Commands are submitted to:

```csharp
engine.Commands.Submit(command)
```

## 4. Command results

| Type | Members | Meaning |
|---|---|---|
| `Accepted` | `AuditId Audit`, `IReadOnlyList<EngineEvent> Immediate` | Command applied. |
| `Rejected` | `IReadOnlyList<RejectionReason> Reasons` | Command refused; every reason returned. |

`RejectionReason`:

- `LocId` — localisation key for the player.
- `Detail` — diagnostic detail for debug UI.

## 5. Current read model

`FieldReadModel` is in `OGSim.Composition/Gameplay.cs`.

| Field | Type | Godot use |
|---|---|---|
| `Tick` | `Tick` | Turn counter. |
| `Date` | `GameDate` | Calendar UI. |
| `Cash` | `Money` | Company balance. |
| `Wells` | `int` | Total well count. |
| `ActivitiesRunning` | `int` | Active operations count. |
| `ProducedThisTick` | `SurfaceVolume` | Monthly production. |
| `Insolvent` | `bool` | Company is finished. |
| `Progress` | `ScenarioProgress` | Scenario progress detail. |
| `Outcome` | `ObjectiveState` | `Pending`, success, or failure. |
| `Beliefs` | `IReadOnlyList<BeliefEntryView>` | Learned reservoir beliefs. |
| `Chain` | `IReadOnlyList<ChainElementView>` | Production path and throughput/deferral. |
| `Bottlenecks` | computed from `Chain` | Elements that refused production. |
| `Wellbores` | `IReadOnlyList<WellStatusView>` | Wells available for well commands. |
| `Prospects` | `IReadOnlyList<ProspectView>` | Exploration targets. |

## 6. BeliefEntryView

| Field | Meaning |
|---|---|
| `Subject` | Entity the belief is about. |
| `PropertyKind` | What was measured. |
| `P10` | High case; probability of exceeding. |
| `P50` | Median case. |
| `P90` | Low case; probability of exceeding. |
| `BestSource` | Best provenance source. |
| `AsOf` | Date of the belief. |

Important petroleum convention: **P90 is the low case and P10 is the high case.**

## 7. WellStatusView

| Field | Meaning |
|---|---|
| `Well` | `EntityRef` identifying the well. |
| `DisplayId` | Renderable well name. |
| `Status` | `WellStatus` state. |
| `ProducedThisTick` | Monthly production for that well. |

Use `Well.Value` to construct an `EntityId<ICompletion>` for
`SetWellChokeCommand` and `AbandonWellCommand`.

## 8. ProspectView

| Field | Meaning |
|---|---|
| `Prospect` | `EntityRef` identifying the prospect. |
| `Play` | Petroleum-system play group. |
| `At` | Map coordinate. |
| `ToMarket` | Distance to market. |
| `ProbabilityOfSuccess` | Overall probability of success. |
| `Source` | Source factor mean. |
| `Reservoir` | Reservoir factor mean. |
| `Seal` | Seal factor mean. |
| `Trap` | Trap factor mean. |
| `Timing` | Timing factor mean. |

Prospects are undrilled structures. `Prospect.Prospect.Value` converts to
`EntityId<IProspect>` for drilling or seismic commands.

## 9. ChainElementView

The full type is in `OGSim.Composition/ProductionLoop.cs`.

Key game-facing members:

- `Element` — flow element id.
- `Throughput` — mass or relevant rate passed this tick.
- `Deferred` — refused amount.
- `IsBottleneck` — true when something was deferred.

Render the chain in order. Show deferred amounts as red bottleneck labels.

## 10. Scenario outcome

`ObjectiveState` values drive the end screen. Check `FieldReadModel.Outcome`:

- `Pending` — keep playing.
- success/failure states — stop the run and show the scenario result.

`Insolvent` is the current concrete failure condition.

## 11. Future full contract surface

`OGSim.Contracts/EngineSurface.cs` defines the eventual public surface:

- `IEngine.AdvanceTick()`
- `IEngine.ReadModel`
- `IEngine.Commands`
- `IEngine.Events(tick)`
- `IEngine.Audit`
- `IEngine.World`
- `IEngine.WriteSave(stream)`

The full `ReadModel` has company, fields, wells, facilities, operations,
logistics, market, finance, HSE, environment, beliefs, exploration, and
objectives. Build Godot presentation code against stable DTOs now, but know the
current playable concrete surface is the smaller `FieldReadModel`.
