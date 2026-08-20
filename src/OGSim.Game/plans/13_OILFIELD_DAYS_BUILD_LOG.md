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


## Built to GAME-SDD-002's mockups

The game SDD arrived with three mockups that had not been built to: the one-page
**Create New Game & World Setup**, the five-panel **New Game UI flow**, and a new
gameplay scene concept. `14_GAME_SDD_CONFORMANCE.md` holds the full measurement;
what changed in the build:

- **`scenes/NewGame.tscn` rebuilt to mockup 5** — company name, starting capital,
  game mode, then WORLD SETTINGS: size, climate profile, five-star oil and gas
  richness, starting era, third-party industry, map seed with a reroll, and
  "Show Seed On Map". Back and Generate World along the bottom. It is drawn in
  slate rather than the yard's wood and paper, because the mockups use two
  registers and setup happens before there is a yard: `SetupChrome`, next to but
  not merged with `ScreenChrome`.
- **The preview is the real world.** A `BasinWorld` built in a `SubViewport` from
  the seed and knobs on screen, painted by `PaintBareGround` — the same tilesets,
  autotiling and scatter the game uses, with no prospects and no wells because at
  setup there is no engine to have any.
- **Land fraction and climate reach the built world.** `TerrainMap` takes both,
  and sea level is read off the sorted height field rather than compared against
  a constant, so the number on the setup screen is the fraction of the basin that
  comes out dry.
- **Starting era sets the epoch.** `EngineSettings.Epoch` follows the picker, and
  the HUD's Year 1 counts from the run's own epoch instead of a fixed 1965.
- **Nine more engine commands on the dispatch board** — repair, overhaul, fit
  monitoring, install manifold/gas plant/treater/tank, remediate injector,
  borrow, repay, and the water flood. The repair order is the one that mattered:
  without it a failed element shut the field in permanently and seed 3 produced
  nothing for 55 months. With it the same seed makes 36,577 m³ a month.


## Brand art, and the UI kit

Thirteen more mockups arrived — a logo, key art, a main menu, four setup
variants, three gameplay styles and a dispatch board. They settle two questions
the build had been guessing at.

**The house style is slate, not wood.** Every screen wrapped around the yard —
menu, setup, generation, the boards' framing — is dark navy with amber panel
banners and a left icon rail. The painted wood and paper stays where it belongs,
on the in-world HUD. `SlateChrome` is that second register; `ScreenChrome` keeps
the first.

**The kit was already in the project and was not being used.** `Beep.ECS.UI.Kit`
ships 73 components, including the exact widgets the mockups draw: `KitPanel`
with its title banner, `KitButton`, `KitLabelValue`, `KitSlider`,
`KitStarRating`, `KitCheckBox`, `KitArrowSelector`, `KitChip`. Two of them had
been hand-written here instead. Everything now sits on the kit.

The kit is theme-driven, which is the part worth writing down: its controls read
`UiSurface.Of` and `UiSurface.Semantic`, which resolve against a Godot `Theme`
carrying a `BeepSemantic` colour set — `neutral` is the panel face and is
consulted first, then `accent`, `accent2`, `success`, `warning`, `danger`,
`info`. So a skin is one theme, not a stylebox per control. `KitTheme` builds it
from the mockup palette and `SceneRouter` hangs it on the window root in
`_EnterTree`, before the first screen draws.

One trap: `KitPanel` is a `Panel`, so it paints but does not lay out, and hung on
its own it reports a zero minimum size and collapses behind the content it was
meant to frame. `SlateChrome.Frame` carries it in a styleboxless
`PanelContainer` alongside a `MarginContainer` — the container hands both
children its full rect, so one paints and the other lays out.

**New screens.** `Splash.tscn` shows the key art and is now the main scene;
`Options.tscn` is the menu's fifth entry, holding display, audio and controls —
the settings §2 puts on the client's side of the line. Difficulty is not among
them: difficulty is the reality profile, fixed when the world is created,
because moving it mid-run would change the models under a running simulation.

**Main menu and setup rebuilt to their mockups.** The menu is the mark over the
key art with the entry stack and field notes; setup is the five-step breadcrumb,
the world settings with named values rather than bare numbers, the seeded preview
under its own legend, and the world's measurements along the bottom.

Three of the mockups' readouts are deliberately not drawn as shown:

- **the company card** on the menu — a name, a reputation and a balance carried
  from a save. There is no save (G-10) and reputation has no engine owner (G-04).
- **"Estimated Fields 18 – 28"** in the world info strip. The host cannot know
  how many accumulations a seed holds; that is what drilling is for.
- **separate oil, gas and NGL potential meters**, which would imply a survey. One
  meter, labelled as the richness setting it reads back.

