# Top-down asset plan

Turning the four reference sheets in `referenceart/TopDown-StartDewValley/` into a
sliced, transparent, animated sprite set for Godot.

The art already exists and is the user's own. Nothing here regenerates it. The
work is **extract, clean, animate** — plus a short list of genuinely missing
subjects.

## What the reference art contains

| Sheet | Contents |
|---|---|
| `trucks.png` | 13 vehicles x **8 directions** — a full rotation set |
| `topdown1.png` | "Additional oil & gas objects", 43 labelled tiles |
| `topdown2.png` | "Drilling and well services", 28 labelled tiles |
| `topdown3.png` | "More isolated objects", 25 labelled tiles |

All four are 1122x1402, 24-bit RGB with a green chroma key and no alpha.

Roughly 96 object tiles across the three object sheets, but several appear on
more than one sheet — flare stack, pipeline manifold, gas compressor, wellhead
tree, separator vessel, heat exchanger, control room cabin, LNG sphere, pipe
rack, generator, water injection pump, helipad, worker safety cabin, electrical
substation and fire water tank all appear twice. Expect **roughly 60 unique
objects** after de-duplication, confirmed once slicing is done. Where a subject
appears twice, the `topdown3` rendering is generally the larger and cleaner one.

## Step 0 — chroma normalisation (done)

The green was **not** consistent, which would have made keying unreliable:

| Sheet | Background | H / S / V |
|---|---|---|
| `trucks` | `#01B702` | 133 / 0.99 / 0.72 |
| `topdown1` | `#16BF32` | 133 / 0.88 / 0.75 |
| `topdown2` | `#55BE47` | 113 / 0.63 / 0.75 |
| `topdown3` | `#17C93F` | 133 / 0.89 / 0.79 |

All four are now recoloured to exactly `#00FF00` in
`referenceart/TopDown-StartDewValley/normalized/`. Originals are untouched.

**The trap this had to avoid:** some sprites are legitimately green. The helipad
deck measures H=126 S=0.65 V=0.51 — nearly identical to the `topdown2` background
in hue and saturation. A naive hue key would have erased it. **Value separates
them**: every background sits at V>=0.72, the helipad at V=0.51. The key is
therefore hue 95–155, saturation >=0.55 and **value >=0.66**. Verified: the
helipad deck and the safety cabin's green cross both survive.

## Step 1 — slice

Grid analysis of `trucks.png` against the exact key:

- **8 clean column bands** — confirms 8 directions, and the column x-ranges are
  exact: 33–114, 149–214, 243–403, 419–567, 586–688, 705–820, 834–945, 965–1083.
- **Rows do not separate by projection.** Tall sprites (the workover rig derrick,
  the crane boom) overlap into neighbouring row bands, and JPEG-style artefacts
  leave 1-pixel false bands. Per-column scans returned 11–16 bands where 13 was
  expected.

So slicing is **connected-component labelling on the alpha mask**, not row/column
projection, with a minimum-area filter to drop artefact specks. Slice within each
known column strip, which constrains the problem and makes the 13-per-column
result checkable.

The object sheets additionally carry a **text label under every tile**. Labels
must be excluded — they are dark text on green, so they survive the key and would
otherwise be sliced as sprites. Two defences: drop components whose bounding box
is wide-and-short and sits directly below a larger component, and eyeball the
contact sheet before anything is uploaded.

SpriteCook's `auto_slice_asset` does connected-alpha slicing too and returns a
`manual_slice_url` for browser review. Worth keeping as a fallback if the local
slicer struggles, but local is free and tunable.

**Output:** `assets/topdown/sprites/<name>.png` (objects) and
`assets/topdown/sprites/<name>_<dir>.png` (vehicles), transparent, trimmed,
with a small uniform pad.

### Open question — the direction mapping

The 8 columns are confirmed, but which column is which compass direction is
**not** something to guess. `referenceart/TopDown-StartDewValley/normalized/_direction-key.png`
shows four vehicles across all 8 columns for confirmation in one glance.

Reading so far: **col 1 = S** (front, facing viewer) and **col 2 = N** (rear) are
unambiguous. Columns 3–8 are side and three-quarter views and need confirming.

One important consequence: columns 3 and 4 show **different equipment on each
flank** of the same truck — on the coiled tubing truck one side shows the exposed
reel, the other a closed white box. So the two sides are **not mirrors of each
other**, and the mirroring trick that saved credits on the isometric set **does
not apply here**. All 8 directions are genuine art and each needs its own
animation.

## Step 2 — animation inventory

Frame counts are chosen per mechanism: 8 for a simple loop, 10–12 where a
mechanical cycle has to read properly.

### Vehicles — `moving`, all 8 directions each

Wheels/tracks turn, body bobs, sprite stays centred. 8 frames.

