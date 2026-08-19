# Godot addons inventory and usage plan

This document records what was found in `src/OGSim.Game/addons` and how each
addon can support the OilGasSim Godot host. It is planning only. No addon has
been enabled, configured, or modified.

## 1. Inventory summary

There are three addons:

| Addon | Version | Language | Purpose |
|---|---:|---|---|
| `beep_game_builder_cs` | 0.5.0 | C# / Godot .NET | File-based skins, 100+ ECS components, scene templates, weather/day-night, editor dock, optional MCP commands. |
| `beep_ui` | 0.2.0 | GDScript | Drag-and-drop themed UI: 22 presets, 11 effects, 114 widget prefabs, toast host. |
| `godot_mcp` | 0.2.0 | C# / Godot .NET | WebSocket bridge to a local MCP server so an AI agent can inspect and drive editor/runtime. |

## 2. Beep UI

### What it provides

`beep_ui` is the most directly useful UI addon for OilGasSim because it is
self-contained and includes an `OilGas` preset specifically described as
"heavy industrial UI".

Key runtime pieces:

- `BeepThemeApplier` — styles a whole `Control` subtree from one preset.
- `BeepPreset` and 22 preset scripts — including `preset_oilgas.gd`.
- `BeepUIEffect` — 11 animated effects: slide, shake, pulse, bob, flash,
  glitch, rotate, fade, typewriter, bounce, offset.
- `BeepWidgetFactory` — builds themed bars, stat displays, captions, panels,
  button lists, grids, toasts, crosshairs, overlays, and scaffolds.
- `BeepToastHost` — working toast notifications with info/success/warning/error.
- `Theme Studio` editor dock — visual preview and widget palette.

### Recommended OilGasSim use

Use it for the **screen/UI shell**:

- Apply `OilGas` as the default game theme.
- Use bars and stat widgets for cash, production, water cut, gas-oil ratio,
  scenario progress, and facility utilisation.
- Use panels for company dashboard, well inspector, prospect cards, flow chain,
  and save/load dialogs.
- Use `BeepToastHost` for engine events, command rejections, and save confirmations.
- Use `BeepUIEffect` for modal entry, snapshot update emphasis, warning pulses,
  and scene transitions.
- Use widgets tab as a fast editor-time starting point, then replace scaffolds
  with the real OilGasSim presentation.

It is GDScript, but a C# Godot project can still instantiate and interact with
its nodes/autoloads. Prefer treating it as a visual layer only.

## 3. Beep Game Builder (C#)

### What it provides

This is a much larger component library and editor workflow:

- `BeepGenreScene` — entry point that instantiates a genre main scene.
- `GameApp` / `Settings` / `Locale` autoloads generated for game session,
  settings, and localisation.
- `GameInfo` resource for static project configuration.
- `ThemePresetComponent` — C# runtime themer with genre/theme/palette and
  optional 9-patch textures.
- `GameFlowComponent` — pause overlay, game-running state, score/lives/end flow.
- `MenuComponent`, `NavigationComponent`, `SceneTransitionComponent`.
- `TurnManager` — a tiny turn counter and `TurnEnded` signal.
- `GameSpeedComponent` — pause/1x/2x/3x control, currently wired to
  `CityEconomyComponent`.
- `SaveLoadManagerComponent` and save/load menu prefabs.
- `DataBinderHostComponent` — two-way UI/data binding.
- `LocalizationComponent` and CSV translation template.
- `ToastNotificationComponent`.
- `WeatherSystemComponent`, `DayNightCycleComponent`, `WindFieldComponent`.
- 100+ other gameplay/controller/world components.

### What to use for OilGasSim

Use the **host-facing infrastructure**:

- `ThemePresetComponent` / skin catalogs for C# scene theming.
- `GameApp`, `Settings`, and `Locale` as session/config/localisation foundations,
  adapted to OilGasSim terms.
- `GameFlowComponent` for pause overlay and run-state signals.
- `SaveLoadManagerComponent` as slot/menu UI scaffolding, with the actual engine
  payload bridged through the OilGasSim host.
- `DataBinderHostComponent` to bind `FieldReadModel` fields to dashboard labels,
  progress bars, well lists, and prospect cards.
- `ToastNotificationComponent` for event/rejection toasts.
- `SceneTransitionComponent` and modal components for game feel.
- `WeatherSystemComponent`, `DayNightCycleComponent`, and `WindFieldComponent`
  for environment ambience if the engine exposes matching environment state.

Use the top-down genre scene/template as a **visual starting point only**, not
as the OilGasSim game architecture.

### What not to use

- Do not use `TurnManager` as the engine tick. The engine already owns time;
  Godot only decides when to call `AdvanceTick`.