What replaced them is measured: land, water, high ground and dry country are
counted off the terrain that was just generated.


## The UI atlas, nine-patched

A supplied UI atlas (`referenceart/Mockup/UI_Atlas`, eight sheets) replaced the
drawn chrome. It ships in `assets/ui/atlas`; the pieces the game uses are cut
into `assets/ui/nine`.

**Nine-patch, not painted.** Every frame, plate and field is a `StyleBoxTexture`
over a cut piece: corners pinned, edges tiled, middle filled. That is what lets
one 242-pixel plate serve a 1400-pixel panel with its bolts and bevel still the
size the artist drew them. A `StyleBoxFlat` can imitate the colours and can never
carry the art.

**The pieces are found, not measured.** `DevAtlasSlice` (`--slice-index`) flood-
fills each sheet's ground away and reports the bounding box of every element;
`--slice` cuts the named ones. Measuring by eye off a screenshot puts a pixel of
neighbouring ground into every nine-patch, which shows on screen as a seam.

Two things that cost a round each and are worth knowing:

- **The buttons have their labels painted in.** A nine-patch stretches its
  middle, so a plate taken whole smears "Confirm" across the button. The cut
  keeps the left N and right N pixels — the rounded end and its rim — and throws
  the lettered middle away. That is the `Cap` column in `DevAtlasSlice.Wanted`.
- **Two plates that touch are one component.** The first `field` cut spanned an
  amber plate and a slate one, so every dropdown drew as two widgets side by
  side. The fix is a different source rect, not a different detector.

**Panels and cards are not the same thing**, and conflating them is what made the
first pass look wrong. In the reference sheets a *card* — Objective, Event,
vehicle status — carries a solid coloured header bar flush across the top of the
frame. A *panel* — World Setup, World Info, Resource Potential — is headed by a
line of gold uppercase type over a hairline, with no bar at all. `Frame` builds
the panel, `Card` builds the card, and putting the card's header on every panel
painted the screen in blocks the art does not have.

Rows follow the sheets too: icon, label in muted grey, value hard right in gold,
hairline under. The kit's `KitLabelValue` draws a filled pill instead, and a
column of those is a wall of lozenges nothing in the reference art has — so the
row is built here, and the kit keeps `KitStarRating`, `KitSlider` and
`KitCheckBox`, which it draws well.


## The gameplay shell: status bar and icon rail

The gameplay mockups frame the world with a status bar across the top and a rail
of icon buttons down the left. Both now exist, drawn on the atlas's plates, on a
`CanvasLayer` above the world — `StatusBar` and `IconRail`.

**The capsules carry published numbers and nothing else.** The mockups show Cash,
Reputation, Oil Rate and Gas Rate. What shipped is date, cash, debt, oil price,
daily rate, wells and weather:

- **No reputation.** Gap G-04 — no published engine metric owns it, and a bar
  reporting 68 would be reporting nothing. Debt took the slot, which the company
  does own.
- **No gas rate.** The read model publishes one produced volume and the chain's
  throughputs in mass. Splitting a gas rate out of that is reservoir engineering,
  which plan 11 §7 puts on the engine's side of the line. The gas plant's own
  throughput is on the chain, where it is measured.
- The daily rate is the month's volume over **thirty**, which is exact rather
  than rounded: the engine's calendar is 30/360, so a month *is* thirty days.

**The rail lists what exists.** Map, Jobs, Leases and Fleet open; Build,
Production, Finance and Research are greyed with the reason on the tooltip. Build
is blocked rather than unwritten — gap G-02, no engine command places a facility
at a coordinate, so a build mode could draw a ghost and never put anything down.

**The wood HUD gave up its status sign.** Cash, wells and production now read on
the shell, and two readouts of one number is how a HUD starts disagreeing with
itself. The objective bar moved to the CHALLENGE sign, where it is not a
duplicate: the status bar reports the company's instruments, the challenge sign
reports the scenario's deadline and target.


## The right-hand column

`SidePanels` completes the gameplay mockups' shell: OBJECTIVES, ALERTS,
PRODUCTION, RESERVES and THE BASIN, scrolled rather than clipped so the stack
survives a scenario with more objectives or a month with more warnings.

- **OBJECTIVES** is `ScenarioProgress`, verbatim. The engine judges at stage 12
  and publishes at stage 13; a host that recomputed "am I there yet" could
  disagree with the run it is displaying. The deadline reads here too, beside
  what it bounds.
- **ALERTS** is the tick's own sealed event set, filtered to Warning and
  Critical. The bus seals hundreds of events on a busy month, and what is dropped
  is dropped by the engine's own severity rather than by the host's taste.
