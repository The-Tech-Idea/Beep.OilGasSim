# Responsibility matrix

This matrix maps the .NET engine to responsibilities and tells the Godot team
where each concept belongs. It is derived from the actual assemblies and module
manifests.

## 1. Host vs engine

| Responsibility | Owner |
|---|---|
| Rendering, sprites, tile maps, scenes, UI | `OGSim.Game` |
| Input, pause, speed, game mode screens | `OGSim.Game` |
| Audio, localisation strings, tutorial content | `OGSim.Game` |
| Save slot selection and file I/O | `OGSim.Game` |
| Choosing when to call `AdvanceTick` | `OGSim.Game` |
| Building/loading the engine | `OGSim.Game` through a host/bridge layer |
| Mutating simulation state | engine `CommandBus` only |
| Reservoir physics and truth | engine, mostly internal to `OGSim.Subsurface` |
| Flow solving | engine `OGSim.Flow` |
| Money, fiscal regime, ledger | engine `OGSim.Company` |
| Operations and activities | engine `OGSim.Operations` + `OGSim.Composition` |
| World generation | engine `OGSim.World` |
| Beliefs and observations | engine `OGSim.Information` |
| Scenario/objectives | engine `OGSim.Objectives` + `OGSim.Composition` |
| Persistence payload format | engine `OGSim.Persistence` |
| Rendering interpretation of read model | `OGSim.Game` |

## 2. Assembly responsibility

| Assembly | Role | Main public/contract concepts |
|---|---|---|
| `OGSim.Kernel` | Foundation types and services. No domain knowledge. | `Tick`, `GameDate`, `Money`, quantities, volumes, streams, materials, identity, events, commands, module composition, tick pipeline, fault policy, audit, state contracts, RNG. |
| `OGSim.Contracts` | Domain interfaces and public read-model/engine surface. | `IEngine`, `IEngineFactory`, `ReadModel`, all `I*` domain contracts, world/view contracts, flow contracts, scenario contracts. |
| `OGSim.Flow` | Generic flow network and solver. Knows only `IFlowElement`. | `IFlowSolver`, `IFlowElementRegistry`, `FlowNetwork`, `FlowSolver`, `SolveState`. |
| `OGSim.Subsurface` | Reservoir truth, compartments, drives, aquifer, material balance. Internal truth types. | `SubsurfaceState`, `ReservoirCompartment`, drive mechanisms, `MaterialBalanceStage`. |
| `OGSim.Wells` | Completions, inflow/outflow, lift, injectors, operating point. | `Completion`, `WellsState`, `IInflowModel`, `IOutflowModel`, `LiftMethod`, `Injector`. |
| `OGSim.Facilities` | Surface equipment and separation, pipelines, tanks, terminals, power. | `Manifold`, `Pipeline`, `Separator`, `CustodyTransferPoint`, `Flare`, `Tank`, `ExportTerminal`, `Separation`, `GasProcessing`, `PowerBalance`. |
| `OGSim.Operations` | Scheduled work, rig calendar, activities. | `Operation`, `OperationScheduler`, `ObligationRegistry`. |
| `OGSim.Company` | Ledger, fiscal regimes, licences, rivals. | `CompanyState`, `CostLedger`, `IFiscalRegime`, `Licence`, `Rival`, `LicenceRound`. |
| `OGSim.Information` | Beliefs, observations, prospect risks, sampling. | `BeliefStore`, `ObservationSampler`, `ProspectRisks`, `Observation`, `Quantiles`. |
| `OGSim.World` | Generated terrain, structure, climate, jurisdictions, prospects. | `BasinWorldGenerator`, `WorldState`, `WorldStep`, `StepStreams`, `StructuralHorizon`. |
| `OGSim.Capabilities` | Technology gates and effects. | `CapabilityState`, `TechnologyState`, `GatingValidator`, `EffectState`, `TechnologyContentKind`. |
| `OGSim.Integrity` | Degradation and hazard models. | `Degradation`, `BowTie`, `IntegrityStage`. |
| `OGSim.Objectives` | Scenario objective evaluation. | `ObjectiveEvaluator`, `ReadModelSchema`, `PredicateState`. |
| `OGSim.Persistence` | Save container, canonical JSON, state blocks, migration. | `SaveFile`, `CanonicalJson`, `StateBlock`, `MigrationChain`. |
| `OGSim.Composition` | The one layer that names concrete types and wires modules into a playable engine. | `EngineBuilder`, `Engine`, `FieldModule`, `FieldReadModel`, commands/activities, `WorldSink`. |
| `OGSim.ReferenceClient` | A headless proof that the published host surface is sufficient. | `Operator`, `Explorer`, `Session`, `Campaign`. |

## 3. Current module manifest matrix

The manifest is defined in `OGSim.Composition/Modules.cs`.

