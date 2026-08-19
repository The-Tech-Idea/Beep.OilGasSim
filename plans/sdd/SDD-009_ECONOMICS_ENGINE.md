# SDD-009 — Economics Engine

**Status:** drafted · **Serves:** R13 · **Design docs:** [08](../design/08_ECONOMICS.md), [R13](../phases/R13_ECONOMICS.md)

The arithmetic of money, pinned — above all the **PSC cost-recovery mechanics**,
which are the single most-misimplemented calculation in this industry's
software, and the **reserves algorithm**, which the growth loop hangs off.

All money is integer cents (`Money`, SDD-001 §8): every identity below is
**exact**, and INV2 has no tolerance term.

---

## 1. The ledger

```csharp
public sealed record Movement(
    Tick Tick, Account Debit, Account Credit, Money Amount,
    MovementCategory Category, EntityRef? Asset, AuditId Cause);

public enum Account { Cash, Debt, Equity, Revenue, Opex, Capex_PPE, Depreciation,
    Royalty, Tax, AbandonmentProvision, Inventory, PartnerPayable, InsurancePremium, Penalty }
```

**The `double`→`Money` boundary, pinned — INV2's exactness depends on it.**
Physical quantities are doubles; the ledger is integer cents; every crossing
(revenue = mass × unit price, escalated day rates = base × cost index,
`Money.FromMillions`) rounds **half-even, exactly once, at the Movement that
enters the ledger** — never upstream, never twice. Inside the ledger,
arithmetic is pure integer; the only doubles the ledger ever sees are already
rounded. Without this pin, "exact to the cent" is a slogan; with it, a one-cent
trial-balance error is a bug by definition.

**Double-entry, always.** INV2 is the trial balance: Σ debits = Σ credits per
tick, and Cash equals opening + movements — exactly, in integers. Revenue may be
credited **only** with a `custody.transferred` cause (R13-V2, architecture-
tested).

> **R20d.12 amendment — the origination check runs at POSTING and never at
> RESTORE, because a restore is not an origination and the check cannot be
> repeated anyway.**
>
> The rule above is about the moment revenue is CREATED: it exists so no code
> path can credit revenue for anything but a metered sale. `CompanyState.Restore`
> replayed a saved ledger through the same `Post`, so every historical movement
> was re-asked to justify itself — and **a freshly composed engine has an empty
> audit trail**, so the first restored revenue credit failed on a cause the trail
> had never heard of. That is not a defect in the save; it is the check being
> asked a question it cannot answer.
>
> **It could not answer it even within one session.** The trail is retained on a
> WINDOW (`AuditRetention.DetailWindowTicks`, design 09 §4.4), so a movement from
> tick 5 in a game saved at tick 60 cites a cause the running engine has already
> summarised away. Re-validating history against a windowed record is
> structurally unsound whether or not a save is involved, and saving the trail
> would not fix it — the window is the point.
>
> So the ledger gains a **replay** path used only by `Restore`: same arithmetic,
> same accounts, same `Movement` records, and **no origination check**. What
> still runs at restore is **INV2**, which is repeatable by construction because
> it is a property of the numbers rather than of a trail — and it is the check
> that would actually catch a corrupted or truncated ledger.
>
> **This was invisible until something composed the owners together** (finding
> 188). `CompanyState`'s own round-trip test supplies a predicate that answers
> true, so it verified that a ledger's VALUES survive and could not have
> discovered that its restore path is unreachable in a real engine.

## 2. Accrual overlays

- **Depreciation — units of production** (the industry method, pinned):
  `dep(tick) = PPE_remaining(asset) · produced(tick) / reserves2P_remaining(asset)`
  computed per asset, integer-rounded down, remainder carried (no cent leaks).
- **Abandonment provision:** `provision(tick) = EstimatedCost(asset) ·
  produced(tick) / EUR_2P(asset)` — accrued per barrel from first production
  (08 §1), against `IObligationRegistry.EstimatedCost` (SDD-007 §6).
- **Inventory** valued at declared standard lifting cost (content) — a stated
  simplification: mark-to-market inventory would inject price volatility into
  the balance sheet for no decision value.

