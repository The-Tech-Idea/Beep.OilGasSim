# Phase R6 — Wells

**Arc II · The physical chain** · Status ⬜ · Depends on: R5 · Enables: R7, R10, R12

---

## 0. Purpose

The connection between the reservoir and everything else — and the phase that
delivers the game's best physical drama: **the operating point, and the well that
dies when it stops existing.**

---

## 1. Goals

| # | Goal | Acceptance |
|---|---|---|
| G1 | The PPDM hierarchy is real | Well → Wellbore → Completion → Perforation, each with the responsibilities in [02](../design/02_DOMAIN_MODEL.md) §3.1 |
| G2 | Rate is the operating point | Rate is where IPR meets VLP — never a configured number (FV3) |
| G3 | A well can die | When the curves stop intersecting the well ceases to flow, with no scripted rule |
| G4 | Skin matters correctly | A skin of +10 costs the analytically predicted productivity (MX2) |
| G5 | Sidetracks and recompletions need no special case | Both are new entities in the existing hierarchy |
| G6 | Commingled production allocates exactly | Multi-perforation production allocates back to compartments summing to what was withdrawn (FV10) |

---

## 2. Design decisions

### 2.1 The completion owns the physics

`ICompletion` computes inflow and outflow and finds the operating point. `IWell`
is identity and status; `IWellbore` is geometry; `IPerforation` is the reservoir
connection.

*Rationale:* it puts the physics at the level that actually has all the inputs —
reservoir connection below, tubing and lift above. A well-level physics model
cannot express two completions on two wellbores of one well.

### 2.2 The operating point is solved, not iterated externally

IPR and VLP are both functions of bottomhole flowing pressure. The completion
finds their intersection by bracketed root-finding. **No intersection means the
well does not flow** — reported as a distinct, explicable outcome, not as a rate
of zero.

*Rationale for the distinction:* "this well produces 0" and "this well cannot
flow at any rate" are different facts with different remedies, and the player
needs to be told which one they have.

### 2.3 Wellhead pressure is set by the network, not by the well

The completion's outflow calculation takes wellhead pressure as a **boundary
condition from the flow solve**. This is what makes backpressure propagate
([04](../design/04_MATERIAL_AND_FLOW.md) §6) — a full tank raises manifold
pressure, which raises wellhead pressure, which raises `Pwf`, which reduces
drawdown, which reduces withdrawal.

The completion therefore participates in the solver's iteration rather than
computing a rate once. **This is the phase where the "one engine" claim is
genuinely tested**, and R4's design must accommodate it without modification.

### 2.4 Perforations carry their own skin, contribution — and standoff

A perforation also records its **standoff to the nearest fluid contact**,
consumed by the coning model ([05](../design/05_SIMULATION_MODELS.md) §3.3b).
That one number is what makes "which zones to perforate" (DDV6) and "how hard
to pull this well" (DPR2) physical decisions rather than labels.

Skin is per perforation, not per well. A well with a damaged lower zone and a
clean upper zone is expressible, and isolating the damaged zone is a real
intervention with a computable benefit.

### 2.5 Well status is a state machine with commanded transitions

Every transition in [02](../design/02_DOMAIN_MODEL.md) §3.4 is a command: audited,
costed, and with a duration. **No transition bypasses the abandonment
obligation.**

### 2.6 Events this phase raises

`well.spudded` · `well.shows` · `well.result` · `well.tested` · `well.online` /
`well.diedNaturally` · `well.shutIn` · `well.economicLimit`.

Two carry unusual weight. **`well.result` must carry the failed
petroleum-system element when the hole is dry** — without it a dry hole teaches
nothing. And `well.diedNaturally` must be distinguishable from a rate of zero:
"cannot flow at any rate" and "produced nothing this tick" have different
remedies.

**Segment boundaries:** a well coming online or shutting in changes network
topology, so both cut the tick ([21](../design/21_INTEGRATION.md) section 5).

---

## 3. Deliverables

`OGSim.Wells`: `IWell`, `IWellbore`, `IWellPath`, `ICompletion`, `IPerforation`,
`IWellComponent`, `IChoke`, `IInflowModel` (Darcy, Vogel, composite, gas
back-pressure), `IOutflowModel`, the operating-point solver, allocation.
Content: `well-component` catalogue, tubing catalogue.

---

## 4. Verification

| # | Test | Passes when |
|---|---|---|
| R6-V1 | Darcy inflow (MX1) | Matches the analytic rate across a parameter sweep |
| R6-V2 | Vogel below `Pb` | Matches the published curve |
| R6-V3 | Composite IPR | Continuous and correct across the bubble point |
| R6-V4 | Skin (MX2) | +10 skin costs the analytically predicted fraction of productivity |
| R6-V5 | Operating point (FV3) | Matches an independently computed intersection across a sweep |
| R6-V6 | The well that dies | As reservoir pressure declines, the well reaches non-intersection and reports it distinctly from zero rate |
| R6-V7 | Tubing size trade | Too-narrow tubing is friction-limited; too-wide loads up — both reproduced |
| R6-V8 | Choke | Critical flow makes rate independent of downstream pressure; sub-critical does not |
| R6-V9 | Backpressure | Raising wellhead pressure reduces rate by the amount the IPR/VLP intersection predicts |
| R6-V10 | Allocation (FV10) | Multi-perforation production allocates back to compartments, summing exactly |
| R6-V11 | Sidetrack | A second wellbore on one well shares identity and licence, and produces independently |
| R6-V12 | Recompletion | A new completion on an existing wellbore works with no special-case code |
| R6-V13 | Horizontal contact | A horizontal well's productivity gain follows from contact length, not a multiplier |
| R6-V14 | Commingling backpressure | A high-pressure well tied into a shared line reduces or shuts in a weaker well on the same line |

**R6-V14 is the phase's most valuable test.** It is an emergent consequence the
player will meet, it is not written anywhere as a rule, and if it does not appear
the network coupling is not real.

---

## 5. Out of scope

Artificial lift (R7) — R6 covers natural flow only, and R6-V6 establishes exactly
the situation R7 exists to solve. Drilling as an operation (R12). Component
degradation (R18).

---

## 6. Risks

| Risk | Mitigation |
|---|---|
| The operating-point root-find fails to bracket | Bracket from physical bounds (0 to `Pr`); no solution is a valid, reported outcome — not a fault |
| Completion participation breaks R4's solver assumptions | Prototype this coupling during R4 (listed as an R4 risk mitigation); if it does not fit, `IFlowElement` is wrong and gets fixed then |
| Allocation drifts with rounding | Allocate by mass fraction and assert the sum every tick (R6-V10) |
| The four-level hierarchy feels like overhead for a simple vertical well | Accept it. R6-V11 and R6-V12 are the payoff, and they are free |
