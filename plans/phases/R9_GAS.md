# Phase R9 — Gas Processing

**Arc II** · Status ⬜ · Depends on: R8 · Enables: R11, R16

---

## 0. Purpose

The longest processing chain in the game, and the phase that creates the
associated-gas dilemma: **an oil field makes gas whether you want it or not, and
you must sell it, re-inject it, or flare it.** Each has a real cost, and flaring
limits can cap oil production.

---

## 1. Goals

| # | Goal | Acceptance |
|---|---|---|
| G1 | Gas reaches sales spec only through real treating | Off-spec gas never passes a custody point (SC3) |
| G2 | Flaring is fully accounted | Flared mass equals rejected mass exactly; emissions and penalties follow |
| G3 | **A flaring cap limits oil production** | With flaring capped and no gas outlet, oil rate is throttled and the cause is attributed to gas handling |
| G4 | Compression is staged and power-hungry | Power matches the polytropic formula (MX6); stages are added as inlet pressure falls |
| G5 | Re-injection closes the loop | Re-injected gas provides pressure support to the compartment |
| G6 | NGL extraction is an economic choice | Extraction yields separately-priced products; the decision depends on the price spread |

---

## 2. Design decisions

### 2.1 Each treating step is an independent unit

Compression, dehydration, sweetening, NGL extraction and sulphur recovery are
separate `IFacilityUnit`s with their own capacities, costs, power draws and
efficiencies. **There is no "gas plant" unit.**

*Rationale:* the player builds exactly the chain their gas requires. Sweet dry
gas needs compression only; sour wet gas needs everything. That difference should
be visible in what they pay for, and it makes gas quality a real property of an
asset rather than a label.

### 2.2 The flaring cap is a *regulatory constraint on the flow network*

When flaring is capped and gas cannot be sold or re-injected, **the network
throttles upstream** — which limits oil, because the oil carries the gas.

*Rationale:* G3 is the phase's most important behaviour. It is real (routine
flaring is restricted or banned in most jurisdictions), it is non-obvious, and it
converts an environmental rule from a fine into a physical production constraint.
It falls out of the solver with no special handling: the flare is an element with
a capacity, and when it is full, backpressure propagates.

### 2.3 Gas lift's supply becomes internal

R7's external purchased lift gas remains available, and R9 adds the internal
path: compressed produced gas routed to gas lift. **Now lift gas competes with
sales gas**, which is a genuine allocation decision.

### 2.4 Sweetening produces sulphur

A by-product with its own market. Small revenue, real disposal obligation if
unsold. Included because it costs almost nothing and it makes sour gas feel
properly consequential in both directions.

### 2.5 NGL requires component tracking — only here

Per open decision FD2, the engine is black-oil everywhere except the NGL plant,
where the gas stream's ethane/propane/butane/pentanes+ split is tracked.

**This boundary is declared explicitly** so component tracking does not leak
across the engine. The NGL plant takes a gas stream, applies recovery
efficiencies per component from the fluid system's declared composition, and
emits separate product streams.

### 2.6 Environment coupling — heat derating

Compressors and turbines lose capacity in high ambient temperature
([13_ENVIRONMENT](../design/13_ENVIRONMENT.md) section 3.3). The consequence is
seasonal and non-obvious: **a desert field loses gas-handling capacity in exactly
the hottest months**, and because gas handling caps oil (G3), oil rate falls in
summer for a reason nowhere near the reservoir.

Worth building explicitly, because it is the clearest demonstration in the game
that the setting is a live input rather than a cost multiplier applied once.

### 2.7 Events this phase raises

`flow.flared` — the **gas trilemma's loop-entry event**, severity `W`. An
approaching flaring cap must be announced *before* it binds, not when oil
production is already throttled (rule IR3).

---

## 3. Deliverables

`OGSim.Facilities` extension: `ICompressor` + `ICompressionModel` (staged
polytropic), `IDehydrator`, `IAcidGasRemoval`, `INglExtraction`, sulphur
recovery, `IFlare`, gas re-injection routing, sales gas specification.
Content: gas unit catalogue, sales gas specs per jurisdiction.

---

## 4. Verification

| # | Test | Passes when |
|---|---|---|
| R9-V1 | Compression power (MX6) | Matches the polytropic formula; staging matches the pressure-ratio limit |
| R9-V2 | Declining inlet pressure | Falling field pressure raises the required ratio and forces additional stages |
| R9-V3 | Dehydration | Water dewpoint is reduced to spec; removed water is accounted for |
| R9-V4 | Sweetening | H₂S and CO₂ are reduced to spec; sulphur is produced in stoichiometric proportion |
| R9-V5 | NGL split | Component recovery matches declared efficiencies; mass is conserved across the split |
| R9-V6 | Spec gate (SC3) | Off-spec gas never reaches a custody point |
| R9-V7 | Flare accounting | Flared mass equals rejected mass **exactly** |
| R9-V8 | **Flaring cap limits oil** | With flaring capped and no gas outlet, oil rate is throttled and the loss is attributed to gas handling |
| R9-V9 | Re-injection | Re-injected gas reaches the compartment and slows pressure decline |
| R9-V10 | Lift gas competition | Gas routed to lift is unavailable for sale; the allocation is visible and auditable |
| R9-V11 | Conservation | Mass balances across the entire gas chain including flare and by-products |

**R9-V8 is the phase's headline test.** If it does not hold, the environmental
system is a fine rather than a constraint, and the design intent is lost.

---

## 5. Out of scope

Gas pipeline transport and LNG (R11). Emissions accounting and penalties (R16) —
R9 produces the flare volumes; R16 prices them. Carbon capture (R17).

---

## 6. Risks

| Risk | Mitigation |
|---|---|
| Component tracking leaks beyond the NGL plant | An architecture test asserting component-level composition appears only in the NGL unit and its outputs |
| The gas chain is tedious to build in the UI | Facility templates (R8.2) give a sensible default chain; the player refines it |
| Flaring caps feel arbitrarily punitive | The cap is jurisdiction content, visible before a licence is bid on, and there are always at least two compliant alternatives |
| Sour gas metallurgy is not modelled but matters | Deferred to R18 as a corrosion-severity factor; noted here so it is not forgotten |
