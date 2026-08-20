# 16 — Stage A: the base and the camera

**Goal: the truck stops being the player.**

This stage removes the driving game and replaces it with the thing every base
builder opens on — a camera over your ground, a base you can see, and clicking to
select. It ships no new engine interaction at all: every command that works today
still works, reached from the boards. What changes is who the player is.

Parent: [15_GAMEPLAY_REDESIGN.md](15_GAMEPLAY_REDESIGN.md).

---

## A1. The camera

| | |
|---|---|
| **Pan** | edge scroll, middle-drag, and W A S D — the keys keep working, they move the view instead of a truck |
| **Zoom** | wheel, to discrete steps between the whole lease and a single pad |
| **Bounds** | clamped to the basin plus a margin, so the world never leaves the frame |
| **Follow** | double-click a unit or a site to centre on it; any pan releases the follow |

**Why discrete zoom steps.** The world is tile art at a fixed pixel size.
Continuous zoom lands the tiles on fractional pixels and the ground shimmers as
it moves — the same resampling the layout audit's OFFGRID check exists to catch,
but across the whole screen. Steps that are whole ratios keep the tiles crisp.

## A2. The yard becomes a base

The yard already exists as scenery. It becomes the place the game is played from.

- **The buildings mean something:** the office is where jobs are commissioned,
  the workshop is where maintenance crews live, the warehouse and fuel farm are
  where vehicles idle, and the gate is where units enter and leave the lease.
- **Idle units stand in it.** A yard with the rig parked in it and one with the
  rig out are visibly different, which is how the engine's one-rig rule becomes
  something you read rather than something you are told.
- **Clicking a yard building opens the thing it is for** — the office opens the
  dispatch board, the workshop opens maintenance, the warehouse opens the fleet.

## A3. Selection

One click selects; the selection card already built shows what was selected.

| Selected | Card shows | Actions offered |
|---|---|---|
| A structure | the five belief factors, POS, distance to market | survey, drill |
| A well | status, this month's volume, a day's rate | test, log, core, choke, abandon |
| A chain element | condition where it is known, throughput, whether it is out | repair, service, fit monitoring |
| The plant | chain length, what is out, what is holding production back | the installs |
| A unit | what it is, where it is going, what it will do on arrival | recall |

**Nothing here is new engine surface.** It is the same eighteen commands the
dispatch board already offers, reached by pointing at the thing they act on
instead of picking from a list — which is the whole difference between a menu
and a base builder.

## A4. What the truck becomes

It stays, as a service vehicle: one unit among several in Stage B. It is no
longer driven, no longer carries the camera, and no longer has to be parked
somewhere for work to happen.

---

## Acceptance

- [ ] The camera pans and zooms over the whole basin and cannot leave it.
- [ ] W A S D moves the view; nothing in the game is driven.
- [ ] Clicking a structure, a well, a chain element or the plant selects it and
      the selection card fills from the read model.
- [ ] Every command reachable before this stage is still reachable.
- [ ] Idle units are visible in the yard, and the rig is absent from it exactly
      when the engine refuses a second drill.
- [ ] The layout audit reports zero faults on the gameplay screen.
- [ ] A forty-year run through `--play` produces byte-identical results to the
      run before this stage. **Stage A must not move a single number.**
