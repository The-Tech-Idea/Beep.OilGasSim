# 20 — Oilfield Days tracker

**What is built in the client, what is next, and what is blocked.**
The engine's equivalent is [`plans/MASTER_TRACKER.md`](../../../plans/MASTER_TRACKER.md);
this is its client-side counterpart and is referenced from it.

**Status:** ⬜ not started · 🟦 in progress · ✅ complete · ❌ blocked

---

## Current state

| | |
|---|---|
| **What runs** | A Godot 4.7.1-mono client with the real engine in process. Eleven screens: splash, menu, new game, options, load, dispatch, lease, fleet, results, pause, gameplay. All eighteen engine commands reachable. Save and load work. |
| **What it looks like** | One register throughout: the supplied UI atlas, nine-patched, over a theme the Beep kit reads. The painted-wood chrome is gone from every screen. |
| **What is measured** | A layout audit (`--audit`) with seven checks reports zero faults on all eleven screens. A ten-year auto-played run (`--play`) finishes at **$134.4M of $600M — Expired**. |
| **The gameplay** | **A base builder, as designed, and the redesign is complete.** The player directs a company from a yard: select a site, choose a job, a crew drives out, and the engine command is submitted **on arrival**. All four stages of [the redesign](15_GAMEPLAY_REDESIGN.md) are done. |
| **The proof it is still the engine's game** | A ten-year auto-played run measured **six times across the rebuild** — before Stage A and after every stage since — returns the same numbers every time: $134.4M, 18 holes, 7 surveys, 16 repairs, 2 units built, 6 wells, 13,067 m3. The client paces input; it does not simulate. |

---

## Built ✅

| | What | Evidence |
|---|---|---|
| C1 | Engine in process — Godot 4.7.1 loads net10 `OGSim.Composition` | settles plan 02 §1; no bridge process |
| C2 | Host-supplied content — `IContentSource` over `res://content` | 80 files; a bad sheet refuses the engine by name |
| C3 | Seeded New Game with world knobs onto `WorldParameters` | mockup 5; land fraction means what it says |
| C4 | Surface-only preview — the real `BasinWorld` in a `SubViewport` | §7A.4; no subsurface to leak |
| C5 | All 18 engine commands on the dispatch board | was 9; repair was the missing one that made the game unwinnable |
| C6 | Save / load — `SaveGame` payload plus a host sidecar | G-10 closed |
| C7 | Atlas chrome — nine-patch plates, one theme, kit widgets | 8 sheets, 3 named plates |
| C8 | Layout audit — 7 checks, each proved by a deliberate break | zero faults, eleven screens |
| C9 | Auto-player — a policy played over ten years | the scenario measured rather than guessed at |
| C10 | **Stage A: the camera is its own rig** — free pan, stepped zoom, clamped to the basin; the truck is parked and no longer carries the view | a ten-year run byte-identical to the one before it |
| C12 | **Structures carry a footprint and a clearance** — `StructureKind` resources and a shelf packer; each on its own gravel plot, chain order kept | replaced two switches on a display id |
| C14 | **Stage A complete** — yard buildings open the board they stand for | a door answers before the plant, on a tighter reach |
| C19 | **Engine: drill and seismic have no world pre-requisite** — the shared `CompartmentCount == 0` refusal removed, SDD-007 amended first, `FieldControl` dropped from `ActivityOrders` | GC-4 closed; 216 composition tests pass |
| C18 | **Stage D: operations** — clickable alerts that take the view and offer the repair, three standing orders, cold wells, an end-of-month line | a standing order sends a unit on a job the player could have sent manually, and nothing else |
| C17 | **Stage C: construction** — six `BuildKind` resources, a construction crew, and a scaffold that becomes a unit when the element appears in the chain | the host never decides a build has finished |
| C16 | **Stage B complete** — chain elements and units selectable, recall while travelling, the yard saved beside the engine's payload | a unit travelling at save time is stood down, never resumed |
| C15 | **Stage B foundation** — six `UnitKind` resources, two behaviours, one state machine, one submit | the command is raised on `Arrived` and nowhere else |
| C13 | **The supplied animation strips are in use** — a flare that burns and pumps that turn, switched by `Throughput > 0 && !Failed` | half the art had never been copied into the project |
| C11 | **Stage A: selection is a click** — a structure, a well, a chain element or the plant, and the actions follow the selection | proximity was the input method when the player was a vehicle |

## Game rules are a mode

[23_GAME_RULES_MODE.md](23_GAME_RULES_MODE.md). OGSim is built for realistic
scenarios; this game needs different **rules**, not different physics. A
contested rule is a contract with two implementations and the run composes one
set — never an `if (mode == …)` (design 03 §3.2). Seam built; GC-4's rule is the
first mover and is **restored** rather than deleted. Drilling is next and waits
on suspended wells.

## Next — the Settlers-shaped game

**The client redesign is done and it was not enough.** A player directs a company
from a yard, but the company is handed a complete refinery and a map of every
structure at month one, so the only verbs are upgrade and drill. The plan is
[22_SETTLERS_SHAPED_GAME.md](22_SETTLERS_SHAPED_GAME.md).

