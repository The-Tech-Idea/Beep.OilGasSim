# Engine system to scene and UI map

This document corrects the previous UI-only scene plan by starting from the
engine systems. It maps resources, technology, operations, facilities,
objectives, information, company/licence, world, and HSE/environment systems to
Godot scenes and UI controls.

It is planning only. No engine or Godot project code has been changed.

## 1. Engine system inventory

| Engine system | Key assemblies/types | What the player needs to see |
|---|---|---|
| Finance and resources | `OGSim.Company`, `OGSim.Kernel` | Cash, ledger accounts, movements, revenue, opex, capex, tax, royalty, debt, reserves, tank inventory, material flows. |
| Technology | `OGSim.Capabilities`, `OGSim.Contracts` | Tech tree nodes, prerequisites, routes, acquired/diffusion state, effects, gating reasons. |
| Operations | `OGSim.Operations` | Scheduled activities, rig calendar, progress, cost, outcome, abandonment obligations. |
| Facilities and flow | `OGSim.Facilities`, `OGSim.Flow` | Production chain, bottlenecks, separators, pipelines, tanks, custody/spec, export capacity, power. |
| Objectives and scenarios | `OGSim.Objectives`, `OGSim.Composition` | Objectives, failure conditions, scores, deadline, scenario progress. |
| Information and exploration | `OGSim.Information`, `OGSim.World` | Beliefs, P10/P50/P90, provenance, prospect POS and factors, value of information. |
| Company/licence/rivals | `OGSim.Company` | Licence terms, commitments, relinquishment, rivals, rounds, disclosure. |
| World | `OGSim.World`, `OGSim.Contracts` | Terrain, settlements, transport, harbours, climate, jurisdictions, prospects. |
| Integrity/HSE/environment | `OGSim.Integrity`, contracts | Condition, degradation, hazard, barriers, HSE metrics, environment severity/access. |
| Persistence | `OGSim.Persistence` | Save/load state, module blocks, validation. |

## 2. Current vs future read surface

The current concrete host surface is `FieldReadModel`:

```text
Tick, Date, Cash, Wells, ActivitiesRunning, ProducedThisTick,
Insolvent, Progress, Beliefs, Chain, Wellbores, Prospects
```

The full contract surface in `OGSim.Contracts/EngineSurface.cs` adds:

```text
CompanyView, FieldView, WellView, FacilityView, OperationView,
LogisticsView, MarketView, FinanceView, HseView, EnvironmentView,
BeliefView, ExplorationView, ObjectiveView
```

Scene planning must therefore distinguish:

- **Implement now** from `FieldReadModel`: cash, production, wells, prospects,
  chain, activity count, scenario outcome.
- **Plan now, expose later** through the full read model or a host bridge:
  ledger details, tech tree, operation schedules, facility utilisation,
  objectives/scores, HSE/environment, logistics/market, licences.

## 3. Resource screen

### Purpose

Show the company as an oil and gas operator: cash, production, inventory,
costs, revenue, and material balance.

### Engine data

Finance:

- `CostLedger.Account` — Cash, Debt, Equity, Revenue, Opex, Capex_PPE,
  Depreciation, Royalty, Tax, AbandonmentProvision, Inventory,
  PartnerPayable, InsurancePremium, Penalty.
- `MovementCategory` — Production, Development, Exploration, Operating,
  Financing, Fiscal, Abandonment, Insurance, Contractual.
- `Movement` — debit/credit, amount, category, asset, audit cause.
- Full read model: `CompanyView`, `FinanceView`, `MarketView`.

Materials and logistics:

- `Composition` — mass flow per material.
- `MaterialStream` — composition, pressure, temperature, provenance.
- `MaterialInventory` — held mass per material.
- `Tank` — `Held`, `Ullage`, `Provenance`, vapour loss.
- `Pipeline` — `Linefill`, `FullLinefill`.
- `LogisticsView` — tanks, berths, nominations.

### Resource UI proposal

Use a management dashboard:

| Section | Controls | Engine values |
|---|---|---|
| Top bar | `ResourceBadgeComponent` or `KitCurrencyBar` | Cash, date/tick, production, wells, activities. |
| Material balance | `TableComponent` | Oil/gas/water produced, used, stored, flared/disposed. |
| Tank and linefill | `KitMeter`, `KitLabelValue` | Held vs capacity, ullage, linefill. |
| Finance summary | `TableComponent` | Revenue, opex, capex, royalty, tax, debt, abandonment provision. |
| Movement ledger | `TableComponent`, `KitPager` | Latest `Movement` entries with cause. |
| Market | `KitLabelValue`, `KitWeatherForecastCard` | Benchmark prices and cost index. |

