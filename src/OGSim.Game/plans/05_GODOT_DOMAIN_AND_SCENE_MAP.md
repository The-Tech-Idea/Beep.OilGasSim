# Godot domain and scene map

This maps engine concepts to Godot systems and scene structure. Use it when
turning the headless engine into the simulation game.

## 1. Top-level Godot architecture

Recommended autoloads:

| Autoload | Responsibility |
|---|---|
| `GameApp` | Application lifecycle, mode switching, save-slot selection. |
| `EngineHost` | The only owner of the engine/bridge client. |
| `SimulationController` | Pause/speed, tick timing, snapshot signals. |
| `EventFeed` | Queues engine events for toast/log UI. |
| `AudioBus` | Game audio; no simulation logic. |
| `Localisation` | Maps `LocId` and engine names to displayed text. |

Scene nodes should read from these autoloads, never hold an engine reference.

## 2. Main scene tree

Suggested root:

```text
Main
├── WorldMap
├── CompanyDashboard
├── FieldInspector
├── ProductionFlowView
├── CommandPanel
├── EventLog
├── TimeControls
└── ModalLayer
```

The engine snapshot is the single source of truth for UI. Rebuild views after
`SimulationController` emits `snapshot_changed`.

## 3. Engine domain to Godot feature

| Engine domain | Godot feature |
|---|---|
| `WorldView`, `WorldState`, terrain/settlements/transport | Map screen with terrain tiles and entity layers. |
| `FieldReadModel.Prospects` | Prospect markers, prospect detail panel. |
| `FieldReadModel.Wellbores` | Well markers and well inspector. |
| `FieldReadModel.Chain` | Production flow diagram from wellhead to export. |
| `FieldReadModel.Beliefs` | Belief/uncertainty inspector. |
| `FieldReadModel.Cash` / finance | Company dashboard and ledger chart. |
| `ObjectiveState` | Scenario result/end screen. |
| `EngineEvent` | Message feed and alert toasts. |
| `IAuditQuery` | Diagnostic/audit screen. |
| commands | Context-sensitive action panel. |

## 4. Production chain visual mapping

Current `SurfaceChain` elements:

| Engine element | Default name | Godot visual suggestion |
|---|---|---|
| manifold | `manifold` | Pipeline manifold sprite/node. |
| gathering flowline | `flowline` | Pipe connection from header to separator. |
| separator | `separator` | Separator-vessel sprite. |
| custody meter | `custody-meter` | Metering station sprite. |
| flare | `flare` | Flare-stack sprite with animation when flaring. |
| water disposal | `water-disposal` | Water-injection/disposal well sprite. |
| tank | `tank` | Crude-oil storage tank sprite. |
| export terminal | terminal | Tanker/loading or export terminal sprite. |

Render `ChainElementView` in order and show `Deferred` as a red bottleneck
badge on the element that refused flow.

## 5. Well visual mapping

Current well status should drive sprite/animation:

| `WellStatus` | Suggested visual |
|---|---|
| Producing | Active pumpjack or wellhead. |
| Shut-in | Static wellhead, dimmed/no flow animation. |
| Abandoned | Plugged/abandoned marker, no active equipment. |

The exact well status enum members should be read from `WellContracts.cs` before
binding visuals.

## 6. Existing asset categories

The repository already contains top-down and isometric reference assets under
`assets/` and `referenceart/`. Map them by game layer, not by engine type:

- terrain and roads: map base;
- wells, pumpjacks, rigs: exploration/drilling/production layer;
- separators, tanks, manifolds, flares, terminals: facility layer;
- trucks, cranes, pipeline equipment: activity/visualisation layer;
- control room, safety, HSE equipment: HSE/event layer.

Do not force engine semantics onto the asset folder. Assets are presentation;
engine entities are simulation IDs.

## 7. Time and date display

`GameDate` is year/month. `Tick` is sequential. Show both:

- top bar date from `FieldReadModel.Date`;
- turn number from `FieldReadModel.Tick`;
- next-advance countdown from Godot pacing, not engine time.

The engine uses 30/360 months. Do not calculate real dates or day-of-week from
engine `GameDate`.

## 8. Localisation

Build a localisation table for:

- command names;
- rejection `LocId` values;
- event categories and severities;
- well/facility status labels;
- scenario objective/outcome labels.

Keep engine IDs as stable keys. Never localise by matching English display text
from engine internals.
