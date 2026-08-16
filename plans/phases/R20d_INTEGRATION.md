# Phase R20d — Integration: from a spine to the game

**Arc IV · Hardening** · Status 🟨 · Depends on: everything built · Enables: R20, R21, R25

Covers the composite programme the tracker records as **R20c** (composition),
**R12b** (the activity catalogue), **R21a–f** (the playable slice and the
scenario runner) and **R20d** (the wiring table). One document, because they are
one piece of work seen from four sides: making the engine that exists into the
game that was designed.

---

## 0. Purpose

Every phase before this one answered "is the model right?". This phase answers a
different question: **does the running engine use it?** For eight subsystems the
honest answer at time of writing is no — complete, tested, and bypassed. The
purpose of R20d is to make that answer *yes* subsystem by subsystem, without a
single stubbed step, until the loop a player lives touches everything the
designs describe.

The distinction matters enough to state as a rule: **a phase mark says built and
tested; only an R20d mark says it is in the game.**

---

## 1. Goals

| # | Goal | Acceptance |
|---|---|---|
| G1 | One activity engine, no bespoke timers | Drilling, testing, surveying, working over, installing and abandoning all run on `OperationScheduler`; the R21b drilling timer is gone (finding 142) |
| G2 | The player learns instead of being told | Nothing reveals subsurface truth except an `Observation` produced by an activity the player paid for |
| G3 | Oil flows through the chain, not past it | 🟨 **the spine is in**: reservoir → completion → header → separation → **metered custody** → sale, and `WellsState.ProduceOver` — the direct path — is deleted rather than bypassed. The gas and water paths and transport are not |
| G4 | A scenario is loaded, never compiled | ✅ mechanism · ⬜ loader — `ScenarioGoal` is gone and `ScenarioRunner` reads a `Scenario`; the records are still built in `Defaults` rather than parsed from JSON, which is R21f |
| G5 | The whole engine saves | One call captures every registered state owner; a reload continues bit-identically (PV2) |

---

## 2. The work, in order

The order is by what each step unblocks, not by phase number.

| Step | What | Why here |
|---|---|---|
| 1 | **R12b — activities on the one engine.** ✅ drilling collapsed (142, 148) · ✅ one activity, one class (149) · ✅ the four measurements: survey, log, core, build-up (150, 151). **The templates that CHANGE something do not come next** — workover, install and abandon each reach a subsystem this list has not yet wired, so all three would complete and mean nothing (finding 153). They move into steps 4 and 6, beside the subsystem that gives each one something to change | The lever: most bypassed subsystems are reached by an activity or not at all. Well test and seismic are the door to G2 |
| 2 | **R21e — the scenario runner** | ✅ R21d's debt, paid: the goal is an `Objective` over a read-model path, and a scenario naming a projection the loop cannot fill is refused when the engine composes. `ScenarioGoal` and `Outcome` are gone (findings 154–156) |
| 3 | **Beliefs wired (R20d.7)** | ✅ both directions, ahead of schedule — step 1's measurements produce `Observation`s through `ObservationSampler`, and stage 13 projects the result as `BeliefEntryView` (R21-V7). A player pays to learn and can see what they learned. POS is the remainder and has no subject until a world generator makes prospects (R20d.8) |
| 4 | **The chain (R20d.1–5)** — with **R12b.8 install** | 🟨 **R20d.1 done** — `IFlowSolver` over the registered network; separation, gas, water, transport; custody becomes a metered point. Closes FV1's 1000-tick half, R11-V13, SC7/SC8. An install template is catalogue work the moment a facility is something the loop routes through rather than around. **Proceeding gap by gap** — four design gaps sit between a well and a sale; three are closed (findings 157, 158, and the registry's own enumeration) and a manifold (159) closed the fourth, so every element between a well and a sale now exists |
| 5 | **Environment (R22 + R20d.13)** | The one unbuilt phase; weather, seasons, access windows feeding stage 2 |
| 6 | **Technology purchasable (R20d.10) + company (R20d.9)** — with **R12b.10 abandon** | The four acquisition routes as commands; licences and commitments; the forty-year arc starts meaning something. An abandonment obligation is a company liability, so the template that discharges one arrives with the ledger that carries it |
| 7 | **Equipment content (R20c.9)** | The sixteen catalogue sheets become loadable kinds; `Defaults.CompletionFor` becomes a loader. Variety arrives here |
| 8 | **R20 · R21 · R25** | Scenarios and balance on real content; the full host surface; the advisor |

**R20d.3/.4 are scouted before they are started** (finding 162). A well produces
one material because SDD-003 §6.1 pins the oil conversion and never says what
else comes up the hole; gas needs an air-density constant `PhysicalConstants`
does not carry; water has no source because `FractionalFlow` is built, tested,
internal and called by nothing. Three SDD amendments come first. That ordering is
not caution — it is what R20d.1 cost by not doing it: four gaps found one at a
time, mid-wire, and one revert.

