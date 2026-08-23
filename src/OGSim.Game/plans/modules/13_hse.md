> Source read in full: `src/OGSim.Composition/Modules.cs`, plus the types this
> module composes. Part of the module review requested 2026-08-23. Nothing in the
> engine was changed to produce this.


# 13 — hse

`internal sealed class HseModule(StyleTerms terms)`

## Manifest

| | |
|---|---|
| **provides** | `EsgAssessment` |
| **requires** | `IHazardModel`, `IAuditTrail`, `IRandomSource`, `AssetIntegrity`, `CrewState`, `CompanyState` |
| **ownsState** | `integrity.esg` |
| **stages** | `Availability` order 1, `HseRegulation` order 0, **and `Company` order 4 only if the style carries insurance** |

`IHazardModel` is required but **never resolved in `Compose`** — the module
builds its own `BowTie`. The requirement survives as an ordering edge.

## Compose

1. `EsgStanding(Defaults.EsgIncidentHalfLifeTicks)` — owned
2. `EsgAssessment(standing, Defaults.Record)` — provided
3. `ThreatStage(new BowTie(Stream(Hazard), audit), integrity, standing, crew, company, insurance, audit)` at order 1
4. `EsgStage(standing)` at order 0
5. `InsurancePremiumStage(...)` at order 4 — **only if `insurance is not null`**

## The three stages

| Stage | Slot | What it does |
|---|---|---|
| `ThreatStage` | Availability 1 | Finds the worst-conditioned element across all barriers; **returns early if `worst >= 1.0`** — a maintained field is not asked to roll. Rolls `ThreatRateAtFailure × (1 − worst)`. Samples every preventive barrier; all fail ⇒ top event |
| `EsgStage` | HseRegulation 0 | `standing.Age(1 tick)` — the rehabilitation, and the only thing that happens when nothing goes wrong |
| `InsurancePremiumStage` | Company 4 | Charges a premium every tick whether or not a loss lands |

## Functions and properties

**`EsgAssessment`** (declared in `Modules.cs`)

| Member | |
|---|---|
| `Observe(Mass flared, SurfaceVolume produced)` | one month enters the record. Called from stage 8, **before** `Of()` is read there |
| `Of()` | the 0–1 fraction a contract takes. Takes **nothing** — the flaring it scores is the aged window the standing owns, not a lifetime tally a caller happens to hold. A parameter here was a second place the answer could come from, and the standing could never fall |
| `FlaringWeight` = 100.0 | flaring is the whole intensity term today |

**`ThreatStage`** statics — `ConsequencePoints(resolved)` and
`ConsequenceLoss(resolved)`: the same straight line from *no mitigating barrier
held* to *all held*, once in points and once in cash. Pure functions, so the
formula is testable without a stochastic run.

Barrier strength is `min(worst element condition, crew competency, procedure
compliance)` — **weakest link, deliberately not an average**, so neglected
maintenance and an untrained crew both surface as safety risk.

## Dependencies and conditions it decides for itself

| Where | Condition |
|---|---|
| **Manifest** | `Slot(terms.Insurance is not null, Company, order: 4)` |
| **`Compose`** | `if (insurance is InsuranceTerms cover) Contribute(...)` |
| **`ThreatStage.Execute`** | `insurance is InsuranceTerms policy ? ClaimFor(policy, loss) : null` — per tick |

The first two are **the same condition written twice**. They are currently kept
in step by a comment; nothing enforces it.

## Content and Defaults consumed

`Defaults.EsgIncidentHalfLifeTicks` (36), `Record` (clean 5.0, worst 40.0 kg/m³),
`Barriers` (three), `ContainmentThreat`, `ThreatRateAtFailure` (0.15),
`ProcedureCompliance` (0.9), `TopEventPointsUnmitigated/Mitigated` (75/25),
`TopEventLossUnmitigated/Mitigated` ($6M/$2M). **No content file** — barriers and
threats are `Defaults`-only.
