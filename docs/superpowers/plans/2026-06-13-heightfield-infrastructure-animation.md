# Heightfield Infrastructure And Animation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement editable heightfield terrain, earthworks, slope-aware transport, vertical infrastructure, downhill hazards, multi-tile facilities, moving vehicles, operational effects, and transparent rig/pumpjack assets.

**Architecture:** Upgrade maps to schema version 2 with a shared vertex heightfield and persisted earthwork deltas. Keep tile ownership and actions tile-based, derive terrain envelopes, graded transport, drainage, and rendering geometry from the heightfield, and make Phaser consume those derived systems without inventing gameplay geometry.

**Tech Stack:** TypeScript 5.8, React 19, Phaser 4, Vitest, Pillow asset tooling, Vite, ASP.NET Core API persistence.

---

## File Structure

New focused modules:

- `src/gameplay/map/heightfield.ts`: sampling, slope, volume, and mutations.
- `src/gameplay/map/terrainEnvelope.ts`: footprint planes and 3D clearance validation.
- `src/gameplay/map/earthworks.ts`: grading previews and committed mutations.
- `src/gameplay/map/flowField.ts`: downhill drainage and hazard transfer graph.
- `src/gameplay/vehicles.ts`: vehicle entities and deterministic route advancement.
- `src/components/map/layers/VehicleLayer.ts`: moving vehicle rendering.
- `src/components/map/layers/FacilityEffectsLayer.ts`: operational tweens and particles.
- `scripts/process-oil-assets.py`: transparent asset cleanup and normalization.

Existing modules retain their ownership:

- `mapTypes.ts` owns persisted map types.
- `transportLayer.ts` owns traversable infrastructure.
- `actions.ts` and `turnEngine.ts` own validation and turn resolution.
- `TerrainLayer.ts` owns fixed-isometric terrain rendering.
- `MapCreatorScreen.tsx` and reducer own map-authoring interactions.

---

### Task 1: Heightfield Schema And Geometry Core

**Files:**
- Create: `Game/client/beep-oil-gas-sim-web/src/gameplay/map/heightfield.ts`
- Create: `Game/client/beep-oil-gas-sim-web/src/gameplay/map/heightfield.test.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/map/mapTypes.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/map/mapGenerator.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/map/mapSerializer.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/map/mapSerializer.test.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/map/mapLegacyAdapter.ts`

- [ ] **Step 1: Write failing shared-vertex and sampling tests**

Add tests that construct a 2x1 field and assert both tiles share the center edge, bilinear sampling returns the expected midpoint, and slope/aspect are deterministic:

```ts
const field = createHeightfield(2, 1, [
  0, 2, 4,
  0, 2, 4,
]);
expect(getTileCornerHeights(field, 0, 0).east).toEqual(
  getTileCornerHeights(field, 1, 0).west,
);
expect(sampleHeight(field, 0.5, 0.5)).toBe(1);
expect(measureTileSlope(field, 1, 0).grade).toBeCloseTo(2);
```

- [ ] **Step 2: Run the focused tests and verify RED**

Run:

```powershell
node node_modules\vitest\vitest.mjs run src\gameplay\map\heightfield.test.ts src\gameplay\map\mapSerializer.test.ts
```

Expected: failure because schema version 2 and heightfield functions do not exist.

- [ ] **Step 3: Add schema version 2 types**

Define:

```ts
export interface StrategyHeightfield {
  width: number;
  height: number;
  baseVertexHeights: number[];
  earthworkVertexDeltas: number[];
}

export interface StrategyMapDocument {
  schemaVersion: 2;
  heightfield: StrategyHeightfield;
  // existing fields remain
}
```

Use row-major vertex index `y * (width + 1) + x`. Heights are finite simulation meters.

- [ ] **Step 4: Implement pure heightfield geometry**

Implement:

```ts
createHeightfield(width, height, baseVertexHeights)
vertexIndex(field, x, y)
getVertexHeight(field, x, y)
sampleHeight(field, tileX, tileY)
getTileHeightRange(field, x, y)
measureTileSlope(field, x, y)
applyVertexDeltas(field, changes)
```

Keep all functions immutable and validate array lengths.

- [ ] **Step 5: Generate shared vertices from existing elevation fields**