> **R20d.14 amendment. The provision accrues per FIELD until a company can have
> two.** The formula above is per asset — `EstimatedCost(asset) · produced(tick)
> / EUR_2P(asset)` — and it is right, because over a life the sum telescopes to
> exactly the asset's cost. What it needs is `EUR_2P(asset)`: the ultimate
> recovery attributable to one well.
>
> Nothing computes that and nothing honestly can yet. §4 forecasts a FIELD's
> decline from a type-curve; splitting it between the wells on that field would
> need an allocation nobody has specified, and dividing by well count would be a
> number invented at the call site (rule F-2's spirit — a figure with no
> derivation behind it).
>
> So the accrual is taken against the company's total outstanding obligation and
> its 2P reserves:
>
> ```text
> provision(tick) = TotalOutstanding · produced(tick) / reserves2P
> ```
>
> The telescoping property survives — a field that produces its reserves accrues
> its whole abandonment cost — and the per-asset split becomes meaningful at the
> same moment as the rest of it: when a company can hold two fields with separate
> gathering systems, which is SDD-006 §7c's remaining half.
>
> **Depreciation reads the same way, and for the same reason.** Units of
> production needs `PPE_remaining(asset)` and the ledger tracks capital by
> ACCOUNT — `Capex_PPE` is one balance, not a balance per well — so the charge is
> taken against the company's capital and its remaining 2P. Per-asset splitting
> arrives with the same change as the provision's.
>
> **Against REMAINING reserves, where the provision is against ULTIMATE, and
> that is not an inconsistency.** A provision is charged against what a field
> will ever give because the bill is fixed and the sum has to telescope to it.
> Depreciation is charged against what is LEFT because the value being written
> off is also what is left — both sides of the fraction shrink together, which is
> what stops a nearly-spent field carrying its plant at cost.

## 3. Fiscal regimes — the exact algorithms

Evaluated per licence per tick, in this order, all integer.

```csharp
// Everything a regime may consider, per licence per tick. Closed deliberately:
// a regime that could reach for arbitrary engine state would be a regime whose
// output could not be reproduced from its inputs, and R13-V4's hand-computed
// fixtures depend on exactly that reproducibility.
public sealed record FiscalInput(
    Money GrossRevenue,
    Money RecoverableOpex,
    Money RecoverableCapex,
    Money Depreciation,
    Money CostPoolCarry,          // §3.2 step 5 — the carryforward, in
    double PriorRFactor);         // §3.2 step 7 — PRIOR tick's, no same-tick circularity

public sealed record FiscalResult(
    Money Royalty,
    Money Tax,
    Money ContractorTake,
    Money CostPoolCarry);         // the carryforward, out

// Design 03 §3.2 — royalty/tax ↔ PSC ↔ service contract ↔ sliding scale.
// The engine calls Assess once per licence per tick at stage 8 and books ONLY
// what comes back: there is no fiscal arithmetic anywhere else (R13-V2).
public interface IFiscalRegime
{
    ContentId Id { get; }
    FiscalResult Assess(FiscalInput input);
}
```

**`CostPoolCarry` appears on both sides**, and that is the pinned shape of §3.2
step 5: the regime is a pure function of its inputs, so the carryforward has to
be threaded through it rather than held inside it. A regime holding its own pool
would be state outside the save's module blocks (SDD-013) and would make the
under-recovery fixture untestable in isolation.

### 3.1 Royalty/tax

```text
royalty  = rate · gross                                  (due even at a loss — 08 §4)
taxable  = gross − royalty − opex − depreciation − allowances − lossCarry
tax      = taxRate · max(0, taxable)
lossCarry' = max(0, −taxable)          indefinite carryforward, no uplift (pinned)
```

### 3.2 Production sharing — pinned precisely

```text
State per licence: costPool (carryforward, cents)
1. royalty    = royaltyRate · gross                      (0 where the PSC has none)
2. costOilCap = capFraction · (gross − royalty)
3. costPool  += recoverableOpex + recoverableCapex        (recoverability per
                cost class is CONTENT — signature bonuses typically excluded)
4. costOil    = min(costPool, costOilCap)
5. costPool  −= costOil                                   (the carryforward — the
                part implementations get wrong: an UNDER-recovered period
                carries forward in full, with no interest, forever)
6. profitOil  = gross − royalty − costOil
7. contractorProfitOil = profitOil · contractorShare(tranche)
     tranche by R-factor: R = cumulativeContractorRevenue / cumulativeContractorCost
     evaluated on the PRIOR tick's cumulative values (no same-tick circularity —
     pinned), against the content tranche table
8. contractorTax = taxRate · max(0, contractorProfitOil − untaxedAllowances)
                   where the regime taxes profit oil (flag)
Contractor take = costOil + contractorProfitOil − contractorTax
```

