# OGSim.Game plans

This folder belongs to the Godot game only. It documents the headless .NET
engine so the game team can build the host without changing engine C# code.

The engine is deliberately headless and contract-first. The Godot project is a
host: it owns rendering, input, pacing, screens, audio, localisation, save slots,
and presentation. It never edits the engine.

## Reading order

1. [00_ENGINE_WORKFLOW.md](00_ENGINE_WORKFLOW.md) — how an engine is built, how a tick runs, and how the game loop actually works.
2. [01_RESPONSIBILITY_MATRIX.md](01_RESPONSIBILITY_MATRIX.md) — every engine assembly, module, and its responsibility.
3. [02_GODOT_HOST_INTEGRATION.md](02_GODOT_HOST_INTEGRATION.md) — how to connect the Godot host to the engine.
4. [03_COMMAND_AND_READ_MODEL_QUICK_REFERENCE.md](03_COMMAND_AND_READ_MODEL_QUICK_REFERENCE.md) — the current concrete surface a Godot host can call.
5. [04_ENGINE_REFERENCE_INDEX.md](04_ENGINE_REFERENCE_INDEX.md) — generated file-by-file engine reference.
6. [engine-reference/](engine-reference/) — one markdown file per engine C# source file.
7. [07_GODOT_ADDONS_INVENTORY_AND_USAGE.md](07_GODOT_ADDONS_INVENTORY_AND_USAGE.md) — what the installed Godot addons provide and how they fit the game.
8. [08_GODOT_SCENE_PLAN.md](08_GODOT_SCENE_PLAN.md) — scene-by-scene plan using the scanned UI kit, HUD components, and scene templates.
9. [09_ENGINE_SYSTEM_SCENE_MAP.md](09_ENGINE_SYSTEM_SCENE_MAP.md) — engine-driven screens for resources, technology, operations, facilities, objectives, information, company, world, and HSE.
10. [10_MOCKUP_REVIEW.md](10_MOCKUP_REVIEW.md) — review worksheet for the five mockups in `referenceart/Mockup`.
11. [11_CASUAL_TOPDOWN_GAME_MODE_PLAN.md](11_CASUAL_TOPDOWN_GAME_MODE_PLAN.md) — tile-based Stardew-style competitive mode for normal players.
12. [12_OILFIELD_DAYS_MOCKUP_REVIEW.md](12_OILFIELD_DAYS_MOCKUP_REVIEW.md) — review worksheet for the five Oilfield Days mockups.
13. [13_OILFIELD_DAYS_BUILD_LOG.md](13_OILFIELD_DAYS_BUILD_LOG.md) — how the Godot build was made, decision by decision.
14. [14_GAME_SDD_CONFORMANCE.md](14_GAME_SDD_CONFORMANCE.md) — the build measured against GAME-SDD-001/002.

### The gameplay redesign

The client shipped a driving game where a base builder was asked for. These are
the revision, and 20 is where progress lives.

15. [15_GAMEPLAY_REDESIGN.md](15_GAMEPLAY_REDESIGN.md) — **read first.** The base, the yard, the dispatch loop, and the law the whole thing lives under.
16. [16_STAGE_A_BASE_AND_CAMERA.md](16_STAGE_A_BASE_AND_CAMERA.md) — the truck stops being the player.
17. [17_STAGE_B_UNITS_AND_DISPATCH.md](17_STAGE_B_UNITS_AND_DISPATCH.md) — crews and vehicles that travel, and arrival submits the command.
18. [18_STAGE_C_CONSTRUCTION.md](18_STAGE_C_CONSTRUCTION.md) — building the plant as work someone does.
19. [19_STAGE_D_OPERATIONS.md](19_STAGE_D_OPERATIONS.md) — running a producing field.
20. [20_GAME_TRACKER.md](20_GAME_TRACKER.md) — **what is built, next and blocked.**
21. [21_GAME_CODE_PATTERNS.md](21_GAME_CODE_PATTERNS.md) — how the client is built: resources, hierarchy, state, signals.
22. [22_SETTLERS_SHAPED_GAME.md](22_SETTLERS_SHAPED_GAME.md) — **the plan that matters.** A yard, a budget and ground nobody has looked at: the map goes dark, the plant starts empty, and the player builds it.

**Read 22 before 15.** Plans 15 rebuilt the client and did not touch what the
world hands the player at the start — which turned out to be the real problem.

`OilField_Days_Beep_Oil_and_Gas_Sim_Game_SDD_Expanded_Godot_WorldGen.html` is the
game-side SDD and is authoritative for the client, with OGSim authoritative for
both. It carries eight embedded mockups. Read 14 before changing a screen.

## Ground rules

- Never change any file under `src/OGSim.*` other than `src/OGSim.Game`.
- Treat engine state as read-only from Godot. Mutations go through `Commands.Submit`.
- Treat engine ticks as the only source of time. Godot controls when ticks happen, not what happens inside them.
- Treat the read model as a per-tick snapshot. Re-read it after `AdvanceTick`, never hold onto it across ticks.
- Engine rejections are normal game feedback, not errors. Surface `RejectionReason.LocId` and `Detail` to the player.

## Current Godot state

The game is `Oilfield Days/oilfield-days`, a Godot 4.7.1-mono project that
references `OGSim.Composition` directly and runs the real engine in process. It
has a main menu, a seeded New Game and world setup, a playable world, four boards
and a results screen. `13_OILFIELD_DAYS_BUILD_LOG.md` records how it was built and
`14_GAME_SDD_CONFORMANCE.md` records where it stands against the game SDD.
