# Casual top-down competitive game mode

This document defines a second OilGasSim mode: a tile-based, Stardew
Valley-inspired oilfield game for normal players. It is not a replacement for
the professional simulation mode. It is a competitive, accessible presentation
over the same headless engine.

It is planning only. No engine or Godot code has been changed.

## 1. Mode identity

Working title:

**Oilfield Days**

Genre:

- top-down life/management sim;
- tile-based exploration and construction;
- Stardew Valley-like daily loop;
- fixed-seed competitive challenges for normal players.

Target player:

- someone who wants to feel like they are running a small oilfield town;
- does not need to know reservoir engineering terms;
- wants to drive, build, inspect, complete jobs, and beat friends on a
  leaderboard.

Visual and narrative constraint:

- **No humans are shown.**
- **No animals are shown.**
- **No NPC characters are shown.**
- The world contains vehicles, machinery, buildings, infrastructure, terrain,
  signage, and control interfaces only.
- The player is represented by a service truck, cursor/drone, or another
  mechanical avatar — never a person.
- Interaction happens through terminals, radios, beacons, control panels, and
  vehicle actions — never conversation with a visible character.

## 2. Relationship to the professional mode

The engine stays the same. The casual mode changes only the host presentation
and interaction layer.

| Layer | Professional mode | Casual mode |
|---|---|---|
| Engine | Full simulation, engineer-friendly read model | Same engine, arcade/standard fidelity, hidden complexity |
| Time | Explicit monthly tick/pause controls | Monthly engine tick behind a visible daily/seasonal rhythm |
| World | Basin map and facility dashboards | Walkable tile town/field |
| Actions | Command panel | Walk-to-object, context menu, hotbar, contract board |
| Score | Multi-dimensional professional score | Weekly challenge leaderboard |
| Failure | Insolvency/HSE/spec issues | Losing contracts, reputation, seasonal failure |

The casual host can still use the same `EngineBuilder`, command bus, and read
model. It wraps them in simpler interactions.

## 3. Reference art and existing assets

### 3.1 Stardew-style reference images

Use these as the visual reference for camera angle, tile density, roads,
buildings, and open-world readability:

- `referenceart/TopDown-StartDewValley/normalized/topdown1.png`
- `referenceart/TopDown-StartDewValley/normalized/topdown2.png`
- `referenceart/TopDown-StartDewValley/normalized/topdown3.png`
- `referenceart/TopDown-StartDewValley/normalized/trucks.png`

All three object sheets are `1122 x 1402`.

### 3.2 Existing extracted top-down sprites

The repository already contains 75 clean oilfield sprites under:

`assets/topdown/sprites/`

These include:

- wells and production: `wellhead-tree`, `pumpjack`, `separator-vessel`,
  `three-phase-separator`, `pipeline-manifold`;
- processing/utilities: `gas-compressor-unit`, `cooling-tower`,
  `generator-unit`, `water-injection-pump`;
- storage/buildings: `oil-storage-tank`, `fuel-tank`, `frac-tank`,
  `main-operations-building`, `maintenance-workshop`, `office-cabin`;
- safety/environment: `flare-stack`, `spill-response-trailer`,
  `oil-containment-boom`, `produced-water-pond`;
- vehicles: `forklift`, `mobile-crane-truck`, `pipeline-construction-excavator`,
  `heavy-equipment-trailer`, `workover-rig`;
- site services: `control-room-cabin`, `worker-accommodation-cabin`,
  `worker-safety-cabin`, `security-checkpoint`, `helipad-platform`.

Animation spritesheets already exist under `assets/topdown/animations/` for:

- `flare-stack_burning`
- `pumpjack_working`
- `generator-unit_working`
- `water-injection-pump_working`
- `well-testing-skid_working`
- `road-barrier-gate_barrier`
- `security-checkpoint_barrier`
- and others.

### 3.3 Tile assets

Godot terrain atlases are already present under:

`src/OGSim.Game/assets/Tilesets/`

Available terrain families:

- Grassland
- Desert
- Coastal
- Forest
- Industrial
- Mud
- Rocky
- Snow

The casual mode should use a compact top-down tile size and the Stardew-style
reference as the proportion guide, not the existing 256x128 isometric atlases.
The top-down mode should create or adapt square tile atlases if the current
Godot tile maps are not top-down-compatible.

## 4. Core loop

The casual mode uses two nested clocks:

