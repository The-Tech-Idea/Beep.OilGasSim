# OGSim top-down asset set

75 sprites extracted from the user-authored reference sheets in
`referenceart/TopDown-StartDewValley/`. This art was **not generated** — it was
drawn by the user and is only being cleaned, cut and animated here.

```
assets/topdown/
  sprites/<name>.png          75 sprites, transparent, trimmed
  animations/<name>_<state>.png   spritesheets (in progress)
  topdown-assets.json         manifest
  _contact-sheet.png          all 75, named, on a checkerboard
```

Masters only — nothing is pre-scaled. Godot handles display scaling. Set the
texture filter to **Nearest**.

## How the sprites were produced

**1. Chroma normalisation.** The green key was inconsistent — `#01B702` on the
trucks sheet, `#55BE47` on topdown2, and drifting within each sheet. All four are
recoloured to exactly `#00FF00` in `referenceart/TopDown-StartDewValley/normalized/`.
Originals untouched.

The threshold that matters: **value >= 0.66**, alongside hue 95–155 and
saturation >= 0.55. Some sprites are legitimately green — the helipad deck
measures H=126 S=0.65 V=0.51, almost identical to the topdown2 *background* in
hue and saturation. Brightness is the only thing separating them. Loosen the
value floor and the helipad disappears.

**2. Slicing.** Connected-component labelling, not row/column projection: tall
sprites overlap neighbouring rows and compression artefacts leave one-pixel false
bands. Components shorter than 34px or smaller than 900px² are dropped, which
removes the per-tile text labels; a top-band cut removes the sheet titles.

**3. Halo removal.** The key only matched pure background, so the anti-aliased
ring where each sprite met the green survived as opaque green pixels. That ring
is **eroded inward from transparency** rather than keyed globally — again to
protect the helipad, whose green is interior and unreachable from outside.
125,380 halo pixels removed across the set.

**4. De-duplication.** Several objects appear on more than one sheet. Preference
order is topdown3 > topdown2 > topdown1, since the later sheets draw the same
object larger and cleaner. 97 tiles reduced to 75 unique sprites.

## Known artefacts

- Small pockets of **dark green remain in tight concave areas** — between the
  pipes of the pipe rack, inside the LNG sphere's leg cage, behind the solar
  array. These are shadowed background that the value threshold deliberately
  spared, and they measure V≈0.4, too close to the helipad's own green to key
  safely. Worst case is ~1,000 pixels on `pipe-rack-section`; at game size it
  reads as shading.
- `road-barrier-gate` had its "Road barrier" caption inside its bounding box —
  the caption is a separate component, but the crop rectangle overlapped it. The
  407 caption pixels were cleared manually. Worth knowing the crop-rectangle
  overlap is possible for any wide, short sprite.

## Animation states

20 of the 75 are animated. Everything else carries an explicit empty `states`
list, so "static" is stated rather than merely absent.

| State | Sprites | Frames |
|---|---|---|
| `burning` | flare-stack | 8 |
| `working` | cooling-tower, air-cooler-bank, shale-shaker, mud-pump, mud-mixing-hopper, power-swivel-unit, gas-compressor-unit, water-injection-pump, generator-unit, well-testing-skid, wind-sock | 8 |
| `working` | wastewater-treatment-tank — rake sweeps a full turn | 12 |
| `working` | site-lighting-tower, site-lighting-pole — lamps flicker on | 4 |
| `beacon` | communications-tower | 8 |
| `alarm` | gas-detector-station, emergency-shutdown-station | 6 |
| `barrier` | road-barrier-gate, security-checkpoint | 8 |

### Vehicles are deferred

Nine vehicles appear on the object sheets as a single top-down view — forklift,
heavy-equipment-trailer, pipeline-construction-excavator, firefighting-foam-unit,
spill-response-trailer, mobile-crane-truck, pump-manifold-trailer,
pipe-handling-trailer and workover-rig. They are installed as **static** sprites
here. Their 8 directions and `moving` animations live in `trucks.png` and are a
separate pass, per `assets/TOPDOWN_PLAN.md`.

## Animating: two constraints that bite

- **Aspect ratio must be between 1:2 and 2:1.** The flare stack (107x231, 2.16:1)
  and road barrier (180x68, 2.65:1) are both rejected as-is. Animation sources are
  therefore **padded to square** with transparency before upload, which also keeps
  the framing identical across frames.
- **Pixel animation needs sources at or under 256x256.** The largest sprite pads
  to 231x231, so the whole set stays in pixel mode.

Upload goes `create_asset_upload` → PUT to **`api.spritecook.ai`** → 
`finalize_asset_upload`. SpriteCook returns a broken `mcp.` host in `upload_url`
that 404s.
