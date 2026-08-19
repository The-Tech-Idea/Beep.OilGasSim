# Oilfield Days — build log

What has actually been built of the casual mode, and the decisions taken while
building it. Planning lives in [11_CASUAL_TOPDOWN_GAME_MODE_PLAN.md](11_CASUAL_TOPDOWN_GAME_MODE_PLAN.md)
and [12_OILFIELD_DAYS_MOCKUP_REVIEW.md](12_OILFIELD_DAYS_MOCKUP_REVIEW.md);
this records the code.

**Project:** `src/OGSim.Game/Oilfield Days/oilfield-days` — its own Godot 4.7
project, separate from `Oilfield Engineer`. Assembly `OilfieldDays`, `net8.0`
under `Godot.NET.Sdk/4.7.0`.

## 1. The scenes

Plan 08 §2's flow, and the five Oilfield Days mockups, as eight scenes:

| Scene | File | What it is |
|---|---|---|
| Main menu | `scenes/MainMenu.tscn` | title, new basin, best runs (plan 08 §6.1) |
| New basin | `scenes/NewGame.tscn` | seed, reality profile, basin size — the knobs `EngineSettings` and `WorldParameters` take (§6.2) |
| Gameplay | `scenes/Gameplay.tscn` | the workspace: the basin, the truck, the HUD, the action bar (§6.5) |
| Dispatch board | `scenes/Dispatch.tscn` | mockup 2 — the work the company can order, and the rig |
| Lease board | `scenes/Lease.tscn` | mockup 3 — the basin's structures, a map, and the five-factor radar |
| The yard | `scenes/Fleet.tscn` | mockup 4 — wells, chain and rig with real meters |
| Challenge result | `scenes/Result.tscn` | mockup 5 — scorecard from `ScenarioProgress`, local leaderboard |
| Pause | `scenes/Pause.tscn` | resume, boards, give up, menu (§6.6) |

`SceneRouter` (autoload) owns the flow: `Go` changes scene, `OpenOverlay` puts a
board over the running game. The boards are overlays rather than scene changes
because the field goes on existing behind them.

**Where the mockups were read against the engine.** The dispatch mockup's job
cards pay invented cash and the lease mockup's build menu sells equipment;
neither exists in the engine, and plan 11 §7 forbids inventing them. So the
dispatch board lists the nine commands with what each needs and what it is aimed
at, and the lease board lists the structures world generation placed. The shape
of both mockups survives — list, detail, confirm — and nothing behind them is
made up.

## 2. Where things are

```
oilfield-days/
  game/
    Main.cs               the workspace: context, input, and the run's start/end
    DevOptions.cs         --seed / --months / --drill-best / --at
    DevScreenshot.cs      --shot=<file>: run, settle, save a PNG, quit
    Host/
      EngineHost.cs       autoload; the ONLY owner of the engine
      SimulationController.cs  autoload; pause/speed and the only tick caller
    World/
      BasinWorld.cs       the generated basin drawn: prospects, wells, chain
      ProspectMarker.cs   a survey stake with a probability-of-success ring
      DualGridTerrain.cs  SpriteCook 15-piece dual-grid renderer
      WorldMap.cs         logical ground
      ServiceTruck.cs     the player, who is a truck
      GameInput.cs        input actions, registered in code
    Ui/
      GameHud.cs          KitPanel/KitLabelValue/KitMeter bound to FieldReadModel
      CommandBar.cs       the nine engine commands, offered by context
      ResultScreen.cs     the scenario's verdict
  scenes/Main.tscn
  assets/…               tilesets, HD props, the tanker truck
```

## 3. The engine runs in process

`OilfieldDays.csproj` targets **net10.0** and holds a `ProjectReference` to
`OGSim.Composition`. Plan 02 §1 left this open because the Godot project was
net8.0; Godot 4.7.1 loads the net10.0 assembly, so the out-of-process bridge of
plan 02 §2 is not needed. `EngineHost` calls `EngineBuilder.CreateNew`, runs the
opening tick, and publishes `FieldReadModel`; `SimulationController` is the only
caller of `AdvanceTick`.

**The first build was wrong and was deleted.** It had a hand-drawn yard, a build
menu of eight priced equipment kinds, a job board paying invented cash, and a
flat per-well revenue — none of which the engine has. What replaced it:

| Deleted | Now |
|---|---|
| hand-placed yard and lease | prospects at `ProspectView.At`, from world generation |
| build menu / shop | the nine commands in `OGSim.Composition` |
| "place a wellhead" | `DrillWellCommand` on a prospect, which may come back dry |
| invented job rewards | the company ledger, through the tick |
| host-side production maths | stages 5→8: solve, commit, meter, post |
| invented equipment list | the composed `SurfaceChain`, drawn in its own order |

