# Phase R10 — Water Handling and Injection

**Arc II** · Status ⬜ · Depends on: R8, R5 · Enables: R13, R18

---

## 0. Purpose

Water is the villain of the late game and the reason most fields actually die.
R10 makes that true in the engine: **water costs money at every step, displaces
oil in every vessel it passes through, and eventually costs more to handle than
the oil it arrives with.**

It also delivers the counterweight: water injection as pressure support, the one
lever that pushes back against decline.

---

## 1. Goals

| # | Goal | Acceptance |
|---|---|---|
| G1 | Water cut rises realistically | An S-curve after breakthrough (SC4) |
| G2 | Water displaces oil in every constraint | Lifting, separation, treating and disposal all bind on gross liquid, not oil |
| G3 | Water is a cost at every step | Lift, separate, treat, dispose — each with its own OPEX line *(priced in R13)* |
| G4 | The economic limit is reached and detected | The engine identifies when water-handling cost exceeds oil value and flags it |
| G5 | Injection supports pressure | Injected water reaches the compartment and slows decline |
| G6 | Waterflood raises recovery | Recovery factor rises by 10–25 points versus unsupported depletion |

---

## 2. Design decisions

### 2.1 Water is a material, and it flows through the same network

No special water plumbing. It is `IMaterial` in `IStream` through `IFlowElement`s,
exactly like oil and gas. **The whole reason water hurts is that it occupies
capacity in shared equipment** — and that only emerges if it shares the network.

### 2.2 Injectivity is the constraint on disposal

A disposal well accepts water at a rate governed by an inflow relationship in
reverse — pressure, permeability, skin. **Injectivity declines** as the formation
plugs with solids and fines.

*Rationale:* it makes water disposal an ongoing operational problem rather than a
one-time build, and it produces the authentic situation where a field is
throttled by disposal capacity, not by anything upstream.

### 2.3 Waterflood is an `IDriveMechanism`, added not edited

Consistent with R5.3. A waterflood compartment has a different pressure response
and a different recovery factor because it has a different drive mechanism, not
because a flag was set.

### 2.4 Zonal water shutoff is a perforation operation

Isolating a watered-out perforation (R6.4) is the cheap answer to rising water
cut, and it should usually be tried before capital solutions.

*Rationale:* it gives the player a genuine escalation ladder — shut off the zone,
then upsize water handling, then convert to injection, then abandon — with
increasing cost at each rung. Escalation ladders are good design.

### 2.5 The economic limit is computed, not thresholded

Continuously: incremental revenue versus incremental cost per well and per field.
Crossing it raises an advisory, **not an automatic shut-in** — the decision stays
with the player, because keeping a marginal well alive is sometimes correct
(shared infrastructure costs, contract commitments, an imminent workover).

### 2.6 Environment and events

Disposal is sensitivity-constrained: discharge and injection standards come from
the jurisdiction's HSE regime, and the consequence of a produced-water release is
multiplied by the location's sensitivity designation
([14_HSE](../design/14_HSE.md) section 5.3). High-volume disposal also drives
induced seismicity risk (R23.9).

`reservoir.waterBreakthrough` (raised in R5) is the **water spiral's entry
event**; this phase supplies its responses — zonal shutoff, more handling
capacity, conversion to injection. Rule CI4 requires at least two, and there are
three.

---

## 3. Deliverables

`OGSim.Facilities` extension: water treatment units (skim, hydrocyclone, filter),
injection pumps. `OGSim.Wells` extension: injector well class, injectivity model.
`OGSim.Subsurface` extension: waterflood drive mechanism, injection coupling.
Economic-limit detection. Content: water unit catalogue, disposal specs.

---

## 4. Verification

| # | Test | Passes when |
|---|---|---|
| R10-V1 | Water cut S-curve (SC4) | Breakthrough then an S-curve rise, matching the reference shape |
| R10-V2 | Gross liquid constraint | Separation, treating and lift all bind on gross liquid; oil rate falls as water cut rises even at constant gross |
| R10-V3 | Disposal limit | Insufficient disposal capacity throttles production and is attributed to water handling |
| R10-V4 | Injectivity decline | Injection rate falls over time; remediation restores it |
| R10-V5 | Pressure support | Injected water reaches the compartment; decline slows measurably |
| R10-V6 | Waterflood recovery | Recovery rises 10–25 points versus the unsupported case |
| R10-V7 | Zonal shutoff | Isolating a watered perforation reduces water cut and raises oil rate |
| R10-V8 | Economic limit | Detected at the analytically correct point; advisory raised, no automatic action |
| R10-V9 | Conservation | Produced water = treated + disposed + discharged + Δ inventory, exactly |
| R10-V10 | Lifting cost (MB6) | Cost per barrel of oil at 90% water cut is roughly an order of magnitude above 10% *(assertion completed in R13)* |

---

## 5. Out of scope

Water chemistry, scaling and souring (R18 hazards). Enhanced oil recovery beyond
waterflood (R17). Water pricing (R13). Environmental discharge regulation (R16).

---

## 6. Risks

| Risk | Mitigation |
|---|---|
| Water cut curves are tuned rather than emergent | It must emerge from relative permeability and the drive mechanism; R10-V1 asserts shape, not a scripted curve |
| Water makes the late game purely miserable | The escalation ladder (§2.4) gives the player agency at every stage; the misery is meant to be *manageable* |
| Injection coupling creates a solver feedback loop | Injection is committed in tick stage 6 and affects the *next* tick's pressure — no intra-tick circularity |
