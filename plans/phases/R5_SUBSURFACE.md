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
pressure. Reservoir and field are groupings for economics and naming, and arrive
with the phase that gives them behaviour (§3).

*Rationale:* [research/PPDM_ALIGNMENT](../research/PPDM_ALIGNMENT.md) §4. It also
makes **compartmentalisation a discovery** (open decision M1): the player believes
in a reservoir, the engine simulates compartments, and the gap between them is
found from pressure data. That is one of the best surprises the subsurface can
deliver.

### 2.2 The compartment is NOT a flow element — the completion is

The compartment is truth. The **completion** is the network's source
`IFlowElement`: it asks the compartment what it can deliver at the current
drawdown and produces the stream. Backpressure still reaches the reservoir —
through the completion, which is where inflow performance belongs anyway.

*Rationale:* this is what makes the "one engine" claim hold at the upstream end
without a truth leak. [23](../design/23_FUNCTION_MATRIX.md) pins
`ICompletion : IFlowElement`, never the reverse, and
[SDD-003](../sdd/SDD-003_SUBSURFACE_AND_WELLS.md) §1 says completions are the
network's source elements.

> **R5.0 correction (finding 102).** This section previously said the compartment
> *is* an `IFlowElement`, with one outlet port per perforation. Three things say
> otherwise: 23's no-cycles rule, SDD-003 §1, and [02](../design/02_DOMAIN_MODEL.md)
> §2.1, which gives the compartment neither ports nor a transform. It is also
> unimplementable as stated alongside §2.5: an `IFlowElement` puts its outlet
> pressure into a `MaterialStream` that the solver hands to every downstream
> element, so a compartment element would publish reservoir pressure — the
> single most valuable piece of truth in the game — to anything holding a
> stream, in the same phase that is supposed to establish the boundary against
> exactly that. Same class as [22](../design/22_DESIGN_COHERENCE.md) finding 82(c):
> a phase document written before the hardening and never back-annotated.

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

`OGSim.Subsurface`: `IReservoirCompartment` and its truth types (`InPlace`,
`ContactSet`, `RockTruth`, `CompartmentLink`) — all `internal` — plus the six
`IDriveMechanism` implementations, the Fetkovich `IAquiferModel`, and the
black-oil `IFluidPropertyModel`. Content: `rock-type`, `fluid-system`,
`drive-mechanism`.

> **R5.0 correction (finding 103): five of the names this list promised are
> declared in no SDD, and a sixth was declared under a different one.**
>
> | Promised | Resolution |
> |---|---|
> | `IFluidSystem` | Two concepts in one name. The PVT half is `IFluidPropertyModel` (SDD-003 §4, implemented in R2.7); the "which materials in what proportion" half is the compartment's `InPlace`, seeded from the `fluid-system` **content kind**. Neither is a new interface |
> | `IMaterialBalanceModel` | Material balance is `IDriveMechanism.SolveEndPressure`; SDD-003 §3.1 is its algorithm. [03](../design/03_ARCHITECTURE.md) §3.2's eleven replaceable slots list `IDriveMechanism` and not this; [05](../design/05_SIMULATION_MODELS.md)'s mention is a row in a table of model *families*, not a contract |
> | `IAquifer` | Declared as `IAquiferModel`. One concept, one name (N1) — the declared name wins |
> | `IReservoir`, `IField`, `IStratigraphicUnit` | Groupings for economics, naming and correlation. R5 gives them no behaviour, and L3 forbids declaring a member that has none, so they are **deferred to the phase that gives them one** rather than declared empty here. [01](../design/01_CONCEPT_MATRIX.md) lists them as concept→contract candidates, which is not a declaration |

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
