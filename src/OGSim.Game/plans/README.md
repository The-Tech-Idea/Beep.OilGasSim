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

## Ground rules

- Never change any file under `src/OGSim.*` other than `src/OGSim.Game`.
- Treat engine state as read-only from Godot. Mutations go through `Commands.Submit`.
- Treat engine ticks as the only source of time. Godot controls when ticks happen, not what happens inside them.
- Treat the read model as a per-tick snapshot. Re-read it after `AdvanceTick`, never hold onto it across ticks.
- Engine rejections are normal game feedback, not errors. Surface `RejectionReason.LocId` and `Detail` to the player.

## Current Godot state

The current Godot project is a near-empty host:

- `project.godot` runs `node_2d.tscn`.
- `Control.cs` is an empty `Godot.Control`.
- `node_2d.tscn` contains a tile map and no game logic.

This plans folder is the bridge between the completed engine and the game work still to be done.
