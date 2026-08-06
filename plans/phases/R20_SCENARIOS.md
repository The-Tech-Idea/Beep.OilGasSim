# Phase R20 — Scenarios and Balance

**Arc IV · Hardening** · Status ⬜ · Depends on: R19 · Enables: R21

---

## 0. Purpose

Prove the whole game works, and make the numbers realistic.

**SC1 — the full-lifecycle acceptance test — is the gate for the entire engine.**
Nothing is finished until it passes.

---

## 1. Goals

| # | Goal | Acceptance |
|---|---|---|
| G1 | A full company lifecycle runs | SC1: licence → seismic → discovery → development → plateau → decline → abandonment, ~40 years |
| G2 | All ten scenarios pass | SC1–SC10 from [12](../design/12_VERIFICATION.md) §4 |
| G3 | Calibration holds | CAL1–CAL10 from [05](../design/05_SIMULATION_MODELS.md) §10 |
| G4 | Invariants hold throughout | Every invariant, every tick, in every scenario |
| G5 | The game is winnable and losable | Neither trivially |

---

## 2. Design decisions

### 2.1 SC1 is the acceptance test for the engine

It exercises every subsystem in combination over the full time horizon and
asserts that the resulting history is *plausible* — production profile, cost
profile, water cut trajectory, recovery factor, field life, and a final state
with all obligations discharged.

**Every invariant is checked every tick throughout**, so SC1 is also the longest
continuous correctness proof the engine has.

### 2.2 Balance lives in content, and band tests constrain it

Every tuned number is content ([10](../design/10_CONTENT_AND_UNITS.md) §5). The
band tests (MB1–MB7) constrain where tuning may go. **A balance change that pushes
recovery factors to 95% fails a build**, which is the only reliable defence
against slow tuning drift.

### 2.3 Scenarios are content

A `scenario` declares a starting world (by seed or authored), a starting company
position, objectives and scripted events. Tutorials, challenges and campaign
missions are all scenarios. **The engine has no scenario-specific code.**

### 2.4 Tutorials teach through the physics

The natural teaching sequence follows the chain: one well, natural flow → it dies
→ install lift → water arrives → build treating → gas cannot be flared → build
the gas chain → the tank fills → schedule a tanker.

**Each step is a real consequence the player just experienced**, not a text box.
The game teaches petroleum engineering by making it necessary.

### 2.5 Balance targets, stated

| Target | Value |
|---|---|
| First discovery | Within roughly the first two in-game years for a competent player |
| First production | Within roughly four in-game years |
| Failure mode | Running out of cash during development is the most common loss |
| Late-game challenge | Reserve replacement, not cash |
| Loop dominance | Shifts through the six stages of [17](../design/17_CROSS_IMPACT_MATRIX.md) section 4 — verified by CI-V13 |
| Slow-loop detectability | No player reaches a downward loop's consequence without its entry event having fired (IR3) |
| Full campaign | Roughly 30–50 in-game years |

---

## 3. Deliverables

`OGSim.Scenario.Tests`: SC1–SC10 as automated tests. Calibration suite CAL1–CAL10.
Balanced content across every catalogue. Tutorial and campaign scenarios as
content. A performance benchmark suite over SC1.

---

## 4. Verification

| # | Test | Passes when |
|---|---|---|
| R20-V1 | SC1 | Completes; every stage occurs; final state plausible; every invariant holds every tick |
| R20-V2 | SC2–SC10 | All pass |
| R20-V2b | SC11 hostile environment | Access windows bind; a missed window costs a year; the licence clock does not pause |
| R20-V2c | SC12 HSE neglect | Barriers degrade, near misses rise, a serious incident eventually occurs — **and every one was preceded by a detectable indicator** (HS3) |
| R20-V2d | SC13 slow-loop visibility | Each loop's entry event fires while at least two exits remain, and the consequence names the entry tick (IR3, IR5) |
| R20-V3 | Calibration CAL1–CAL10 | All within their bands, or exact where analytic |
| R20-V4 | Band tests MB1–MB7 | All pass with shipped content |
| R20-V5 | Winnability | A scripted competent play sequence succeeds |
| R20-V6 | Losability | A scripted poor play sequence fails, for the designed reason |
| R20-V7 | Tutorial | Each tutorial scenario's objective is achievable by the intended action |
| R20-V8 | Performance | SC1 completes within the CI budget; per-tick time is recorded with a regression threshold |
| R20-V9 | Determinism at scale | SC1 produces an identical digest across platforms and runs |
| R20-V10 | No scenario-specific code | Architecture test: scenarios reference only content and the command surface |

---

## 5. Out of scope

Player-facing UI (host concern). Difficulty presets beyond the fidelity dial
(deferrable to content). Balance for multiplayer (not a goal).

---

## 6. Risks

| Risk | Mitigation |
|---|---|
| SC1 is slow and hard to debug when it fails | Per-stage assertions and a state digest per tick localise a failure to a tick and a subsystem |
| Balance passes are open-ended | Band tests and calibration checks define "done"; balance stops when they all pass |
| Late-arriving cross-subsystem defects | Scenario tests SC3, SC4, SC7, SC8 run from their enabling phases onward, not first at R20 |
| Content volume needed for a full campaign is large | The campaign is content and can grow after the engine ships; SC1 needs only one basin |