## 4. Decisions taken while building


**The engine is behind an interface and is not referenced yet.** `IEngineGateway`
is the only door; `SandboxGateway` implements it with host-side state and a flat
per-well production rate. This follows plan 11 §12's order — movement, world,
yard and lease, *then* engine commands — and it means every screen and every
command path is finished before the net8/net10 question of
[02_GODOT_HOST_INTEGRATION.md](02_GODOT_HOST_INTEGRATION.md) §1 has to be
answered. The sandbox invents no reservoir: a wrong reservoir is worse than none.

**Terrain is dual grid, not blob autotiling.** The generated 15-piece atlases
picture 2x2 *corner* patterns, so a drawn tile sits between four logical cells
with a half-tile offset. Each material is its own `TileMapLayer`; the mask order
and the mask→frame table are SpriteCook's and are not re-derivable by eye.

**The game has its own `Directory.Build.props`, deliberately empty.** Without it
the project inherits the engine's (`net10.0`, nullable, warnings-as-errors) and
neither the beep addons nor the Godot source generators compile under those.
Implicit usings stay off for the same reason: the addons say `using Godot;` and
`using System.IO;` in one file, which makes `FileAccess` and `Timer` ambiguous.

**The old empty host was moved aside.** `Control.cs` declared a global class
named `Control`, which shadows `Godot.Control` for the whole project. It and
`control.tscn` now sit in `Oilfield Days/_old-host/`.

**Thirty days make a month.** The engine's calendar is 30/360, so a host day is
an exact subdivision of an engine tick rather than an approximation that drifts.

**The ground is the project's own 17-piece tilesets, on a noise bitmap.**
`assets/tilesets` holds eighteen sheets in a 17-piece layout — a 3x3 core, a
vertical strip, a horizontal strip, a single and an inner corner in a 5x5 grid.
They ship as JPEG on a transparency checkerboard, so `flat17/` holds them keyed
to real alpha: the checker is found by brightness-and-saturation, flood-filled
from the border so a pale patch *inside* a tile survives, and the last two
pixels of every edge are peeled to take the JPEG fringe with them.

`EdgeMaskTerrain` draws them. It is **not** the dual-grid renderer the SpriteCook
15-piece atlases need — those picture corners and sit between cells; these
picture edges and sit on them — so the two have separate classes and the wrong
one puts every boundary half a tile out.

The layout comes from `TerrainMap`: Perlin fBm with five **octaves**, lacunarity
2.0 and **persistence** 0.48, a ridged pass that only bites above the halfway
mark so the tops break up and the lowlands stay smooth, and a second field for
moisture. Water, shore, grass, dry grass and rock each get their own
`TileMapLayer`, stacked low to high, so every transition is drawn by the sheets'
own edge pieces. Anything that gets built on is levelled first. Scenery scatters
by a hash of the tile, on open grass only.

**The world is built, not marked up.** Six tiles to the engine's kilometre, so a
well is a gravel pad you drive onto and the road to it is a journey; at one tile
per kilometre the basin was a chart with pins in it. Three `TileMapLayer`s —
grass, gravel, road — each with its own atlas and its own dual-grid paint, plus a
scenery pass that scatters trees, scrub and boulders by a hash of the tile so a
basin is dressed the same way every time. The yard has the mockup's buildings on
it and a truck runs the road whenever the engine reports an activity.

**Art comes from the project's own libraries.** `assets/topdown/sprites` (75
sprites cut from the reference sheets) dresses the world, because it is drawn
top-down; `assets/sprites/256` (76 sprites) is the icon set for the boards. The
player truck is cut from `referenceart/TopDown-StartDewValley/trucks.png` with
the chroma key and halo erosion `assets/topdown/README.md` specifies — three
facings drawn, the fourth a free mirror. Two gaps were generated to match: a
pumpjack (the sheets have none) and tree/scrub/boulder.

**The earlier HD-line note, kept because it is still true of the pumpjack:** The first pass
dressed the game from `assets/topdown/sprites` — the sprites cut from the
reference sheets — and they are the wrong line: `assets/_sample-hd-topdown/`
holds the style the project settled on, and its README carries the recipe.
Everything on the lease is now that line: the pumpjack, storage tank, control
room and tanker truck are the existing 1024 px masters, and the wellhead,
separator, flare, generator, water-injection pump and manifold were generated to
match (`pixel=false`, 1K, `bg_mode=white` then `remove_background`,
`smart_crop power_of_2`, the three referenceart sheets as `style_asset_ids`, and
the flat front/side elevation wording that keeps the camera out of isometric).
Files are the masters with transparent padding trimmed — trimming is not
scaling, and the anchor is the bottom centre.