- **PRODUCTION** is a trend, and the history is the host's — the read model
  carries one month at a time and the engine keeps no series to ask for. Each
  point is a value the engine published on the tick it was sampled; nothing is
  interpolated, smoothed or filled in, so a gap in play is a gap in the line. The
  sample is taken once per tick, not once per frame.
- **RESERVES** is Proved / Probable / Possible, not the mockup's oil / gas / NGL
  split, which the read model does not carry and the host cannot derive. The
  replacement ratio prints "not yet measurable" where the engine publishes null:
  under twelve months there is no window to measure over, and 0.00 would state a
  replacement failure that has not happened.
- **Next Payday**, the mockups' fifth panel, is absent. There is no payday in the
  engine's economics; cash settles every tick.

The wood HUD is now only what belongs to the yard — hotbar, prompt, toasts. Every
readout moved to the shell, and nothing is drawn twice.


## Save and load — G-10 closed

The SDD's ship blocker is done. `SaveSlots` owns the folder, the names and the
file I/O; `EngineHost.Save` and `EngineHost.Load` go through `SaveGame.Write` and
`SaveGame.Read`/`Load`, which is R19 §5's split — the engine owns the payload and
the host owns everything about where it goes.

**The sidecar is not a second copy of engine state.** A save carries the world
seed, the epoch and the tick, which is all the engine needs. But the host drew a
basin from that seed at a particular size, land fraction and climate, and those
are the client's own presentation choices; reloading without them rebuilds the
same simulation under different ground. So the draft rides beside the save as a
small JSON file, and nothing in it is a fact the engine also holds.

**Slot names come from the tick, not the clock.** A save list ordered by the
player's wall clock reorders itself when a machine's clock moves. `m0043-00` is
month 43, first save at that month.

**A loaded game resumes on the following month, and this is an engine
constraint rather than a choice.** The read model is published by the Close
stage, so a freshly restored engine has none — the state is all there and nothing
has yet projected it. There is no "project without advancing" on the engine
surface, and the engine's own reload tests advance a tick for the same reason. So
`Load` runs one tick, which is the month that would have come next anyway; what is
lost is the chance to look at the saved month before playing it. **A projection
callable at tick zero would remove this, and that is an engine change.** It is
the one thing about save/load worth taking back to the design set.

Round trip, seed 3: saved at month 43 with $109.7M, 2 wells and 36,590 m3;
reopened at month 44 and played to 46 with $113.5M, the same wells and 36,589
m3 — the field carried on exactly where it left off.

The menu's CONTINUE and LOAD GAME are live. Continue opens the newest slot,
`Load.tscn` lists them all with company, month, cash, wells, basin and seed read
from the sidecars rather than by opening payloads, and the pause menu saves.


## The dispatch board on the atlas

`gameplay_2_DispatchBoard.png` is a slate screen and the board was still painted
wood, so it moved onto the atlas chrome: a header strip with the run's four
readings, AVAILABLE WORK down the left as nine-patch rows, ORDER DETAILS on the
right, and the equipment strip along the bottom.

The logic did not change — the rows are still the engine's eighteen commands,
their state is still whether the engine would accept them now, and the difficulty
stamp is still read off a prospect's published probability of success rather than
assigned.

One layout note worth keeping: **a `Label` reports its full text as its minimum
width**, so a long reward description grew its row until the stamps on the right
were pushed off the card. Clipping the text with `TrimEllipsis` puts the loss
where a reader can see it, rather than in a stamp that silently vanished.


## The selection card

`gameplay1` puts a card for the selected thing at the bottom of the screen, and
`SlateChrome.Card` had been built for the atlas's card treatment without anything
using it. `SelectionCard` is that: whatever the truck is standing at, described,
with the coloured header bar flush across the top of the frame.

**A prospect's card is the exploration game in one panel.** The five factors —
source, reservoir, seal, trap, timing — are each a probability the engine already
published, and their product is the chance of success. Showing them apart is the
whole point: two prospects at the same odds fail for different reasons and are
worth different measurements, and the weakest bar is the one a survey, a log or a
core should be spent on. Nothing is recomputed here and no truth is shown; these
are beliefs, which is why they move when the company measures.

A well's card carries its status and what it made this month and a day; the
plant's carries the chain length, how many elements are out of service, and what
is holding production back.

**Two Godot traps, both now commented at the site:**

- `_Ready` runs *after* the owner has placed a control, so an anchoring preset
  applied there silently discards the offsets it was given. The card kept
  reappearing underneath the side column until that came out.
- Two controls pinned to the same edge are not laid out around each other. The
  one added later simply covers the other, so the card had to be moved out of the
  side column's lane rather than told to avoid it.


