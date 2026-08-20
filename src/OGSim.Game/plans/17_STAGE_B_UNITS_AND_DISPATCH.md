# 17 — Stage B: units and dispatch

**Goal: work is carried to the field by something that has to get there.**

This is the stage that makes it a Settlers game. A job is commissioned at the
yard, a unit drives out, and **the engine command is submitted when the unit
arrives** — not when the button is pressed.

Parent: [15_GAMEPLAY_REDESIGN.md](15_GAMEPLAY_REDESIGN.md). The law this stage
lives under is §2 of that document, and every design choice below is downstream
of it. How it is built is [21_GAME_CODE_PATTERNS.md](21_GAME_CODE_PATTERNS.md) —
in particular P1 (a unit kind is a `Resource`, not a class), P2 (behaviour is the
hierarchy, kind is data) and P4 (the command is submitted on exactly one state
transition).

---

## B0. How a unit is defined

Per P1 and P2: a unit's **kind** is a `UnitKind` resource — display name,
portrait, frames, road speed, the job it carries, where it stands in the yard —
and its **behaviour** is one of two classes.

```
res://data/units/rig-convoy.tres        UnitKind
res://data/units/survey-crew.tres       UnitKind
res://data/units/wireline-truck.tres    UnitKind
res://data/units/coring-unit.tres       UnitKind
res://data/units/well-services.tres     UnitKind
res://data/units/maintenance-crew.tres  UnitKind
res://data/units/construction-crew.tres UnitKind

scenes/units/Unit.tscn                  the shared body
scenes/units/Vehicle.tscn               inherited: facing, turning wheels
scenes/units/Crew.tscn                  inherited: walk cycle
```

**Adding the eighth unit is a `.tres` and an art set.** No class, no `case`, no
recompile. If adding one ever requires a subclass, P2 has been broken and the
roster below has quietly become a hard-coded list again.

## B1. The roster

| Unit | Carries out | How many |
|---|---|---|
| Drilling rig convoy | drill | **one — engine truth.** A second drill command is refused while one runs |
| Survey crew | seismic survey | as many as the engine will run at once |
| Wireline truck | wireline log | as many as the engine will run at once |
| Coring unit | cut core | as many as the engine will run at once |
| Well services | well test, choke, abandon, remediate injector | as many as the engine will run at once |
| Maintenance crew | repair, service, fit monitoring | as many as the engine will run at once |
| Construction crew | the installs and export expansion | Stage C |

**"As many as the engine will run at once" is measured, not chosen.** At startup
the host does not know the number; it discovers it the way a player would, by
being refused. A unit is on the yard while the engine is still accepting that
kind of work and is not while it refuses — which means the roster is a *readout
of engine state*, not a resource the host invented. §2c of the parent document
is why this matters more than it looks.

## B2. A job's life

```
commissioned ──▶ travelling ──▶ ARRIVED: command submitted ──▶ working ──▶ returning ──▶ idle
                     │                        │
                     │                        └─ engine REFUSES: the unit turns
                     │                           round and the refusal is shown
                     │                           in full, every reason
                     └─ recalled by the player: no command is ever submitted
```

Three things this shape gets right:

- **A refusal on arrival is shown, not swallowed.** The engine can refuse for
  reasons that were not true when the job was commissioned — the rig became busy,
  cash ran out. The unit turns round and the player is told why, all of it
  (§9.1).
- **Recall is free before arrival**, because nothing has been submitted yet.
  After arrival the activity is the engine's and the host cannot take it back.
- **The unit is not the activity.** It stands at the site while the engine runs
  the months. If the player saves and reloads, the activity survives (the engine
  saved it) and the unit is re-placed at its subject — the host's own bookkeeping,
  in the sidecar beside the save.

## B3. Travel

| | |
|---|---|
| Path | along the roads the world already lays between the yard and each site |
| Speed | scaled so a cross-lease trip is a few seconds at 1×, proportionally faster at 2× and 4× |
| At pause | units stop. Nothing moves when the clock does not |
| Skip | a "send and skip" option jumps the unit to the site and submits at once, for players who do not want the drive |

**Why travel is allowed to exist at all** is §2b: it decides *when* a command is
submitted and nothing else. **Why "send and skip" must exist** is the same
sentence read the other way — if travel only paces input, then skipping it must
be available, and a game that forced the wait would be charging for something the
simulation does not model.

## B4. What the host stores, and what it must not

**Stores** (client bookkeeping, saved in the sidecar):

- each unit's kind, position, state, and what it is going to do on arrival
- which structure a job was commissioned against

**Must not store, or invent:**

- fuel, wear, condition or a wage for any unit — the engine has no such concept
  (G-13), and a truck that ran out of diesel would be the host simulating
- a travel cost in money or time charged to the company
- a roster limit beyond what the engine refuses

## B5. What the player sees while a job runs

The read model publishes `ActivitiesRunning` as a count and nothing per activity
(gap G-15). So a unit standing on a pad can honestly show:

- what it is doing — the host knows, it submitted the command
- the month it started — the host knows, it read the tick
- that it is still running — `ActivitiesRunning` has not fallen

and **must not** show a percentage, a bar, or a finish date. Until G-15 exists,
"drilling, started month 14" is the whole truth and a progress bar would be a
guess dressed as a measurement.

---

## Acceptance

- [ ] Commissioning a job sends a unit; the command is submitted on arrival and
      not before. Verified by log: the submit line follows the arrival line.
- [ ] A job dispatched and then recalled before arrival submits nothing.
- [ ] A refusal on arrival turns the unit round and shows every reason.
- [ ] The rig is absent from the yard exactly while the engine refuses a drill.
- [ ] Units stop at pause and scale with speed.
- [ ] "Send and skip" produces a run identical to letting the unit drive.
- [ ] **A run played with travel matches a run played from the console** —
      same seed, same commands, same months: byte-identical. This is the test
      that the host is pacing input rather than simulating.
- [ ] Save and reload puts every unit back at its subject with its job intact.
- [ ] A new unit kind can be added with a `.tres` and an inherited scene, with no
      C# change. Demonstrated, not asserted.
- [ ] No `UnitKind` field changes a simulation outcome — the resource carries
      look and pacing only.
