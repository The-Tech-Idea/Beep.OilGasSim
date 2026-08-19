# HD top-down sample

Sprites in the **reference-art rendering style** (not pixel art) with a
**Stardew-style flat elevation camera**, plus animations and a full direction set
for one vehicle.

Files are 1024x1024 masters with real alpha — four times the resolution pixel
mode produced. Nothing is pre-scaled; Godot handles display scaling.

## Contents

| File | What it is |
|---|---|
| `pumpjack.png`, `crude-oil-storage-tank.png`, `control-room-cabin.png` | Style samples |
| `tanker-truck_E.png` / `_N.png` / `_S.png` | The same truck in three generated directions |
| `animations/pumpjack_working.png` | 12 frames, 640 px, full pumping stroke |
| `animations/tanker-truck_moving_E.png` | 12 frames, 640 px, wheels rotating |
| `sample.png` | The four style samples on a checkerboard |
| `directions.png` | All four truck facings, including the free mirror |
| `pumpjack-frames.png` | The pumping cycle frame by frame |
| `truck-wheels-frames.png` | Wheels zoomed 2x per frame — proof the tyres turn |
| `_wheelcheck.png` | Static tyre detail at native resolution |

## Direction rule

A `moving` animation is needed for every direction on one side, **including up and
down**. Only three are generated; the fourth is free.

| Direction | Source |
|---|---|
| East — facing right | generated |
| North — driving away, up the screen | generated |
| South — driving toward, down the screen | generated |
| West — facing left | **free**, Godot `flip_h` of East |

`directions.png` shows all four. The mirrored West is indistinguishable from a
generated facing, which is why sprites must carry **no lettering** — text reverses
under a flip and gives the trick away.

## Animation rules

**Tyre rotation is default and mandatory — but it only reads from the side.**
The East `moving` animation shows the wheels turning clearly:
`truck-wheels-frames.png` is the evidence — follow the hub bolt pattern and the
tread blocks across frames 1 to 12. This works because the *still* was prompted
with chunky tread blocks and a contrasting silver hub; the animation cannot add
detail that is not in the source, which is exactly why the earlier 64 px pixel
attempt failed.

**North and South were tested and the rotation does not read** — see
`truck-N-frames.png` and `truck-S-frames.png`. From behind or head-on you barely
see the tyre face, so 12 frames buy little more than a slight suspension bob and
some frame-to-frame drift in the cab and tank shape. Two sensible responses:

- **Skip the animation for North and South** and use the static sprite. The
  vehicle is translating across the map anyway, and that movement is what sells
  the motion. This saves roughly 320 credits across the fleet.
- Or keep a short bob-only loop if the stillness looks wrong in context.

The two sheets are kept in this folder so the call can be made by looking rather
than by argument.

**A vehicle with a function gets a second animation set.** `moving` and `working`
are additive, not alternatives. A wireline truck needs its drum spooling as
`wireline-service-truck_working` *on top of* its three `moving` directions. Same
for cementing, coiled tubing, crane, excavator and forklift.

**Every animated sprite names its states**, and the manifest, the spritesheet
files and `assets/README.md` must agree.

## The two things that had to be fixed to get here

### 1. "Top-down three-quarter at 60 degrees" produces isometric

Asking for a *top-down three-quarter view looking down at about 60 degrees* —
which sounds like Stardew — reliably produced isometric: diamond ground plates,
corner-on cubes. It did so **with and without** the reference sheets, so it was
the wording, not the style guidance.

Stardew's camera is far lower. You mostly see the **front face** with a sliver of
roof. The language that works:

> Draw it as a flat FRONT ELEVATION with only a very slight downward tilt,
> exactly like a building in a 2D side-scrolling game. The camera is almost level
> with the building and only a little above it. The front wall faces the viewer
> completely flat and square-on, its edges parallel to the edges of the frame.
> Only a thin shallow sliver of the flat roof shows above the front wall. BOTH
> SIDE WALLS ARE COMPLETELY HIDDEN. No corner of the building points toward the
> viewer. Nothing recedes away on a diagonal. There is NO diamond-shaped ground
> plate and NO isometric cube.

Swap "FRONT ELEVATION" for "SIDE ELEVATION facing RIGHT" on machines and vehicles,
or "REAR ELEVATION" for the North facing. The negative clauses do real work —
dropping them brings the isometric straight back.

### 2. Transparent backgrounds do not work in non-pixel mode

`bg_mode: "transparent"` is silently ignored when `pixel: false` — every first
attempt came back with a baked-in gradient. In pixel mode SpriteCook
post-processes the cut-out; in detailed mode it does not.

Two steps instead:

1. Generate with `bg_mode: "white"` — a flat background cuts far more cleanly
   than a gradient.
2. Call `remove_background(asset_id=...)`. **1 credit**, about two seconds,
   returns a new asset with real alpha.

## Other settings that matter

```
model              gemini-3.1-flash-image
pixel              false
resolution         1K          -> 1024x1024 output
bg_mode            white       -> then remove_background()
smart_crop_mode    power_of_2
style_asset_ids    the three referenceart sheets
```

- **Keep the reference sheets attached.** Unlike the pixel-art attempt, where they
  fought the projection and had to be dropped, here they deliver the look and no
  longer break the camera once the elevation wording is right.
- **Use `reference_asset_id` for extra directions.** The North and South truck
  views were generated with the East truck as the reference, which is what keeps
  it recognisably the same vehicle.
- Animation runs in *detailed* mode at this resolution, still **20 credits**, and
  returns 640 px frames.

## Cost to convert the full set

| Item | Credits |
|---|---|
| 76 stills @ 12 + 76 background removals @ 1 | 988 |
| 8 vehicles x 2 extra directions (still + removal) @ 13 | 208 |
| 8 vehicles x 1 East `moving` animation @ 20 | 160 |
| 25 non-vehicle / `working` animations @ 20 | 500 |
| **Total** | **~1,856** |

Animating North and South as well would add ~320 and, on this evidence, buy very
little. West stays free throughout — both the still and the spritesheet mirror.