1. **Host daily loop** — driving, inspecting, dispatching, placing, collecting.
2. **Engine monthly tick** — production, money, contracts, and scoring advance.

### 4.1 Daily loop

Player actions:

- move a service truck or cursor around the tile map;
- open a dispatch terminal or contract panel;
- accept jobs from the automated dispatch board;
- drive trucks between tiles;
- inspect wells and facilities;
- place or upgrade simple structures;
- buy/repair equipment;
- end the shift/day.

### 4.2 Monthly engine tick

At a defined host moment:

- host submits queued commands;
- host calls `AdvanceTick`;
- engine produces the new `FieldReadModel`;
- host translates the snapshot into:
  - cash and score;
  - production animation states;
  - facility condition/status;
  - available jobs;
  - leaderboard progress.

### 4.3 Seasonal/competition loop

- A casual challenge lasts one in-game year or several years.
- Every month is one engine tick.
- Players compete on the same generated world and fixed seed.
- At the end, the host shows a scorecard and local leaderboard.

## 5. Competition model

Professional mode already defines multi-dimensional scoring. The casual mode
should reuse those dimensions but present only a small, readable subset:

| Casual score card | Engine meaning |
|---|---|
| Town Reputation | Social licence, HSE standing, completed jobs. |
| Field Value | Cash, reserves, produced value. |
| Efficiency | Capital efficiency, operating cost, uptime. |
| Clean Operations | Emissions/flaring/spills, HSE. |
| Legacy | Abandonment obligations discharged, restored sites. |

Leaderboard rules:

- fixed seed;
- same reality profile;
- local leaderboard first;
- optional online later;
- scores are deterministic and replayable;
- compare within profile, never across professional and casual presets.

Challenge types:

- develop a small lease with limited cash;
- rescue a failing field;
- run a clean seasonal operation;
- fastest to first oil;
- highest town reputation with a safe field;
- best recovery with one rig.

## 6. Tile map and world representation

### 6.1 Tile layers

```text
CasualWorld
├── TerrainLayer
│   ├── GroundTiles
│   ├── Roads
│   ├── Water
│   └── LeaseBoundaries
├── BuildingLayer
│   ├── Facilities
│   ├── Wells
│   └── Decorations
├── VehicleLayer
├── InteractionLayer
├── WeatherLayer
└── LightingLayer
```

### 6.2 Tile mapping

| World object | Godot representation |
|---|---|
| Terrain | `TileMapLayer` from top-down terrain atlas |
| Roads | `TileMapLayer` road tiles |
| Wells | `assets/topdown/sprites/wellhead-tree.png` or `pumpjack` sprite |
| Separator | `assets/topdown/sprites/separator-vessel.png` |
| Storage | `assets/topdown/sprites/oil-storage-tank.png` |
| Flare | animated `assets/topdown/animations/flare-stack_burning.png` |
| Maintenance shop | `assets/topdown/sprites/maintenance-workshop.png` |
| Office/control room | `assets/topdown/sprites/office-cabin.png` |
| Worker housing | `assets/topdown/sprites/worker-accommodation-cabin.png` |
| Vehicles | `assets/topdown` or `trucks.png` directional frames |

### 6.3 Camera and player

- top-down camera, following a service truck or mechanical cursor;
- square tile world;
- `TopDownController` addon can be used for vehicle/cursor movement;
- `MinimapComponent` for town/field overview;
- context prompts through `InteractionPromptComponent` and terminal/control
  panel templates.

## 7. Player verbs mapped to engine

The casual mode should not invent simulation mechanics. Each fun action maps to
an engine command or host action.

| Casual action | Host/engine mapping |
|---|---|
| Drive/move cursor | Host-only. |
| Accept a drilling job | Host queues `DrillWellCommand`. |
| Drive a service truck to a well | Host animation; underlying job is an operation. |
| Inspect a well | Read `WellStatusView`. |
| Open/shut well | `SetWellChokeCommand`. |
| Place separator | `InstallSeparatorCommand`. |
| Expand export | `ExpandExportCommand`. |
| Perform survey | `SeismicSurveyCommand`. |
| Perform well test | `WellTestCommand`. |
| Perform log | `WirelineLogCommand`. |
| Cut core | `CutCoreCommand`. |
| Abandon old well | `AbandonWellCommand`. |
| Sell/export oil | Engine custody/export mechanics through tick. |
| Clean spill / maintain equipment | Future host commands or scenario scripts; do not fake engine state. |

