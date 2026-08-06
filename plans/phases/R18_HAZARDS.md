# Phase R18 — Degradation, Hazards and Maintenance

**Arc III** · Status ⬜ · Depends on: R12, R16, R17 · Enables: R20

---

## 0. Purpose

Nothing stays as good as the day it was built. R18 makes equipment decay under
service, makes failure a consequence of neglect rather than random punishment,
and gives the player three genuinely different maintenance strategies with no
dominant answer.

---

## 1. Goals

| # | Goal | Acceptance |
|---|---|---|
| G1 | Decay is driven by service severity, not the calendar | A well producing sour, wet, sandy fluid degrades faster than a clean one at the same age |
| G2 | Failure is a consequence, not a coin flip | Hazard rate rises with falling condition; a well-maintained asset rarely fails |
| G3 | Availability feeds the solver | Failed equipment is absent from the network at tick stage 4, and the production loss is attributed to it (SC7) |
| G4 | Three strategies, no dominant answer | Run-to-failure, scheduled and condition-based each win in different circumstances |
| G5 | Hazards have purchasable mitigations | Every hazard has a preventive measure with a cost |
| G6 | Every outcome is auditable | Each failure records its stream, draw and threshold |

---

## 2. Design decisions

### 2.1 Severity factors, declared in content

Water cut, H₂S/CO₂ content, sand production, duty cycle, temperature, time since
service. Each equipment type declares which factors it is sensitive to and how
strongly.

*Rationale:* it makes degradation an emergent property of *how the asset is being
operated*, which means the player's production choices have maintenance
consequences. Producing hard is not free.

### 2.1b Severity is evaluated against the previous tick's service

Per [03](../design/03_ARCHITECTURE.md) §6.1: this tick's rates and water cut are
unknown at stage 4, so decay and hazard rates use the previous tick's solved
values. One-tick lag, deterministic, and stated once so every severity factor
answers "which tick's data?" the same way.

### 2.2 Hazard rate rises with falling condition

A smooth, steeply-rising relationship rather than a threshold. Failures draw from
the `hazard` RNG stream and are audited.

*Rationale:* a threshold teaches the player to sit just above it. A rising rate
teaches them that deferring maintenance has a growing, visible cost.

### 2.3 Failure removes the element from the network

R4.2's decision — unavailable elements are absent, not zero-capacity — pays off
here. **A compressor failure limiting gas handling and therefore limiting oil
production requires no special code**; it falls out of the network solve, and the
attribution names the compressor.

That is SC7, and it is the phase's most important behaviour.

### 2.4 Three strategies with real trade-offs

| Strategy | Wins when |
|---|---|
| Run to failure | The asset is marginal, easily replaced, or near end of life |
| Scheduled | Predictability matters more than optimality; most assets, most of the time |
| Condition-based | The asset is critical and downtime is expensive — needs monitoring technology |

**Marginal wells rationally run to failure; the main export compressor rationally
gets condition monitoring.** That both are correct in their context is what makes
this a decision rather than an upgrade path.

### 2.5 Hazards distinguish prevention from response

Every hazard has: trigger conditions, a rate, a consequence, and **a mitigation
purchasable in advance**. Hydrates → insulation or inhibitor. Corrosion →
resistant alloy or inhibitor. Blowout → better well control equipment and
practice.

*Rationale:* hazards must never read as random punishment. They are the price of
decisions made earlier, and the player must have been able to see them coming.

### 2.6 Souring is included deliberately

Long-term waterflood can turn a sweet reservoir sour through sulphate-reducing
bacteria. **Metallurgy chosen years earlier is now wrong.** It is a slow-burning
consequence of a development decision, discovered late, and it is exactly the
kind of long-arc consequence this game should have.

### 2.7 Environment drives several severity factors

Several degradation severity factors are environmental rather than operational
([13_ENVIRONMENT](../design/13_ENVIRONMENT.md) section 3.4): marine salt air on
external corrosion, ambient temperature on elastomers and on hydrate and wax
risk, ice scour on subsea lines, and **remoteness on intervention cost** — which
is what makes the optimal maintenance strategy setting-dependent rather than
universal.

Run-to-failure is rational on a land well and irrational on a subsea one. That
reversal is the clearest demonstration that section 2.4's three strategies have
no dominant answer.

### 2.8 Events this phase raises

`equipment.conditionThreshold` · `equipment.failed` · `equipment.repaired`.

**Failure creates a segment boundary** ([21](../design/21_INTEGRATION.md)
section 5), so a mid-month failure costs the remaining fraction of the month
rather than all of it — which is what makes response speed a real variable.

---

## 3. Deliverables

`OGSim.Operations` extension: `IDegradationModel` (severity-weighted),
`IHazardModel`, incident types and consequences, three maintenance strategies,
condition monitoring, availability computation feeding tick stage 4.
Content: `hazard` catalogue, degradation profiles per equipment type, maintenance
templates.

---

## 4. Verification

| # | Test | Passes when |
|---|---|---|
| R18-V1 | Severity-driven decay | Harsh service degrades faster than mild service, by the declared factors |
| R18-V2 | Hazard curve | Failure rate rises with falling condition as declared; no threshold behaviour |
| R18-V3 | Availability | A failed element is absent from the network; production loss is attributed to it |
| R18-V4 | Compressor cascade (SC7) | A compressor failure limits gas handling and therefore oil, with correct attribution |
| R18-V5 | Strategy comparison | Over a long run, each strategy wins in its designed circumstance |
| R18-V6 | Mitigation efficacy | Each mitigation reduces its hazard rate by the declared amount |
| R18-V7 | Determinism | The same seed produces identical failure sequences |
| R18-V8 | Audit | Every failure records stream, draw and threshold |
| R18-V9 | Souring | Long waterflood raises H₂S; corrosion severity rises for equipment not rated for it |
| R18-V10 | Repair | Repair and replacement restore condition; both are `IOperation`s with cost and duration |

---

## 5. Out of scope

Detailed reliability engineering (Weibull fitting, MTBF catalogues) — the model is
condition-driven and content-tuned. Safety-case and personnel-safety modelling
beyond incident consequences.

---

## 6. Risks

| Risk | Mitigation |
|---|---|
| Failures feel unfair | Rising rates, visible condition, available mitigations, and an auditable draw for every event |
| Maintenance becomes tedious micromanagement | Strategies are set per asset class and inherited; the player intervenes by exception |
| Hazard tuning makes the game punishing or trivial | Band-tested through SC1's full lifecycle; the hazard model is a plugin with an "off" implementation for testing |
| Souring is surprising in a bad way | It is foreshadowed: water chemistry data is purchasable, and the risk is visible to a player who looks |
