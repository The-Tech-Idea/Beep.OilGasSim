# Phase R5 — Subsurface

**Arc II · The physical chain** · Status ⬜ · Depends on: R1–R4 · Enables: R6, R10, R14, R15

---

## 0. Purpose

The reservoir: where the material comes from, and the one state variable —
**pressure** — that governs everything downstream. After R5, the engine can
deplete a reservoir and get the pressure history right, with no wells attached.

---

## 1. Goals

| # | Goal | Acceptance |
|---|---|---|
| G1 | Depletion is physical | Pressure falls by material balance, not by a decline formula |
| G2 | Recovery factor **emerges** | RF is never stored; it is the outcome of drive mechanism × development, and lands in the bands MB1/MB2 |
| G3 | The bubble-point transition is real | Crossing `Pb` changes `Rs`, `Bo`, `μo` and the phase split, with no scripted event |
| G4 | Gas reservoirs are deducible | `p/Z` versus cumulative production is **exactly linear** for a volumetric reservoir (MX3) |
| G5 | Compartments are independent | Two compartments deplete separately unless connected by declared transmissibility |
| G6 | Aquifers support pressure and bring water | Influx responds to pressure drop and elapsed time |

---

## 2. Design decisions

### 2.1 The compartment is the simulated unit

Not the reservoir, not the field. A hydraulically connected volume with one
pressure. `IReservoir` and `IField` are groupings for economics and naming.

*Rationale:* [research/PPDM_ALIGNMENT](../research/PPDM_ALIGNMENT.md) §4. It also
makes **compartmentalisation a discovery** (open decision M1): the player believes
in a reservoir, the engine simulates compartments, and the gap between them is
found from pressure data. That is one of the best surprises the subsurface can
deliver.

### 2.2 The compartment is an `IFlowElement`

It has one outlet port per perforation drawing on it. Its constraint is the
inflow the reservoir can deliver at the current drawdown; its transform produces a
stream at reservoir conditions.

*Rationale:* this is what makes the "one engine" claim hold at the upstream end.
The reservoir is not a special source feeding the network — it is the first
element of it, and backpressure reaches it like any other element.

### 2.3 Drive mechanism as a plugin, chosen at world-gen

Six shipped implementations: solution gas, gas cap expansion, water drive,
compaction, gravity drainage, combination. Waterflood and gas injection are added
in R10 and R9 as further implementations — **not as modifications to the existing
ones**.

*Rationale:* it makes recovery factor emergent (G2), makes identifying the drive
worthwhile gameplay, and makes EOR an addition rather than an edit.

### 2.4 Material balance solves for pressure implicitly

Withdrawal is known after the flow solve; the new pressure is the one at which
expansion plus influx plus injection equals withdrawal. Solved iteratively within
the commit step (tick stage 6).

**Non-convergence is a model fault**, not a clamped value.

### 2.5 Truth and belief are separated from the start

The compartment holds the **truth**. Beliefs about it live in R14. R5 builds the
truth side only, and the `internal` visibility boundary is established here so
R14 does not have to retrofit it.

### 2.6 Events this phase raises

`reservoir.bubblePoint` · `reservoir.waterBreakthrough` /
`reservoir.compartmentInferred`, all at **tick stage 6** (material balance), so
they reflect the production that just happened
([21_INTEGRATION](../design/21_INTEGRATION.md) section 4).

The first two are **loop-entry events** for the gas-handling and water spirals.
Severity `W`, both auto-pause, and both must fire while the player still has at
least two responses available (rule IR3).

---

## 3. Deliverables

`OGSim.Subsurface`: `IReservoirCompartment`, `IReservoir`, `IField`,
`IFluidSystem`, `IDriveMechanism` (×6), `IAquifer`, `IMaterialBalanceModel`,
`IStratigraphicUnit`. Content: `rock-type`, `fluid-system`, `drive-mechanism`.

---

## 4. Verification

| # | Test | Passes when |
|---|---|---|
| R5-V1 | Volumetrics | STOOIP and GIIP match the analytic formulae |
| R5-V2 | `p/Z` linearity (MX3) | **Exactly** linear for a volumetric gas reservoir; the x-intercept equals GIIP |
| R5-V3 | Water drive | Pressure is maintained; recovery lands in band MB1 (35–75%) |
| R5-V4 | Solution gas drive | Pressure falls steeply; recovery lands in band MB2 (5–30%) |
| R5-V5 | Bubble-point crossing | `Rs` falls, `Bo` peaks then falls, `μo` rises, GOR rises — all in the correct direction and rough magnitude |
| R5-V6 | Compartment independence | Withdrawal from A does not change B's pressure when transmissibility is zero |
| R5-V7 | Connected compartments | Non-zero transmissibility equalises pressure at the expected rate |
| R5-V8 | Aquifer influx | Influx responds to pressure drop; water arrives at the connected perforations |
| R5-V9 | In-place conservation | Cumulative production (at reservoir conditions) + remaining in place + injected = original in place, every tick |
| R5-V10 | Backpressure reaches the reservoir | A downstream restriction reduces withdrawal and slows depletion |
| R5-V11 | Integration error bound | One-step monthly depletion stays within the calibration bands of a sub-stepped reference on the steepest realistic decline; exceeding the per-tick withdrawal validity limit raises a model fault |

---

## 5. Out of scope

Wells (R6) — R5 is tested by attaching synthetic sinks directly to compartment
outlets. Injection (R10). World generation (R15) — R5 compartments are built by
tests and content. Beliefs (R14).

---

## 6. Risks

| Risk | Mitigation |
|---|---|
| Material balance iteration is unstable near the bubble point | Test the transition specifically (R5-V5); treat non-convergence as a fault, never a clamp |
| Recovery factors land outside industry bands | Band tests MB1/MB2 fail loudly; tuning happens in content, not code |
| Compartment connectivity makes the solve expensive | Compartment counts are small (tens per field); benchmark alongside R4-V18 |
| Truth leaks into consumers before R14 exists | Establish the `internal` boundary and its architecture test **in this phase**, not in R14 |
