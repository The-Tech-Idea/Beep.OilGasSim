# Engine workflow

This document describes the complete workflow from "create a new game" to "show
the player what happened this month". It is based on the actual C# source under
`src/OGSim.*`, not on the repository-level design documents.

## 1. The boundary

The engine is headless. It has no Godot dependency and no rendering dependency.
Its job is to simulate an oil and gas company. The Godot game is a host that:

- owns the main loop and pause/speed;
- builds or loads an engine;
- submits commands;
- advances ticks;
- renders the returned read model;
- polls events and audit data;
- owns save slots and file I/O.

The engine does not know about nodes, scenes, sprites, input actions, or UI.

## 2. Building a new engine

The public entry point in the currently implemented code is:

```csharp
EngineBuilder.Build(EngineSettings settings)
EngineBuilder.CreateNew(EngineSettings settings, WorldParameters world)
```

Both are in `OGSim.Composition/EngineBuilder.cs`.

`EngineSettings` has no defaults by design:

- `Epoch` — the starting `GameDate`.
- `WorldSeed` — the deterministic world seed.
- `Retention` — audit retention policy.
- `LogSink` and `MinimumLogLevel` — host-provided logging.
- `FaultHandling` — `Strict` or `Resilient`.
- `RealityProfile` — `simulation` or `arcade` in the current shipped profiles.

`EngineBuilder.Build` performs these steps:

1. Creates `SimulationClock`, `AuditTrail`, and `RandomSource`.
2. Builds the fifteen shipped modules (`ShippedModules`).
3. Asks `ModuleComposer` to validate and compose the whole module set.
4. If composition fails, returns `BuildRefused` naming every problem.
5. Creates `Log`, `EventBus`, `TickPipeline`, and `CommandBus`.
6. Binds the command registrations that composition validated.
7. Returns `Built(new Engine(...))`.

`CreateNew` does the same, then runs world generation into the composed engine.

## 3. All-or-nothing composition

`ModuleComposer` validates before constructing anything:

- every `Requires` is provided by some module;
- no contract is provided twice;
- no `StateKey` is owned twice;
- the module dependency graph is acyclic;
- no two modules claim the same `(StageId, Order)` slot;
- every declared provide, state, stage, and command is actually delivered.

There is no partially composed engine and no degraded mode. A refusal names
every problem.

## 4. The shipped modules

The current shipped set in `EngineBuilder.ShippedModules` is:

`subsurface`, `wells`, `flow`, `facilities`, `operations`, `company`,
`information`, `world`, `capabilities`, `integrity`, `hse`, `objectives`,
`materials`, `field`, and `diagnostics`.

The full responsibilities are in
[01_RESPONSIBILITY_MATRIX.md](01_RESPONSIBILITY_MATRIX.md).

## 5. World generation

`EngineBuilder.CreateNew` resolves `IWorldGenerator` (`BasinWorldGenerator`)
and calls `Generate` with:

- the host-provided `WorldParameters`;
- a freshly built `WorldSink`;
- the dedicated `StreamId.WorldGen` random stream.

`WorldSink` writes generated truth into:

- `FieldControl` — places prospects/fields the field module can use;
- `IBeliefStore` — creates the initial public beliefs through the normal
  observation door;
- `WorldState` — stores generated world truth and surface knowledge;
- `ProspectRisks` — establishes probability-of-success factors for prospects.

This is why a new game learns its field size, position, difficulty, and initial
beliefs from generation rather than from hard-coded gameplay constants.

## 6. The command path

Commands are the only mutation path. They are submitted by the host between
ticks.

Current command types live in `OGSim.Composition`:

- `DrillWellCommand(Target, TotalDepth)`
- `WellTestCommand(Target)`
- `WirelineLogCommand(Target)`
- `CutCoreCommand(Target)`
- `SeismicSurveyCommand(Target)`
- `SetWellChokeCommand(Well, Open)`
- `InstallSeparatorCommand()`
- `ExpandExportCommand()`
- `AbandonWellCommand(Well)`

`CommandBus.Submit` has two phases:

1. **Validation** — pure and mutation-free. Every rejection reason is returned,
   not just the first.
2. **Application** — may not fail. A command that reaches application has been
   proven applicable.

Accepted commands are audited and any raised events are published by the bus.
Rejected commands are also audited as rejections and are normal player feedback.

## 7. The tick pipeline

`StageId` in `OGSim.Kernel/Events.cs` defines the full 14-stage order:

| Stage | Name | Purpose |
|---|---:|---|
| 0 | Open | `SimulationClock.Advance` moves time. |
| 1 | Commands | Command effects are staged here by the full engine design. |
| 2 | Environment | Environment severity/access windows are updated. |
| 3 | Operations | Activities advance and finished activities complete. |
| 4 | Availability | The constant-availability segment plan is built. |
| 5 | SolveFlow | The flow network is solved. |
| 6 | MaterialBalance | Reservoir compartments commit withdrawals. |
| 7 | Custody | Metered production is recognised. |
| 8 | Economics | Cash, revenue, costs, and fiscal effects are posted. |
| 9 | HseRegulation | HSE and regulatory consequences are applied. |
| 10 | Information | Beliefs are updated from measurements. |
| 11 | Company | Company-level state is updated. |
| 12 | Objectives | Scenario progress and failure are evaluated. |
| 13 | Close | Events are sealed and the read model is published. |

