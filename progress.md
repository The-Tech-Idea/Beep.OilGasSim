Original prompt: i want oil and gas sim to have  world generator that generates either iso map  like in all iso map game and i have kenny asssets in H:\GameDev\GFX\GameAssets you can use. i want to differetn types suitable for my game like civilization. remove existing maps and asset.  existing maps and framework is not working

## 2026-06-05

- Replacing the broken React Three Fiber desert map with a 2D isometric strategy-map renderer.
- Plan: pure deterministic world generator, renderer in the existing React game shell, selected Kenney PNG assets copied from `H:\GameDev\GFX\GameAssets`, old GLB/world map files removed.
- Added `isoWorldGenerator` with deterministic terrain presets: desert frontier, coastal delta, fold belt, and offshore shelf.
- Replaced `BasinMapCanvas` with a Phaser-hosted isometric map and added a world-type switch in the map HUD.
- Copied a curated PNG set into `Game/client/beep-oil-gas-sim-web/public/assets/iso`.
- Removed the old `src/components/map/world` Three.js scene files, `public/assets/kenney` GLB copies, and unused Three/R3F dependencies.
- Added frontend tests for deterministic world generation and transient SignalR teardown errors.
- Verification completed:
  - `node node_modules\vitest\vitest.mjs run`
  - `npm test`
  - `node scripts\run-tsc.cjs -b`
  - `node scripts\run-vite.cjs build`
  - `dotnet test Beep.OilGasSim.slnx`
  - `web_game_playwright_client.js` against `http://localhost:5173`, with screenshots/state in `Game/client/beep-oil-gas-sim-web/output/web-game/iso-map`.

## TODO / Notes

- Vite still reports the pre-existing SignalR Rollup pure-comment warning and a large JS chunk warning.
- The dev API and Vite client were started for browser verification. Logs/PIDs are under `Game/.codex-run`.
- `Game/client/beep-oil-gas-sim-web/test.cmd` was added so `npm test` avoids the local Bun shim, matching the existing `dev.cmd` and `build.cmd` pattern.

## 2026-06-06

- Searched online for oil and gas game assets after the initial Kenney-based map work.
- Found a usable OpenGameArt isometric drilling/processing rig by Varkalandar / Hansjörg Malthaner.
- Added the original source sheet as `public/assets/iso/oga-drilling-rig-source.png`, cropped/resized the no-drive rig into `public/assets/iso/facility-drilling-rig.png`, and documented attribution in `public/assets/iso/ATTRIBUTION.md`.
- Wired `facility-drilling-rig.png` into the Phaser map for `ExplorationDrilling` blocks.
- Disabled Phaser audio for the map renderer because the map does not use audio and reload verification showed AudioContext teardown errors.
- Verification completed after the asset integration:
  - `npm test`
  - `node scripts\run-tsc.cjs -b`
  - `node scripts\run-vite.cjs build`
  - `web_game_playwright_client.js` against `http://localhost:5173`
  - Local Playwright console probe confirmed one canvas, `render_game_to_text`, and no console/page errors.

## 2026-06-06 Fun Mode Objective Pass

- Clarified the solo Fun Mode objective: it is a 12-turn company-value score chase against time, cash burn, drilling risk, license costs, and oil price swings.
- Added `getCompetitionSummary` with tests for solo score-chase wording and multiplayer/rival race wording.
- Surfaced the objective in the top bar, map HUD, right panel, leaderboard/score chase panel, lobby summary, and first tutorial step.
- Changed the top bar from `Rank #1` to `Solo Score` when there are no rival companies, avoiding misleading competition language.
- Added a responsive narrow-screen game layout so the fixed sidebar/right panel no longer collapse the map canvas to zero width.
- Verification completed:
  - `npm test`
  - `node scripts\run-tsc.cjs -b`
  - `node scripts\run-vite.cjs build`
  - `web_game_playwright_client.js` against `http://localhost:5173`
  - Desktop and 390px-wide Playwright screenshots/probes
  - In-app Browser smoke check with no console errors.
