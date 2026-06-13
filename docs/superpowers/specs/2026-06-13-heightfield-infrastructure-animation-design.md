# Heightfield, Infrastructure, And Animation Design

## Goal

Upgrade the fixed-elevation isometric strategy map into a deterministic, editable heightfield world where terrain affects construction, transport, hazards, geology, and facility operation, while retaining the fixed isometric camera, logical strategy grid, Kenney-style assets, and turn-based simulation.

## Scope

This design covers:

- Continuous editable heightfields.
- Turn-based grading, excavation, cut, fill, and foundations.
- Road grade and vehicle slope effects.
- Ground, elevated, and buried infrastructure.
- Downhill water, spill, and contamination movement.
- Multi-tile structure footprints and vertical envelopes.
- Moving service vehicles and facility activity effects.
- Cleanup of the supplied rig and pumpjack artwork into transparent runtime assets.

True free-camera 3D, continuous rigid-body physics, voxel excavation, subsurface reservoir deformation, and full fluid dynamics are excluded.

## Core Heightfield

The logical tile grid remains authoritative for ownership, actions, and selection. Terrain elevation becomes a shared vertex grid of `(width + 1) x (height + 1)` samples. Each tile references its four corner samples, allowing adjacent tiles to share edges without cracks.

Each height sample stores elevation in simulation meters. Rendering converts meters to fixed isometric vertical pixels through one scale constant. Simulation uses unquantized values; rendering may use interpolated or visually stabilized values but must not change gameplay results.

The heightfield supports:

- Bilinear height sampling at any tile-local coordinate.
- Tile slope, grade, aspect, minimum height, maximum height, and average height.
- Cut/fill volume between current and proposed surfaces.
- Immutable generated base elevation plus mutable earthwork deltas.
- Deterministic serialization and migration from schema version 1 maps.

## Terrain Rendering

The camera remains fixed orthographic isometric. Terrain is rendered as deterministic chunked faces rather than independent raised tile sprites.

Each terrain chunk produces:

- Top faces derived from shared height vertices.
- Visible side and retaining faces.
- Terrain material regions and detail overlays.
- Stable painter ordering based on projected ground position.

Phaser mesh use is limited to small terrain chunks with controlled overlap. Buildings, vehicles, vegetation, effects, labels, and infrastructure remain ordinary depth-sorted game objects. Ground-contact depth determines sprite occlusion; physical height never changes the sprite's depth key.

Map Lab exposes height contours, slope classes, vertex handles, cut/fill previews, and clearance envelopes. Normal gameplay hides editing handles and logical seams.

## Earthworks

Earthworks are explicit queued construction actions:

- Grade area to target elevation.
- Cut slope.
- Fill depression.
- Create level foundation.
- Build berm.
- Excavate trench.
- Create road ramp or switchback preparation.

Every earthwork preview reports affected vertices, cut volume, fill volume, cost, duration, required equipment, blocked infrastructure, drainage impact, and resulting slope classes.

Earthworks may not modify vertices beneath completed structures or through protected infrastructure envelopes. Existing roads and pipelines may only be modified through dedicated upgrade, relocation, bridge, or burial actions.

## Foundations

Structure placement uses a hybrid rule:

- Minor unevenness is auto-graded and included in construction cost and duration.
- Major unevenness requires a separate foundation earthwork project.
- Excessive cut/fill, unstable slope, blocked access, or envelope collision rejects placement.

Each structure definition declares:

- Logical footprint cells.
- Foundation plane tolerance.
- Maximum automatic cut/fill depth and volume.
- Physical height.
- Required side and overhead clearance.
- Road-access connection points.
- Construction equipment and crane-access requirements.
- Hazard origin height and hazard radius modifiers.

The foundation becomes a reserved terrain envelope. Nearby earthworks cannot undermine it.

## Roads And Vehicles

The transport graph remains the source of truth, extended with segment geometry and grade.

Each segment records:

- Surface class: ground, bridge, or tunnel.
- Start and end elevation.
- Grade percentage and slope class.
- Traversal distance.
- Speed multiplier.
- Fuel and maintenance multiplier.
- Weight and clearance limits.
- Drainage or culvert capability.

Slope classes:

- Gentle: normal construction and travel.
- Moderate: higher construction cost, slower vehicles, and increased operating cost.
- Steep: impassable until graded, rerouted through switchbacks, bridged, or tunneled.

Vehicle pathfinding minimizes travel time plus fuel, maintenance, hazard, and clearance penalties rather than tile count. Vehicles are simulation entities with route, segment progress, speed, cargo/task, and state. Their rendered position interpolates along the selected segment at sampled terrain or deck elevation.

## Vertical Infrastructure

All infrastructure occupies an elevation envelope:

- Ground roads and pads follow prepared surface elevation.
- Bridges occupy elevated decks and require supports and approach grades.
- Tunnels occupy buried corridors and require portals, suitable geology, and cover.
- Pipelines may be surface, elevated, or buried.
- Buried crossings require minimum cover and separation from other buried assets.

Crossings are valid only when horizontal intersection, vertical separation, support, portal, and clearance rules pass. Tile identity alone is insufficient for collision checks.

## Downhill Flow

Water, oil spills, and contamination use a deterministic directional-flow graph derived from heightfield gradients.