Interaction:

- select a time period;
- drill into a movement's audit cause;
- filter by `MovementCategory`;
- hover a tank/pipeline to explain why production is throttled.

## 4. Technology tree screen

### Purpose

Show the company's research/licence/acquisition state and explain every gated
rejection.

### Engine data

- `TechnologyNode`:
  - `Id`
  - `AvailableFrom`
  - `DiffusionLagTicks`
  - `Prerequisites`
  - `Effects`
  - `GrantsDetectClass`
  - `Routes`
- `AcquisitionRoute` — Research, Licence, Service/Acquisition, Diffusion.
- `TechnologyState`:
  - `Has(tech)`
  - `Acquired`
  - `MaxDetectClass`
  - `ApplyDiffusion(...)`
  - `ActiveEffects()`
- `CapabilityState` — saved acquisitions and era.
- `EffectState` — derived effective envelopes, unlocked options, selected
  plugins, parameters.
- `Requirements` and `GatingValidator` — explain why a command/operation is
  refused.

### Tech tree UI proposal

```text
TechnologyScreen
├── Header
│   ├── Era
│   ├── DetectClass
│   └── AcquiredCount
├── TreeCanvas / GraphContainer
│   ├── TechnologyNodeCard
│   │   ├── NodeName
│   │   ├── RouteBadges
│   │   ├── Status
│   │   ├── PrerequisiteLinks
│   │   └── EffectSummary
│   └── DiffusionTimerHint
└── InspectorPanel
    ├── EffectList
    ├── Requirements
    └── AcquisitionButton
```

Use these addon controls:

- `KitPanel` / `KitNodeCard` for each technology node.
- `KitTooltip` for effects and gating reasons.
- `KitMeter` for diffusion progress.
- `KitLabelValue` for era/detect class/acquired count.
- `TableComponent` for effect summary.
- `KitModalShade` for acquisition confirmation.

Visual state:

| Node state | Visual |
|---|---|
| Acquired | success/accent border. |
| Available, prerequisites met | normal actionable. |
| Available, prerequisites missing | disabled with prerequisite tooltip. |
| Not yet in era | locked/future. |
| Diffusing | progress ring toward `AvailableFrom + DiffusionLagTicks`. |

Important: the current shipped sandbox composition uses `AllCapabilities`.
`TechnologyState` is the real campaign implementation. Build the screen against
the `TechnologyNode`/`TechnologyState` contract, but it may show all-tech or an
empty tree until technology content is present.

## 5. Operations screen

### Purpose

Show scheduled work, rig contention, progress, cost, outcomes, and abandonment
liabilities.

### Engine data

- `OperationState` — Scheduled, Active, Standby, Completed, Failed, Cancelled.
- `OutcomeGrade` — OnTime, Delayed, OverBudget, Partial, Failure, Disaster.
- `Operation`:
  - `Id`, `Spec`, `State`, `ProgressDays`, `Accrued`, `Outcome`.
- `OperationScheduler`:
  - rig calendar;
  - `Refusals(...)`;
  - `Submit(...)`.
- `ObligationRegistry`:
  - `Outstanding`, `Assets`, `EstimatedCost`, `TotalOutstanding`.
- Full read model: `OperationView`.

### Operations UI proposal

```text
OperationsScreen
├── RigCalendar
│   ├── RigTimeline
│   └── ReservedBlocks
├── OperationList
│   ├── OperationRow
│   │   ├── Template
│   │   ├── State
│   │   ├── ProgressMeter
│   │   ├── AccruedCost
│   │   └── OutcomeBadge
├── ObligationPanel
│   ├── OutstandingAssets
│   └── TotalAbandonmentCost
└── NewOperationDialog
    ├── TemplateSelect
    ├── TargetSelect
    ├── RequirementsPreview
    └── Submit
```

Use:

- `TableComponent` for operations and obligations.
- `KitMeter` for progress.
- `KitLabelValue` for accrued cost and total obligation.
- `KitTooltip` for scheduler refusals and outcome explanation.
- `KitModalShade` for new-operation submission.