In `mapGenerator.ts`, average neighboring tile elevation samples into vertex heights, then derive each legacy tile's `elevation` from its four-corner average for compatibility.

- [ ] **Step 6: Implement schema migration**

`parseStrategyMapDocument` must accept version 1 and migrate it to version 2 by averaging adjacent tile elevations into vertices. Serialization always emits version 2.

- [ ] **Step 7: Run focused and full tests**

Run:

```powershell
node node_modules\vitest\vitest.mjs run src\gameplay\map\heightfield.test.ts src\gameplay\map\mapSerializer.test.ts src\gameplay\map\mapGenerator.test.ts
node scripts\run-tsc.cjs -b
```

Expected: all pass.

- [ ] **Step 8: Commit**

```powershell
git add Game/client/beep-oil-gas-sim-web/src/gameplay/map
git commit -m "feat: add shared map heightfield"
```

---

### Task 2: Heightfield Rendering, Picking, And Map Lab Inspection

**Files:**
- Create: `Game/client/beep-oil-gas-sim-web/src/components/map/terrainChunkGeometry.ts`
- Create: `Game/client/beep-oil-gas-sim-web/src/components/map/terrainChunkGeometry.test.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/components/map/layers/TerrainLayer.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/components/map/isometricProjection.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/components/map/isometricProjection.test.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/components/map/interaction/MapInteractionController.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/components/map/camera/MapCameraController.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/components/map-creator/MapDiagnosticsPanel.tsx`
- Modify: `Game/client/beep-oil-gas-sim-web/src/components/map/strategySceneModel.ts`

- [ ] **Step 1: Write failing terrain-face tests**

Assert a 1x1 sloped field emits two top triangles with shared projected vertices and the lower visible sides only:

```ts
const geometry = createTerrainChunkGeometry(map, { x: 0, y: 0, width: 1, height: 1 });
expect(geometry.topFaces).toHaveLength(2);
expect(geometry.vertices.filter(uniqueVertex).length).toBe(4);
expect(geometry.sideFaces.every((face) => face.dropMeters > 0)).toBe(true);
```

- [ ] **Step 2: Verify RED**

Run the new geometry and projection tests. Expected: missing chunk geometry.

- [ ] **Step 3: Extend projection to arbitrary terrain points**

Add:

```ts
worldToIso(tileX: number, tileY: number, elevationMeters: number, mapHeight: number)
isoToTerrain(worldX: number, worldY: number, map: StrategyMapView)
```

`isoToTerrain` tests candidate cells and interpolated top triangles, selecting the closest projected face.

- [ ] **Step 4: Build deterministic chunk geometry**

Use 8x8 logical chunks. Emit vertices and triangle faces from shared height samples, plus east/south/map-edge side faces. Keep material IDs by terrain tile.

- [ ] **Step 5: Replace median region elevation rendering**

`TerrainLayer` renders chunk top and side faces in projected painter order. Do not create one large overlapping Phaser Mesh; use bounded graphics paths or small chunk meshes because Phaser Mesh has no general depth buffer.

- [ ] **Step 6: Update picking and camera bounds**

Picking uses heightfield top faces. Camera bounds include minimum/maximum projected vertex heights and structure height allowance.

- [ ] **Step 7: Expose height diagnostics**

Scene text and Map Lab diagnostics report selected-tile average elevation, maximum grade, aspect, and cut/fill state.

- [ ] **Step 8: Verify visually and commit**

Run tests, TypeScript, build, then Strategy and Map Lab browser screenshots. Confirm no cracks, correct selection, and no console errors.

```powershell
git add Game/client/beep-oil-gas-sim-web/src/components/map Game/client/beep-oil-gas-sim-web/src/components/map-creator
git commit -m "feat: render continuous isometric heightfield"
```

---

### Task 3: Earthwork Preview, Costing, And Terrain Mutation

**Files:**
- Create: `Game/client/beep-oil-gas-sim-web/src/gameplay/map/earthworks.ts`
- Create: `Game/client/beep-oil-gas-sim-web/src/gameplay/map/earthworks.test.ts`
- Create: `Game/client/beep-oil-gas-sim-web/src/gameplay/map/terrainEnvelope.ts`
- Create: `Game/client/beep-oil-gas-sim-web/src/gameplay/map/terrainEnvelope.test.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/types.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/balancing.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/actions.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/actions.test.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/turnEngine.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/turnEngine.test.ts`