| Module | Provides | Requires | Owns state | Stages | Commands |
|---|---|---|---|---|---|
| `materials` | `IFluidPropertyModel`, `IMaterialCatalog` | — | — | — | — |
| `diagnostics` | `IAuditTrail`, `SimulationClock`, `IRandomSource` | — | — | — | — |
| `world` | `IWorldGenerator`, `WorldState` | — | — | — | — |
| `capabilities` | `IGatingValidator`, `ICapabilitySet`, `IEffectState` | — | — | — | — |
| `information` | `IBeliefStore`, `IObservationModel`, `ObservationSampler`, `ProspectRisks` | `IAuditTrail`, `IRandomSource` | — | — | — |
| `integrity` | `IDegradationModel`, `IHazardModel` | `IAuditTrail` | — | — | — |
| `hse` | — | `IHazardModel`, `IAuditTrail` | — | — | — |
| `objectives` | — | — | — | — | — |
| `subsurface` | `IDriveMechanism`, `SubsurfaceState` | `IFluidPropertyModel`, `TickProduction` | `subsurface.compartments` | 6 | — |
| `wells` | `IInflowModel`, `IOutflowModel`, `WellsState` | `IFluidPropertyModel`, `IFlowElementRegistry` | `wells.completions` | — | — |
| `flow` | `IFlowSolver`, `IFlowElementRegistry`, `TickProduction` | `IAuditTrail` | — | — | — |
| `facilities` | `ISeparationModel`, `IHydraulicModel`, `SurfaceChain` | `IFluidPropertyModel`, `IFlowElementRegistry` | — | — | — |
| `operations` | — | `IAuditTrail` | — | — | — |
| `company` | `IFiscalRegime`, `CompanyState` | `IAuditTrail` | `company.ledger` | — | — |
| `field` | `FieldControl`, `CloseStage`, `IObligationRegistry` | field dependencies listed below | `field.activities`, `company.obligations` | 3,4,5,7,8,12,13 | 9 command types |

The `field` module requires the composed states and services that cross
subsystem boundaries: subsurface state, wells state, company state, tick
production, fluid model, audit, random source, clock, belief store, observation
sampler, flow solver, fiscal regime, flow element registry, surface chain, world
state, and prospect risks.

## 4. Kernel responsibility

| Kernel type/group | Responsibility |
|---|---|
| `SimulationClock`, `Tick`, `GameDate` | Own time. One tick is one 30/360 month. |
| `TickPipeline`, `StageId`, `ITickStage` | Own the fixed 14-stage turn order. |
| `Command`, `ICommandBus`, validators/appliers | Own the only mutation path and two-phase command rule. |
| `EngineEvent`, `IEventBus`, `Severity`, `EventCategory` | Own sealed, pollable events; no subscribe mechanism. |
| `IAuditTrail`, `AuditTrail` | Own full audit history and cause chains. |
| `IFaultPolicy`, fault records | Own failure routing: continue, abandon tick, or halt. |
| `ModuleManifest`, `IModuleComposition`, `ModuleComposer` | Own all-or-nothing composition. |
| `IStateOwner`, `StateRegistry`, `StateBlock` | Own deterministic save/restore shape. |
| `Quantities`, `Volumes`, `Streams`, `Materials`, `Money`, `DetMath` | Own dimension-safe, deterministic simulation primitives. |
| `RandomSource`, `StreamId` | Own named deterministic RNG streams. |
| `Content`, `ContentLoader`, `PluginRegistry` | Own content and model-plugin binding. |

## 5. Domain contract responsibility

| Contract group | Responsibility |
|---|---|
| `IEngine`, `IEngineFactory`, `ReadModel`, `WorldView` | Public host surface. |
| `IFlowElement`, `IFlowSolver`, `IFlowElementRegistry`, `PortSpec` | Generic flow network contract. |
| `IInflowModel`, `IOutflowModel`, `ICompletion`, `ILiftMethod` | Well performance contract. |
| `ISeparationModel`, `IHydraulicModel`, `ISpecificationGate` | Facility performance contract. |
| `IFiscalRegime`, `FiscalInput`, `FiscalResult` | Fiscal/economics contract. |
| `IBeliefStore`, `Observation`, `ProspectRisks` | Information contract. |
| `IWorldGenerator`, `WorldParameters`, `IWorldSink` | World generation contract. |
| `IDegradationModel`, `IHazardModel`, `BowTie` | Integrity/HSE contract. |
| `IOperation`, `IOperationScheduler`, `IObligationRegistry` | Operations contract. |
| `IScenarioRunner`, `Objective`, `ObjectiveEvaluator` | Objectives contract. |

## 6. Godot implementation ownership

Use this table when deciding where new game code goes.

| Game feature | Godot-side implementation | Engine data/contract used |
|---|---|---|
| Map screen | Render `WorldView`/world state and prospect markers | `WorldView`, `WorldState`, `FieldReadModel.Prospects` |
| Company dashboard | Render cash, production, chain, well list | `FieldReadModel` |
| Drilling UI | Select prospect, choose depth, submit command | `DrillWellCommand` |
| Exploration UI | Select survey/log/core/test target | `SeismicSurveyCommand`, `WellTestCommand`, `WirelineLogCommand`, `CutCoreCommand` |
| Well controls | Open/close/abandon well | `SetWellChokeCommand`, `AbandonWellCommand` |
| Facility upgrades | Install separator, expand export | `InstallSeparatorCommand`, `ExpandExportCommand` |
| Message/event log | Poll sealed events | `Engine.Events` / `EventBus.Sealed` |
| Diagnostics/attribution | Render audit queries | `IAuditTrail`, `IAuditQuery` |
| Save/load | Own file slots and streams | `SaveFile`, `StateBlock`, `IStateOwner` |
| Time controls | Own pause/speed; call `AdvanceTick` | `TickPipeline` |
