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
| G3 | Oil flows through the chain, not past it | Reservoir → completion → separation → (gas · water paths) → transport → **metered custody** → sale; the direct well→sale path is deleted, not bypassed |
| G4 | A scenario is loaded, never compiled | The `ScenarioGoal` record inside `OGSim.Composition` is replaced by an `IScenarioRunner` reading `Scenario` content (finding 141's contracts) |
| G5 | The whole engine saves | One call captures every registered state owner; a reload continues bit-identically (PV2) |

---

## 2. The work, in order

The order is by what each step unblocks, not by phase number.

| Step | What | Why here |
|---|---|---|
| 1 | **R12b — activities on the one engine.** Collapse the drilling timer (finding 142), then templates: well test, seismic, log/core, workover, install, abandon | The lever: most bypassed subsystems are reached by an activity or not at all. Well test and seismic are the door to G2 |
| 2 | **R21e — the scenario runner** | R21d's debt; pays before content is written against the ad-hoc goal |
| 3 | **Beliefs wired (R20d.7)** | The survey activities from step 1 produce `Observation`s; the player stops being told where the oil is. The exploration game begins here |
| 4 | **The chain (R20d.1–5)** | `IFlowSolver` over the registered network; separation, gas, water, transport; custody becomes a metered point. Closes FV1's 1000-tick half, R11-V13, SC7/SC8 |
| 5 | **Environment (R22 + R20d.13)** | The one unbuilt phase; weather, seasons, access windows feeding stage 2 |
| 6 | **Technology purchasable (R20d.10) + company (R20d.9)** | The four acquisition routes as commands; licences and commitments; the forty-year arc starts meaning something |
| 7 | **Equipment content (R20c.9)** | The sixteen catalogue sheets become loadable kinds; `Defaults.CompletionFor` becomes a loader. Variety arrives here |
| 8 | **R20 · R21 · R25** | Scenarios and balance on real content; the full host surface; the advisor |

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
