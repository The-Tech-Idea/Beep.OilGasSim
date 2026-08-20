# 15 — Gameplay redesign: the base, the yard, and the dispatch loop

**The player is not a truck.** That is the defect this document answers.

The build to date puts the player *inside* a single service truck, driven with
W A S D, and every action is taken by parking next to something and pressing a
number. It reads as an adventure game with an oilfield behind it. The game this
is meant to be — stated at the outset and restated since — is **The Settlers with
oil**: you hold a base, you commission work from it, crews and vehicles carry
that work out to the field, and the field runs while you decide what to do next.

This document is the revision. It is the parent of the stage documents that
follow it and the thing they are measured against.

---

## 1. What changes

| | Now | After |
|---|---|---|
| The player | drives one truck | directs a company from a yard |
| Input | W A S D, park, press a number | select a site, choose a job, dispatch |
| Camera | follows the truck | free pan and zoom over the lease |
| Units | one truck, cosmetic | a roster of crews and vehicles that travel and return |
| Work | happens instantly wherever you stand | is carried to a site and starts on arrival |
| Building | a menu entry | a construction job with a crew and a build-out |
| The yard | scenery | the base: where units live, where work is commissioned |

**What does not change: OGSim decides everything.** Not one number in the list
above moves an outcome. That is the subject of §2, and it is the constraint the
whole redesign is shaped around.

---

## 2. The law this redesign lives under

Plan 11 §7 and GAME-SDD-001 §1 say the engine is authoritative and the client
renders. A logistics layer is exactly the kind of thing that erodes that, quietly
and in good faith, so the boundary is stated here in its strongest form.

### 2a. The engine owns every outcome

Whether a job is possible, what it costs, how long it takes, whether the hole is
dry, how much the field makes, when equipment breaks — all of it is the engine's,
reached by submitting a command and reading the snapshot. The host computes
**none** of it.

### 2b. Travel paces input; it does not simulate

A crew driving to a site takes real seconds. When it arrives, the host submits
the command. So travel decides **when a command is submitted** and nothing else.

This is legitimate and worth being precise about why: a host has always decided
when a player may act — a menu that is closed, a button not yet clicked. Travel
is the same power, made diegetic. What would *not* be legitimate is travel that
changed the command's cost, its duration, or its result, because those belong to
the engine's activity model.

**The test:** dispatch a job and let a crew drive to it; submit the identical
command from a console at the moment of arrival. The run must be byte-identical.
If it is not, the host is simulating.

### 2c. No invented scarcity

The yard's roster is **derived from what the engine will accept**, not chosen to
make the game harder.

- The rig is one, because **the engine serialises rig work** — a second drill
  command is refused while the first is running. That scarcity is engine truth
  and the yard shows it.
- Survey, wireline, coring and maintenance crews are as many as the engine will
  run at once. If it accepts two concurrent surveys, the yard has two survey
  crews.

A roster the player buys and grows is the natural next thing to want, and it
**cannot be built honestly yet**: OGSim has no crew or fleet entity, so a limit
of "you own two trucks" would be a difficulty the engine does not know about, and
a saved game could not carry it. That is registered as a gap (§6) rather than
invented.

### 2d. The host may lay things out; it may not place them

The engine has no coordinates for facilities (gap G-02), so the host chooses
where the chain sits and draws it. That is presentation. What the host must not
do is let a player *choose* a coordinate and imply it matters — a separator built
on the north pad and one built on the south pad are the same separator to the
engine, and a build mode that suggested otherwise would be a lie with a ghost
attached.

---

## 3. The loop

