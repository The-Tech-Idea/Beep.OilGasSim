# Phase R11 — Transport and Export

**Arc II** · Status ⬜ · Depends on: R8, R9 · Enables: R13

**Completes the chain: reservoir → export berth.** After R11 the physical
simulation is end to end, and the acceptance scenario SC1 becomes runnable in
outline.

---

## 0. Purpose

Move material from the field to the point where ownership changes and money
arrives — and make export a rhythm rather than a continuous drain, so tank
capacity, lifting frequency and production rate must be balanced.

---

## 1. Goals

| # | Goal | Acceptance |
|---|---|---|
| G1 | Pipeline capacity **emerges from hydraulics** | The same pipe has different capacity for different fluids; capacity falls as inlet pressure declines (E4, E5) |
| G2 | Debottlenecking has multiple real answers | Looping, pumping, and viscosity reduction each work, with different economics |
| G3 | Export is a rhythm | Cargo scheduling, berth occupancy and tank ullage interact; a late tanker shuts in the field (SC8) |
| G4 | Revenue happens at custody transfer | Only metered, on-spec material at a custody point generates revenue |
| G5 | Flow assurance is a real risk | Hydrate, wax, corrosion and erosion conditions raise hazard rates and have purchasable mitigations |

---

## 2. Design decisions

### 2.1 Capacity is never configured

An `IPipeline` declares geometry and a rating. Its throughput is whatever the
hydraulics permit for the fluid actually flowing, at the actual inlet pressure.

*Rationale:* G1 is the phase's central claim. A configured capacity would make
pipelines inert numbers; emergent capacity makes "why is my line full?" a
question with a physical answer and several distinct remedies.

### 2.2 Gas lines use the pressure-squared form

Deliberately called out because the consequence is severe and non-obvious: **a
gas line's capacity collapses as inlet pressure declines**, so an export line
sized at first gas is inadequate years later even at lower rates. The remedy is
compression, and it must be anticipated.

### 2.3 Linefill is inventory

The material in a pipeline is real, owned, and takes time to traverse. It appears
in the conservation check and in the balance sheet.

*Rationale:* it is cheap, it is correct, and it produces the authentic delay
between a production change and its arrival at the terminal.

### 2.4 A berth is a scheduled resource; a cargo is an operation

`IBerth` has occupancy; `ICargo` is an `IOperation` (R12's contract, used here
ahead of that phase for the cargo case only) with a nomination, a loading window,
a loading rate and a laytime. Overrunning incurs demurrage.

**Decision: cargo scheduling is player-controlled, with contract-driven
obligations.** A term contract obliges liftings on a schedule; spot cargoes are
opportunistic. The tension between production rate, tank capacity and lifting
schedule is the export game.

### 2.5 Custody transfer is the revenue event

`ICustodyTransferPoint` meters, applies the specification gate, records the
transfer with its measurement uncertainty, and is the **only** place revenue
originates ([research/PPDM_ALIGNMENT](../research/PPDM_ALIGNMENT.md) §8).

*Rationale:* one rule, and several gameplay consequences follow free — inventory
is capital rather than revenue, off-spec material is worthless until treated, and
"where do I sell this?" becomes a real question.

### 2.6 Flow assurance is risk, not physics

Conditions are evaluated (temperature, water presence, pressure, velocity,
composition) and raise hazard rates for blockage, deposition or failure — each
with a purchasable mitigation. Full transient multiphase modelling is out of
scope per [02](../design/02_DOMAIN_MODEL.md) §9.

### 2.7 Environment couplings

| Setting factor | Effect |
|---|---|
| Terrain | Pipeline cost per km varies severalfold; river and mountain crossings dominate |
| Climate | Insulation, burial depth, **ice scour protection** |
| Ambient temperature | Drives hydrate and wax risk — a 4 °C seabed is the worst case |
| Port water depth | **Limits tanker size**, and therefore parcel economics |
| Weather | **Berth closure** — a storm shuts the port and tanks fill |

`env.storm` closing a berth is the trigger for SC8, and it creates a **segment
boundary** because berth availability is a network constraint.

### 2.8 Events this phase raises

`custody.transferred` · `tank.full` at the terminal / `flow.specRejected` at the
custody point / cargo nomination and loading events.

---

## 3. Deliverables

`OGSim.Transport`: `IPipeline`, `IHydraulicModel` (Darcy-Weisbach,
Panhandle/Weymouth), pump and compressor stations, linefill, flow-assurance risk
evaluation, `ITruckingRoute`, `IRailLink`, terminal, tank farm, `IBerth`,
`ICargo`, `ICustodyTransferPoint`, `ITransportContract`.
Content: `pipe-spec` catalogue, `vessel` catalogue, `contract-template`.

---

## 4. Verification

| # | Test | Passes when |
|---|---|---|
| R11-V1 | Liquid hydraulics (MX4) | Pressure drop matches Darcy-Weisbach for known cases |
| R11-V2 | Diameter scaling (MX5) | Doubling diameter raises capacity by the analytically predicted factor |
| R11-V3 | Gas hydraulics (CAL10) | Capacity follows the pressure-squared form; declining inlet pressure erodes capacity |
| R11-V4 | Viscosity sensitivity | Heavier crude reduces capacity in the same pipe |
| R11-V5 | Looping | Adding a parallel line raises capacity by the predicted amount |
| R11-V6 | Pump station | Boosting pressure restores capacity; power matches the model |
| R11-V7 | Linefill | Material in transit appears in inventory and in the conservation check |
| R11-V8 | Berth occupancy | Two cargoes cannot occupy one berth; queuing works |
| R11-V9 | Late tanker (SC8) | Tanks fill, wells shut in, production is deferred not stored, and recovers on lifting |
| R11-V10 | Custody transfer | Revenue originates only at a custody point, only for on-spec material |
| R11-V11 | Metering uncertainty | Measured volume differs from true volume within the declared tolerance, and the difference is audited |
| R11-V12 | Flow assurance | Hydrate-forming conditions raise the hazard rate; insulation or inhibitor reduces it |
| R11-V13 | Conservation | End-to-end: reservoir withdrawal = custody-transferred + inventory + injected + flared + spilled + tolerance |

**R11-V13 is the whole-chain conservation test** and it is the moment the "one
engine" claim is fully verified.

---

## 5. Out of scope

Pricing and revenue accounting (R13) — R11 records custody transfers; R13 values
them. LNG (R17 technology). Marine logistics beyond berth occupancy. Looped
network topology (open decision FD4) unless R11 proves it necessary.

---

## 6. Risks

| Risk | Mitigation |
|---|---|
| Looped export lines are needed and the solver is tree-only | R11-V5 tests looping as a *parallel segment* modelled as one element with combined hydraulics — which covers the common case without general graph support |
| Cargo scheduling becomes tedious micromanagement | Contract-driven default schedules; the player intervenes only when they want to |
| Hydraulic correlations are outside their validity range for game-scale pipes | Validity ranges are checked and violations are model faults (R2-V10 policy), never silent extrapolation |
| Linefill complicates conservation | It is inventory like any other; R11-V7 asserts it explicitly |