## Drilling spread across the basin

A development run drilling "the best prospect" three times put three holes into
the same structure, because the read model keeps a structure on the prospect list
after it has been drilled. `14_GAME_SDD_CONFORMANCE.md` records that as an engine
finding — the behaviour is arguably right, the field's documentation is not.

Host side, `DrilledSites` remembers which structures this run has ordered a hole
into, and both pickers — the dispatch board's and the development harness's —
prefer the best structure nothing has been sunk into yet, falling back to the
best overall once they all have. That is the client recalling its own commands
rather than second-guessing the engine.

It changed what a run looks like. Four holes on seed 3 at POS 0.23, 0.18, 0.15
and 0.14 return **one** well, which is the exploration game as designed; the
earlier three-holes-one-structure run returned three wells and showed no risk at
all.

A loaded save starts this record empty, and says so at the call site: which
structures were drilled is not among what the engine publishes, so a reload
cannot recover it. The worst that follows is a picker offering a structure that
has already been drilled, which is a legal order.


## The last screens migrated

Lease, Fleet, Results and the pause menu moved onto the atlas chrome, so every
screen outside the yard is now one register. `SlateChrome` grew the shapes those
screens were built around — `Sign`/`ContentOf`, `Text`, `Body`, `Backdrop`,
`Rosette`, `Medal`, `Meter`, `IconCard`, `Slab`, `Tag` — and the wood palette was
mapped by the job each colour did rather than by name: cream was body type,
faded was secondary, gold was heading, good and bad were the two verdicts.

Three things the migration taught, all now commented at the site:

- **A coloured plate under a list row reads as a button.** The atlas's blue,
  green and red plates are its *buttons*; using the blue one as a selection
  background turned every selected row into a giant Primary button. Selection is
  now the recessed field plate lifted a little, with an amber title carrying the
  rest of the signal.
- **A nine-patch is the wrong tool for a twelve-pixel bar.** The plates carry a
  bevel and bolts drawn for something four times that height, so patching them
  into a progress track gave two identical-looking plates and no readable fill.
  `Track` and `Fill` are flat on purpose.
- **`GetNode("%Content")` resolves a scene-unique name**, which only exists for
  nodes an editor authored. A panel built in code has none, so the lookup logged
  a "node not found" for every panel on screen before falling through to the
  search that actually worked.

`Card` and the old `Card` were two different things sharing a word — a titled
panel that describes one thing, and a button in a list. The second is now `Slab`,
which is what every call site actually wanted.


## Panels that size to their contents

A panel positioned by anchor has a plain `Control` for a parent, and **only a
container re-fits its children when their minimum size changes**. So a panel that
set its anchors at construction — before any content existed — froze at the size
it was handed, and every row added afterwards ran past the frame. The size looked
deliberate, which is what made it hard to see.

`SelfSizingPanel` fixes it at the source: it listens for `MinimumSizeChanged` and
takes its combined minimum, and stands aside inside a container, where a second
opinion about the rect would fight the sort every frame. Measured on the lease
board: `WHAT WE BELIEVE` was pinned at 520 and now takes 533, which is what its
content actually needs.

`Sign` also grows away from the edge it was pinned to, so a panel that outgrows
its asked-for size opens inwards rather than off the screen.

**The other half of the complaint was a list, not a panel.** A scroll cut to an
arbitrary height slices its last row in half, and a half-row against a frame edge
reads as a panel that does not fit its contents. The lease board's list is now
five rows and four gaps exactly, so what is visible is whole.


## Borders, measured rather than guessed

The nine-patch margins were picked by eye on the first pass and were wrong in
both directions.

**Content was not inset by the border.** `Patch` set the texture margins — where
the piece is sliced — and then content margins far smaller than the rim the piece
draws. Text was printed on top of the bevel and over the corner bolts. Content
padding is now larger than the rim on every plate, and `Frame`'s own margins are
26 rather than 14 for the same reason.

**The slices were wider than the art.** The field plate is ninety pixels tall and
was being sliced thirty from the top and thirty from the bottom, leaving a
thirty-pixel middle band to stretch — so a 38-pixel field had 60 pixels of fixed
edge crammed into it and the whole plate squashed. Measured off the pieces: the
bolted panel carries a rim about twenty-four deep, the field plate eighteen
across and twelve down.

Three named plates now carry those numbers — `PanelPlate`, `FieldPlate`,
`RolePlate` — so no call site restates a margin and there is one place to correct
if the art changes.


## Sizing and alignment, measured

