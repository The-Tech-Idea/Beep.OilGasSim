# Phase R14 — Information and Uncertainty

**Arc III** · Status ⬜ · Depends on: R5, R13 · Enables: R15, R16

---

## 0. Purpose

The exploration game. R14 separates **what is true** from **what the player
knows**, and builds the economy by which the second approaches the first at a
price.

Everything before R14 has operated on truth. After R14, **the player's decisions
are made against beliefs**, and the gap between belief and truth is where the
drama lives.

---

## 1. Goals

| # | Goal | Acceptance |
|---|---|---|
| G1 | Truth is structurally unreachable | Architecture test: no assembly outside `OGSim.Information` can reference the truth model |
| G2 | Beliefs update correctly | A Bayesian update from an observation produces the analytically correct posterior |
| G3 | Information is priced and imperfect | Every source has a cost, a duration, a footprint and an error model; none eliminates uncertainty |
| G4 | POS decomposes into five factors | Source, reservoir, seal, trap, timing — each independently updatable |
| G5 | Plays correlate | One well's result updates beliefs about every prospect in its play |
| G6 | Value of information is computable | The engine can state the expected value of a proposed purchase against the current belief and decision |
| G7 | The `p/Z` deduction works | A player producing a gas reservoir can infer GIIP from pressure history |

---

## 2. Design decisions

### 2.1 Truth is `internal` and the boundary is tested

Not "hidden by convention" — structurally unreachable, enforced by an
architecture test established back in R5.5.

*Rationale:* every game of this type eventually leaks the answer through a debug
path, a convenience accessor, or a save file. Once it leaks, the exploration game
is over. The layering rule is the only version that survives contact with
schedule pressure.

### 2.2 Observation, not revelation

An `IInformationSource` reads truth and returns a **sampled observation with
error** — never truth itself. The error model is per source and per property
kind: 3-D seismic sees structure well and porosity poorly; a core sees everything
superbly at one point.

### 2.2b Below the detectability tier, a survey finds nothing

The observation model consumes the accumulation's `TrapSubtlety`
([06](../design/06_WORLD_AND_EXPLORATION.md) §2.3): a survey below the class's
minimum tier **spawns no lead** — not a noisy one, none. The belief layer may
mark acreage "beyond current imaging" (public industry knowledge) but carries
*nothing* about what is there; the leak test R15-V10 extends to cover it.
Re-screening with a higher tier — including cheap re-processing of data you
already shot — is what makes imaging an exploration lever (07 §2b).

### 2.3 Beliefs are per property, per entity

Each is a distribution updated by observations. **Provenance ranking** (R2.2)
weights the update: a core beats a log beats seismic beats an analogue.

### 2.4 Play correlation is a structural prior

Prospects in a play share prior distributions on source, reservoir and seal.
Observing one updates the shared prior, which propagates to siblings. Trap risk
stays prospect-specific.

*Rationale:* this is the single mechanism that makes exploration **learnable**
([06](../design/06_WORLD_AND_EXPLORATION.md) §2.1). Without it, every well is an
independent coin flip and there is nothing to get better at.

### 2.5 A dry hole must produce a diagnosis

Not merely "dry". The engine reports which of the five elements failed, because
that is what updates the play model. **A dry hole that teaches nothing is the
design failure that kills exploration games**, so the diagnosis is a required
output of the drilling outcome, not an optional extra.

### 2.6 Value of information is exposed and is allowed to be wrong

Computed from the player's **current beliefs** — which may be mistaken — against
the pending decision. It is decision support, not an oracle. A player with a bad
prior gets confident, wrong advice, which is exactly what happens in reality.

### 2.6b The pressure survey — information priced in deferred production

`p/Z` (R14.6) and drive-mechanism identification need **average reservoir
pressure**, and a flowing well does not report it — the flowing bottomhole
pressure sits below reservoir pressure by the drawdown. Measuring it means a
**build-up survey**: shut the well in for days and read the gauge as pressure
recovers toward the compartment average.

Modelled as an `IInformationSource` whose cost is **the shut-in itself** — the
deferred volume is computed by the solver like any other shut-in, plus a small
survey fee. Accuracy improves with shut-in duration (a longer build-up sees
further into the reservoir), so even the survey has a depth-versus-cost dial.