The flow system:

- Routes material through lower neighboring samples.
- Accumulates in local depressions.
- Splits flow according to descent weights.
- Applies permeability, terrain, and containment modifiers.
- Treats roads as barriers unless drainage or culverts exist.
- Supports berms, trenches, retention ponds, and drainage channels.
- Rebuilds only affected flow regions after earthworks.

This is a turn-based volume transfer model, not continuous fluid physics. Hazard previews show downstream exposure before construction or cleanup decisions.

## Operational Geology

Terrain affects operations without deforming the abstract subsurface reservoir model:

- Slope and roughness affect seismic confidence.
- Rig access and foundation work affect drilling cost and duration.
- Elevation, aspect, and terrain affect erosion and landslide exposure.
- Emergency response time follows the slope-aware transport graph.
- Excavation and tunnels apply geology-dependent cost and risk.

## Facilities And Effects

Facilities use static transparent sprites plus lightweight engine animation:

- Drilling rig: subtle vibration, travelling-block or hook overlay, drill activity, dust.
- Pumpjack: pivoted beam/horsehead tween with fixed base.
- Producing well: valve/pressure pulses and optional flare.
- Processing: fan rotation, steam, warning lights, and pipe-flow highlights.
- Tank farm: loading activity and service-vehicle visits.

Mechanical overlays and effects are separate objects so animation speed and visibility reflect operational state. Off-screen animations pause or reduce update frequency.

## Asset Processing

The supplied files:

- `Game/gfx/animated_rig_sheet.png`
- `Game/gfx/animated_well_suckerpump_sheet.png`

contain usable artwork but opaque baked checkerboards. The pipeline will:

1. Select the strongest representative frame from each sheet.
2. Remove checkerboard pixels with color-distance and connected-background masking.
3. Decontaminate edge colors to prevent white halos.
4. Crop to visible bounds.
5. Place the object on a normalized transparent canvas.
6. Record visible bounds, footprint, and bottom-center ground anchor.
7. Export static runtime PNGs and visual validation contact sheets.

The original source files remain unchanged.

## Data Ownership

- `StrategyMapDocument` owns generated base heights and persisted earthwork deltas.
- A heightfield service owns sampling, slope, volume, and mutation calculations.
- A terrain-envelope service owns 3D placement and clearance validation.
- The transport layer owns road/vehicle connectivity and grade-weighted routing.
- The flow layer owns derived drainage and hazard movement.
- Visual composition consumes these systems and never invents gameplay geometry.

All derived layers are reproducible from persisted map state and simulation entities.

## Delivery Milestones

### 1. Heightfield Core

Add shared vertices, sampling, slope calculations, serialization migration, Map Lab diagnostics, and deterministic tests.

### 2. Heightfield Rendering

Render chunked terrain tops/sides, update picking and camera bounds, and verify seam-free fixed-isometric output.

### 3. Earthworks And Foundations

Add previews, queued actions, cut/fill costs, mutation, foundation reservations, and hybrid auto-grading.

### 4. Grade-Aware Transport

Extend road segments and pathfinding with grade, speed, operating cost, ramps, and switchback requirements.

### 5. Vertical Infrastructure

Add bridge, tunnel, surface pipeline, buried pipeline, portals, supports, and envelope collision rules.

### 6. Downhill Flow

Add drainage derivation, spill/flood transfer, containment structures, downstream previews, and local recomputation.

### 7. Multi-Tile Structures

Add footprint rotation, foundation planes, access points, physical heights, clearance, hazard origins, and placement UI.

### 8. Vehicles, Facility Effects, And Assets

Add moving service vehicles, task routes, operational effects, transparent rig/pumpjack cleanup, and performance controls.

## Testing

Each milestone uses test-first development and includes:

- Pure unit tests for geometry and deterministic calculations.
- Serialization and migration tests.
- Placement and action-validation tests.
- Routing and flow integration tests.
- Scene-model text output for browser verification.
- Strategy and Map Lab screenshots at desktop and mobile sizes.
- Console/page-error checks.
- Full Vitest, TypeScript, production build, and relevant .NET tests.

## Acceptance Criteria

- Adjacent terrain shares vertices and renders without cracks.
- Earthwork previews exactly match committed terrain mutations and costs.
- Foundations never float, intersect roads, or overlap reserved envelopes.
- Trucks slow on moderate grades and cannot traverse steep or insufficient-clearance segments.
- Bridges, tunnels, and buried pipelines cross only with valid separation.
- Floods and spills move downhill and respond to roads, culverts, berms, and trenches.
- Multi-tile facilities align to prepared terrain and preserve correct foreground occlusion.
- Rig and pumpjack assets render with true alpha and no checkerboard or halo.
- Vehicles and effects reflect operational state and pause off-screen.
- Existing gameplay remains deterministic and all automated/browser verification passes.

## References

- Phaser Mesh guidance: https://docs.phaser.io/phaser/concepts/gameobjects/mesh
- Transport Fever 2 construction alignment concepts: https://wiki.transportfever2.com/doku.php?id=modding%3Aconstructionbasics
- EPA spill prevention and drainage context: https://www.epa.gov/oil-spills-prevention-and-preparedness-regulations/spill-prevention-control-and-countermeasure-19