Three rounds of fixing layout by looking at screenshots found three faults and
missed several, so the fourth round measures instead. `DevLayoutAudit` (`--audit`)
walks every visible control on the settled frame the screenshot is taken from and
asks four questions:

- **SQUEEZED** — combined minimum larger than the rect, so something inside is
  being crushed.
- **OUTSIDE** — the rect escapes its parent's.
- **OFFSCREEN** — the rect leaves the viewport.
- **TRIMMED** — a label whose text is wider than its rect, so it is showing an
  ellipsis where a word should be.

The fourth is the one that matters most and the one no other check can make.
**A trimmed label reports a small minimum size — trimming is how it fits** — so
it passes every "does it fit" test while visibly losing its text. Measuring the
string against the rect is the only way to see it.

Two exclusions, both real rather than convenient: anything under a
`ScrollContainer` or a clipping control cannot escape or leave the screen, and a
`Control` under a `Node2D` is a sign in the world at a world coordinate, so
measuring it against the viewport reports the entire yard as off-screen whenever
the camera looks elsewhere.

What it found once it was honest: **six trimmed labels on the dispatch board.**
Each row was carrying the equipment and the reward on one line beside two stamps,
in 272 pixels of a line that wanted 355. The reward is already the row a player
reads in ORDER DETAILS, so the second copy bought an ellipsis and nothing else.

All eleven screens now audit clean.


## What the audit found once it could see

`--audit` grew from four checks to seven, and each new one was written because
something had already shipped past the eye.

| Check | What it catches |
|---|---|
| SQUEEZED | a combined minimum larger than the rect |
| TRIMMED | text wider than its rect — an ellipsis where a word should be |
| BORDER | content margins shallower than the rim the plate draws |
| ONRIM | a child anchored across a plated parent, printed over the frame |
| UNCENTRED | equal content margins on a plate whose face is not centred |
| OUTSIDE / OFFSCREEN | a rect escaping its parent or the viewport |
| OFFGRID / UNEVEN | a rect on a half pixel; a row of mismatched heights |

Three of them found real faults the moment they existed:

- **TRIMMED** — six dispatch rows carrying equipment and reward on one line
  beside two stamps, 355px of text in 272px.
- **ONRIM** — every list row in the build. A row anchored to its parent's full
  rect gets the *whole* rect, rim included: the stylebox's content margins are
  what a **container** honours, and an anchored child is not laid out by one. Top
  and bottom were worst, where the offsets were left at zero.
- **UNCENTRED** — every button. The plates are not vertically symmetric: the
  button pieces carry a drop shadow along the bottom, so the face is centred
  *above* the middle of its box, and text centred in the box lands below the
  face. Bottom margins now run deeper by twice the measured lift, because
  centring splits the difference.

And **OUTSIDE** caught the pause menu, whose column had run thirty pixels past
its frame since the save entry was added.

**One check was written and then deleted.** "Is this control tall enough for the
type it holds" cannot fail: a Label's minimum height *is* its line height and
every container honours it. It was verified against a deliberately squashed row,
found unable to fire, and removed — a check that cannot fail reads as coverage
and is not. Every other check here was proved by breaking something on purpose
and watching it report.

Two exclusions, both real: anything under a `ScrollContainer` cannot escape or
leave the screen, and a `Control` under a `Node2D` is a sign in the world at a
world coordinate — measuring it against the viewport reported the whole yard as
off-screen whenever the camera looked elsewhere.

All eleven screens audit clean.


## The gameplay HUD, and panels that fold

The last painted wood was the in-world HUD, and it is now on the atlas with the
rest of the shell. What is left of it is the prompt for whatever is under the
wheels, the toasts, and the ACTIONS panel.

**The hotbar went.** It listed the same numbered actions the ACTIONS panel
already lists — a second copy of one list, crowding the bottom of the screen and
disappearing behind the selection card. The same call as the status sign earlier:
two readouts of one thing is how a HUD starts disagreeing with itself.

**The side panels fold.** Each header is a handle: click it and the body hides,
the frame self-sizes down to the header alone, and everything below moves up.
RESERVES starts folded because it is the one a player consults rather than
watches. Built into `SlateChrome.Collapsible` rather than with the kit's
`KitCollapsiblePanel` — a good widget that draws its own panel and its own handle
from the kit's geometry, which would put a second, differently-shaped frame in
the middle of a screen made of atlas nine-patches.

**The status bar had quietly outgrown the screen.** Widening the content margins
to clear the plate rims widened every capsule with them, and the row ran past the
right edge taking the menu button with it. Capsules are chips rather than panels
and now carry a chip's inset.


## Stage A — the player stops being a truck

