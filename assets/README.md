# OGSim art assets

Isometric pixel-art sprites for the oil & gas simulation, generated with SpriteCook
and art-directed from the three sheets in `referenceart/`.

## Layout

```
assets/
  sprites/256/<name>.png        76 sprites, 256x256, transparent
  animations/<name>_<state>.png 33 spritesheets at native frame size
  spritecook-assets.json        manifest: asset ids, categories, frame data, hashes
```

**Masters only — nothing is pre-scaled.** Sprites ship at the resolution they were
rendered at and Godot handles display scaling. An offline downscale to 64 px was
tried and discarded: it threw away too much of the detail that makes a separator
distinguishable from a scrubber.

If you need these to sit on a small tile, prefer a low-resolution `SubViewport`
with the whole scene rendered small and upscaled at integer factors, or simply use
larger tiles, rather than shrinking each sprite. Set the texture filter to
**Nearest** so the pixel grid stays crisp either way.

## Naming

Sprites are kebab-case industry terms (`blowout-preventer`, `pumpjack`,
`shale-shaker`). Animations are `<sprite>_<state>`, so `forklift` has
`forklift_moving` and `forklift_working` and both animate the same base sprite.

States in use:

| State | Meaning |
|---|---|
| `moving` | vehicle in transit — wheels/tracks turn, body rocks, sprite stays centred |
| `working` | machine operating on station — the working part moves, chassis stays planted |
| `burning` | flare flame alight |
| `barrier` | boom barrier raising from closed to open |
| `beacon` | tower warning light pulsing |
| `alarm` | gas detector lamp flashing |

A sprite is either static or names every state it has; the manifest's `states`
array, the `<sprite>_<state>.png` files and this table must agree. 27 of the 76
sprites are animated, giving 33 animations. Six carry both `moving` and `working`
— coiled-tubing-truck, wireline-service-truck, cementing-unit, mobile-crane-truck,
pipeline-construction-excavator and forklift — and their static sprite doubles as
the idle frame.

## Spritesheet format

One horizontal row, no padding, frames left to right, 8 fps, loops cleanly.
Frame count is 8 except `pumpjack_working` and
`pipeline-construction-excavator_working`, which are 10 so the mechanical cycle
reads properly.

Frame size varies per animation (158–256 px) because SpriteCook crops each
animation to its own content bounds — read `frame_size` and `frames` from the
manifest rather than assuming.

Vehicles animate in place. Movement across the map is the engine's job; the
sprite only sells the motion.

## Facing and mirroring

A horizontal flip is an exact pixel operation — Godot's `flip_h` costs nothing and
loses nothing — so a sprite drawn facing one way gives you the opposite facing for
free. In this isometric projection a flip maps SE↔SW and NE↔NW; it can never
produce a different azimuth, because rotating the image would rotate the ground
plane and the vertical axis with it.

Current facings are **not consistent** across the set: of the directional sprites,
roughly 19 face SW, 13 SE, 2 NE and 1 NW, and the vehicle fleet alone splits 6 SW
/ 5 SE. Anything whose facing is merely a mirror of the one you want can be
normalised for free; only a genuinely different azimuth needs re-rendering.

**Twelve sprites must not be mirrored** without checking, because they carry
lettering, hazard hatching or chiral detail that reverses: covered-warehouse,
wireline-service-truck, emergency-shutdown-station, gas-detector-station,
security-checkpoint, electrical-substation, fire-water-tank, worker-safety-cabin,
hazardous-material-cabinet, road-barrier-gate, distillation-column and
lng-storage-sphere (plus the oil-containment-boom coil).

## Known defects

- `road-barrier-gate.png` has three stray mini-sprites baked into its transparent
  corners and needs a re-render.

## Regenerating

`spritecook-assets.json` holds the SpriteCook `asset_id` for every sprite and
animation. To re-render or re-animate one, pass its `asset_id` back to SpriteCook
rather than generating from scratch — that keeps the design identical. The three
reference sheets are uploaded as style assets and their ids are in the manifest
under `style_reference_asset_ids`; pass all three as `style_asset_ids` when adding
a new object so it matches the set.

Constraints worth knowing before adding assets:

- SpriteCook's `width`/`height` are hints. Output lands on a power-of-two crop
  driven by how much of the canvas the subject fills, so a subject drawn small
  yields a small sprite. Generating at `resolution: "2K"` and telling the prompt
  the object fills the frame is what reliably produces 256.
- Pixel animation rejects sources larger than 256x256, so a 512 render has to be
  downscaled and re-uploaded before it can be animated.
- SpriteCook returns a broken host in `upload_url` (`mcp.spritecook.ai`, which
  404s). Upload to `api.spritecook.ai` instead.

## Alternative perspective

`_pilot-topdown/` holds a four-sprite evaluation of a Stardew-style top-down view,
with its own `FINDINGS.md`. It is a scratch folder, not part of this set.
