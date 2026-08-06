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

## 3. Fiscal regimes — the exact algorithms

Evaluated per licence per tick, in this order, all integer:

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

## 6. Prices

```text
Mean-reverting (OU in log space, per benchmark):
  ln P(t+1) = ln P(t) + κ·(ln μ − ln P(t)) + σ·ε,   ε ~ N(0,1) from `price` stream
Jumps: with per-tick probability p_jump (content), multiply by J ~ LogNormal —
  drawn from `price`, audited (a shock is a consequential draw, 09 §4.2)
Realised = benchmark + qualityDiff(API, sulphur bands) + locationDiff(network
  distance to the sales point — the generated transport graph prices this)
Gas/NGL: separate benchmarks, same machinery; gas adds a seasonal sine term
  (amplitude content) — the winter premium.
Cost index (ED4): costIndex(t+1) = costIndex(t) · (1 + η·priceYoY(t) + drift)
  applied to day rates, capex classes and service prices; η, drift content.
```

## 7. Contracts, hedges, insurance

```text
Take-or-pay: shortfall = max(0, committed − delivered) per window;
  penalty = shortfall · penaltyRate  (a Movement, category Penalty)
Hedge (collar): settle per tick = volume · (clamp(benchmark, floor, cap) − benchmark)
Insurance (ED6): premium(class) = exposure(class) · rate(record) per year;
  a claim pays min(loss − deductible, limit); record re-rates premiums at
  renewal from the audited incident history — the barrier model prices the policy.
```

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