- Do not use `GameSpeedComponent` directly as the engine speed control. It is
  currently coupled to `CityEconomyComponent`. Build the OilGasSim pacing
  controller against the engine instead, optionally copying the UI pattern.
- Do not use generic `GameStateManagerComponent` as the engine save format. Use
  it for UI slot flow only, then persist engine state through the engine bridge.
- Skip combat, shooter, platformer, racing, cardgame, puzzle, survival, and RPG
  gameplay components unless a future minigame is explicitly designed.
- Weather visuals should not drive simulation truth; engine environment remains
  the source of truth.

## 4. Godot MCP Bridge

### What it provides

`godot_mcp` connects Godot to a local MCP server over WebSocket:

- Editor inspection/editing of scene tree, nodes, shaders, tweens.
- Runtime inspection and controlled actions.
- Generic command registry for project-specific commands and state.
- Explicit `McpGameAdapter` for game-specific commands without reflection.
- Node/property inspection and screenshots.
- Local-token authentication.

### Recommended OilGasSim use

Use it as a **development and test tool**, not a shipped player feature.

Possible adapters:

- `sim.new_game` — build a new engine with configured seed/profile.
- `sim.advance_tick` — call `AdvanceTick` once or N times.
- `sim.submit_command` — submit a command and return accepted/rejected result.
- `sim.snapshot` — return the current `FieldReadModel` as JSON.
- `sim.events` — return latest sealed engine events.
- `sim.audit` — run an audit query.
- `sim.save` / `sim.load` — exercise the save/load host path.

Register only explicit commands and state providers. Do not expose arbitrary
reflection or engine internals.

Security rules:

- keep the bridge localhost-only;
- always set a token;
- keep editor writes disabled for normal use;
- do not ship this as a player-facing remote-control surface without a security
  review.

## 5. Proposed mapping to the OilGasSim plan

| Planned game area | Addon to use | Role |
|---|---|---|
| Theme and overall UI style | `beep_ui` `OilGas` preset | Primary industrial UI skin. |
| Menu/HUD/panel widgets | `beep_ui` `BeepWidgetFactory` | Fast themed bars, stats, panels, toasts. |
| UI motion | `beep_ui` `BeepUIEffect` | Slide, pulse, fade, shake, typewriter. |
| C# runtime theming | `beep_game_builder_cs` `ThemePresetComponent` | C# scene-level theme application. |
| Session/settings/localisation | `beep_game_builder_cs` `GameApp`, `Settings`, `Locale` | Adapt to OilGasSim session metadata. |
| Pause/run state | `beep_game_builder_cs` `GameFlowComponent` | Pause overlay and run-state signals. |
| Engine pacing | Custom `SimulationController` | Owns pause/speed and calls `AdvanceTick`. |
| Engine snapshot → UI | `beep_game_builder_cs` `DataBinderHostComponent` | Bind read-model values to controls. |
| Event/rejection feedback | `beep_ui` `BeepToastHost` or `ToastNotificationComponent` | Player-facing messages. |
| Save/load menu | `beep_game_builder_cs` `SaveLoadManagerComponent` | Slot UI scaffolding; engine bridge owns payload. |
| Environment ambience | `beep_game_builder_cs` weather/day-night/wind | Visual atmosphere only. |
| AI-assisted build/test | `godot_mcp` + `beep_game_builder_cs` MCP commands | Editor scaffolding and runtime smoke tests. |

## 6. Risks and constraints

- `beep_game_builder_cs` and `godot_mcp` are C# editor plugins and require the
  project to build before they can be enabled.
- `beep_ui` is GDScript; mixing is supported but keep the game's engine-facing
  logic in C# and treat `beep_ui` as a visual/theme layer.
- The C# builder addon is generic and game-genre oriented. It should not become
  the architecture. OilGasSim remains a management simulation with the headless
  .NET engine as the only simulation source of truth.
- Some components, such as `GameSpeedComponent`, are coupled to generic demo
  systems and must not be assumed to drive OilGasSim correctly.
- The MCP bridge is powerful and should be treated as a privileged developer
  tool.

## 7. Immediate planning decisions

1. Keep engine time and game pacing in an OilGasSim-owned `SimulationController`.
2. Use `beep_ui` `OilGas` as the first UI theme candidate.
3. Build company/dashboard/well/prospect screens as normal Godot controls and
   apply themes, not as generic genre scenes.
4. Use toast hosts for command rejections and engine events.
5. Use save/load menu prefabs as UI scaffolding, but define an OilGasSim save
   DTO that carries both engine payload and host metadata.
6. Use MCP only for development, automation, and smoke testing.

No addon code or project settings were changed by this review.
