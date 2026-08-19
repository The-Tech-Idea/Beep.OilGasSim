# Mockup review worksheet

The user created five mockups in:

`referenceart/Mockup/`

This worksheet records what each mockup is expected to show and the engine
values that should be present. Use it during review and when turning the
mockups into Godot scenes.

## 1. File inventory

| Mockup | File | Dimensions |
|---|---|---|
| Gameplay workspace / HUD | `GamePlayHud.jpg` | 2752 x 1536 |
| Resource / company dashboard | `ResourceCompany Dashboard mockup.jpg` | 2752 x 1536 |
| Technology tree | `Technology Tree mockup.jpg` | 1376 x 768 |
| Facility chain / bottleneck | `FacilityChainBottleneck mockup.jpg` | 2752 x 1536 |
| Exploration / prospect | `ExplorationProspect Screen mockup.jpg` | 2752 x 1536 |

## 2. Gameplay workspace / HUD

Expected from the prompt:

- Top-left resource strip: Cash, Date, Tick, Wells, Active Activities,
  Produced This Tick.
- Top-right controls: Pause, 1x, 2x, 4x, minimap.
- Center top-down oilfield with wells, prospects, manifold, flowline, separator,
  tank, flare, export terminal.
- Right inspector: selected object details and meters.
- Bottom command dock: Drill, Seismic, Well Test, Wireline Log, Cut Core,
  Open/Shut Well, Install Separator, Expand Export, Abandon Well.
- Event/toast area.

Engine values to verify:

- `FieldReadModel.Cash`
- `FieldReadModel.Date`
- `FieldReadModel.Tick`
- `FieldReadModel.Wells`
- `FieldReadModel.ActivitiesRunning`
- `FieldReadModel.ProducedThisTick`
- `FieldReadModel.Wellbores`
- `FieldReadModel.Prospects`
- `FieldReadModel.Chain`
- command buttons from `03_COMMAND_AND_READ_MODEL_QUICK_REFERENCE.md`

Review checklist:

- [ ] All top-bar values have an engine source.
- [ ] Pause/speed controls are host controls, not engine controls.
- [ ] Map markers correspond to `Wellbores` and `Prospects`.
- [ ] Inspector uses `ChainElementView` or well/prospect data.
- [ ] No unused combat/platformer widgets are present.

## 3. Resource / company dashboard

Expected from the prompt:

- Header: company, date, tick, cash, debt, scenario status.
- Production panel.
- Material balance table: Oil, Gas, Water × Produced, Used, Stored,
  Flared/Disposed, Inventory.
- Storage/logistics: tank fill/ullage, pipeline linefill, export capacity,
  cargo window.
- Finance summary: Revenue, Opex, Capex, Royalty, Tax, Depreciation,
  Abandonment Provision, Net Cash Flow.
- Ledger preview with categories.
- Navigation tabs.

Engine values to verify:

- `FieldReadModel.Cash`, `ProducedThisTick`, `Wells`
- Full read model: `CompanyView`, `FinanceView`, `LogisticsView`
- `CostLedger.Account`
- `MovementCategory`
- `Tank.Held`, `Tank.Ullage`
- `Pipeline.Linefill`, `Pipeline.FullLinefill`
- `ExportTerminal.Tier`

Review checklist:

- [ ] Resource strip is understandable without simulation logic.
- [ ] Material balance uses material names, not arbitrary resources.
- [ ] Finance labels match engine accounts.
- [ ] Ledger rows include cause/category, not just amount.
- [ ] Tank and linefill are visually distinct.

## 4. Technology tree

Expected from the prompt:

- Header: Era, Detect Class, acquired count, search/filter.
- Tree nodes and prerequisite lines.
- Node cards with name, icon, route badges, status.
- States: acquired, available, locked, diffusing.
- Inspector: description, prerequisites, effects, detect class, routes,
  diffusion timer.
- Effect summary and gating preview.

Engine values to verify:

- `TechnologyNode.Id`, `AvailableFrom`, `DiffusionLagTicks`, `Prerequisites`,
  `Effects`, `GrantsDetectClass`, `Routes`
- `TechnologyState.Has`, `Acquired`, `MaxDetectClass`
- `AcquisitionRoute`
- `EffectState.EffectiveEnvelope`, `IsUnlocked`, `SelectedPlugin`, `Parameter`
- `GatingValidator` result details

Review checklist:

- [ ] Nodes are connected as a graph, not an unordered list.
- [ ] Route badges use Research/Licence/Service/Diffusion.
- [ ] Locked and diffusing states are distinct.
- [ ] Inspector can explain missing prerequisites.
- [ ] Screen remains usable even when `AllCapabilities` is active.

## 5. Facility chain / bottleneck

Expected from the prompt:

- Header: field, production, water cut, GOR, status.
- Chain: wells → manifold → flowline → separator → custody → tank → export,
  with flare and disposal branches.
- Node throughput, capacity, utilisation.
- Bottleneck callout with deferred mass and cause.
- Inspector: tier, capacity, pressure, linefill, ullage, spec breaches.
- Chain summary table.
- Action panel.

Engine values to verify:

- `ChainElementView.Element`, `Throughput`, `Deferred`, `IsBottleneck`
- `Manifold.Slots`
- `Separator.Tier`, capacities, `OperatingPressure`
- `Pipeline.Geometry`, `Linefill`, `FullLinefill`
- `CustodyTransferPoint.Spec`, `LastBreaches`
- `Tank.Held`, `Ullage`
- `ExportTerminal.Tier`
- `FieldReadModel.Bottlenecks`

Review checklist:

- [ ] Flow direction is clear.
- [ ] Bottleneck cause is attributable to the correct element.
- [ ] Spec breach and constraint data are shown separately.
- [ ] Action buttons match valid engine commands.

## 6. Exploration / prospect

Expected from the prompt:

- Header: campaign/region, date, budget, active operations.
- Basin map with licences, prospect markers, drilled/dry/producing states.
- Prospect inspector: play, distance, POS, five-factor radar.
- Belief panel: P90/P50/P10, provenance, as-of.
- Prospect list.
- Value-of-information table.
- Exploration action dock.

Engine values to verify:

- `FieldReadModel.Prospects`
- `ProspectView.Play`, `At`, `ToMarket`, `ProbabilityOfSuccess`, factor means
- `BeliefEntryView.P10`, `P50`, `P90`, `BestSource`, `AsOf`
- `InformationValue.Cost`, `ExpectedValue`, `WorthBuying`
- `ExplorationView` from full contract surface

Review checklist:

- [ ] P90 is displayed as the low case and P10 as the high case.
- [ ] Prospect list can be sorted/filtered.
- [ ] VOI table is separate from POS.
- [ ] Map states are visually distinct.

## 7. Overall mockup review notes

These are the engine-level questions to answer for every mockup:

1. Is every displayed value traceable to an engine read model, event, audit
   query, or host-owned setting?
2. Is the player action connected to a command or host action?
3. Does the mockup avoid inventing engine values that are not currently
   exposed?
4. Does the mockup use industrial/control-room visual language rather than
   generic game HUD language?
5. Is the layout compatible with the planned `OilGas` theme and available
   `beep_ui`/`beep_game_builder_cs` widgets?

## 8. Next step

Once the visual review is complete, create Godot scene stubs using:

- `08_GODOT_SCENE_PLAN.md`
- `09_ENGINE_SYSTEM_SCENE_MAP.md`

Start with the gameplay workspace and resource dashboard because they establish
the visual language and host/engine data flow.

No engine or Godot code was changed by this document.
