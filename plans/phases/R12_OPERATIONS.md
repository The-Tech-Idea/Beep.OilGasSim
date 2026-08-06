# Phase R12 — Operations and Scheduling

**Arc III · The company** · Status ⬜ · Depends on: R6, R8 · Enables: R13, R16, R18

---

## 0. Purpose

Until R12, things exist the moment a test creates them. R12 makes everything take
**time, money and a resource** — and makes "what is my company doing right now?"
a single answerable question.

---

## 1. Goals

| # | Goal | Acceptance |
|---|---|---|
| G1 | One abstraction for every scheduled activity | Drilling, completing, working over, constructing, surveying, laying pipe, abandoning — all are `IOperation` |
| G2 | Resources are contended | Two operations needing one rig cannot both run; the conflict is reported, not silently serialised |
| G3 | Operations can fail | A drilling operation can encounter trouble, cost more, take longer, or fail outright |
| G4 | Nothing appears instantly | Every asset in the game arrives through an operation |
| G5 | Prerequisites are checked at scheduling, not at completion | An impossible operation is rejected when proposed, with a domain reason |

---

## 2. Design decisions

### 2.1 One `IOperation` contract, many outcome types

Duration, cost profile over time, required resources, prerequisites, a risk
profile, and an outcome applied on completion.

*Rationale:* one abstraction means one scheduler, one cost-accrual path, one
progress projection, one audit shape, one failure model. It also makes the
"company activity" view a single query rather than a union of subsystem views.

### 2.2 Cost accrues over the operation, not at the end

A six-month drilling operation spends money for six months. **This is what makes
cash flow tight during development** and it is the mechanism by which an
over-committed company runs out of money mid-well — a real and instructive
failure.

### 2.3 Resources are reserved at scheduling

A rig is committed to an operation for its duration and unavailable to others.
Contention is a scheduling-time rejection with a reason ("no rig available until
month 14"), never a silent queue.

*Rationale:* silent queuing hides the constraint. An explicit rejection tells the
player they need another rig, which is the decision.

### 2.4 Operations have risk profiles, and trouble is graded

Not binary success/failure. Graded outcomes: on time; delayed; over budget;
partial success (well drilled but not to target); failure (hole lost); disaster
(well control incident).

Each is drawn from the `operations` RNG stream and audited with its draw
([09](../design/09_DIAGNOSTICS.md) §4.2), so the player can verify fairness.

### 2.5 The abandonment operation cannot be skipped

Every well and facility carries an abandonment obligation from creation. It is
accrued financially from first production (R13.8) and discharged only by
completing the operation.

### 2.5b Operations are tech-gated at scheduling

Operation templates carry `requiresTech` like equipment does
([07](../design/07_TECHNOLOGY.md) §2c). Validation happens at **scheduling**,
naming the missing capability; execution never re-checks, so a mid-operation
change strands nothing. Before R17 exists, composition supplies
`AllCapabilities` — which is not scaffolding but the shipped sandbox
all-tech modifier ([18](../design/18_GAME_MODES.md) §5), so pre-R17 test
suites run a real configuration.

### 2.6 Environment couplings

Operations is where the setting bites hardest
([13_ENVIRONMENT](../design/13_ENVIRONMENT.md) sections 3.2 and 3.6):

| Factor | Effect |
|---|---|
| Water depth / terrain | **Determines the rig class** — land rig, jack-up, semi-sub, drillship; day rates differ by an order of magnitude |
| Access | Mobilisation cost and duration; a remote site may need a road built first |
| Climate | **Seasonal windows** — an arctic operation is schedulable only inside the ice-road window |
| Weather | Standby: interrupted time accrues cost without progress (section 2.2) |
| Remoteness | Crew rotation, spares lead time, and **emergency response time** |

`env.accessWindowClosing` is a `D`-severity event carrying lead time: the player
is warned twice before a window shuts, because missing one costs a year while the
licence clock keeps running.

### 2.7 Events this phase raises — the largest set in the engine

`operation.scheduled` · `.started` · `.interrupted` · `.completed` · `.failed`,
plus `equipment.*` and the drilling-specific `well.*` events R6 defines.

Operation completion that brings a well or facility online **creates a segment
boundary**; scheduling and cost accrual do not
([21](../design/21_INTEGRATION.md) section 5).

### 2.8 The fatigue coupling

Crew size and rotation (R12.7) feed the human-error threat rate in
[R23](R23_HSE.md). **Lean crewing is cheaper and measurably less safe**, and that
trade must be visible in the read model *before* an incident, not after.

---

## 3. Deliverables

`OGSim.Operations`: `IOperation`, scheduler, resource reservation and contention,
`IRig`, `IPersonnel`, drilling operations (depth progress, hazards, graded
outcomes), completion and workover operations, construction operations,
abandonment operations.
Content: rig catalogue, operation templates, crew disciplines.

---

## 4. Verification

| # | Test | Passes when |
|---|---|---|
| R12-V1 | Duration | Operations complete after the correct elapsed ticks |
| R12-V2 | Cost accrual | Cost is spread over the operation, matching the declared profile |
| R12-V3 | Resource contention | A second operation needing a committed rig is rejected with a reason |
| R12-V4 | Prerequisites | An operation with unmet prerequisites is rejected at scheduling |
| R12-V5 | Graded outcomes | All six outcome grades occur at their declared rates over a large sample |
| R12-V6 | Determinism | The same seed produces identical outcome sequences |
| R12-V7 | Audit | Every outcome is audited with its RNG stream, draw and threshold |
| R12-V8 | Cancellation | A cancelled operation releases resources and stops accruing; sunk cost is retained |
| R12-V9 | Crew effect | Higher skill reduces duration and risk by the declared amount |
| R12-V10 | Abandonment | Every well and facility carries an obligation from creation; only the operation discharges it |
| R12-V11 | Tech gating at scheduling | An operation whose `requiresTech` is unmet is rejected at scheduling naming it; the same command validates under `AllCapabilities`; a tech change mid-operation changes nothing |

---

## 5. Out of scope

Financing the operations (R13). Technology gating what operations are available
(R17). Detailed drilling mechanics — deliberately, per
[02](../design/02_DOMAIN_MODEL.md) §9.

---

## 6. Risks

| Risk | Mitigation |
|---|---|
| One `IOperation` proves too generic for drilling's detail | Drilling adds depth progress as operation-specific state, without a second contract; verify during design of R12.4 |
| Graded outcomes feel arbitrary | Every outcome is audited with its draw; the player can verify fairness (R12-V7) |
| Scheduling becomes a solver | Reservation is first-come with explicit rejection; there is no optimiser, and none is wanted |
| Long operations make the monthly tick feel slow | A design/UX matter for the host, not the engine; the engine exposes progress and expected completion |