- [ ] **Step 1: Write failing cut/fill preview tests**

Test grading four vertices to a target plane:

```ts
const preview = previewGradeArea(map, ['1:1'], { mode: 'level', targetMeters: 2 });
expect(preview.cutVolumeM3).toBe(100);
expect(preview.fillVolumeM3).toBe(100);
expect(preview.resultingMaxGrade).toBe(0);
expect(commitEarthwork(map, preview).heightfield.earthworkVertexDeltas).toEqual(preview.vertexDeltas);
```

Also test rejection beneath a reserved structure envelope.

- [ ] **Step 2: Verify RED**

Expected: no earthwork actions or envelope service.

- [ ] **Step 3: Define actions and balance**

Add action types:

```ts
'grade-area' | 'cut-slope' | 'fill-area' | 'build-berm' | 'excavate-trench'
```

Add `earthworkVertexDeltas`, target elevation, preview hash, cut/fill volume, and affected tile IDs to queued actions. Balance cost separately per cut and fill cubic meter and derive turns from equipment capacity.

- [ ] **Step 4: Implement immutable previews**

Preview functions calculate affected shared vertices, enforce maximum side slope, compute cut/fill volume, return changed drainage cells, and produce a deterministic hash checked again at resolution time.

- [ ] **Step 5: Implement terrain envelopes**

Define:

```ts
interface TerrainEnvelope {
  id: string;
  tileIds: TileId[];
  minElevation: number;
  maxElevation: number;
  clearanceTop: number;
  kind: 'foundation' | 'road' | 'bridge' | 'tunnel' | 'pipeline';
}
```

Reject earthworks that intersect immutable envelopes or undermine foundation buffers.

- [ ] **Step 6: Integrate validation and resolution**

`validateAction` checks ownership, equipment, conflicts, cash, and preview hash. `turnEngine` commits exact preview deltas after duration and emits cut/fill events.

- [ ] **Step 7: Verify and commit**

Run earthwork, action, turn engine, full tests, TypeScript, and build.

```powershell
git add Game/client/beep-oil-gas-sim-web/src/gameplay
git commit -m "feat: add turn based earthworks"
```

---

### Task 4: Hybrid Foundations And Multi-Tile Structure Envelopes

**Files:**
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/map/isoVisualRegistry.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/map/isoVisualRegistry.test.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/objectRegistry.ts`
- Create: `Game/client/beep-oil-gas-sim-web/src/gameplay/map/structurePlacement.ts`
- Create: `Game/client/beep-oil-gas-sim-web/src/gameplay/map/structurePlacement.test.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/actions.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/turnEngine.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/components/map/layers/FacilityLayer.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/components/map/TileDecisionCard.tsx`

- [ ] **Step 1: Write failing footprint and foundation tests**

Test rotated 2x2 footprints, automatic minor grading, major-grade rejection, road access, and overhead clearance:

```ts
const preview = previewStructurePlacement(state, {
  kind: 'tank-farm',
  anchorTileId: '4:4',
  rotation: 1,
  size: 'large',
});
expect(preview.footprintTileIds).toEqual(['4:4', '4:5', '5:4', '5:5']);
expect(preview.autoGrade.valid).toBe(true);
expect(preview.accessConnectionTileId).toBe('3:4');
```

- [ ] **Step 2: Verify RED**

Expected: registry supports only square count metadata and single-tile object placement.

- [ ] **Step 3: Add operational structure metadata**

Each facility definition gains:

```ts
footprint: Array<{ x: number; y: number }>;
physicalHeightMeters: number;
foundationToleranceMeters: number;
maxAutoGradeDepthMeters: number;
maxAutoGradeVolumeM3: number;
sideClearanceTiles: number;
overheadClearanceMeters: number;
accessEdges: Array<'north' | 'east' | 'south' | 'west'>;
hazardOriginHeightMeters: number;
```

- [ ] **Step 4: Implement placement preview**

Rotate footprint offsets, calculate best-fit foundation plane, classify auto-grade versus required earthwork, test all envelopes, decorations, transport corridors, and access connections.

- [ ] **Step 5: Persist footprint and envelope**

`StrategyMapObject` gains anchor tile, rotation, occupied tile IDs, base elevation, physical height, and envelope ID. Existing single-tile objects migrate with defaults.

- [ ] **Step 6: Render compounds from footprints**

Use the footprint polygon for pads, selection, shadows, and road entrances. Keep sprite depth at the front-most ground-contact point, not sprite top.

- [ ] **Step 7: Verify and commit**

Run registry, placement, action, turn, scene tests, build, and browser placement scenarios.

```powershell
git add Game/client/beep-oil-gas-sim-web/src
git commit -m "feat: add graded multi tile facilities"
```

---

### Task 5: Grade-Aware Roads And Vehicle Routing

**Files:**
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/map/transportLayer.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/map/transportLayer.test.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/actions.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/turnEngine.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/components/map/layers/InfrastructureLayer.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/components/map/strategySceneModel.ts`