**Worked-example fixtures are mandatory** (R13-V4/V5): each shipped regime
carries a hand-computed multi-period example — including an under-recovery
period and a tranche crossing — committed as content-adjacent test data.

### 3.3 Service contract · sliding scale

```text
Service: contractorTake = fee(escalated by cost index) · deliveredVolume
Sliding: royaltyRate = table(dailyRate or R-factor)  — a content table, step
         function, evaluated on prior-tick values (same no-circularity rule)
```

## 4. Reserves — the algorithm the growth loop hangs off

Quarterly, and on demand after material events (R13 risk note):

```text
For each DISCOVERED accumulation:
  1. plan = the sanctioned development, else the best viable template
  2. access gate: a plan blocked by AccessRequirements (SDD-003 §3.0b) ⇒ the
     volumes are CONTINGENT (technology trigger) — finding 51's booking rule
  3. forecast production via the plan's CONTENT TYPE-CURVE (Arps parameters
     per development template) scaled to the recoverable quantile — **the
     solver never runs inside reserves computation** (quarterly full-field
     re-simulation is the trap this line exists to forbid); TRUNCATED at the
     economic limit under current prices and costs
  4. 1P = P90 truncated · 2P = P50 · 3P = P10; no viable plan ⇒ contingent
RRR(year) = Σ additions(2P) / Σ production        (integer volumes, exact)
```

Price sensitivity (SC6) needs no extra code: step 3's truncation moves with
price, so a crash mechanically writes reserves down.

> **R20d.12.34 amendment (finding 208, and an F-4 stop): RRR is reported on
> PROVED, not 2P.** The line above said 2P and finding 208 recorded the decision
> as Proved without either noticing the other — the finding argued from the code
> and the SDD was never opened. That is the conflict F-4 exists for, so it is
> settled here before anything is implemented rather than in the implementation.
>
> **Proved wins on three counts.** `Lending.Redetermine` takes a parameter named
> `provedReserves` and `Bank.Settle` passes `Remaining(...).Proved`, so the
> borrowing base already moves on 1P — an RRR on 2P would score a company on a
> measure its own lender ignores, in the one indicator IR2 names for the
> LIQUIDATION SPIRAL, which is a credit phenomenon before it is a geological one.
> It is also the convention every reporting company follows, which is why the
> lender was written that way. And 2P includes volumes a bank will not lend
> against, so a company could show replacement above one while its facility
> shrank every quarter — the exact false comfort a standing indicator must not
> give.
>
> **The formula is unchanged**, because "additions" already means what the
> implementation computes: additions over a period are the change in booked
> reserves plus what was produced from them, so
>
> ```text
> RRR = (proved_now − proved_then + produced_between) / produced_between
> ```
>
> is the same statement with the identity substituted, and is the form to
> implement because a period's ADDITIONS are not separately recorded anywhere.
>
> **Period: a trailing twelve months** (finding 208). IR2 calls RRR a *standing*
> indicator, and a since-inception ratio converges and stops moving — a company
> sliding into the spiral would watch it drift by thousandths. Twelve months is
> also the rhythm the bank already redetermines on.
>
> **`produced_between` of zero leaves RRR UNDEFINED, and it is published as
> undefined.** A field that produced nothing replaced nothing, and the ratio's
> denominator is what it did not do; reporting 0.0 would state a replacement
> failure that did not happen, and reporting 1.0 would state a success. The
> projection carries `double?` and a host shows "—", which is the same rule §5's
> refusals follow: a fallback answers "unknown" and never invents a number.

## 5. Reserve-based lending

```text
borrowingBase = advanceRate · PV(discountRate, 1P after-fiscal cash flows, N years)
  PV in integer cents, per-tick discount factors from PhysicalConstants table
rate = baseRate + esgSpread(EsgStanding)        — spread table content (08 §5)
Redetermination: quarterly, and immediately on reserves recomputation.
Covenant: debt > borrowingBase ⇒ finance.covenantRisk → cure window (content
ticks) → forced amortisation schedule. Pinned: the bank never calls instantly;
the cure window is the player's warning (IR-consistent).
```

