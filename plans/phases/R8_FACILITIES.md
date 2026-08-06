# Phase R8 — Facilities and Separation

**Arc II** · Status ⬜ · Depends on: R6 · Enables: R9, R10, R11

---

## 0. Purpose

Surface processing: take the multiphase mixture arriving from the wells and split
it, clean it, and hold it. This is where **specifications** enter the game, and
specifications are what make the whole processing chain necessary rather than
decorative.

---

## 1. Goals

| # | Goal | Acceptance |
|---|---|---|
| G1 | Facilities are containers; units are physics | There is no monolithic process class; a "gas plant" is a facility whose units happen to be gas units |
| G2 | Separation is capacity-limited on two axes | Gas capacity and liquid capacity bind independently, and either can be the bottleneck |
| G3 | A full tank shuts in wells | Backpressure propagates from tank to reservoir within the same tick (FV5) |
| G4 | Off-spec material does not pass | A spec gate rejects, and the rejected mass is fully accounted for |
| G5 | Power is a real dependency | Insufficient power takes units offline, and the production loss is attributed to power |
| G6 | Multi-stage separation recovers more liquid | Staged pressure reduction yields more stock-tank oil from identical reservoir fluid |

---

## 2. Design decisions

### 2.1 `IFacility` has no process behaviour

It is a site, an owner, a cost centre, a power balance and a container. All
transformation is in `IFacilityUnit`s.

*Rationale:* [research/PPDM_ALIGNMENT](../research/PPDM_ALIGNMENT.md) §3. This
single rule prevents the failure where "refinery" becomes a class with a hundred
fields, and it makes the player's build a real composition — **the plant they get
is exactly the units they paid for.**

### 2.2 Facility templates are convenience, not a separate concept

A `facility-template` is a named list of units, expanded at build time into an
ordinary facility. **The engine has no notion of a templated facility after
construction** — so a template can always be modified, extended or partially
built, and templates never diverge from hand-built facilities.

### 2.3 A tank is an `IFlowElement` with state

Inventory, capacity, ullage. Its constraint is ullage: when full, it accepts
nothing, which propagates backpressure through the network to the reservoir.

**This is the single most important coupling in the export chain** and it is why
`Buffer` was one of R4's five synthetic elements — the shape was proven before
the real thing was written.

### 2.4 Specifications are declared content, evaluated at gates

An `ISpecification` is a set of limits on stream properties. It is attached to a
custody transfer point or to a unit inlet. A stream failing a spec **does not
pass**, and the failing parameter and margin are audited.

*Rationale:* this is the mechanism that makes the player build a dehydrator. Not
a tech-tree prompt — a rejection with a reason.

### 2.5 Power balance is per facility

Units declare power demand; sources declare supply. A shortfall takes units
offline by a declared priority order at tick stage 4, before the flow solve at
stage 5 ([03](../design/03_ARCHITECTURE.md) §6).

**The balance uses declared duty, not solved rates.** An ESP's or compressor's
power demand nominally depends on this tick's rate — which stage 5 has not yet
solved. Stage 4 therefore balances **declared duty** (nameplate for equipment
scheduled to run), per the lag rule in [03](../design/03_ARCHITECTURE.md) §6.1.
Conservative, deterministic, and it means a power shortfall is decided before
the solve rather than discovered inside it.

**Decision: priority order is declared per facility, and defaults to
safety-critical → export-critical → processing → discretionary.** An
undeclared-priority unit is a content validation error, not an arbitrary choice.

### 2.6 Separation efficiency is never 100%

Carry-over and carry-under are modelled. Undersized vessels separate worse at
high rate through the residence-time term.

*Rationale:* it makes vessel sizing a real decision rather than a threshold, and
it produces the authentic late-life problem where a separator sized for early oil
rates handles late-life liquid volumes poorly.

### 2.7 Environment couplings

Facility design is where the setting becomes capital
([13_ENVIRONMENT](../design/13_ENVIRONMENT.md) section 3.3):

| Setting factor | Effect on this phase |
|---|---|
| Cold climate | Winterisation, freeze protection, heat tracing, enclosed modules |
| Hot climate | **Cooling oversized; tank vapour losses rise** |
| Ground conditions | Foundations, piling, permafrost provisions |
| Access | Module size limited by the transport route |
| Sensitivity | Footprint, noise and zero-discharge constraints |
| Water depth | Onshore pad versus platform — an order-of-magnitude cost difference |

These arrive through the shared effect-application path from
[R22](R22_ENVIRONMENT.md) section 2.1; **no facility unit reads an environment
profile directly.**

### 2.8 Events this phase raises

`tank.high` · `tank.full` · `flow.specRejected` · `power.shortfall`.
`tank.full` and `power.shortfall` change constraints, so both create **segment
boundaries**.

---

## 3. Deliverables

`OGSim.Facilities`: `IFacility`, `IFacilityUnit`, `ISeparator`,
`ISeparationModel` (single and multi-stage), `ITreater`, `IStabiliser`,
`IDesalter`, `ITank`, `ISpecification`, `IPowerSource`, power balance,
`IFlowNode` (manifold), flowlines.
Content: `facility-unit`, `facility-template`, `specification`, `pipe-spec`.

---

## 4. Verification

| # | Test | Passes when |
|---|---|---|
| R8-V1 | Phase split | A known fluid splits into the expected oil/gas/water fractions at the separator's (P,T) |
| R8-V2 | Dual capacity | Gas-limited and liquid-limited cases each bind correctly and are attributed correctly |
| R8-V3 | Carry-over | Undersized vessels at high rate show degraded separation |
| R8-V4 | Multi-stage recovery | Staged separation yields more stock-tank liquid than single-stage from identical feed |
| R8-V5 | Tank backpressure (FV5) | Filling a tank reduces reservoir withdrawal within the same tick |
| R8-V6 | Spec rejection (FV6) | Off-spec material does not pass; rejected mass is accounted for exactly |
| R8-V7 | Power shortfall | Units go offline in the declared priority order; the loss is attributed to power |
| R8-V8 | Treating | Water cut is reduced to spec; the removed water appears in the water stream |
| R8-V9 | Composition | A facility built from units behaves identically to one built from a template |
| R8-V10 | Conservation | Every unit conserves mass; nothing is created or destroyed in treating |

---

## 5. Out of scope

Gas treating (R9). Water treating and disposal (R10). Pipelines beyond flowlines,
terminals and berths (R11). Construction as an operation (R12) — R8 builds
facilities directly in tests. Degradation (R18).

---

## 6. Risks

| Risk | Mitigation |
|---|---|
| Unit taxonomy grows unbounded | The taxonomy is content; adding a unit kind requires an `IFacilityUnit` implementation only when its *transform* is genuinely new |
| Power priority ordering is arbitrary | Declared in content and validated; an undeclared priority is a load error |
| Separator models become the engine's complexity sink | Fidelity levels are plugins; the standard model is efficiency-based, and the flash-calculation version is optional |
| Tank backpressure destabilises the solver | R4's `Buffer` proved the shape; R8-V5 tests it with the real element |
