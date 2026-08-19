# Top-down pilot — findings

Four sprites plus one animation, rendered to judge whether to convert the
isometric set to a Stardew-style top-down view. **98 credits spent.** Nothing
under `assets/sprites/` or `assets/animations/` was touched.

## Files

| File | What it shows |
|---|---|
| `256/` | The four pilot sprites, masters |
| `animations/tanker-truck_moving.png` | 8 frames, 210 px each, horizontal strip |
| `comparison.png` | Isometric vs top-down, same four subjects |
| `motion.png` | The 8 animation frames at full size |
| `motion-64.png` | The same 8 frames downscaled to 64 px — kept as evidence of how much detail that loses |
| `wheels.png` | Wheels zoomed 4x across all 8 frames |
| `mirror-test.png` | East original beside its free `flip_h` West |

Pre-scaled 64 px copies were removed: the offline downscale costs too much detail,
and Godot handles display scaling.

## What worked

**The projection.** All four are correctly axis-aligned — square footprints, no
isometric diamond. The decisive prompt choice was **not passing the existing
`style_asset_ids`**: those three reference sheets are isometric and exist to
enforce that projection, so feeding them in would have fought the change. The
palette was described in the prompt text instead and held up fine.

**Mirroring.** `flip_h` produces a clean, correct West facing from the East
render — see `mirror-test.png`. The pumpjack in particular is flawless. This
confirms the two-facings-for-the-price-of-one economy the plan depends on.

## What needs a prompt change in the full set

**1. Buildings come out corner-on unless you forbid it.** The first
control-room-cabin showed two walls, reading half-isometric. It only came out
square-on after the prompt explicitly said *"like a dollhouse facade, ONLY the
front wall is visible, NO side walls, NO corner, not turned or angled"*. Every
building prompt needs that language.

**2. Painted text reverses when mirrored.** The tanker truck picked up "CRUDE
OIL" lettering on the tank, and it reads backwards in the mirrored West version
— clearly visible in `mirror-test.png`. Any sprite that will be mirrored must
prohibit lettering in its prompt: *"no text, no lettering, no writing, no labels
or placards anywhere on the object"*. This costs nothing, but it has to be right
first time.

**3. Size is still a lottery.** `width`/`height` remain hints. The cabin came
back at 128 on its second attempt and needed a third run at `resolution: "2K"`
with "drawn large and filling the whole frame" to land on 256 — the same
behaviour seen across the isometric set.

## What did not work

**Wheel rotation is weak, and vanishes entirely if the sprite is shrunk.** At the
native 210 px frame the hubs vary only slightly between frames; scaled down to
64 px a wheel is roughly 7 pixels across and reads as a static dark disc
(`motion-64.png`). Adding tread contrast to the still would help marginally, but
no amount of prompting carries a convincing rotation through 7 pixels.

Displaying at or near native size is therefore the fix that costs nothing — which
is also the reason the pre-scaled copies were dropped in favour of letting Godot
scale. Beyond that:

1. **Accept it.** The body bob plus movement across the map already sell motion.
   Many games never show wheel spin at all.
2. **Hand-author the wheel cycle.** Real pixel artists fake this with a 2–3 frame
   tread offset rather than true rotation. Cheap in credits, costs manual work.

Separately, the animation has mild frame-to-frame drift — the cab silhouette and
tank banding wobble slightly between frames. Same behaviour as the isometric
animations; acceptable at speed, but worth knowing it is inherent to the tool.

## Verification run

- All four PNGs are exactly 256×256 and 64×64, `Format32bppArgb`, all four
  corners fully transparent (alpha 0).
- Spritesheet geometry checked: 1680×210 = 8 frames × 210 px, as reported.
- No stray artefacts in the transparent margins on any of the four.