- [ ] **Step 1: Write failing grade classification and weighted-route tests**

Assert gentle, moderate, and steep classes; moderate speed penalties; steep blocking; and a longer gentle route winning over a short steep route.

```ts
expect(classifyRoadGrade(0.03)).toBe('gentle');
expect(classifyRoadGrade(0.09)).toBe('moderate');
expect(classifyRoadGrade(0.18)).toBe('steep');
expect(findVehicleRoute(layer, '0:0', '3:0')?.tileIds).toEqual(gentleDetour);
```

- [ ] **Step 2: Verify RED**

Expected: transport cells have constant traversal cost and no segment grade.

- [ ] **Step 3: Replace cell-only edges with transport segments**

Add segment IDs, endpoint elevations, length, grade, slope class, speed multiplier, fuel multiplier, maintenance multiplier, weight limit, and clearance profile.

- [ ] **Step 4: Implement weighted Dijkstra routing**

Route cost combines time, fuel, maintenance, hazards, and clearance. Preserve a compatibility helper returning tile IDs where existing callers need it.

- [ ] **Step 5: Enforce road construction rules**

Moderate grades increase action cost and duration. Steep segments require a grading preview or switchback route; do not silently build them.

- [ ] **Step 6: Render road surfaces on sampled elevations**

Segment endpoints sample the heightfield. Moderate segments show retaining/grade treatment. Roads no longer use a single center elevation.

- [ ] **Step 7: Verify and commit**

```powershell
git add Game/client/beep-oil-gas-sim-web/src/gameplay/map/transportLayer* Game/client/beep-oil-gas-sim-web/src/components/map/layers/InfrastructureLayer.ts
git commit -m "feat: add grade aware transport routing"
```

---

### Task 6: Bridges, Tunnels, And Buried Pipelines

**Files:**
- Create: `Game/client/beep-oil-gas-sim-web/src/gameplay/map/infrastructureEnvelope.ts`
- Create: `Game/client/beep-oil-gas-sim-web/src/gameplay/map/infrastructureEnvelope.test.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/types.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/actions.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/turnEngine.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/pipelineRouting.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/components/map/layers/InfrastructureLayer.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/components/map/MapLegend.tsx`

- [ ] **Step 1: Write failing vertical crossing tests**

Cover valid bridge-over-road, invalid low bridge, valid buried-pipeline road crossing, insufficient pipeline cover, tunnel portal slope, and overlapping buried corridors.

- [ ] **Step 2: Verify RED**

Expected: all infrastructure conflicts only by tile.

- [ ] **Step 3: Add infrastructure modes**

Add:

```ts
type InfrastructureMode = 'ground' | 'bridge' | 'tunnel' | 'surface' | 'elevated' | 'buried';
interface ClearanceEnvelope {
  floorMeters: number;
  ceilingMeters: number;
  widthTiles: number;
}
```

- [ ] **Step 4: Implement envelope intersection**

Test horizontal corridor overlap and vertical separation. Bridge supports and tunnel portals reserve ground footprints. Buried pipelines require terrain cover and separation.

- [ ] **Step 5: Add construction validation and costs**

Bridge cost uses deck length plus supports; tunnel cost uses length and geology; burial cost uses trench volume and crossing depth. Require approach grades and valid portals.

- [ ] **Step 6: Render infrastructure modes**

Draw bridge decks/supports, tunnel portals and hidden dashed tunnel previews, surface/elevated pipes, and buried pipeline overlays only in inspection mode.

