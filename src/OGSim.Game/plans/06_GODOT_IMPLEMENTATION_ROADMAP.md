# Godot implementation roadmap

This is a phased plan for turning `OGSim.Game` from the current tile-map shell
into a playable host for the engine. It changes only the Godot project.

## Phase 0 — Baseline

Current state:

- `project.godot` runs `node_2d.tscn`.
- `Control.cs` is empty.
- No engine reference, no game loop, no command UI.

Acceptance:

- the existing scene still runs;
- this plans folder is present;
- no engine C# file has been modified.

## Phase 1 — Engine host boundary

Deliver:

- one `EngineHost` owner;
- one selected host pattern from `02_GODOT_HOST_INTEGRATION.md`;
- new-game/load-game entry point;
- a single synchronous tick call;
- a log of build/start results.

Acceptance:

- a headless host can build `EngineBuilder.Build` or start the bridge;
- a completed tick produces a `FieldReadModel`;
- failures are visible instead of silently swallowed.

## Phase 2 — Read-only snapshot UI

Deliver:

- `SimulationController` autoload with pause/normal/fast;
- `snapshot_changed` signal;
- debug read-model inspector showing tick, date, cash, wells, activities,
  production, insolvent, and outcome.

Acceptance:

- every `TickCompleted` updates the inspector;
- paused game does not advance;
- `TickAbandoned` and `TickHalted` are visible.

## Phase 3 — Company dashboard

Deliver:

- cash and monthly production;
- well list from `Wellbores`;
- bottleneck list from `Chain`;
- calendar/turn display.

Acceptance:

- dashboard is rebuilt only from the latest `FieldReadModel`.

## Phase 4 — Command panel

Deliver:

- drill command with prospect selection and depth;
- seismic survey command;
- open/shut well command;
- abandon well command;
- install separator command;
- expand export command;
- domain-typed rejection display.

Acceptance:

- commands are submitted between ticks;
- accepted and rejected results are both surfaced;
- exclusive rig/scheduler state is respected.

## Phase 5 — World map

Deliver:

- terrain tile map from the world view/generator data available to the host;
- prospect markers from `FieldReadModel.Prospects`;
- well markers from `FieldReadModel.Wellbores`;
- click-to-inspect panels.

Acceptance:

- map reflects snapshot data;
- map does not read subsurface truth or internal engine state.

## Phase 6 — Production flow diagram

Deliver:

- visual chain from manifold to export;
- throughput labels;
- deferred/bottleneck labels;
- connection state animations.

Acceptance:

- flow diagram matches `ChainElementView` order and data.

## Phase 7 — Event and audit UI

Deliver:

- sealed event feed from `Engine.Events`;
- severity/category styling;
- audit screen for entity/category/cause-chain/loss queries.

Acceptance:

- only latest tick events are polled;
- history is pulled from audit, not from retained events.

## Phase 8 — Persistence

Deliver:

- save slot list and metadata;
- save/load file I/O in Godot;
- state payload through the engine/bridge;
- migration/validation error display.

Acceptance:

- a save/load cycle reproduces the same deterministic engine state;
- the Godot host owns slots and paths.

## Phase 9 — Scenario and game feel

Deliver:

- scenario start/success/failure screens;
- insolvency handling;
- audio and animation hooks;
- tutorial text using command/rejection localisation.

Acceptance:

- a full playthrough can be started, played, lost/won, and restarted.

## Phase 10 — Polish and diagnostics

Deliver:

- performance profiling of snapshot/UI rebuilds;
- no per-tick allocations in Godot presentation where avoidable;
- clear separation between engine DTOs and Godot presentation models;
- documentation updates in this folder.

Acceptance:

- game remains responsive at the desired maximum speed;
- no engine code changed;
- new game code is reviewable against the workflow and responsibility docs.