```
        ┌──────────────────────────────────────────────────┐
        │  THE YARD — office, workshop, warehouse, fuel     │
        │  crews and vehicles idle here between jobs        │
        └───────────────┬──────────────────────────────────┘
                        │ 1. commission
                        ▼
        ┌──────────────────────────────────────────────────┐
        │  PICK A SITE          PICK A JOB                 │
        │  a structure          survey / drill / log / core │
        │  a well               test / choke / abandon      │
        │  a chain element      repair / service / monitor  │
        │  the plant            build a separator, a tank…  │
        └───────────────┬──────────────────────────────────┘
                        │ 2. dispatch — a unit leaves the yard
                        ▼
        ┌──────────────────────────────────────────────────┐
        │  TRAVEL — seconds, drawn, skippable at speed      │
        └───────────────┬──────────────────────────────────┘
                        │ 3. arrival — THE COMMAND IS SUBMITTED
                        ▼
        ┌──────────────────────────────────────────────────┐
        │  OGSim runs the activity over its own months      │
        │  the host draws what the read model reports       │
        └───────────────┬──────────────────────────────────┘
                        │ 4. the unit returns to the yard
                        ▼
                    back to idle
```

Meanwhile, and without the player doing anything: the field produces, the chain
jams, prices move, equipment wears and fails. Those are ticks, not jobs.

---

## 4. Why this is the same game the engine was built for

The engine's own loop is *commit capital under uncertainty → wait → find out*.
The redesign does not soften that; it gives it a body.

- **Waiting becomes visible.** A four-month drill is currently a number going up
  in a panel. With a rig convoy that leaves the yard, sets up, and stands on the
  pad for four months, the wait is the thing you can see and the rig is visibly
  the bottleneck it already is.
- **The one-rig constraint becomes legible.** The engine refuses a second drill
  while one runs. Today that is a greyed button; after, it is a rig that is
  physically somewhere else.
- **Maintenance becomes a logistics problem**, which is what it is. A failed
  separator shuts the chain in; a crew has to get there. The measured run in
  `14_GAME_SDD_CONFORMANCE.md` spent sixteen months of ten years on repairs — the
  redesign is what makes that legible instead of a line in a log.

---

## 5. The stages

Each has its own document, its own acceptance test, and can ship on its own.

| Stage | Document | What it delivers |
|---|---|---|
| **A** | [16_STAGE_A_BASE_AND_CAMERA.md](16_STAGE_A_BASE_AND_CAMERA.md) | the yard as a real base, free camera, click selection — the truck stops being the player |
| **B** | [17_STAGE_B_UNITS_AND_DISPATCH.md](17_STAGE_B_UNITS_AND_DISPATCH.md) | a unit roster, travel, arrival-submits-command, return |
| **C** | [18_STAGE_C_CONSTRUCTION.md](18_STAGE_C_CONSTRUCTION.md) | facility installs as construction jobs with a crew and a build-out |
| **D** | [19_STAGE_D_OPERATIONS.md](19_STAGE_D_OPERATIONS.md) | standing orders, the maintenance loop, the alert-to-dispatch path |

How all four are built — resources over constants, behaviour in the hierarchy and
kind in data, one state machine, signals outward — is
[21_GAME_CODE_PATTERNS.md](21_GAME_CODE_PATTERNS.md). It binds the stages the way
SDD-000 binds the engine's phases.

Progress is tracked in [20_GAME_TRACKER.md](20_GAME_TRACKER.md).

---

## 6. Gaps this redesign registers

Numbered in the game SDD's series, continuing from G-12.

| | Gap | Why it blocks | What would close it |
|---|---|---|---|
| **G-13** | No crew or fleet entity in OGSim | A roster the player buys and grows would be a difficulty the engine does not know about and a save could not carry | An engine concept of a unit with a state, or an explicit ruling that the roster is client-side and saved by the client |
| **G-14** | No coordinates for facilities (restates G-02) | A build mode that lets a player choose where a separator goes implies a placement the engine does not model | A placement command with a footprint, per GAME-SDD-001 §G-02 |
| **G-15** | Activity progress is not published per activity | The read model reports `ActivitiesRunning` as a count, so a unit standing on a pad cannot show how far along its job is | A per-activity view carrying its kind, subject and remaining ticks |

**G-15 is the one that bites soonest.** Stage B can dispatch and arrive without
it, but a rig that stands on a pad for four months with no progress to show is a
worse experience than the panel it replaced. Until it exists, the host shows what
it honestly has — *working*, and the month it started — and never a percentage
(§12 of the game SDD, and the same rule that keeps the world-generation screen
from counting to 78%).