## 8. Casual HUD

The casual HUD should look like a life-sim game, not a control-room dashboard.

```text
CasualHUD
├── TopLeft
│   ├── Day/Season/Year
│   ├── Cash
│   ├── Reputation
│   └── Energy or Actions Left
├── TopRight
│   ├── MiniMap
│   └── Challenge Timer
├── BottomLeft
│   ├── Hotbar
│   └── ContextPrompt
├── BottomRight
│   ├── Quest/JobTracker
│   └── ToastNotifications
└── ModalLayer
```

Use addon components:

- `KitLabelValue` for date/cash/reputation.
- `ResourceBadgeComponent` for compact resources.
- `KitInventorySlot` / `KitSlotGrid` for hotbar.
- `KitContextMenu` for object actions.
- `KitToast` / `ToastNotificationComponent` for job and event feedback.
- `KitDialogBox` / terminal template for dispatch and contract messages.
- `MinimapComponent` for town overview.
- `DataBinderHostComponent` for read-model values.
- `WeatherSystemComponent`, `DayNightCycleComponent`, `WindFieldComponent` for
  life-sim atmosphere.

## 9. Casual scene plan

### 9.1 `CasualMain`

Root `Node2D`.

Children:

- `World` with tile layers, entities, buildings, vehicles;
- `Player`;
- `Camera2D`;
- `InteractionController`;
- `CasualHUD`;
- `Weather`;
- `DayNight`;
- `Minimap`;
- `ModalLayer`;
- `SimulationController`;
- `EngineHost`.

### 9.2 `ContractBoard`

A modal or docked panel:

- lists generated jobs;
- shows reward and deadline;
- queues a command or host task;
- uses `TableComponent` and `KitPanel`.

### 9.3 `MaintenanceYard`

A drivable small maintenance yard:

- control-room cabin;
- maintenance workshop;
- covered equipment warehouse;
- fuel/storage tanks;
- automated dispatch terminal;
- vehicle parking and service bay.

### 9.4 `FieldLease`

A playable field:

- well sites;
- separator;
- tank;
- flare;
- roads;
- truck loading point.

### 9.5 `ChallengeResult`

End screen:

- scorecard;
- leaderboard;
- replay/save summary;
- challenge restart.

## 10. Use of reference images

The reference images should guide:

- proportion of vehicles, structures, and equipment to tile size;
- road width and border treatment;
- object density;
- readable top-down silhouettes;
- use of clear grass/soil/road tile distinctions;
- UI overlay should remain lighter and less technical than professional mode.

Do not copy the reference art; use it as direction. The oilfield assets are
already extracted and should be placed onto the chosen tile art.

Explicitly exclude humans, animals, and NPC sprites from all casual-mode
mockups, scenes, UI, and marketing. Use only mechanical objects and structures.

## 11. Engine and host constraints

- Do not add casual-specific code to the engine.
- Do not use the host daily clock as engine time. The engine still ticks monthly.
- Do not allow the host to mutate engine state directly; use commands.
- Do not expose reservoir truth in the casual mode.
- Keep casual scores deterministic and profile-stamped.
- Keep casual actions replayable through command submission.

## 12. Implementation phases

Planning-only order:

1. Build a top-down movement/tile prototype using the Stardew reference and
   existing terrain assets.
2. Add player, camera, minimap, and simple interaction prompts.
3. Add a maintenance yard and field lease using extracted oilfield sprites.
4. Connect drive-to-object actions to engine commands through `EngineHost`.
5. Add a contract board and monthly tick/seasonal loop.
6. Add casual HUD, hotbar, toast, dialogue, and scene transitions.
7. Add weather/day-night/wind ambience.
8. Add challenge result and local leaderboard.
9. Add MCP smoke tests for deterministic challenges.
10. Polish art direction and performance.

## 13. Open decisions

- tile size and whether to make new square top-down atlases;
- mechanical avatar art source: service truck, cursor, or drone;
- whether any visible crew/operator is represented indirectly through vehicles
  and equipment; no humans or animals in any case;
- exact challenge duration;
- online leaderboard timing;
- which casual actions are host-only vs engine commands;
- whether to use `AllCapabilities` or arcade/standard fidelity profile for the
  first casual prototype.

No engine or Godot code was changed by this document.