> **R20d review amendment (finding 147) — the shape, declared.** The algorithm
> above was pinned and no type carried it, while `CompanyView.BorrowingBase` and
> `.BorrowingRate` had sat on the read model since the contract passes.
>
> ```csharp
> public sealed record BorrowingTerms(
>     Money BorrowingBase,
>     double Rate,
>     double EsgSpread);      // carried separately: a company that cannot see WHY
>                             // its debt got more expensive cannot act on it
>
> public enum CovenantState { Clear, Curing, Amortising }
>
> public sealed record CovenantStatus(CovenantState State, int TicksRemaining);
>
> public interface IReserveBasedLending
> {
>     ContentId Id { get; }
>     BorrowingTerms Redetermine(
>         SurfaceVolume provedReserves, Money debt, double esgStanding);
>     CovenantStatus Assess(BorrowingTerms terms, Money debt, CovenantStatus previous);
> }
> ```
>
> The model computes and the ledger books — `Assess` returns a status and never
> posts a movement, so the cure-window pin above stays a statement about state
> transitions rather than about money moving.

## 6. Prices

```csharp
// Design 03 §3.2 — random walk ↔ mean-reverting ↔ scripted scenario ↔
// historical replay. The stream is handed IN rather than held: the model must
// draw from `price` and no other, and a model that owned a source could quietly
// draw from the wrong one (D-6, and the R1-V5 independence guarantee).
public interface IPriceModel
{
    ContentId Id { get; }
    Money Advance(Money current, IRandomStream price);
}
```

```text
Mean-reverting (OU in log space, per benchmark):
  ln P(t+1) = ln P(t) + κ·(ln μ − ln P(t)) + σ·ε,   ε ~ N(0,1) from `price` stream
Jumps: with per-tick probability p_jump (content), multiply by J ~ LogNormal —
  drawn from `price`, audited (a shock is a consequential draw, 09 §4.2)
Realised = benchmark + qualityDiff(API, sulphur bands) + locationDiff(network
  distance to the sales point — the generated transport graph prices this)
Gas/NGL: separate benchmarks, same machinery; gas adds a seasonal sine term
  (amplitude content) — the winter premium.
Cost index (ED4): costIndex(t+1) = costIndex(t) · (1 + η·priceYoY(t)/12 + drift)
  applied to day rates, capex classes and service prices; η, drift content.
```

> **R20d.12 amendment (F-4). The cost index divides the year-on-year move by
> twelve, and the version without it was wrong.** Written as
> `costIndex · (1 + η·priceYoY + drift)` and stepped MONTHLY, a year-on-year
> signal is applied twelve times over: a market sitting 50% above where it was a
> year ago lifts costs 17.5% in the first month, then again on the new figure,
> and again — sevenfold across the year rather than 17.5%.
>
> Measured before it was believed. A decade of the shipped market produced an
> index of 1.78 and a company that earned $76M against a $600M target, which
> reads as a punishing market and is an arithmetic error. `η·yoy` is an ANNUAL
> adjustment, so a monthly tick applies a twelfth of it; the same decade now
> gives 1.18.
>
> The recurrence is otherwise unchanged and η keeps its meaning: the fraction of
> a year's price move that ends up in the price of a rig.
>
> **A FLOOR belongs with it**, and it is not a tuning fudge. Costs are sticky
> downwards — rigs are stacked rather than scrapped and crews are kept on — so
> without one a long enough slump drives the index towards zero and makes
> everything free, turning the worst market in the game into the best time to
> build anything.

## 7. Contracts, hedges, insurance

```text
Take-or-pay: shortfall = max(0, committed − delivered) per window;
  penalty = shortfall · penaltyRate  (a Movement, category Penalty)
Hedge (collar): settle per tick = volume · (clamp(benchmark, floor, cap) − benchmark)
Insurance (ED6): premium(class) = exposure(class) · rate(record) per year;
  a claim pays min(loss − deductible, limit); record re-rates premiums at
  renewal from the audited incident history — the barrier model prices the policy.
```