| # | Vehicle | Also needs `working` |
|---|---|---|
| 1 | coiled-tubing-truck | yes — reel spins |
| 2 | wireline-service-truck | yes — drum spools |
| 3 | cementing-unit | yes — pump pulses |
| 4 | flatbed-pipe-truck | — |
| 5 | heavy-equipment-trailer | — |
| 6 | pipeline-construction-excavator | yes — dig cycle, 10 frames |
| 7 | vacuum-spill-response-truck | yes — pump running |
| 8 | firefighting-foam-unit | yes — monitor spraying |
| 9 | forklift | yes — forks raise |
| 10 | mobile-crane-truck | yes — hook lifts |
| 11 | workover-rig | yes — block travels, 10 frames |
| 12 | pump-manifold-trailer | yes — pump pulses |
| 13 | pipe-handling-trailer | yes — boom swings |
| 14 | **empty-flatbed-truck** | — (new, see Step 4) |

`working` is generated for **one** direction only — a machine on station does not
turn — unless a specific direction is needed in-game.

### Objects — `working` and signal states

| Object | State | Frames |
|---|---|---|
| flare-stack | `burning` | 8 |
| cooling-tower | `working` — fan + vapour | 8 |
| air-cooler-bank | `working` — four fans | 8 |
| shale-shaker | `working` — deck shakes | 8 |
| mud-pump | `working` — pistons | 8 |
| mud-mixing-hopper | `working` | 8 |
| power-swivel-unit | `working` — rotates | 8 |
| gas-compressor-unit | `working` | 8 |
| water-injection-pump | `working` | 8 |
| generator-unit | `working` — exhaust + lights | 8 |
| well-testing-skid | `working` | 8 |
| wastewater-treatment-tank | `working` — rake sweeps | 12 |
| communications-tower | `beacon` | 8 |
| gas-detector-station | `alarm` | 6 |
| emergency-shutdown-station | `alarm` | 6 |
| road-barrier-gate | `barrier` | 8 |
| security-checkpoint | `barrier` | 8 |
| site-lighting-tower / pole | `working` — lights on | 4 |

Everything else — tanks, vessels, separators, buildings, racks, ponds, cabinets,
containers, wind sock, camera pole — is **static**, and will carry an explicit
empty `states` list rather than an omitted key.

## Step 3 — pipeline

1. Normalise chroma (**done**).
2. Slice to individual transparent PNGs; build a contact sheet and check it.
3. De-duplicate across the three object sheets, preferring the cleaner rendering.
4. Upload each sprite that needs animating via `create_asset_upload` →
   PUT to **`api.spritecook.ai`** (SpriteCook returns a broken `mcp.` host) →
   `finalize_asset_upload`.
5. `animate_game_art` per state, `output_format: "spritesheet"`.
   Sources are well under 256px after slicing, so this runs in **pixel** mode.
6. Download spritesheets, verify geometry, assemble the manifest.

## Step 4 — gaps to fill

Four subjects the game needs that the reference sheets do not contain:

| Subject | Why | Approach |
|---|---|---|
| **empty-flatbed-truck** | requested — a truck with no load | `edit_asset_id` on the flatbed pipe truck, one edit per direction, removing the pipe load. Editing rather than generating keeps the chassis identical across all 8 directions. |
| **pumpjack** | core production asset, absent from all four sheets | generate in the sheet's style, 1 direction + `working` |
| **drilling-rig-derrick** | only the truck-mounted workover rig is present | generate, 1 direction + `working` |
| **crude-oil-tanker-truck** | the red tank vehicle is the firefighting foam unit; there is no crude hauler | generate 8 directions, or edit from the vacuum truck |

## Cost

Slicing, chroma work and mirroring are free. Only SpriteCook calls cost credits.
Animations are 20 each; stills 12; background removal 1; edits 12.

| Item | Count | Credits |
|---|---|---|
| Vehicle `moving`, 13 x 8 directions | 104 | 2,080 |
| Vehicle `working`, 1 direction each | 10 | 200 |
| Object states | 18 | 360 |
| Empty truck — 8 edits | 8 | 96 |
| Empty truck `moving`, 8 directions | 8 | 160 |
| Missing subjects — stills + animations | ~6 | ~150 |
| **Total** | | **~3,050** |

Balance is **4,854**, so the full scope fits with room to spare.

**A cheaper first cut** — animate only the four cardinal directions per vehicle
and add the diagonals later:

| Item | Credits |
|---|---|
| Vehicle `moving`, 13 x 4 directions | 1,040 |
| Vehicle `working` + object states | 560 |
| Empty truck (edits + 4 directions) | 176 |
| Missing subjects | ~150 |
| **Total** | **~1,930** |

## Risks

- **Slicing is the main risk**, not animation. Merged rows, label text and
  artefact specks all have to be handled, and the fix is a contact-sheet review
  before any credits are spent.
- **Animation drift.** Every animation run so far shows mild frame-to-frame
  wobble in silhouette detail. Acceptable at speed, but inherent to the tool.
- **8-direction consistency.** Each direction is animated independently, so a
  vehicle may drift slightly differently in each. Worth reviewing one vehicle
  across all 8 before committing to the remaining twelve.
- **Wheel rotation reads best from the side.** On the isometric set, rotation was
  clear side-on and invisible head-on. The same is likely here for columns 1
  and 2, so those two may be better left static or given a bob-only loop.

## Recommended order

1. Confirm the direction mapping from `_direction-key.png`.
2. Slice everything, review the contact sheet — **zero credits so far**.
3. Animate **one** vehicle across all 8 directions (160 credits) and review.
4. If it holds up, run the rest of the fleet, then the objects, then the gaps.