## 6. Facilities and flow screen

### Purpose

Show the production chain, bottlenecks, specification breaches, and export
capacity.

### Engine data

- `IFlowElement` and `FlowConnection`.
- `Manifold` — `Slots`, `SlotAt`.
- `Separator` — gas/liquid capacities, operating pressure, tier.
- `Pipeline` — geometry, linefill, erosional velocity.
- `CustodyTransferPoint` — `Spec`, `LastBreaches`.
- `Tank` — `Held`, `Ullage`.
- `Flare` and disposal.
- `ExportTerminal` — fitted `ExportTier`.
- `ChainElementView` — throughput, deferred, bottleneck.
- Full read model: `FieldView`, `FacilityView`, `LogisticsView`.

### Facility UI proposal

```text
FacilityScreen
├── ChainFlowGraph
│   ├── WellSources
│   ├── Manifold
│   ├── Flowline
│   ├── Separator
│   ├── CustodyMeter
│   ├── Tank
│   └── ExportTerminal
├── ElementInspector
│   ├── CapacityMeters
│   ├── SpecBreachList
│   └── TierInfo
└── BottleneckPanel
    └── DeferredMassList
```

Use:

- custom `GraphEdit`/`Node2D` chain visual;
- `KitPanel` and `KitLabelValue` for element inspector;
- `KitMeter` for utilisation and tank ullage;
- `TableComponent` for spec breaches and deferred mass;
- `KitTooltip` for "why is this element throttling production?".

## 7. Objectives and scenario screen

### Purpose

Show what the player is trying to achieve, what must never happen, and how the
run is scored.

### Engine data

- `Scenario`:
  - `Objectives`, `Failures`, `Scoring`, `RealityProfile`, `Deadline`.
- `Objective`:
  - `Condition`, `Deadline`, `Weight`, `Visible`.
- `ScenarioProgress`:
  - objective states, score dimensions, overall state.
- `ObjectiveState`:
  - Pending, Met, Failed, Expired.
- `ScoreDimension`:
  - Reserves, Recovery, CapitalEfficiency, FindingCost, OperatingCost,
    Uptime, Hse, Legacy.

### Objectives UI proposal

```text
ObjectivesScreen
├── DeadlineHeader
├── ObjectivesPanel
│   ├── ObjectiveCard
│   │   ├── ConditionSummary
│   │   ├── StateBadge
│   │   └── Weight
│   └── FailureCard
├── ScoresPanel
│   └── ScoreDimensionBar
└── CampaignProgress
```

Use:

- `KitLabelValue` for deadline and overall state.
- `KitPanel` for objective/failure cards.
- `KitMeter` for progress and score dimensions.
- `KitTooltip` for readable predicate summaries.

The engine evaluates objectives at stage 12 and publishes progress at stage 13.
The UI must not evaluate objectives itself.

## 8. Information and exploration screen

### Purpose

Show what the company believes, how it learned it, and which prospects are
worth more data or a well.

### Engine data

- `BeliefEntryView`:
  - subject, property kind, P10/P50/P90, provenance, as-of.
- `ProspectView`:
  - play, location, distance to market, POS, five factors.
- `ProspectRisk`:
  - `ProbabilityOfSuccess`, per-factor `FactorBelief`, shared play factors.
- `InformationValue`:
  - source, subject, cost, expected value, `WorthBuying`.
- `Observation` / `ObservationSampler` — fairness record.

### Exploration UI proposal

```text
ExplorationScreen
├── WorldMap
│   └── ProspectMarkers
├── ProspectList
│   ├── ProspectCard
│   │   ├── POS
│   │   ├── FactorRadar
│   │   └── DistanceToMarket
├── BeliefInspector
│   ├── P10/P50/P90
│   ├── Provenance
│   └── AsOf
└── ValueOfInformationPanel
    └── InformationValueTable
```

Use:

- `KitRadarChart` for five-factor POS.
- `TableComponent` for belief and VOI lists.
- `KitPanel` for prospect cards.
- `KitTooltip` for factor meaning.
- `MinimapComponent` for overview.

Remember: P90 is the low case and P10 the high case.

## 9. Company, licence, and rivals screen

### Purpose

Show licences, work commitments, relinquishment, fiscal regimes, and rival
activity.

### Engine data

- `LicenceTerms` — term months, work commitment, bond, relinquishment steps,
  fiscal regime, HSE regime.