- [ ] **Step 7: Verify and commit**

```powershell
git add Game/client/beep-oil-gas-sim-web/src
git commit -m "feat: add vertical infrastructure envelopes"
```

---

### Task 7: Downhill Drainage And Spill Movement

**Files:**
- Create: `Game/client/beep-oil-gas-sim-web/src/gameplay/map/flowField.ts`
- Create: `Game/client/beep-oil-gas-sim-web/src/gameplay/map/flowField.test.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/hazards.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/hazards.test.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/engine/turnPhases.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/map/earthworks.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/components/map/layers/HazardLayer.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/components/map/MapToolbar.tsx`

- [ ] **Step 1: Write failing directional-flow tests**

Test downhill transfer, weighted splits, depression accumulation, impermeable road blocking, culvert passage, berm diversion, and trench capture.

```ts
const flow = buildFlowField(map);
expect(flow.outletsByCell.get('1:1')).toEqual([
  { tileId: '1:2', weight: 0.75 },
  { tileId: '2:1', weight: 0.25 },
]);
```

- [ ] **Step 2: Verify RED**

Expected: hazards remain fixed to one tile.

- [ ] **Step 3: Implement derived flow graph**

Sample edge midpoint heights, route only to lower neighbors, normalize descent weights, identify sinks, and apply permeability and infrastructure modifiers.

- [ ] **Step 4: Add turn-based volumes**

Hazards gain volume and concentration. Each environmental phase transfers a bounded fraction downstream, merges compatible hazards, and applies contamination to reached cells.

- [ ] **Step 5: Recompute locally after earthworks**

Earthwork previews list invalidated flow cells. Committed work rebuilds those cells plus neighbors, not the entire map.

- [ ] **Step 6: Add flow visualization**

Inspection overlay shows arrows, sinks, downstream exposure, barriers, culverts, berms, and trenches.

- [ ] **Step 7: Verify and commit**

```powershell
git add Game/client/beep-oil-gas-sim-web/src/gameplay Game/client/beep-oil-gas-sim-web/src/components/map
git commit -m "feat: simulate downhill spills and drainage"
```

---

### Task 8: Operational Geology And Elevation Effects

**Files:**
- Create: `Game/client/beep-oil-gas-sim-web/src/gameplay/operationalTerrain.ts`
- Create: `Game/client/beep-oil-gas-sim-web/src/gameplay/operationalTerrain.test.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/discovery.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/actions.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/ai.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/balancing.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/components/map/TileDecisionCard.tsx`

- [ ] **Step 1: Write failing operational modifier tests**

Assert rough slopes reduce seismic confidence, rig access increases cost/duration, erosion risk rises on steep exposed slopes, and emergency response uses weighted travel time.

- [ ] **Step 2: Verify RED**

Expected: current actions depend mainly on terrain kind and abstract height.

- [ ] **Step 3: Implement one modifier selector**

Return:

```ts
{
  seismicConfidenceMultiplier,
  rigAccessCostMultiplier,
  drillingDurationMultiplier,
  erosionRisk,
  landslideRisk,
  emergencyResponseTurns,
}
```

from heightfield slope/aspect, terrain, geology secrets, weather, and transport access.

- [ ] **Step 4: Integrate actions, discovery, AI, and UI**

Use the same selector everywhere to avoid divergent formulas. Show the modifiers and reasons before queueing an action.

- [ ] **Step 5: Verify simulations and commit**

Run operational tests, AI tests, strategy simulation harness, full tests, and build.

```powershell
git add Game/client/beep-oil-gas-sim-web/src/gameplay Game/client/beep-oil-gas-sim-web/src/components/map/TileDecisionCard.tsx
git commit -m "feat: apply terrain to field operations"
```

---

### Task 9: Moving Vehicles And Facility Activity

**Files:**
- Create: `Game/client/beep-oil-gas-sim-web/src/gameplay/vehicles.ts`
- Create: `Game/client/beep-oil-gas-sim-web/src/gameplay/vehicles.test.ts`
- Create: `Game/client/beep-oil-gas-sim-web/src/components/map/layers/VehicleLayer.ts`
- Create: `Game/client/beep-oil-gas-sim-web/src/components/map/layers/FacilityEffectsLayer.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/types.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/engine/turnPhases.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/components/map/StrategyMapScene.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/components/map/strategySceneModel.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/components/map/layers/FacilityLayer.ts`