**Texture filtering is `LinearWithMipmaps`, not `Nearest`.** The masters are
1024 px art shown two to three cells tall, which is a large reduction; nearest
sampling made the tank's ladder and the flare's lattice crawl.

## 5. What works today

Measured on seed 3, three holes drilled through the real command path:

- the engine composes fifteen modules and generates a basin of seven prospects,
  each at a real coordinate with a probability of success risked from five
  petroleum-system factors;
- driving to a stake offers **shoot seismic** or **drill (2,000 m)**; driving to
  a well offers **open/shut**, **well test**, **wireline log**, **cut core** and
  **abandon**; driving to the plant offers **install separator** and
  **expand export**;
- a dry hole costs the money and the months and leaves a marker — and disproves
  the play's source factor, which re-prices every prospect drawing on it;
- a discovery de-risks the play: the run's POS went 0.23 → 0.32 → 0.40 as the
  first well came in;
- the field produced 36,593 m³ in month 31 and cash went $49.7M → **$146.8M**;
- the chain draws in the engine's own order with per-element throughput, and the
  separator's 15,747 t of deferral shows as the bottleneck it is;
- the objective meter is the scenario's own — $600M by month 120 — and the run
  ends on `Insolvent` or a terminal `ObjectiveState`.

## 6. The five mockups, built

| Mockup | Screen | What it took |
|---|---|---|
| 1 Casual main scene | `Gameplay.tscn` | wooden HUD signs, hotbar, context prompt, the run sign, minimap top-right, labelled yard buildings |
| 2 Dispatch terminal | `Dispatch.tscn` | icon cards with state plates and difficulty stamps, parchment order with equipment art, green dispatch / red back, equipment strip |
| 3 Lease construction | `Lease.tscn` | structure cards with POS badges, the basin map with the chosen pad lit, parchment card with cost/area/placement and the five factors as bars, placement-mode footer |
| 4 Vehicle/equipment garage | `Fleet.tscn` | WELLS/CHAIN/RIG tabs, rows of art + name + two meters, parchment detail with the thing large and its meters, action row |
| 5 Challenge result | `Result.tscn` | title with a rank rosette, scorecard of icon rows, local leaderboard with medals and the player's row lit, three actions |

Shared pieces live in `ScreenChrome`: wooden signs with title plates, parchment
cards, chunky painted buttons, `Icon` off `assets/icons` (the 256 library),
`Badge`, `Rosette`, `Medal`, and the four-chip `TopBar` every board wears.

**Where a mockup asked for something the engine does not have, the layout stayed
and the content changed.** The dispatch cards are the nine commands rather than
invented jobs; their difficulty stamp is the target's probability of success,
read not assigned. The lease menu is the basin's own structures rather than a
shop, and its padlock is a structure whose source a hole has already disproved.
The garage's condition and fuel bars are a well's share of the month and a chain
element's pass rate. The scorecard says plainly that the shipped scenario scores
nothing yet. The HUD's "Reputation" and "Actions-Left" positions carry engine
numbers, because plan 11 §11 forbids inventing the two the mockup names.

**Known gaps against the images:** the fences and warning signs mockup 1 draws
around the yard; a well's own production reads zero while the field reads its
month (`WellStatusView.ProducedThisTick` is what the engine publishes, and it is
shown rather than patched); and the difficulty stamp crowds the right edge of a
dispatch card.

## 7. Not built yet

- **terrain**: `WorldView` (terrain, settlements, transport, harbours) is in the
  contract surface but the concrete `Engine` does not publish it, so the basin
  floor is neutral ground — plan 09 §2's "plan now, expose later";
- the main menu, new-game setup, save/load and pause screens of plan 08 §6;
- the garage and challenge-result screens (mockups 4 and 5);
- the minimap the mockups show top-right — a 24 km basin needs one;
- the event feed and audit screen (plan 06 phase 7);
- animated plant (`flare-stack_burning`, `pumpjack_working` and the rest exist
  under `assets/topdown/animations`);
- weather, day/night and the beep_ui theme pass — no addon is enabled yet;
- **the yard is still the old pixel line**: maintenance workshop, the two
  warehouses, office cabin, fuel tank, frac tank and lighting pole are the only
  sprites left from `assets/topdown/sprites` and do not match the lease. Six
  sprites at 13 credits each closes it.
