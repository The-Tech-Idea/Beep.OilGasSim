> Collected from the sixteen module documents. This is the input to
> [27 — the dependency manager](../27_DEPENDENCY_MANAGER.md).

# Every condition a module decides for itself

Found by reading all sixteen modules in `src/OGSim.Composition/Modules.cs` and
the types they compose. Nothing here is a search result; each was read in place.

## A. Conditions — a module choosing behaviour

| # | Module | Condition | Why it is a problem |
|---|---|---|---|
| **C1** | facilities | `if (startingState == OpeningPosition) works.Commission(chain); else if (startingState != BareGround) throw` | A module compares content ids to decide whether the company owns a plant. The refusal on an unknown id is right; the branch is not the module's to make |
| **C2** | field | `Slot(terms.Hedge is not null, Company, order: 3)` in the **manifest** | One fact stated in two places |
| **C3** | field | `if (hedge is HedgeTerms collar)` in **`Compose`** | The other half of C2. Kept in step by a comment; nothing enforces it |
| **C4** | hse | `Slot(terms.Insurance is not null, Company, order: 4)` in the **manifest** | Same shape as C2 |
| **C5** | hse | `if (insurance is InsuranceTerms cover)` in **`Compose`** | The other half of C4 |
| **C6** | hse / `ThreatStage` | `insurance is InsuranceTerms policy ? ClaimFor(policy, loss) : null` | A branch on configuration evaluated **every tick**, inside a stage |
| **C7** | company | `Defaults.OpeningCashFor(startingState)` | A two-branch lookup that throws on a third value, hidden inside `Defaults` where no style can see it |
| **C8** | company / `LicenceStage` | `if (!licence.IsLive) return;` | Correct and necessary, but it is tenure policy living in a stage |
| **C9** | field / `ActivityOrders` | `if (!licence.IsLive)` refuses **every** activity | Tenure gating every verb. A style that has no licence mechanic still runs this check |
| **C10** | materials | `profile.Selected(FluidSlot) is ContentId chosen ? Bind(chosen) : new BlackOilModel(...)` | A **fallback** — law L2's default dependency. Defensible (the simulation profile is deliberately empty) but invisible |

**C2/C3 and C4/C5 are the same defect twice**: a claimed stage slot must be
filled, so the manifest and `Compose` must agree, and today nothing makes them.

## B. Static numbers inside modules

None of these is content, and none can be changed by a style.

| # | Module | Value | What it decides |
|---|---|---|---|
| **S1** | company | `royaltyRate: 0.125`, `taxRate: 0.40` | the entire fiscal take |
| **S2** | integrity | `baseRatePerYear: 0.05`, `conditionExponent: 4.0` | **how often anything breaks** |
| **S3** | wells | `Density.FromSpecificGravity(0.85)` | the outflow column |
| **S4** | facilities | `Density.FromSpecificGravity(0.85)`, `Viscosity(3e-3)` | pipeline hydraulics |
| **S5** | flow | `SolverSettings.Pinned` | damping, tolerances, iteration budget. Its own doc says *"content-supplied"* and **no content kind exists** |

## C. Neutrals that are illegal

A dependency manager that switches things off by zeroing a value will crash on
all five. Each was found by reading the validator, not by hitting it.

| # | Type | Refuses |
|---|---|---|
| **N1** | `WorkingInterest.Validate` | a sellable cap of `0` — *"must be in (0, 1]"* |
| **N2** | `Hedge.Validate` | a hedged fraction of `0` |
| **N3** | `BowTie.Materialises` | a rate `<= 0` |
| **N4** | `ClimateProfile.Validate` | a climate closed in all twelve months |
| **N5** | `CrewState` ctor | training that buys nothing |

**Consequence:** the manager must answer **presence** as well as **value**. For
these five the neutral is *not contributing the stage*, never a zeroed number.

## D. Dead declarations found while reading

Not conditions, but they belong in the same review — each is a member with no
behaviour (law L3) or a contract with no consumer.

| # | Where | What |
|---|---|---|
| **D1** | subsurface | `Defaults.MaxTickPressureDropFraction` is validated, threaded through `CommitTick` into `ReservoirCompartment.CommitWithdrawal`, and **never read**. The limit that binds is the drive's own `MaxTickVoidageFraction` |
| **D2** | wells | `IInflowModel` and `IOutflowModel` are provided, required by no manifest and resolved by nobody. The live models are built in `ProductionLoop.Drill` and `Defaults.CompletionFor` |
| **D3** | operations | provides nothing, owns nothing, contributes nothing |
| **D4** | objectives | as D3 |
| **D5** | content | `content/materials/*.json` (9 files) and `content/rock-types/*.json` are present and **not loaded** — the live lists are `Defaults.Materials` and `Defaults.TheRock` |

## Count

**10 conditions · 5 static-number sites · 5 illegal neutrals · 5 dead
declarations**, across sixteen modules.

## Resolution — what W4 did with each (2026-08-23)

| # | Resolution |
|---|---|
| **C1** | facilities asks `Has(OpensHoldingAPlant)`; the unknown-starting-state refusal moved to `DependencyManager.For`, which validates by resolving the opening cash |
| **C2/C3** | field's manifest slot and `Compose` both ask `Has(Hedging)` — one entry, asked twice, cannot disagree |
| **C4/C5** | hse the same, with `Has(Insurance)` |
| **C6** | the policy's answer to a loss is decided ONCE at compose (`Func<Money, Money?>`); the per-tick configuration branch is gone from `ThreatStage` |
| **C7** | company asks `ValueOf<Money>(OpeningCash)`; the starting-state branch runs once, inside the manager's construction |
| **C8** | **refined by reading**: the `IsLive` guard is a one-time-transition guard (re-running would forfeit the same bond monthly), so it STAYS — what became the manager's question is the stage's PRESENCE: company claims and fills the Company-0 slot on `Has(Tenure)` |
| **C9** | `ActivityOrders` takes `tenureGoverns` from `Has(Tenure)`; the check is absent for a style with no licence mechanic, not vacuously true |
| **C10** | materials binds `ValueOf<ContentId>(FluidModel)`; the silent fallback moved into the manager's construction where it is visible |
| **S1** | `FiscalRegime` entry, built from `Defaults.RoyaltyRate`/`TaxRate` — fresh per build, because the regime carries a loss carryforward |
| **S2** | `HazardRate` entry, built from `Defaults.HazardBaseRatePerYear`/`HazardConditionExponent` |
| **S3/S4** | untouched — fluid properties, W8's content migration |
| **S5** | `SolverSettings` entry, defaulting to `Pinned` |
| **N1–N5** | honoured by design: an absent entry contributes no stage and is never asked for a value |
| *(new)* | `Banking` — presence entry the review missed; the manifest's command list and the handler registration ask the same entry |
| *(new)* | **C9 had over-widened while relocating**: moving tenure into `ActivityOrders` made it gate *every* activity, including the monthly repairs — so on licence expiry (month 480) an Engineer field died of its first unrepairable failure, measured as the custody-breach test failing at zero throughput from month ~500. Resolved by `ActivityTerms.Develops`: tenure refuses what develops (R16's own words), never upkeep or closure |