The camera was a child of the truck, and that one parenting decision was the
whole defect: the only way to look at anything was to drive to it. `CameraRig` is
its own node now — pan by keys, screen edge or middle-drag, zoom in steps,
clamped to the basin — and the truck is parked at the yard with `ControlsEnabled`
off until Stage B gives it a job.

Selection replaced proximity. `Pick` casts the click through the canvas transform
and asks the world what is within reach; the action panel and the selection card
follow the choice instead of following a bumper.

Three things worth keeping:

- **Zoom is stepped, not continuous.** The ground is tile art at a fixed pixel
  size, and a fractional zoom lands every tile on a fractional pixel — the whole
  screen resamples and shimmers as it moves. Whole ratios keep it crisp. It is
  the same fault the layout audit's OFFGRID check catches on one control,
  happening everywhere at once.
- **Pan and drag both divide by the zoom.** Without it a pan crosses five times
  less world at 0.2 than at 1.0, and a middle-drag sends the view flying. Both
  read as broken controls rather than as a broken camera.
- **`Camera2D`'s own limits only bite while it moves itself.** A position set
  directly walks straight past them, so the clamp is explicit — and it stands
  down when the basin is smaller than the window, because there is nothing to
  clamp against and fighting over it would jitter.

**Acceptance met, including the strict one:** the ten-year auto-played run is
byte-identical to the run before the stage — $134.4M, 18 holes, 7 surveys, 16
repairs, 2 units built, 6 wells, 13,067 m3 in the last month. Stage A moved no
numbers, which is what it promised.

Still owed in Stage A: clicking a yard building should open the board it stands
for, and the minimap now marks where the view is rather than where the truck is.


## Structures have a footprint, a clearance and a plot

The plant was laid out on one fixed spacing whatever was being placed, so a
storage tank and a metering station were dealt the same slot and everything ended
up touching. A refinery is mostly the gaps — access for a crane, room to drop a
vessel, a fire break — and structures sharing a wall read as a shelf of icons
rather than as a site.

**Footprint and clearance are now data.** `StructureKind` is a `[GlobalClass]
Resource` with art, draw height, footprint in tiles and clearance in tiles, one
`.tres` per kind under `data/structures/`. It replaced **two `switch` statements
on a display id** — one choosing art, one choosing a height — which is exactly
the shape plans 21 §P2 exists to prevent. Adding the next structure is a `.tres`
and a sprite.

`PlantYard` is a shelf packer: fill a row left to right until the next plot will
not fit, drop by the tallest plot in that row, start again. **Chain order is
kept**, because order is the one geometric fact the engine does publish — a
player reading the yard left to right is reading the actual chain, and packing
tightest-first would waste less ground and destroy that.

The gravel follows the plots rather than being one slab under the lot, which is
what makes the gaps read as access rather than as a mistake.

**Two unit traps, both worth writing down:**

- `MakeProp` measures in **tiles** and the resource is authored in **pixels**,
  because a designer sizing a sprite thinks in the sprite's own units. The first
  run drew every structure 112 *tiles* tall — a grey smear across the top of the
  screen. The conversion belongs at the call site, not in the `.tres`.
- An animation strip must be scaled off its **frame**, not its sheet, or an
  eight-frame strip draws one eighth the size of the still beside it.

## The other half of the supplied art

`assets/topdown` ships a still for every structure **and twenty animation
strips**, and the build had copied only the stills. A flare that never lights and
a pump that never turns are the same picture whether the field is at plateau or
shut in, which throws away the one thing a top-down plant is good at showing.

Five kinds now carry a strip — flare, gas plant, water intake, water disposal and
the treater — and `WorkingProp` advances it. **Running is the engine's word:** the
animation is switched on by `Throughput > 0 && !Failed`, both read straight off
the chain view. Nothing decides that a thing is working; it renders that it is.

The treater's still changed with it, from a desalter vessel to a wastewater
treatment tank, because that is the vessel the animation belongs to and the
desalter had no strip.

Fifteen strips are still unused and they are not waste: the drilling set —
`mud-pump`, `shale-shaker`, `power-swivel-unit` — belongs to the rig convoy in
Stage B, and the gate, lighting and wind sock belong to the yard.

**The regression held throughout:** $134.4M, 18 holes, 7 surveys, 16 repairs, 2
units built, 6 wells, 13,067 m3. Layout audit clean.


## Stage A finished, Stage B standing in the yard

**Stage A is complete.** The yard's buildings open what they stand for — the
control room and the workshop open the dispatch board, the warehouse and crew
quarters open the fleet, the gate opens the lease. A door answers before the
plant does, on a tighter reach than a structure gets: the plant site sits in the
middle of the yard, so without that the office and the workshop would both be
"the plant" and clicking a base building would open nothing.