*Why this matters:* without it, `p/Z` would read as free telemetry and the
deduction mechanic would be dishonest. With it, "how big is my reservoir?" costs
real production to answer — which is exactly the industry's actual trade.

### 2.7 Production history is an information source

Ranked near the top of the provenance ordering. It is how compartmentalisation is
discovered (open decision M1), how drive mechanism is identified, and how the
`p/Z` deduction works. **The dynamic data is the most trustworthy thing about a
reservoir**, and the game should reward the player who reads it.

### 2.8 The setting prices the information

Acquisition cost and duration are a function of the source *and* the setting
([06](../design/06_WORLD_AND_EXPLORATION.md) section 3.1a). Land seismic is cheap
on plains and expensive in swamp; marine surveys need a weather window, and a
missed window costs a year.

**Consequence:** the value-of-information calculation (R14.9) must use the
setting-adjusted cost and delay, never a catalogue price. A prospect in a hard
setting needs a higher expected value to justify the same survey.

### 2.9 Events this phase raises

`discovery.*` · `belief.updated` · `reservoir.compartmentInferred` /
`rival.result`, all at **tick stage 10** — after material balance, so beliefs
always reflect the production that just happened.

---

## 3. Deliverables

`OGSim.Information`: `ITruthModel` (internal), `IBelief<T>`, Bayesian update,
`IInformationSource`, `IObservationModel`, seismic (2-D, 3-D, 4-D), well logs,
cores, well tests, production-history inference, `IRiskFactorSet`,
`IVolumetricEstimate`, value-of-information, play correlation.
Content: `information-source` catalogue with costs, durations and error models.

---

## 4. Verification

| # | Test | Passes when |
|---|---|---|
| R14-V1 | Truth isolation | Architecture test passes across all assemblies |
| R14-V2 | Bayesian update | Posterior matches the analytic result for conjugate cases |
| R14-V3 | Error models | Each source's observations are distributed as declared over a large sample |
| R14-V4 | Variance reduction | Every source reduces variance; none reduces it to zero |
| R14-V5 | Provenance weighting | A core outweighs a log outweighs seismic, by the declared amounts |
| R14-V6 | Play correlation | One result measurably updates sibling prospects' POS in the correct direction |
| R14-V7 | Dry hole diagnosis | The failed element is correctly identified and the play model updates accordingly |
| R14-V8 | POS decomposition | The five factors multiply to the reported POS; each updates independently |
| R14-V9 | Volumetrics | P10/P50/P90 propagate correctly through the log-normal product |
| R14-V10 | Value of information | Matches a hand-computed EVI for a simple decision |
| R14-V11 | `p/Z` deduction | Producing a volumetric gas reservoir lets GIIP be inferred within the expected error |
| R14-V12 | Compartment discovery | Pressure and production data reveal compartmentalisation the player was not told about |
| R14-V13 | Wildcat success rate (MB4) | Across a generated basin, 10–35% |
| R14-V14 | Detectability gate | A D2 accumulation under a 3-D-only survey spawns no lead and leaks nothing into the read model; adding attributes and re-screening the same acreage spawns it |
| R14-V15 | Staleness widens what is not watched | A belief about a PRODUCING compartment's pressure has a wider σ after a year than it started with, and a belief about the rock does not — porosity is not a thing that goes out of date (SDD-008 §2d) |
| R14-V16 | A shut-in field's pressure belief does not go stale | Drift is charged on what was PRODUCED FROM, so a compartment nobody is drawing on tells the company nothing new and its belief is no less true for the wait (SDD-008 §2d) |

**R14-V12 is the phase's most satisfying test** and the direct realisation of
open decision M1.

---

## 5. Out of scope

World generation (R15) — R14 is tested against hand-built truth. Licence rounds
(R16). Seismic interpretation as a minigame (open decision W3, declined).

---

## 6. Risks

| Risk | Mitigation |
|---|---|
| Bayesian updates are subtly wrong and nobody notices | Conjugate cases have analytic answers (R14-V2); non-conjugate cases use sampling with convergence tests |
| Truth leaks through the read model | The read model is built from beliefs only; an architecture test asserts it does not reference truth types |
| Play correlation makes exploration too easy | The correlation strength is content-tuned and constrained by band test MB4 |
| Value-of-information turns the game into arithmetic | It uses the player's own beliefs, so it is only as good as their model — and it does not evaluate what they have not thought to consider |
