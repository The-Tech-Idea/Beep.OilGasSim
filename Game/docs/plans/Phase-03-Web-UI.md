# Phase 3 — Basic Web Game UI

**Goal:** Solo browser match playable end-to-end.

## TODO

- [x] GameShell layout (GDD-010 wireframe)
- [x] Babylon.js basin map, 20 block meshes
- [x] Block selection → RightPanel
- [x] Action queue + Commit Turn
- [x] Turn results cards
- [x] Company dashboard, leaderboard
- [x] ApiClient + game state stores

## Verification

Player completes solo Desert Frontier match in browser.

```powershell
# Terminal 1 — API
cd Game
dotnet run --project src/Beep.OilGasSim.Api

# Terminal 2 — Client
cd Game/client/beep-oil-gas-sim-web
npm install
npm run dev
```

Open http://localhost:5173 — start a game, bid on blocks, explore, commit turns.