> **R13.3 amendment (finding 250): the take-or-pay half, typed.** The tracker
> named `ISalesContract` — spot, term, take-or-pay, hedge — as one task and
> this formula block as its whole specification; neither states a shape a
> compiler can check, and R13's own verification table assigns exactly one
> id to it, R13-V10, "take-or-pay penalty". Under F-1 the take-or-pay half is
> pinned here; spot and term need no new mechanism (every sale already prices
> off the composed `IPriceModel` through the one custody-transfer door — that
> IS a spot sale), and hedges and insurance stay open, each its own task, not
> fabricated alongside this one for symmetry with a task name.
>
> `ISalesContract` itself is corrected to a concrete type, the same
> correction findings 117 and 246 already made for compression and pumping:
> nothing else in this engine implements "a sales contract" polymorphically,
> so a plugin interface would be speculative generality rather than a seam
> anything reaches through.
>
> ```csharp
> // Company layer, beside LicenceTerms/Licence — the same shape: terms are
> // fixed content, progress is state that resets on its own clock.
> public sealed record TakeOrPayTerms(
>     SurfaceVolume CommittedVolume,   // per window, stock-tank m³
>     int WindowMonths,
>     Money PenaltyRate);              // per m³ of shortfall
>
> public sealed record TakeOrPayAssessment(
>     SurfaceVolume Delivered, SurfaceVolume Shortfall, Money Penalty);
>
> public sealed class TakeOrPayContract : IStateOwner
> {
>     public void RecordDelivery(SurfaceVolume delivered);   // every tick
>     public TakeOrPayAssessment? AssessAt(Tick now);         // null off-window
> }
> ```
>
> **Assessed on a stored window boundary, not `elapsed % WindowMonths`.**
> `Licence.AssessAt` re-derives its deadlines from fixed `Terms`, which a
> take-or-pay window cannot: the boundary itself ADVANCES each time it is
> crossed, so it is state (`_windowEndsAt`), captured and restored like
> `Licence.IsLive` — a modulo against a restored tick would still be correct
> arithmetic, but the boundary would no longer be *stored*, which is the
> difference between a fact and a recomputation of one law L5 cares about.
>
> **Delivered is read from `ProductionLoop.ProducedThisTick`**, not
> re-derived from the audit trail the way `IsCustodyTransfer` checks a
> cause: the loop already computes this tick's custody-transferred volume to
> build that same audit entry (`RecordCustody`), and a second computation of
> the identical fact from its own audit record would be two owners of one
> number rather than one reading the other's output.
>
> **Posted the same way the licence bond is**: `Account.Penalty` against
> `Account.Cash`, `MovementCategory.Contractual` — both already declared and
> already proven end to end by the bond forfeit, so this needed no new
> ledger vocabulary, only a second poster of the existing kind.

## 8. Error surface

| Situation | Response |
|---|---|
| Trial balance off by one cent | INV2 — halt (integers make this a real bug, never rounding) |
| Regime table gaps (tranche/sliding ranges not covering domain) | Content fault at load |
| Negative costPool, negative carryforward | Invariant fault — the algorithms above cannot produce them; if seen, the code diverged from this SDD |
| Revenue without custody cause | Architecture test failure (R13-V2) |

## 9. Test mapping

R13-V1 (INV2 exact) · V2 (revenue provenance) · V3/MB6 (lifting cost end-to-
end) · V4/V5 (**fixture worked examples** — §3's algorithms vs hand arithmetic,
incl. under-recovery and tranche crossing) · V6 (reserves classes = §4) · V7
(SC6 via truncation) · V8 (RRR exact) · V9 (provision covers cost) · V10
(take-or-pay) · V11 (interest splits sum to 100% in integers) · V12
(restructuring) · V13 (cost-index elasticity) · new **R13-V14**: UoP
depreciation remainder-carry leaks no cents over an asset's life.

## 10. Open items

| # | Item | Trigger |
|---|---|---|
| S009-1 | Ring-fencing rules (losses/cost pools per licence vs consolidated) — start per-licence, the stricter and simpler | R13.4 review |
| S009-2 | Discount rate for reserves PV: fixed content vs company cost of capital | R13.7 — start fixed (SEC-style convention), revisit with ED1 sign-off |