**What step 4 has cost so far, and why that is the point.** One attempt at
wiring the solver into the loop found FOUR gaps in the elements between a well
and a sale: a separator with no pressure to impose (157), an S4 that could only
express friction and not control (158), a registry nothing could ask what it
held, and a manifold that is designed, costed and declared nowhere (159). None
was visible while each module was tested against its own fixtures — every one of
them surfaced within an hour of trying to make two modules touch. All four are
now closed and the wiring itself is what remains. **A phase mark
says built and tested; only an R20d mark says it is in the game**, and the
distance between those two statements is measured in findings like these.

**Two of the thirteen wiring rows have no step above, and finding 153 is how
that surfaced.** R20d.8 (world generation) and R20d.11 (integrity & HSE) are in
the tracker's wiring table and in nobody's order — which was invisible until an
activity needed one of them. **R12b.6 workover waits on R20d.11**: it restores a
condition, and no component has one to restore until integrity owns state and
runs at stage 4. Both belong in this order before step 8, and where exactly is
the next revision of this section rather than a guess made here.

---

## 3. Design decisions already taken (and where they are argued)

- **An activity is the only verb.** Nothing the player does to the world happens
  except as an operation on the one scheduler — tracker §R12b, finding 142.
- **Mass moved by an operation posts into the tick's conservation terms** with
  the operation as the audited element; a routed test separator wins over the
  operation's own report — SDD-007 §5b, finding 147.
- **Surveys are activities, not lookups.** The belief store updates only through
  `Observation`s, and an `Observation` exists only because an activity completed
  — SDD-008 §3, §7.
- **The four mechanics with pinned algorithms and no tasks** — souring, VOI,
  reserve-based lending, operation mass — have contracts as of finding 147 and
  land in steps 1, 3, 4 and 6 respectively.
- **Wiring order is not build order.** Beliefs before the chain, because being
  told where the oil is falsifies more of the game than a bypassed separator.

---

## 4. What this phase closes

The deferred verifications that named "the tick loop" as their blocker:
FV1 (1000-tick conservation), R6-V14 follow-ons, R10-V9, R11-V13, SC7, SC8,
PV2/PV3/PV4/PV8, R14.6, R18.5, R24.5 — plus PD1 (the command set derived from
the 61-decision catalogue) once activities are commands.

---

## 5. Verification

Each wiring step re-runs the owning phase's deferred suite *through the composed
engine* rather than through fixtures — that is the point of the phase. New
end-to-end checks live in `OGSim.Composition.Tests`: the loop tests
(`ProductionLoopTests`, `GameplayTests`) grow one scenario per step, and the
stage list assertion (`The_shipped_engine_runs_the_stages_its_modules_declared`)
is updated per step so an unclaimed stage cannot arrive silently.

### 5.1 The verifications, declared (finding 213)

**Written down after the fact, which is the defect being closed rather than a
style note.** This phase and R12b shipped 29 tests carrying ids — `R20dV1`,
`R12bV10` and the rest — against a phase document that declared none of them, so
every one cited a verification that did not exist. The convention is that a
verification id appears verbatim in a test name *so the mapping can be trusted*;
an id pointing at nothing is worse than no id, for the same reason finding 209
gave when three tests carried the wrong one.

Nothing here is new scope. Each row states what its tests already assert, so the
table is a record of the phase as built and the ids resolve from today.

| # | Verification | What it asserts |
|---|---|---|
| R20d-V1 | The surface chain is real and visible | The read model carries the chain in FLOW ORDER and every element on a flowing leg reports what crossed it; a header sums its inlets, has no pressure drop, reports no constraint of its own and declares its slots as inlet ports; a second well commingles rather than being refused; a facility-limited field produces its capacity rather than its potential; provenance blends by MASS and not by well count, so a well producing nothing takes no share of the sale. Refusals: a well with no slot is refused *before it is paid for*, a slot beyond the header is an invariant fault, and a header with no slots is a model fault |
| R20d-V3 | Gas leaves the separator | A well produces gas, the separator sends it to the flare, and flared gas earns nothing |
| R20d-V4 | The water leg exists before it carries anything | The leg is present and DRY before breakthrough — the absence of water is modelled rather than the leg being absent |
| R20d-V5 | Revenue is caused by a metered delivery | A sale cites a custody transfer (SDD-009 §1), and a month with no delivery records no transfer |
| R12b-V2 | The header a full field needs can be installed | The ladder reaches the size a developed field requires, so the refusal in R20d-V1 is a decision rather than a dead end |
| R12b-V8 | Capacity is a purchase with a price | A player sees the jam, pays for a bigger vessel and the field flows; a vessel is CAPITALISED and a survey is not; the top of the ladder is refused with a reason |
| R12b-V10 | An abandonment obligation is carried, not conjured | A well carries its obligation from the day it opens; shutting a well in does not discharge it; abandoning a plugged well is refused; abandoning the last well closes the field |

**`R24V4` was not one of these and is renamed rather than declared.** Its two
tests assert that objectives read the snapshot and touch nothing after stage 12,
which is R24-V18 — *"stage placement (I-V4, I-V5)"* — so the id was a phase
prefix welded onto an INVARIANT id, producing `R24-V4`, which R24 does not have
(it declares V14–V18). Renamed to `R24V18_*`.

`Record_EveryVerificationIdInATestNameIsDeclared` fails when a test cites an id
no document declares, so this cannot silently recur.