- [ ] **Step 1: Write failing vehicle advancement tests**

Test deterministic progress, grade-adjusted speed, route completion, blocked clearance, and state serialization:

```ts
const next = advanceVehicle(vehicle, route, 5);
expect(next.segmentProgress).toBeCloseTo(0.4);
expect(next.speed).toBe(baseSpeed * route.segments[0].speedMultiplier);
```

- [ ] **Step 2: Verify RED**

Expected: trucks are decorative sprites only.

- [ ] **Step 3: Add simulation entities**

Add vehicle ID, owner, kind, task, route segment IDs, progress, speed, cargo, state, origin, and destination. Turn phases assign service, construction, cleanup, and hauling tasks.

- [ ] **Step 4: Render moving vehicles**

Interpolate position and elevation along transport segments. Orient to segment direction, sort from current ground contact, and stop at compound entrance anchors.

- [ ] **Step 5: Add facility effects**

Use operational state to control pumpjack rocking, rig vibration/hook overlay, dust, flare, steam, fans, warning lights, and flow pulses. Pause tweens and particles outside the camera viewport.

- [ ] **Step 6: Expose deterministic browser state**

`render_game_to_text` includes visible vehicles, routes, progress, active facility effects, and coordinates.

- [ ] **Step 7: Verify and commit**

Use tests plus browser time stepping to verify movement across gentle/moderate grades, compound arrival, effect activation, and zero errors.

```powershell
git add Game/client/beep-oil-gas-sim-web/src
git commit -m "feat: animate vehicles and operating facilities"
```

---

### Task 10: Transparent Rig And Pumpjack Asset Pipeline

**Files:**
- Create: `Game/client/beep-oil-gas-sim-web/scripts/process-oil-assets.py`
- Create: `Game/client/beep-oil-gas-sim-web/scripts/process-oil-assets.test.py`
- Create: `Game/client/beep-oil-gas-sim-web/public/assets/kenney-iso/oil/rig.png`
- Create: `Game/client/beep-oil-gas-sim-web/public/assets/kenney-iso/oil/pumpjack.png`
- Create: `Game/client/beep-oil-gas-sim-web/output/oil-asset-validation/contact-sheet.png`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/map/isoVisualRegistry.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/map/isoVisualRegistry.test.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/public/assets/AI_SPRITE_PROMPTS.md`

- [ ] **Step 1: Write failing image-processing tests**

Using temporary fixtures, assert:

```py
assert output.mode == "RGBA"
assert output.getchannel("A").getextrema()[0] == 0
assert visible_bounds(output).width > 0
assert checkerboard_pixel_count(output) == 0
assert edge_halo_score(output) < 0.02
```

- [ ] **Step 2: Verify RED**

Run:

```powershell
python -m unittest scripts/process-oil-assets.test.py
```

Expected: processor is missing.

- [ ] **Step 3: Implement checkerboard removal**

Flood-fill connected background from image edges using color-distance matching against the two checker colors. Convert matched pixels to alpha zero, decontaminate partially covered edges using nearest foreground colors, and retain internal light equipment details.

- [ ] **Step 4: Extract representative frames**

Detect the 12 horizontal object groups, choose the frame with strongest silhouette and least moving-part occlusion, crop visible bounds, and normalize to:

- Rig: 512x512 canvas, 2x2 footprint, bottom-center anchor.
- Pumpjack: 384x384 canvas, 1x1 footprint, bottom-center anchor.

Do not overwrite the source sheets.

- [ ] **Step 5: Generate validation artifacts**

Create contact sheets on dark, light, grass, and desert backgrounds. Record visible bounds and anchors as JSON next to the output.

- [ ] **Step 6: Register and verify assets**

Update facility visuals and browser screenshots. Check for checkerboard remnants, white halos, cropping, and scale mismatch.

- [ ] **Step 7: Commit**

```powershell
git add Game/client/beep-oil-gas-sim-web/scripts Game/client/beep-oil-gas-sim-web/public/assets/kenney-iso/oil Game/client/beep-oil-gas-sim-web/src/gameplay/map/isoVisualRegistry* Game/client/beep-oil-gas-sim-web/public/assets/AI_SPRITE_PROMPTS.md
git commit -m "feat: add transparent oil facility assets"
```

---

### Task 11: Map Lab Earthwork Authoring And Export

**Files:**
- Modify: `Game/client/beep-oil-gas-sim-web/src/components/map-creator/mapCreatorReducer.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/components/map-creator/mapCreatorReducer.test.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/components/map-creator/MapCreatorControls.tsx`
- Modify: `Game/client/beep-oil-gas-sim-web/src/components/map-creator/MapCreatorScreen.tsx`
- Modify: `Game/client/beep-oil-gas-sim-web/src/components/map-creator/MapDiagnosticsPanel.tsx`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/map/mapTiledProjection.ts`
- Modify: `Game/client/beep-oil-gas-sim-web/src/gameplay/map/autotileEngine.ts`

