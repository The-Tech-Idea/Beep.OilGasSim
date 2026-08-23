> Source read in full: `src/OGSim.Composition/Modules.cs`, plus
> `ProductionLoop.cs`, `Activities.cs`, `Scenario.cs`, `Gameplay.cs`.
> Part of the module review requested 2026-08-23.

# 14 — field

`internal sealed class FieldModule(FacilityLadders ladders, TakeOrPayTerms takeOrPay, LiftTiers liftTiers, IReadOnlyList<FluidSystemDefinition> fluidSystems, RuleSet rules, StyleTerms terms)`

**This module is most of the engine.** Nine or ten stage slots, eleven state
keys, all thirty-one commands. Nothing else comes close.

It exists because it is the only thing entitled to know wells and compartments
are *both* real — `OGSim.Wells` cannot name a compartment and `OGSim.Subsurface`
cannot name a completion, so the numbers crossing between them cross here.

## Manifest

**provides** — `FieldControl`, `CloseStage`, `IObligationRegistry`, `Bank`,
`ReserveHistory`, `WorkingInterest`, `ObjectiveStage`, `TakeOrPayContract`,
`LiftTiers`

**requires** — 31 contracts: `SubsurfaceState`, `WellsState`, `CompanyState`,
`PlantBuilder`, `WeatherState`, `EsgAssessment`, `TickProduction`,
`IFluidPropertyModel`, `IAuditTrail`, `IRandomSource`, `SimulationClock`,
`IBeliefStore`, `ObservationSampler`, `IFlowSolver`, `IFiscalRegime`,
`IPriceModel`, `MarketState`, `IFlowElementRegistry`, `SurfaceChain`,
`WorldState`, `ProspectRisks`, `AssetIntegrity`, `CapabilityState`, `Licence`,
`IGatingValidator`, `IEffectState`, `IHydraulicModel`, `IReserveBasedLending`,
`ReserveHistory`, `ReservesBook`, `CrewState`

It both provides **and** requires `ReserveHistory` — self-satisfying.

**ownsState** — eleven keys: `field.activities`, `company.obligations`,
`field.flood`, `field.export`, `company.facility`, `company.reserve-history`,
`field.abandoned`, `company.take-or-pay`, `company.working-interest`,
`objectives.evaluation`, `objectives.reporting`

**stages** — `Operations` 0, `Availability` 0, `SolveFlow` 0, `Custody` 0,
`Economics` 0, `Company` 2, `Information` 0, `Objectives` 0, `Close` 0,
**and `Company` 3 only if the style has a hedge**

**commands** — all 31.

## Compose, in order

1. `ObligationRegistry(Defaults.AbandonmentCostOf)` — owned, provided
2. `ProductionLoop? loop = null` — a **forward reference**, because `loop` needs
   `field` and `field` needs `loop`. A genuine cycle, broken by assigning once
   and invoking only at read-model time
3. `FieldControl(...)` — including the licence and the drill template, because a
   well that stands discharges a work commitment and the field is where a well
   comes into existence
4. `ExportTerminal(EntityRef(Facility, 1), ladders.Export[0])`
5. `WorkingInterest.Validate(terms.WorkingInterest)`; `WorkingInterest()` — owned, provided
6. `ProductionLoop(...)` — 30 arguments
7. `Bank(lender, reserves, company, audit, () => loop.CumulativeProduced)` — owned, provided
8. `FacilitiesState.ExportState(terminal, ladders)` — owned
9. `ReserveHistory()` — owned, provided
10. Five stage contributions: `SegmentationStage`, `SolveFlowStage`,
    `StalenessStage`, `CustodyStage`, `EconomicsStage`
11. `OperationScheduler(Stream(Operations), audit, MaterialCount, () => crew.DurationFactor)`;
    `scheduler.Register(Defaults.TheRig)`
12. `ObservationDoor(sampler, beliefs, Defaults.SpaceOf)`
13. **The activity catalogue — 25 activities**
14. `ActivityState(scheduler, company, catalogue, market)` — owned
15. `FieldProjection(...)` — 12 arguments
16. `ReadModelPaths(Defaults.ProjectedPaths)`; `ScenarioRunner(Defaults.FirstField, paths.Schema)` — owned
17. `ObjectiveStage(...)` — contributed, owned **and** provided
18. `CloseStage(projection, objectives)` — contributed and provided
19. `TakeOrPayContract(takeOrPay, start: Tick(0))` — owned, provided, staged at order 2
20. `HedgeStage` at order 3 — **only if the style has a collar**
21. `ActivityStage(activities, audit, weather)` at order 0
22. `ActivityOrders(...)`, then every activity registers its own command pair
23. Five command pairs that are **not** activities: choke, voidage, borrow,
    repay, sell working interest, train crew

## The ten stages

| Stage | Slot | What a player notices |
|---|---|---|
| `ActivityStage` | Operations 0 | Advances every running job a month, loses days to weather as **standby** (paid, no progress), posts the month's cost, and lets a finished job apply its meaning |
| `SegmentationStage` | Availability 0 | Ages and rolls every element for failure, drops what broke out of the network, propagates the shut-in upstream, and **splits the month at the earliest failure day** |
| `SolveFlowStage` | SolveFlow 0 | Solves the chain per segment at that segment's own ambient temperature |
| `StalenessStage` | Information 0 | Widens the pressure belief on compartments that actually produced — so an old well test decays and re-testing becomes worth paying for |
| `CustodyStage` | Custody 0 | Receives oil into the tank, takes boil-off, loads a cargo, accrues laytime, charges demurrage, records the metered transfer |
| `EconomicsStage` | Economics 0 | Files flaring into the ESG record, moves the price, posts revenue/royalty/tax/opex/abandonment provision/depreciation, re-prices the bank against ESG, charges interest, runs the covenant sweep |
| `TakeOrPayStage` | Company 2 | Records delivered volume; posts a shortfall penalty when a 12-month window closes short |
| `HedgeStage` | Company 3 | Settles a collar on part of the month's production. **Present only if the style has one** |
| `ObjectiveStage` | Objectives 0 | Latches `Insolvent` and `TakenOver`, evaluates the scenario, records the verdict |
| `CloseStage` | Close 0 | Publishes the read model. **Throws if stage 12 did not run** |