- `CommitmentProgress` and `CommitmentAssessment`.
- `ILicence` — expiry and fiscal regime.
- `Rival`, `RivalPersonality`, `Bid`, `LicenceRound`, `PublicDisclosure`.
- Full read model: `ExplorationView`.

### UI proposal

```text
CompanyScreen
├── LicencePanel
│   ├── Expiry
│   ├── CommitmentProgress
│   └── RelinquishmentTimeline
├── FiscalPanel
│   └── RegimeSummary
└── RivalsPanel
    ├── RivalPublicResults
    └── LicenceRoundHistory
```

Use `TableComponent`, `KitMeter`, `KitLabelValue`, and `KitTooltip`.

## 10. HSE, integrity, and environment screen

### Purpose

Show equipment condition, degradation/hazard, barriers, emissions/flaring, and
environment access.

### Engine data

- `ServiceSeverity`, `IDegradationModel`, `IHazardModel`.
- `BowTie` barriers.
- `HseView` — process safety, personal safety, barriers, emissions, flaring.
- `EnvironmentView` — severity, forecast, access windows, lost days.

### UI proposal

```text
HseEnvironmentScreen
├── ConditionPanel
│   └── ConditionMeters
├── BarrierPanel
│   └── BowTieBarrierList
├── EmissionsPanel
│   ├── EmissionsIntensity
│   └── FlaringIntensity
└── EnvironmentPanel
    ├── ForecastList
    ├── AccessWindows
    └── LostDays
```

Use `KitMeter`, `TableComponent`, `KitWeatherForecastCard`, and `KitToast` for
critical HSE alerts.

## 11. World/map screen

### Purpose

Render the public world surface and place company-owned/explored entities on it.

### Engine data

- `WorldView` / `GeneratedSurface`.
- `WorldParameters`.
- `FieldReadModel.Prospects`.
- `FieldReadModel.Wellbores`.

### UI proposal

- Tile map from `GeneratedTerrain`.
- Layers for terrain, transport, settlements, harbours, sensitivity zones,
  climate/jurisdiction polygons.
- Prospect and well markers from the read model.
- Click markers to open inspector scenes.

## 12. Engine system to addon control mapping

| Engine concept | Addon UI control |
|---|---|
| Resource strip | `ResourceBadgeComponent`, `KitCurrencyBar`, `KitLabelValue` |
| Metered progress | `KitMeter`, `KitRadialMeter` |
| Tabular finance/operations/beliefs | `TableComponent`, `KitPager` |
| Titled panel | `KitPanel`, `KitPanelContainer`, `PanelFrameComponent` |
| Inspector tabs | `KitTabPanel`, `KitTabStrip`, `TabGroupComponent` |
| Tooltip/gating explanation | `KitTooltip`, `TooltipComponent` |
| Confirmation | `KitModalShade`, `ModalComponent`, `KitDialogBox` |
| Alerts | `KitToast`, `ToastNotificationComponent` |
| Selection/filter | `KitSegmentedIconGroup`, `KitOptionButton`, `KitSelect` |
| POS radar | `KitRadarChart` |
| Weather/environment | `KitWeatherForecastCard`, `WeatherForecastUI` |
| Data binding | `DataBinderHostComponent` |

## 13. Implementation order

Planning-only order:

1. Extend the host bridge/read surface until every screen has a defined data
   source. Do not fake engine values.
2. Build Resource screen first because cash/production/inventory is the
   management loop.
3. Build Facilities/chain screen because it explains production bottlenecks.
4. Build Operations screen because drilling and abandonment drive the loop.
5. Build Technology screen when `TechnologyState` content is available.
6. Build Exploration/beliefs screen after prospects are rendered.
7. Build Objectives/scenario screen around `ScenarioProgress`.
8. Build HSE/environment screen when the full read model exposes those views.
9. Build Company/licence/rivals screen after licence content is present.
10. Use MCP commands to smoke-test each scene with deterministic engine states.

## 14. Constraints

- The current `FieldReadModel` is narrower than the full contract read model.
- The current shipped capability composition is `AllCapabilities`; the tech tree
  UI should be contract-ready but may not have gameplay content yet.
- The host must not read internal engine truth directly. Use read models,
  events, audit queries, and explicit bridge commands.
- The addon widgets are presentation only. The engine remains the source of
  truth for every number shown.

No engine or Godot code was changed by this document.