The current concrete composition fills only the stages that have implementations:

- stage 3: `ActivityStage`
- stage 4: `SegmentationStage`
- stage 5: `SolveFlowStage`
- stage 6: `MaterialBalanceStage`
- stage 7: `CustodyStage`
- stage 8: `EconomicsStage`
- stage 12: `ObjectiveStage`
- stage 13: `CloseStage`

Open is handled by `SimulationClock.Advance` inside `TickPipeline.AdvanceTick`.
Commands are submitted outside the tick by the host.

## 8. Tick execution

`TickPipeline.AdvanceTick`:

1. Advances the clock exactly once.
2. Builds a `TickContext` for the new tick and date.
3. Executes every composed `ITickStage` in declared `StageId` order.
4. Routes any `FaultException` to the configured `IFaultPolicy`.
5. Seals the event set at tick close.
6. Returns `TickCompleted`, `TickAbandoned`, or `TickHalted`.

`TickAbandoned` means the tick was discarded whole and the previous state is
retained. `TickHalted` means state is untrustworthy and the engine stops.

## 9. The production loop inside a tick

The current field simulation crosses several stages:

1. **SegmentationStage** builds a `SegmentPlan` from element availability.
2. **SolveFlowStage** runs `ProductionLoop.SolveFlow`, which solves the flow
   network for each segment and records per-element throughput/deferrals.
3. **MaterialBalanceStage** consumes `TickProduction.Withdrawals` and commits
   them to subsurface compartments, moving reservoir pressure and contacts.
4. **CustodyStage** recognises metered production at the custody point.
5. **EconomicsStage** posts revenue and costs through the company ledger and
   fiscal regime.
6. **ObjectiveStage** evaluates the scenario against the month's facts.
7. **CloseStage** publishes the `FieldReadModel`.

The chain is constructed by `FacilitiesModule`: manifold, flowline, separator,
custody meter, flare, disposal injector, and tank. Well completions are source
elements; the surface chain is the path from wellhead to sale.

## 10. Truth vs belief

Subsurface truth is internal to `OGSim.Subsurface`. Godot can never receive a
true reservoir pressure directly.

What the player can see arrives through:

- `FieldReadModel` — public game facts such as cash, well status, production,
  bottlenecks, and scenario outcome;
- `BeliefEntryView` — learned values as P10/P50/P90 with provenance and date;
- `ProspectView` — undrilled prospects and the company's current probability
  of success across five petroleum-system factors;
- `WorldView` in the contract surface — static public world geography.

Measurements such as well tests, wireline logs, cores, and seismic surveys are
activities that sample truth and create or refine beliefs.

## 11. The read model

The currently implemented concrete read model is `FieldReadModel` in
`OGSim.Composition/Gameplay.cs`:

- `Tick` and `Date`
- `Cash`
- `Wells` count
- `ActivitiesRunning`
- `ProducedThisTick`
- `Insolvent`
- `Progress` / `Outcome`
- `Beliefs`
- `Chain` with `Bottlenecks`
- `Wellbores`
- `Prospects`

The full contract read model in `OGSim.Contracts/EngineSurface.cs` is much
larger and includes `CompanyView`, `FieldView`, `WellView`, `FacilityView`,
`OperationView`, `LogisticsView`, `MarketView`, `FinanceView`, `HseView`,
`EnvironmentView`, `BeliefView`, `ExplorationView`, and `ObjectiveView`. It is
the eventual public surface; the concrete `FieldReadModel` is the currently
playable slice.

## 12. Events and audit

`EventBus` collects events during a tick and seals them at close. Only the most
recent tick's events are retained; older history lives in `AuditTrail`.

`IAuditQuery` and `AuditTrail` provide entity, category, cause-chain, and
production-loss queries. The host can use this to build diagnostics screens,
message logs, and "why did my field underperform?" views.

## 13. Persistence

Engine state owners implement `IStateOwner`:

```csharp
StateKey Key { get; }
int SchemaVersion { get; }
void Capture(IStateWriter writer);
void Restore(IStateReader reader);
```

`StateBlock` is the canonical in-memory reader/writer. `CanonicalJson` is the
deterministic JSON representation. `SaveFile` validates headers, module blocks,
and migration chains.

The host owns slots, paths, and file I/O. The engine owns payload generation.
The fully specified `IEngine.WriteSave(Stream)` is the contract-level target; the
current concrete `Engine` record exposes the composed `StateRegistry`.

## 14. Determinism

The engine is deterministic by construction:

- one `SimulationClock` advances only in `AdvanceTick`;
- `RandomSource` provides named streams, so unrelated draws cannot shift;
- no `System.Math` transcendentals in simulation code — `DetMath` is used;
- dictionaries are used for lookup, not enumeration;
- stage order is declared and stable;
- event order is `(Stage, Day, Subject, EventId)`;
- invariant culture is used for textual conversion.

Godot must not inject wall-clock time or `Random` into engine state.

## 15. Recommended host loop

The simplest current loop mirrors `OGSim.ReferenceClient`:

```text
read previous FieldReadModel
choose commands based only on that snapshot
submit one command at a time where the rig/scheduler is exclusive
call Pipeline.AdvanceTick()
read the new FieldReadModel
if Outcome != Pending, stop
```

Godot can pause between these steps, run at 1x/2x/4x/8x, and animate the
transition. The engine's tick logic is identical at every speed.
