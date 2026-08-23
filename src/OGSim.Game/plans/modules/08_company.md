> Source read in full: `src/OGSim.Composition/Modules.cs`, plus the types this
> module composes. Part of the module review requested 2026-08-23. Nothing in the
> engine was changed to produce this.


# 08 — company

`internal sealed class CompanyModule(ContentId startingState, StyleTerms terms)`

## Manifest

| | |
|---|---|
| **provides** | `IFiscalRegime`, `IPriceModel`, `MarketState`, `CompanyState`, `ReservesBook`, `IReserveBasedLending`, `Licence`, `CrewState` |
| **requires** | `IAuditTrail`, `IBeliefStore`, `MarketState` |
| **ownsState** | `company.ledger`, `company.market`, `company.licence`, `company.crew` |
| **stages** | `Company` order 0 |

It **requires `MarketState` which it also provides** — a self-satisfying
requirement, tagged in the source as finding 229.

The belief store is required because reserves are worked out from what the
company *believes* is down there. Declaring it is also what orders this module
after the one that owns beliefs.

## Compose

1. `RoyaltyTaxRegime(new ContentId("concession"), royaltyRate: 0.125, taxRate: 0.40)` → `IFiscalRegime`
2. `CrewState(...)` — **owned and provided**
3. `Provide(Defaults.Market)` → `IPriceModel`
4. `MarketState(OilPricePerTonne, CostElasticity, CostDrift)` — **owned and provided**
5. `Provide<IReserveBasedLending>(Defaults.Lender)`
6. `ReservesBook(beliefs, market, Defaults.TypeCurve)`
7. `CompanyState(Defaults.OpeningCashFor(startingState), cause => IsCustodyTransfer(audit, cause))` — **owned and provided**
8. `Licence(new EntityId<ILicence>(1), terms.Licence, granted: Tick(0))` — **owned and provided**
9. `LicenceStage(licence, company, audit)` at order 0

## The stage

**`LicenceStage`** — stage 11 (`Company`), order 0.

- Returns immediately `if (!licence.IsLive)` — the assessment re-detects the same
  unmet item every call after the deadline, so calling it unguarded would forfeit
  the same bond every month for the rest of the game
- On loss: posts the **whole** bond `Penalty → Cash` under `Contractual`, with a
  `Financial` audit cause
- On expiry: `StateTransition` audit only, **no movement** — a company that met
  every commitment and merely ran out of term broke no promise

## Functions and properties

**`CrewState`** (`OGSim.Company/Crew.cs`)

| Member | |
|---|---|
| `TrainingCost` | one-time price |
| `Trained` | has it been bought |
| `Competency` | feeds the bow-tie barrier strength in **hse** |
| `DurationFactor` | feeds the scheduler in **field** |
| `Train()` | the one transition |
| `Key` = `company.crew`, `SchemaVersion` 1, `Capture`/`Restore` | |

One owner, so the barrier term and the duration term are the same fact rather
than two guesses at it.

**`CompanyState`** — the ledger. Revenue may only be caused by a custody
transfer: `IsCustodyTransfer` asks the **trail** rather than trusting the
posting, so a movement cannot *claim* to be a sale, only cite an entry that was.

**`MarketState`** — this month's price and the year the cost index is driven
from. Owned because a reloaded game otherwise resumed the price walk from the
right dice at the wrong place and sold the same barrels for different money.

**`Licence`** — `IsLive`, `LossReason`, `Terms`, `Progress`, `Expiry`,
`AssessAt(tick)`, `ExpireIfDue(tick)`, `RecordDelivery(template, quantity)`.

## Dependencies and conditions it decides for itself

| Where | Condition |
|---|---|
| `Compose` | `Defaults.OpeningCashFor(startingState)` — a two-branch lookup that **throws** on a third value. The branch is inside `Defaults`, where no style can see or change it |
| `LicenceStage.Execute` | `if (!licence.IsLive) return;` — correct, but it is tenure policy living in a stage |

## Static numbers found

`royaltyRate: 0.125`, `taxRate: 0.40` — written inline in `Compose`, not content
and not `Defaults`.

## Content and Defaults consumed

`Defaults.CrewCompetencyUntrained/Trained`, `CrewDurationFactorUntrained/Trained`,
`CrewTrainingCost`, `Market`, `Economics.OilPricePerTonne`, `CostElasticity`,
`CostDrift`, `Lender`, `TypeCurve`, `OpeningCashFor`, and `terms.Licence` from
the style. **No content file drives this module.**
