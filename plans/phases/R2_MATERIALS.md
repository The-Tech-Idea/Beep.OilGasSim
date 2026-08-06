# Phase R2 — Materials, Properties, Streams

**Arc I · Foundation** · Status ⬜ · Depends on: R1 · Enables: R3, R4, R5

---

## 0. Purpose

Build the three abstractions that make "one engine for oil or gas" true:
`IProperty`, `IMaterial`, `IStream`. After this phase, the engine can describe
any substance and any moving mixture of substances without knowing what oil is.

---

## 1. Goals

| # | Goal | Acceptance |
|---|---|---|
| G1 | Any substance is describable as data | A synthetic material with arbitrary properties is registered and used with no code change |
| G2 | No code branches on material identity | Architecture test finds no comparison against a specific material id outside the catalogue |
| G3 | Facts carry their uncertainty and provenance | Every `IProperty` has a distribution, a provenance and an as-of; a scalar-only property is not constructible |
| G4 | Streams mix and split without losing mass or provenance | Mixing then splitting returns the original composition and contributor proportions |
| G5 | Reservoir and surface volumes never silently interconvert | Enforced by R1.2's volume conditions; conversion requires the FVF from the fluid model |
| G6 | Phase behaviour is a property of the material, not a switch | A material's phase at (P,T) is asked of the material, never inferred from its name |

---

## 2. Design decisions

### 2.1 Properties are distributions, always

**Decision: `IProperty` holds a distribution, never a bare value.** A measured
property is a distribution with very small variance — not a special case.

*Rationale:* the alternative (scalar with an optional uncertainty) invites every
consumer to read the scalar and ignore the uncertainty, and within a few months
the uncertainty is decorative. Making the distribution the only representation
means the exploration game cannot quietly stop working.

**Supported distributions:** point (measured), normal, log-normal, triangular,
uniform. Log-normal is essential — hydrocarbon volumes are products of uncertain
terms and come out strongly right-skewed
([05](../design/05_SIMULATION_MODELS.md) §1.4).

### 2.2 Provenance is required, not optional

Every property states how it is known. This drives three things: the default
uncertainty, the belief-update weighting in R14, and the player-facing "how do we
know this?" answer.

**Ordering by confidence:** `Assumed` < `Analogue` < `Seismic` < `Log` <
`WellTest` < `Core` < `ProductionHistory` < `Measured`. Note that
`ProductionHistory` ranks near the top: **the dynamic data is the most
trustworthy thing about a reservoir**, which is why the `p/Z` deduction in R14.6
is so powerful.

### 2.3 Streams carry mass, not volume

**Decision: composition is mass flow per material.** Volumes are derived on
demand at a stated condition.

*Rationale:* mass is conserved; volume is not (gas expands, oil shrinks). The
conservation invariant in R4.6 is only meaningful in mass. This decision, plus
R1.2's volume conditions, makes the double-count structurally impossible rather
than merely tested against.

### 2.4 Provenance in streams

A stream carries the proportion each source compartment contributed. Mixing
combines proportions by mass; splitting preserves them.

*Rationale:* allocation is a real industry problem, and royalties, working
interests and reserves depletion all need the answer. Carrying it in the stream
costs a small dictionary and removes the need for a separate allocation
subsystem later.

### 2.5 Black-oil model as the default `IFluidPropertyModel`

Standing / Vazquez-Beggs / Beggs-Robinson / Lee et al. / Dranchuk-Abou-Kassem
correlations, behind the plugin contract. A table-lookup implementation and a
constant-properties implementation ship alongside as alternative fidelity levels.

**The constant-properties implementation is not a stub.** It is a legitimate
simplification for arcade fidelity, complete and tested, and it must produce
sensible behaviour — just less nuanced behaviour.

---

## 3. Deliverables

| Project | Contents |
|---|---|
| `OGSim.Contracts` | `IProperty`, `IPropertyKind`, `IMaterial`, `IStream`, `IFluidPropertyModel`, distributions |
| `OGSim.Kernel` *(extension)* | Implementations of the above; catalogues |
| Content | `property-kind` and `material` catalogues for the standard set in [research/PPDM_ALIGNMENT](../research/PPDM_ALIGNMENT.md) §5 |

---

## 4. Verification

| # | Test | Passes when |
|---|---|---|
| R2-V1 | Material agnosticism | A synthetic material with oil-like properties behaves identically to crude oil through every stream operation |
| R2-V2 | No identity branching | Architecture test passes across all assemblies |
| R2-V3 | Mix/split round-trip | Mass and provenance are preserved exactly through mix → split |
| R2-V4 | Mass conservation in stream algebra | Randomised operation sequences conserve mass to floating-point tolerance |
| R2-V5 | Log-normal propagation | A product of log-normal terms is log-normal with the analytically correct parameters |
| R2-V6 | Bubble-point behaviour | Crossing `Pb` produces the expected `Rs`, `Bo` and `μo` responses (direction and rough magnitude) |
| R2-V7 | `Bo` round-trip | rb ↔ stb conversion via `Bo` round-trips exactly |
| R2-V8 | Phase split | A known fluid at a known (P,T) splits into the expected phase fractions |
| R2-V9 | Provenance ordering | Uncertainty defaults are monotonic in provenance confidence |
| R2-V10 | Correlation validity ranges | A correlation given out-of-range inputs raises a model fault, never extrapolates silently |

**R2-V10 deserves emphasis.** Industry correlations have validity ranges, and
silently extrapolating one is a classic way to produce plausible nonsense. Under
[09](../design/09_DIAGNOSTICS.md) §5.1 this is a model fault: the tick is
abandoned, not fudged.

---

## 5. Out of scope

Reservoirs (R5), wells (R6), anything that flows through a network (R4 defines
the contract; R2 only defines what flows). No content loading yet — R2's
catalogues are populated in-process by tests; R3 makes them data.

---

## 6. Risks

| Risk | Mitigation |
|---|---|
| Distribution arithmetic is subtly wrong | Analytic tests for every combination rule (R2-V5); no numerical shortcuts without a matching closed-form test |
| Stream provenance dictionaries grow large in a big field | Provenance is per *compartment*, not per well; a large field has tens, not thousands. Benchmark in R4 |
| Correlation implementations drift from their published form | Each correlation cites its source in a comment and has a test against published example values |
| The property abstraction feels heavy at call sites | Accept it — it is the exploration game. Provide ergonomic construction helpers, never a scalar back door |