**Stage B has its foundation.** Six unit kinds stand in the yard — rig convoy,
survey crew, wireline truck, coring unit, well services, maintenance crew — and
the shape is the one plans 21 asked for:

- **`UnitKind` is a `Resource`**, one `.tres` each, carrying art, strip, draw
  height, road speed, the job it carries and where it parks. There is no
  `WirelineTruck` class and there will not be one.
- **Two behaviours, not six.** `VehicleUnit` drives and mirrors to face travel;
  `CrewUnit` walks with a bob. Which one a kind gets is how it moves, not what it
  is called.
- **One state machine, one submit.** `Unit` is Idle → Travelling → Working →
  Returning, and there is exactly one transition that raises `Arrived`. The
  `Dispatcher` listens and submits there. A second path to submitting is the
  defect this shape exists to make impossible, and a lifecycle spread across
  booleans would have no single place to hold the rule.
- **Units know nothing about commands.** They raise a signal; the dispatcher
  builds the command. Plans 21 §P5 and §P8, and the reason the engine boundary
  stays one class deep even though six new node types now exist.

**Pacing is derived, not declared.** `SimulationController.Multiplier` scales the
yard off the month length, so a crew crosses the lease in the same number of
MONTHS at 1x and at 4x. Without it, fast-forwarding would silently cost more
months of travel than playing slowly — the client would be changing the run by
how fast the player watched it.

**A refusal on arrival is normal, not exceptional.** The engine can refuse for
reasons that were not true when the job was commissioned — the rig became busy,
the cash ran out. The unit turns round and every reason is reported, which is
§9.1 applied to a crew standing on a pad.

**The regression held again:** $134.4M, 18 holes, 7 surveys, 16 repairs, 2 units
built, 6 wells, 13,067 m3. The auto-player submits straight to the engine and the
yard is not in its path — which is exactly Stage B's acceptance test in reverse:
a run played through units must match a run played from the console, and the
console run has not moved.

Still owed in Stage B: the boards' remaining actions do not route through the
yard yet, recall has no button, and units are not saved with the game.


## Stage B complete

Every job the world offers now goes out with a unit, and the command is submitted
on arrival.

**Chain elements are selectable.** Clicking a separator selects the separator —
reach is half a plot, because the plots are packed with clearance and a generous
radius would hand every click to the largest neighbour. Its card shows throughput
and condition, and its actions are the engine's own rules rather than a menu:
**repair only while failed, overhaul only while it still runs, monitoring only
while there is nothing to read.** Offering the other one would be offering a
refusal.

**Condition is shown only where the engine publishes it.** A null condition is
UNMEASURED — the company has not fitted a kit — and printing "as new" would
report truth nobody bought.

**Units are selectable, and recall is offered only while travelling**, because
nothing has been submitted yet. After arrival the activity is the engine's and
the host cannot take it back; a recall there would be offering to undo something
the client does not own.

**A working unit shows what and since when, never how far.** `ActivitiesRunning`
is a count (gap G-15), so a progress bar would be a guess dressed as a
measurement. The card says so out loud rather than leaving a blank.

**The yard is saved beside the engine's payload.** The engine saves the ACTIVITY,
which is its state; the host saves which unit was carrying it and where the unit
had got to. Neither is a copy of the other — an activity has no vehicle and a
vehicle has no duration.

**A unit that was travelling when the game was saved is stood down, not
resumed.** Resuming would submit its command on arrival — work the player last
saw as "on its way", in a session that has since been reloaded — and a save that
quietly commits work after loading is the worst kind of surprise. What was
already the engine's survives, because the engine saved it.

Verified end to end: saved at month 25 with the roster packed into the sidecar,
reopened at 26 with the yard restored, played on. **The regression held for the
fourth time** — $134.4M, 18 holes, 7 surveys, 16 repairs, 2 units built, 6 wells,
13,067 m3 — which is Stage B's own acceptance test: a run through units must
match a run from the console, and the console run has not moved once.