- [ ] **Step 1: Write failing reducer tests**

Test raise/lower vertex, level area, smooth area, set target elevation, undo one earthwork, and preserve heightfield through regeneration locks.

- [ ] **Step 2: Verify RED**

Expected: Map Lab only edits terrain categories and obstacles.

- [ ] **Step 3: Add height brushes and previews**

Add inspect, raise, lower, level, smooth, berm, trench, bridge approach, and tunnel portal brushes. Preview cut/fill volume and invalid envelope conflicts before applying.

- [ ] **Step 4: Add overlays and diagnostics**

Expose contours, slope classes, drainage arrows, envelope clearance, foundations, bridge/tunnel profiles, and buried infrastructure.

- [ ] **Step 5: Export complete data**

JSON includes heightfield and earthworks. Tiled export includes vertex-height data and infrastructure envelopes. Tiling export records heightfield algorithm/version and derived slope class.

- [ ] **Step 6: Verify and commit**

Browser-test painting, undo, regeneration locks, import/export round trip, and diagnostics.

```powershell
git add Game/client/beep-oil-gas-sim-web/src/components/map-creator Game/client/beep-oil-gas-sim-web/src/gameplay/map
git commit -m "feat: add heightfield tools to map lab"
```

---

### Task 12: End-To-End Verification And Documentation

**Files:**
- Modify: `Game/README.md`
- Modify: `progress.md`
- Create: `docs/architecture/heightfield-and-infrastructure.md`

- [ ] **Step 1: Run all frontend verification**

```powershell
node node_modules\vitest\vitest.mjs run --testTimeout=60000
node scripts\run-tsc.cjs -b
node scripts\run-vite.cjs build
```

Expected: all pass; only documented SignalR pure-comment and chunk-size warnings may remain.

- [ ] **Step 2: Run backend verification**

```powershell
dotnet test Beep.OilGasSim.slnx
```

Expected: all API and persistence tests pass with schema-2 payloads.

- [ ] **Step 3: Run browser scenarios**

Verify:

1. Map Lab creates and exports a graded mountain road.
2. Strategy rejects a steep road, then allows it after grading.
3. A bridge crosses a ground road with valid clearance.
4. A buried pipeline crosses below a road.
5. A spill flows downhill and is diverted by a berm.
6. A multi-tile facility auto-grades a minor slope.
7. A truck slows uphill and arrives at the compound entrance.
8. Rig and pumpjack render transparently with operational effects.

Capture desktop and mobile screenshots and confirm zero console/page errors.

- [ ] **Step 4: Add architecture documentation**

Document persisted versus derived data, coordinate conventions, height units, slope thresholds, envelope rules, flow recomputation, vehicle time stepping, and asset-processing commands.

- [ ] **Step 5: Update progress and commit**

```powershell
git add Game/README.md progress.md docs/architecture/heightfield-and-infrastructure.md
git commit -m "docs: document heightfield game systems"
```

---

## Final Acceptance Gate

Do not mark the program complete until:

- Map schema migration is deterministic and round-trips.
- Terrain renders without cracks at changed vertices.
- Earthwork preview and committed results match exactly.
- Foundation and infrastructure envelopes prevent all tested overlaps.
- Weighted routes account for slope and clearance.
- Flow reacts correctly to earthworks and drainage infrastructure.
- Vehicles and effects are deterministic under `window.advanceTime(ms)`.
- Rig and pumpjack assets contain real alpha and no checkerboard pixels.
- Full frontend/backend/browser verification passes.