## Functions and properties

### `FieldControl` (`ProductionLoop.cs:2211`, `IStateOwner`)

| Member | |
|---|---|
| `Slots` / `HasFreeSlot` / `FreeSlots` | tie-in capacity, from the chain's manifold |
| `AddCompartment(...)` / `AddGasCompartment(...)` | how a reservoir comes to exist |
| `Drill(drains, totalDepth)` | opens a well; registers the abandonment obligation **and** records the licence delivery |
| `TieInWaiting()` | brings suspended wells on when a plant lands |
| `WellNamed(well)` | the completion, or null |
| `LiveWellCount` | wells minus abandoned |
| `IsAbandoned` | the field has closed |

### `ProductionLoop` (`ProductionLoop.cs:260`, `IStateOwner`)

| Member | |
|---|---|
| `SolveFlow(context)` · `StoreAndExport(tick, duration)` · `RecordCustody()` · `PostEconomics(tick)` · `AdvancePrices()` | the four stages' bodies |
| `ProducedThisTick` / `CumulativeProduced` / `Exported` / `Delivered` | what came out |
| `WaterCut` / `SourFraction` | how the field is ageing |
| `FlaredThisTick` / `CumulativeFlared` | the ESG input |
| `AmbientThisTick` / `SeverityThisTick` | what the solve ran at |
| `VoidageReplacement` / `SetVoidageReplacement(ratio)` / `ImportedThisTick` / `InjectionHeadroom` | the flood |
| `Storage` / `Berth` | tank and cargo |
| `Chain()` | `IReadOnlyList<ChainElementView>` — throughput, deferred mass, condition, failure, breaches, utilisation |
| `LastSolvedStateOf(well)` / `NameOf(element)` | |
| `Key` = `field.flood`, `SchemaVersion` **2** | |

### `ActivityState` (`Activities.cs:237`, `IStateOwner`)

`Scheduler` · `Catalogue` · `InProgress` · `Running` · `Operations()` ·
`Of(template)` · `IsRunning(template, target)` · `SpecFor(...)` · `Begin(...)` ·
`Finish(...)` · `PostAccrual(...)` · `Key` = `field.activities`

### `Activity<TCommand>` — the shape every one of the 25 shares

`Terms` · `Template` · `LeavesAnAsset` · `OnePerTarget` · `Spend` ·
`Aim(command)` · `OwnRefusals(command)` · `Complete(done, tick)` ·
`Register(composition, orders)`

### `ActivityOrders.Refusals(template, target, depth)` — the one validator

Returns **every** reason, never the first. In order:

1. **Tenure** — the licence is not live (two distinct reasons: expired vs forfeited)
2. **Cash** — `company.Ledger.Cash < market.Quoted(terms.Cost)`
3. **Work subject** — the rule set's `IWorkSubjectRule`
4. **Access** — `RequiresAccess` and the season is closed
5. **One per target** — this template is already running on this target
6. **Resources** — the scheduler's own refusals (the rig)
7. **Gating** — era and technology, through `IGatingValidator`

## Dependencies and conditions it decides for itself

| Where | Condition |
|---|---|
| **Manifest** | `Slot(terms.Hedge is not null, Company, order: 3)` |
| **`Compose`** | `if (hedge is HedgeTerms collar) { Validate; Contribute; }` |
| `Compose` | `WorkingInterest.Validate(terms.WorkingInterest)` — **throws** on a zero cap |
| `ActivityOrders` | seven refusal branches, one of which (tenure) is a style concern |
| `Compose` | `() => field.IsAbandoned` and `() => loop.CumulativeProduced` — forward references, not conditions, but they mean two objects observe each other |

The hedge condition is **written twice** and kept in step only by a comment.

## Static numbers found

None inline — this module is unusually clean on that count. Every figure comes
from `Defaults` or from a constructor argument. It is the sheer *number* of
`Defaults.*` reads that stands out: **over fifty distinct members**.

## Content and Defaults consumed

**Content** — `content/facilities/*.json` (via `FacilityLadders`),
`content/contracts/oil-take-or-pay.json`, `content/wells/*.json` (via
`LiftTiers`), `content/fluid-systems/*.json`.

**Defaults** — more than fifty members, including every one of the 25 activity
`*Terms`, `TheRig`, `AbandonmentCostOf`, `Flowline`, `FlowlineRating`,
`FirstGatheringLine`, `CompletionFor`, `MaterialCount`, `LiquidOrdinals`,
`Economics`, `ReservoirTemperature`, `SurfaceOilDensity`, `GasPricePerTonne`,
`SpaceOf`, `Eras`, `ProjectedPaths`, `FirstField`, `MaximumDrillingDepth`, and
the four belief kinds.

## Can it be omitted?

**No.** It is the only provider of nine contracts, owns eleven state keys, fills
nine or ten stage slots and registers all thirty-one commands. `EngineBuilder`
also resolves `CloseStage` directly to produce the read model. Dropping it
produces no engine at all.