| Phase | What | Status | Size |
|---|---|---|---|
| **S1** | The map goes dark — risk registered on discovery, not generation; a block survey finds structures | ✅ — the licence is cut into 16 blocks, `seismic-2d` shoots one, and a new game knows of no structure at all. The area became an ENTITY rather than a coordinate and a radius, which is what let it be built at all (SDD-007 §5 gives an activity one `EntityRef`). `S1V1`–`S1V4` pin it; no existing test broke | medium |
| **S2** | The plant starts empty — `SurfaceChain` becomes a set, Install creates rather than upgrades | ⬜ | large |
| **S3** | Connections laid by the player — flowlines between built elements | ❌ | blocked on G-02 |
| **S4** | The yard extends — more crews, more sheds | ❌ | blocked on G-13 |

**The good news, verified:** `IFlowElementRegistry` already exposes `Add` and
`Connect`, `ViewFor` builds the topology from whatever exists, and availability is
already downstream-closed — so a half-built chain shuts in correctly today. The
flow engine was built for this; what blocks it is `Modules.cs` composing a fixed
ten-element chain at startup.

## Done — the client redesign

| Stage | Document | Status | Blocked on |
|---|---|---|---|
| **A** | [16 — base and camera](16_STAGE_A_BASE_AND_CAMERA.md) | ✅ | camera, click selection, parked truck, yard doors — and a byte-identical ten-year run |
| **B** | [17 — units and dispatch](17_STAGE_B_UNITS_AND_DISPATCH.md) | ✅ | roster, travel, arrival-submits, recall, selection, and the yard in the save |
| **C** | [18 — construction](18_STAGE_C_CONSTRUCTION.md) | ✅ | catalogue as resources, crew dispatch, scaffolds that come down when the chain grows |
| **D** | [19 — operations](19_STAGE_D_OPERATIONS.md) | ✅ | alert-to-dispatch, standing orders, cold wells, the month reported |

How they are built: [21 — game code patterns](21_GAME_CODE_PATTERNS.md).

---

## Blocked ❌ — and what would unblock it

| Gap | What it stops | What would close it |
|---|---|---|
| **G-02 / G-14** | The build mockup's tile grid and placement ghost. Stage C puts additions in the next free bay because the engine has no coordinate for a facility. | A placement command with a footprint. |
| **G-13** | A roster the player buys and grows. Stage B derives the roster from what the engine accepts because inventing a limit would be a difficulty the engine cannot save. | A crew or fleet entity, or an explicit ruling that the roster is client-side. |
| **G-15** | A progress bar on a running job. Stage B shows *what* and *since when* because `ActivitiesRunning` is a count. | A per-activity view: kind, subject, remaining ticks. |
| **G-04** | Reputation anywhere — menu card, status bar, results. | A published company metric that owns it. |
| **G-09** | A preview drawn from engine world-generation rather than client terrain. | A surface-only host projection. |

---

## Findings the client has raised against the engine

| # | Finding | Status |
|---|---|---|
| GC-1 | **`FieldReadModel.Prospects` does not filter what has been drilled.** The field is documented as "every structure the world placed that the company has **not drilled**"; the projection filters on `risks.Knows` and nothing else. Seed 3: prospect #1 drilled three times, POS climbing 0.23 → 0.32 → 0.40, count static at seven. The behaviour is defensible — a second well into a found structure is appraisal — but it and the documentation cannot both be right. | open, engine's call |
| GC-2 | **A restored engine has no read model until a tick runs.** `FieldReadModel` is published at Close, so `Load` advances one month and a loaded game resumes on the month after the one it was saved on. | open; a projection callable at tick zero would close it |
| **GC-4 — FIXED** | **A world could generate that was unplayable from month one.** On 2 of 12 seeds tested (5 and 6, at 24 km) every activity is refused with `reject.no-target` — *"there is nothing here to work on"* — for the whole ten years. The cause is in `Activities.Refusals`: the check is `field.CompartmentCount == 0`, a FIELD condition, while `FieldReadModel.Prospects` was at the same moment publishing **11 and 12 structures** with probabilities of success attached. So the read model advertises targets the command layer will not accept, and a player can select a structure, order a hole, and be told there is nothing there — every month, for a decade, with no way to tell it is terminal. The auto-played run reports a tidy `Expired` at $1.7M and looks exactly like a balance result. | open, engine's call |
| GC-3 | **The shipped scenario is winnable, and brutal.** Measured on eight seeds at 24 km with one policy: **1 Met** (seed 1, $594.0M, 15 holes, 8 wells), **1 Expired** (seed 3, $134.4M), **4 insolvent** before month 55 (seeds 2, 4, 7, 8 — each after three dry holes), and **2 dead worlds** (see GC-4). So the target is reachable but the run is decided early: on every seed that failed, the company was broke inside five years having drilled three holes and found nothing. What the numbers say is not that $600M is too high — it is that **there is no recovery from an unlucky opening**, because $50M buys about three holes and a dry one returns nothing to fund the next. | open, design question |

---

## Working rules for this client

- The engine decides every outcome; the client renders and paces input.
- Nothing is drawn that the read model does not publish — no invented
  reputation, no estimated field count, no fake progress.
- A refusal shows **every** reason, never the first.
- Chrome is one file; data a designer would tune is a `Resource`.
- Dev tools are command-line flags and never run in a game.