**One escaping trap, three times.** A C# string containing `
` written through a
shell heredoc loses a level of escaping and becomes a real newline, which is an
unterminated literal. Raw strings in the editing script, every time.


## Stage C — construction

Adding to the plant is work a crew does. The plant's action panel lists the
catalogue, a construction crew drives out, and the install command is submitted
on arrival like every other job.

**The catalogue is data.** One `BuildKind` `.tres` per addition — separator,
manifold, gas plant, treater, tank, export capacity — carrying its name, what it
unblocks, its icon, and the display-id fragment the finished element will wear.
It deliberately carries **no price, duration or capacity**: all three are the
engine's, and a designer able to edit them here would be editing the simulation
from the client.

**The host does not decide when a build has finished.** It counts the chain
elements answering to the kind's fragment when the work starts, and the scaffold
comes down when that count goes up. A build that completed on a host timer would
drift from the engine the first time a fault abandoned a tick — so the completion
signal is the engine's own chain, read.

**No placement is offered, and that is the honest version of the build mockup.**
OGSim has no coordinate for a facility (gap G-02/G-14), so every separator is the
same separator wherever it is drawn. The host picks the next bay and shows it.
The tile grid and placement ghost in the supplied mockup are a real screen for a
mechanic that does not exist, and drawing the ghost anyway would be the one thing
plans 15 §2d forbids.

**The regression held for the fifth time:** $134.4M, 18 holes, 7 surveys, 16
repairs, 2 units built, 6 wells, 13,067 m3.


## Stage D — operations

The last stage, and the one aimed at the part of a run that fills most of it.

**An alert is the front of a path.** Every failed element is a row in ALERTS that
can be clicked: the view goes to it, it is selected, and its repair is the offered
action. Two clicks from "the separator has failed" to "a crew is on its way". The
panel already knew what was wrong; what it lacked was anywhere to go.

**Standing orders hold a policy the player would otherwise re-issue thirty times
a decade** — keep the plant running, answer bottlenecks, keep the rig busy. They
are the same three the measurement harness encodes, in the same order, and the
order matters for the same reason: a failure shuts in everything behind it, so a
month spent on it is never wasted.

**They are not the client playing the game.** A standing order chooses WHEN to
send a unit on a job the player could have sent it on manually. It computes no
outcome, every decision it makes is one the read model already showed, and every
command it causes goes out with a crew and takes the same refusal. Each is off by
default and each is visible while it is on — an automation a player cannot see is
one they will blame the engine for.

**Bottleneck patience is three months.** A chain reports a jam the month a well
comes on and again while a crew is walking to it; building for every one of those
would spend the company on plant it did not need.

**A well that made nothing this month is drawn cold.** It renders a published
number — `ProducedThisTick`, or the lack of it — and it is the one thing about a
producing field that should be readable without opening a panel.

**The month has a shape now.** An end-of-month line reports what was produced and
what it fetched, or names how many elements are out of service. A month that
passes silently was the old experience and the reason the mid-game read as a wait.

Two things this stage broke and fixed:

- **A toast column with no cap covers the game.** Months can arrive faster than a
  toast fades — a thirty-month fast-forward runs before a single fade completes —
  so thirty summaries stacked over the whole screen. The oldest go immediately
  rather than waiting their turn.
- **A fresh `StyleBoxEmpty` per control leaks.** Every focus ring and flat button
  wanted the same nothing, and hundreds of them were still referenced by theme
  overrides at shutdown, which Godot reports as leaked unsafe references on the
  way out. One shared instance, and the exit is clean.

**The regression held for the sixth and last time:** $134.4M, 18 holes, 7 surveys,
16 repairs, 2 units built, 6 wells, 13,067 m3. Six stages of gameplay rebuild and
the simulation has not moved once — which was the whole point of measuring it.


## Measuring the scenario, and two bugs found by doing it

One seed is an anecdote. Eight is evidence, and running eight found two defects
before it found any balance.

**The harness measured its own deadlock.** The policy tested *"is something
broken"* and stopped there, so a repair the engine refused every month wedged the
whole thing: it explored nothing, repaired nothing, and reported a tidy ten-year
`Expired`. A policy that cannot fall through when its preferred action is refused
is a policy that measures its own silence. The branch is now taken on a command
being **accepted**, not on a condition being true.

**And it was swallowing the refusals that would have said so.** `Accepted`
returned a bool and dropped the reasons. It now prints the first refusal of each
command kind — one complaint per kind, so the log stays evidence rather than
noise — and that single change turned a mystery into a one-line answer.

**Then the real finding.** With refusals visible, seeds 5 and 6 said
*"there is nothing here to work on"* to every seismic and every drill, for ten
years, while the read model published eleven and twelve structures with
probabilities attached. The condition is `field.CompartmentCount == 0` in
`Activities.Refusals` — a FIELD check — so the read model and the command
validator disagree about what exists. It is registered as **GC-4** and it is the
engine's call.

**What eight seeds actually say about the scenario:** 1 Met at $594.0M, 1 Expired
at $134.4M, 4 insolvent before month 55, 2 unplayable. It is winnable. Every
failure has the same shape — three dry holes and broke inside five years — which
is not a claim that $600M is too high but that **$50M buys about three holes and
a dry one returns nothing**, so an unlucky opening has no recovery.
