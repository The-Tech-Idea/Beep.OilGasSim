# Beep Oil and Gas Sim

## Economy, Finance, and Market Design

**Document ID:** BOGS-GDD-004
**Version:** 0.1
**Status:** Draft
**Parent Document:** BOGS-GDD-001 — Master Game Design Document
**Related Documents:**

* BOGS-GDD-002 — Gameplay Systems Design
* BOGS-GDD-003 — Oil and Gas Lifecycle Simulation Design
  **Project Name:** Beep Oil and Gas Sim
  **Short Name:** Beep O&G Sim

---

# 1. Purpose

This document defines the economic, financial, and market systems for Beep Oil and Gas Sim.

The economy system controls how players earn money, spend money, finance projects, manage debt, survive commodity price changes, value assets, and compete financially.

The goal is to make the player feel like they are managing a real oil and gas company, while keeping the rules simple enough for gameplay.

This document defines:

* Company financial model
* Cash flow
* CAPEX
* OPEX
* Revenue
* Debt
* Credit risk
* Commodity prices
* Hedging
* Taxes and royalties
* Asset valuation
* Company valuation
* Final scoring contribution
* Market events
* MVP economy rules

---

# 2. Economy Design Goals

The economy should create meaningful strategic pressure.

Players should constantly ask:

```text
Can I afford this?
Should I borrow?
Should I drill or preserve cash?
Should I hedge production?
Should I sell this asset?
Should I develop now or wait?
Will this asset pay back before the match ends?
Can I afford abandonment later?
```

The economic system should reward:

* Good capital discipline
* Smart exploration spending
* Timely development
* Balanced debt usage
* Strong production management
* Risk management
* Responsible abandonment planning

It should punish:

* Overbidding
* Drilling too many risky wells
* Overbuilding facilities
* Ignoring debt
* Ignoring abandonment liability
* Poor timing during market downturns

---

# 3. Core Financial Resources

Each company should track the following financial resources.

## 3.1 Cash

Cash is the company’s available money.

Cash is used for:

* License bids
* Geological studies
* Seismic
* Drilling
* Development
* Production optimization
* Debt repayment
* Abandonment
* HSE actions
* Asset purchases

If cash becomes negative, the company enters financial distress.

---

## 3.2 Debt

Debt allows the player to fund large projects.

Debt should be useful but risky.

Debt affects:

* Interest payments
* Credit rating
* Final score
* Ability to borrow more
* Investor confidence
* Bankruptcy risk in future versions

---

## 3.3 Revenue

Revenue comes mainly from oil and gas sales.

For MVP, revenue comes from oil only.

```text
Revenue = Produced Oil Volume × Realized Oil Price
```

Later versions can include:

* Gas revenue
* Condensate revenue
* LNG revenue
* Pipeline tariffs
* Carbon storage revenue
* Asset sale revenue
* Government incentives

---

## 3.4 CAPEX

CAPEX is capital expenditure.

CAPEX is spent on long-term investments:

* License acquisition
* Seismic
* Exploration wells
* Appraisal wells
* Field development
* Facilities
* Pipelines
* Production upgrades
* Abandonment preparation

In the game, CAPEX reduces cash immediately or over several turns.

---

## 3.5 OPEX

OPEX is operating expenditure.

OPEX is the recurring cost of running assets.

OPEX includes:

* Field operations
* Maintenance
* Staff
* Logistics
* Facility operation
* Water handling
* Insurance
* Environmental monitoring

OPEX is deducted every turn from producing or active assets.

---

## 3.6 Abandonment Liability

Abandonment liability is the future cost required to safely close assets.

It should appear on the company balance sheet as a negative value.

Players may delay abandonment, but unresolved liability reduces final score.

---

# 4. Company Financial State

Recommended C# model:

```csharp
public sealed class CompanyFinance
{
    public Guid CompanyId { get; set; }

    public decimal Cash { get; set; }
    public decimal Debt { get; set; }

    public decimal RevenueThisTurn { get; set; }
    public decimal OpexThisTurn { get; set; }
    public decimal CapexThisTurn { get; set; }
    public decimal InterestThisTurn { get; set; }
    public decimal TaxThisTurn { get; set; }

    public decimal NetIncomeThisTurn { get; set; }
    public decimal FreeCashFlowThisTurn { get; set; }

    public decimal AssetValue { get; set; }
    public decimal AbandonmentLiability { get; set; }

    public int CreditRating { get; set; }
    public int InvestorConfidence { get; set; }
}
```

Recommended MVP starting values:

```text
Starting Cash: $500M
Starting Debt: $0
Starting Credit Rating: 70/100
Starting Investor Confidence: 60/100
Starting Revenue: $0
Starting Production: 0
```

---

# 5. Turn Cash Flow

Each turn, the server calculates company cash flow.

## 5.1 Basic Cash Flow Formula

```text
Ending Cash =
Starting Cash
+ Revenue
+ Asset Sale Income
+ New Debt
- CAPEX
- OPEX
- Interest
- Tax
- License Fees
- Abandonment Spend
- Penalties
```

## 5.2 Free Cash Flow

```text
Free Cash Flow =
Revenue
- OPEX
- CAPEX
- Interest
- Tax
- Abandonment Spend
```

Free cash flow should be shown in the company dashboard.

---

# 6. Revenue System

## 6.1 MVP Oil Revenue

For MVP, only oil revenue is required.

```text
Oil Revenue =
Produced Oil This Turn × Realized Oil Price
```

Example:

```text
Produced Oil: 3.5 MMbbl
Oil Price: $75/bbl

Revenue = 3,500,000 × 75
Revenue = $262.5M
```

## 6.2 Produced Oil Volume

For a 6-month turn:

```text
Produced Oil =
Daily Production Rate × 182.5 × Uptime
```

Example:

```text
Daily Production: 20,000 bopd
Uptime: 95%

Produced Oil = 20,000 × 182.5 × 0.95
Produced Oil = 3,467,500 bbl
```

---

# 7. Commodity Price System

Commodity prices create market drama.

For MVP, only oil price is required.

## 7.1 Oil Price Attributes

```csharp
public sealed class CommodityMarket
{
    public int TurnNumber { get; set; }

    public decimal OilPrice { get; set; }
    public decimal GasPrice { get; set; }

    public MarketTrend Trend { get; set; }
    public double Volatility { get; set; }

    public string MarketSummary { get; set; } = "";
}
```

```csharp
public enum MarketTrend
{
    Stable,
    Bullish,
    Bearish,
    Volatile,
    Crash,
    Boom
}
```

---

## 7.2 MVP Oil Price Range

Recommended MVP values:

```text
Starting Oil Price: $75/bbl
Minimum Normal Price: $45/bbl
Maximum Normal Price: $110/bbl
Crash Price Range: $30–45/bbl
Boom Price Range: $110–140/bbl
```

---

## 7.3 Oil Price Movement

Each turn, the oil price may change.

Simplified formula:

```text
New Oil Price =
Previous Oil Price
+ Trend Modifier
+ Random Volatility
+ Event Modifier
```

Example modifiers:

```text
Stable trend: -$3 to +$3
Bullish trend: +$2 to +$8
Bearish trend: -$8 to -$2
Volatile trend: -$15 to +$15
Crash event: -$25 to -$40
Boom event: +$20 to +$35
```

---

## 7.4 Market Forecast

Players should receive imperfect forecasts.

Example:

```text
Market Outlook:
Analysts expect oil prices to remain stable to bullish next turn.
Confidence: Medium
Expected range: $72–86/bbl
```

The forecast should not always be correct.

This creates decisions around hedging and investment timing.

---

# 8. Hedging System

Hedging allows players to reduce price risk.

## 8.1 Hedge Action

```text
Action: Hedge Production
Target: Company
Effect: Locks price for a percentage of next turn production
Cost: Small transaction fee or reduced upside
```

## 8.2 MVP Hedge Options

For MVP, allow:

```text
Hedge 25% of next turn production
Hedge 50% of next turn production
Hedge 75% of next turn production
```

The hedged price is based on current market conditions.

Example:

```text
Current oil price: $75/bbl
Hedge price: $72/bbl
Hedged amount: 50% of next turn production
```

## 8.3 Hedge Revenue Formula

```text
Revenue =
Hedged Volume × Hedge Price
+
Unhedged Volume × Market Price
```

Example:

```text
Next turn production: 4 MMbbl
Hedged: 50% at $72/bbl
Market price: $55/bbl

Hedged revenue = 2,000,000 × 72 = $144M
Unhedged revenue = 2,000,000 × 55 = $110M
Total revenue = $254M
```

Without hedge:

```text
4,000,000 × 55 = $220M
```

The hedge protected $34M.

---

## 8.4 Hedge Tradeoff

If prices rise, hedging limits upside.

Example:

```text
Production: 4 MMbbl
Hedged 50% at $72/bbl
Market price rises to $95/bbl

Hedged revenue = 2,000,000 × 72 = $144M
Unhedged revenue = 2,000,000 × 95 = $190M
Total revenue = $334M
```

Without hedge:

```text
4,000,000 × 95 = $380M
```

The hedge cost the player $46M of upside.

---

# 9. CAPEX System

CAPEX represents investment.

## 9.1 CAPEX Categories

```text
License CAPEX
Exploration CAPEX
Appraisal CAPEX
Development CAPEX
Facility CAPEX
Optimization CAPEX
Abandonment CAPEX
```

## 9.2 MVP CAPEX Values

Initial placeholder balancing:

```text
Geological Study: $5M
2D Seismic: $15M
Exploration Well: $40M
Appraisal Well: $30M
Small Development: $120M
Standard Development: $220M
Large Development: $350M
Optimize Field: $20M
Abandon Field: variable
```

## 9.3 Development CAPEX Payment

Two options are possible.

### Simple MVP Method

Pay development CAPEX immediately when approved.

```text
Approve Standard Development:
Cash decreases by $220M immediately.
Construction takes 3 turns.
```

### More Realistic Method

Spread CAPEX over construction turns.

```text
Standard Development:
Total CAPEX: $220M
Duration: 3 turns
Turn 1 spend: $88M
Turn 2 spend: $88M
Turn 3 spend: $44M
```

Recommended MVP:

```text
Use immediate payment for simplicity.
```

Recommended post-MVP:

```text
Use staged CAPEX.
```

---

# 10. OPEX System

OPEX is deducted each turn.

## 10.1 Field OPEX

Each producing field has OPEX per turn.

```text
Field Profit Before Tax =
Revenue - OPEX
```

## 10.2 OPEX Types

```text
Fixed OPEX
Variable OPEX
Water handling OPEX
Maintenance OPEX
Logistics OPEX
Environmental monitoring OPEX
```

## 10.3 MVP OPEX Formula

```text
OPEX This Turn =
Fixed Field OPEX
+
Produced Volume × Variable OPEX Per Barrel
```

Example:

```text
Fixed OPEX: $10M per turn
Variable OPEX: $8/bbl
Production: 3 MMbbl

OPEX = $10M + 3,000,000 × $8
OPEX = $34M
```

---

# 11. Debt and Credit System

Debt allows growth but increases financial risk.

## 11.1 Take Debt Action

```text
Action: Take Debt
Effect: Adds cash and debt
Cost: Interest payments
Risk: Lower credit rating
```

## 11.2 MVP Debt Options

```text
Borrow $50M
Borrow $100M
Borrow $200M
```

## 11.3 Interest Rate

Interest rate depends on credit rating.

Recommended MVP:

```text
Credit Rating 80–100: 5% annual interest
Credit Rating 60–79: 8% annual interest
Credit Rating 40–59: 12% annual interest
Credit Rating 20–39: 18% annual interest
Credit Rating 0–19: 25% annual interest
```

Since each turn is 6 months:

```text
Turn Interest = Debt × Annual Interest Rate × 0.5
```

Example:

```text
Debt: $200M
Annual interest: 8%
Turn interest = 200M × 0.08 × 0.5
Turn interest = $8M
```

---

## 11.4 Credit Rating

Credit rating is from 0 to 100.

Credit rating is affected by:

Positive:

```text
Positive cash flow
Low debt
Strong production
Debt repayment
High reputation
```

Negative:

```text
High debt
Negative cash flow
Missed payments
Large dry-hole losses
Safety incidents
Unfunded abandonment liability
```

## 11.5 Debt Limit

Recommended MVP rule:

```text
Maximum Debt = 2 × Asset Value + 100M
```

This allows distressed companies to borrow, but not infinitely.

Simpler MVP rule:

```text
Maximum Debt = $500M
```

Recommended for first prototype:

```text
Use fixed maximum debt of $500M.
```

---

# 12. Financial Distress

Financial distress occurs when a company has insufficient cash.

## 12.1 Trigger

```text
Cash < $0
```

## 12.2 MVP Distress Rule

If cash becomes negative:

```text
1. Company automatically takes emergency debt.
2. Emergency debt has high interest.
3. Credit rating decreases.
4. Investor confidence decreases.
```

Example:

```text
Cash after turn: -$30M
Emergency debt issued: $50M
New cash: $20M
Credit rating: -10
```

## 12.3 Post-MVP Distress Options

Later versions can include:

* Forced asset sale
* Bankruptcy
* Government bailout
* Merger
* Loss of operator status
* Investor revolt

For MVP, avoid player elimination.

---

# 13. Tax and Royalty System

Taxes and royalties reduce profit.

## 13.1 MVP Royalty

Use a simple royalty on revenue.

```text
Royalty = Revenue × Royalty Rate
```

Recommended MVP rate:

```text
Royalty Rate = 10%
```

Example:

```text
Revenue: $200M
Royalty: $20M
```

## 13.2 Post-MVP Fiscal System

Later versions may include:

* Corporate income tax
* Production sharing contract
* Royalty
* Cost recovery
* Profit oil split
* Windfall tax
* Carbon tax
* License fees
* Local content cost

---

# 14. License Fees

License blocks should have holding costs.

This discourages players from buying too many blocks without activity.

## 14.1 MVP License Fee

```text
Annual License Fee = $2M per block
Turn License Fee = $1M per block
```

Example:

```text
Company owns 5 blocks.
Turn license fee = $5M.
```

## 14.2 License Expiry

Optional for MVP.

Post-MVP rule:

```text
License expires if not drilled or extended after X turns.
```

---

# 15. Asset Sales

Players may sell assets to generate cash.

## 15.1 Sell Asset Action

```text
Action: Sell Asset
Target: License, discovery, or field
Effect: Receives cash
Cost: Lose future upside
```

## 15.2 MVP Asset Sale

For MVP, asset sale is to the market/NPC.

Sale price is based on estimated value, not hidden truth.

```text
Sale Price =
Estimated Asset Value × Market Discount
```

Recommended market discount:

```text
License: 50–80% of estimated option value
Discovery: 60–90% of estimated value
Producing field: 70–100% of estimated value
Late-life field: 30–70% of estimated value
```

## 15.3 Strategic Use

Asset sales help players:

* Recover from cash shortage
* Fund development
* Exit marginal projects
* Reduce abandonment exposure
* Focus portfolio

---

# 16. Asset Valuation

Asset valuation is important for leaderboard and financing.

## 16.1 Asset Types

Assets have different valuation methods:

```text
License block
Prospect
Discovery
Commercial field
Producing field
Late-life field
Infrastructure
```

---

## 16.2 License Block Value

A license block has option value.

Simplified formula:

```text
License Value =
Estimated Chance of Success
× Estimated Resource Value
× Confidence Modifier
- Expected Exploration Cost
```

Example:

```text
Chance of success: 30%
Estimated resource value: $500M
Confidence modifier: 0.7
Expected exploration cost: $40M

Value = 0.30 × 500M × 0.7 - 40M
Value = $65M
```

---

## 16.3 Discovery Value

Discovery value depends on estimated reserves and commerciality.

```text
Discovery Value =
Estimated Recoverable Volume
× Value Per Barrel In Ground
× Commerciality Modifier
× Confidence Modifier
```

Example:

```text
Estimated volume: 100 MMbbl
Value per barrel in ground: $4
Commerciality modifier: 0.8
Confidence modifier: 0.6

Value = 100M × 4 × 0.8 × 0.6
Value = $192M
```

---

## 16.4 Producing Field Value

Producing field value is based on expected future cash flow.

Simplified MVP formula:

```text
Producing Field Value =
Remaining Recoverable Volume
× Netback Per Barrel
× Recovery Confidence
- Abandonment Liability
```

Where:

```text
Netback Per Barrel =
Oil Price
- OPEX Per Barrel
- Royalty Per Barrel
```

Example:

```text
Remaining reserves: 40 MMbbl
Oil price: $75/bbl
OPEX: $15/bbl
Royalty: $7.5/bbl
Netback: $52.5/bbl
Recovery confidence: 0.25
Abandonment liability: $40M

Value = 40M × 52.5 × 0.25 - 40M
Value = $485M
```

The recovery confidence factor prevents field values from becoming unrealistically huge in simplified gameplay.

---

# 17. Company Valuation

Company valuation determines leaderboard rank.

## 17.1 MVP Company Value

```text
Company Value =
Cash
- Debt
+ License Value
+ Discovery Value
+ Producing Field Value
- Abandonment Liability
+ Reputation Bonus
```

## 17.2 Reputation Bonus

```text
Reputation Bonus =
(Reputation - 50) × $2M
```

Example:

```text
Reputation: 70
Bonus = (70 - 50) × 2M
Bonus = $40M
```

If reputation is below 50, this becomes a penalty.

---

# 18. Final Score

Final score is calculated at match end.

## 18.1 Recommended Final Score

```text
Final Score =
Cash
- Debt
+ Asset Value
+ Proven Reserves Value
+ Reputation Bonus
- Safety Penalty
- Environmental Penalty
- Unfunded Abandonment Penalty
```

## 18.2 Unfunded Abandonment Penalty

```text
Unfunded Abandonment Penalty =
Remaining Abandonment Liability × 1.5
```

Example:

```text
Remaining liability: $60M
Penalty = $90M
```

This encourages responsible abandonment.

---

# 19. Market Event System

Market events create volatility.

## 19.1 Event Categories

```text
Oil price events
Gas price events
Service cost events
Financial market events
Fiscal/regulatory events
Infrastructure events
Demand events
```

## 19.2 MVP Market Events

MVP should include:

```text
Oil Price Crash
Oil Price Boom
Rig Cost Inflation
Service Cost Drop
Tax Increase
Pipeline Outage
Investor Optimism
Investor Panic
```

---

## 19.3 Example Event Cards

### Oil Price Crash

```text
Event: Oil Price Crash
Effect:
- Oil price decreases by $25–40/bbl
- Investor confidence decreases by 5
- Hedged companies are protected
Duration: 1–3 turns
```

### Oil Price Boom

```text
Event: Oil Price Boom
Effect:
- Oil price increases by $20–35/bbl
- Producing companies gain advantage
- Hedged companies receive less upside
Duration: 1–2 turns
```

### Rig Cost Inflation

```text
Event: Rig Cost Inflation
Effect:
- Exploration and appraisal well costs increase by 20%
Duration: 2 turns
```

### Investor Panic

```text
Event: Investor Panic
Effect:
- Credit rating threshold becomes stricter
- New debt interest rates increase
Duration: 2 turns
```

---

# 20. Service Cost Index

Service cost affects drilling and development.

## 20.1 Service Cost Formula

```text
Final Action Cost =
Base Cost × Basin Service Cost Index × Market Event Modifier
```

Example:

```text
Base exploration well cost: $40M
Service cost index: 1.2
Rig inflation event: 1.2

Final cost = 40M × 1.2 × 1.2
Final cost = $57.6M
```

## 20.2 MVP Simplification

For MVP, use fixed costs first.

Add service cost index after the basic economy feels balanced.

---

# 21. Investor Confidence

Investor confidence measures market trust in the company.

## 21.1 Effects

High investor confidence:

```text
Lower borrowing cost
Better asset valuation
Positive final score modifier
```

Low investor confidence:

```text
Higher borrowing cost
Lower company valuation
Financial distress risk
```

## 21.2 MVP Rule

Investor confidence can be merged with credit rating in MVP.

Recommended MVP:

```text
Use Credit Rating only.
Add Investor Confidence later.
```

---

# 22. Reputation and Economy

Reputation has financial value.

Good reputation can:

* Reduce regulatory delays
* Lower environmental penalties
* Improve license tie-breakers
* Increase final valuation
* Improve credit rating

Bad reputation can:

* Increase license costs
* Increase penalties
* Lower investor confidence
* Increase abandonment scrutiny

Recommended MVP:

```text
Reputation affects final score and some event outcomes.
```

---

# 23. Economy UI Requirements

The company dashboard should show financial health clearly.

## 23.1 MVP Dashboard

```text
Cash
Debt
Credit Rating
Revenue This Turn
OPEX This Turn
CAPEX This Turn
Net Cash Flow
Production
Asset Value
Company Value
Abandonment Liability
Rank
```

## 23.2 Financial Warning Indicators

The UI should warn players when:

```text
Cash is low
Debt is high
Credit rating is falling
Development would consume too much cash
Abandonment liability is high
Oil price forecast is bearish
Asset is losing money
```

Example:

```text
Warning:
Approving this development will reduce cash from $260M to $40M.
Your company may need debt if oil prices fall.
```

---

# 24. AI Integration

The AI Command Center should help players understand financial decisions.

## 24.1 CFO Advisor

The CFO AI should answer:

```text
Can we afford this development?
Should we borrow?
Should we repay debt?
Should we hedge production?
Which asset is hurting our cash flow?
What is our biggest financial risk?
```

## 24.2 Market Analyst

The Market Analyst should answer:

```text
Is the oil price outlook good?
Should we delay development?
Should we hedge?
Which fields are most exposed to price risk?
```

## 24.3 AI Must Not Cheat

The AI can use:

```text
Current company cash
Known production
Known forecast
Known asset estimates
Known debt
Known market data
Known abandonment liability
```

The AI must not use:

```text
Future price events
Hidden geology
Competitor private financials
Undiscovered asset values
```

---

# 25. MVP Economy Rules

For the first playable version, use the following rules.

## 25.1 Starting Company

```text
Starting Cash: $500M
Starting Debt: $0
Credit Rating: 70/100
Reputation: 50/100
```

## 25.2 Commodity

```text
Oil only
Starting Oil Price: $75/bbl
Normal price range: $45–110/bbl
```

## 25.3 Costs

```text
Geological Study: $5M
2D Seismic: $15M
Exploration Well: $40M
Appraisal Well: $30M
Small Development: $120M
Standard Development: $220M
Large Development: $350M
Optimize Field: $20M
License Fee: $1M per block per turn
```

## 25.4 Revenue

```text
Revenue = Produced Oil × Realized Oil Price
```

## 25.5 OPEX

```text
OPEX = Fixed Field OPEX + Produced Volume × Variable OPEX
```

Recommended MVP:

```text
Fixed Field OPEX: $5M–15M per turn
Variable OPEX: $8–18/bbl
```

## 25.6 Royalty

```text
Royalty Rate: 10% of revenue
```

## 25.7 Debt

```text
Maximum Debt: $500M
Interest based on credit rating
Emergency debt if cash goes negative
```

## 25.8 Hedging

```text
Can hedge 25%, 50%, or 75% of next turn production
Hedge price slightly below current oil price
```

## 25.9 Valuation

```text
Company Value =
Cash
- Debt
+ Asset Value
- Abandonment Liability
+ Reputation Bonus
```

---

# 26. Example Economy Turn

## Starting Turn

```text
Company: Beep Energy
Cash: $220M
Debt: $100M
Oil Price: $75/bbl
Production: 20,000 bopd
Uptime: 95%
Field OPEX: $32M
Development CAPEX this turn: $0
License Fees: $4M
```

## Production

```text
Produced Oil =
20,000 × 182.5 × 0.95
= 3,467,500 bbl
```

## Revenue

```text
Revenue =
3,467,500 × $75
= $260.06M
```

## Royalty

```text
Royalty =
$260.06M × 10%
= $26.01M
```

## Interest

```text
Debt: $100M
Annual interest: 8%
Turn interest:
100M × 0.08 × 0.5 = $4M
```

## Ending Cash

```text
Ending Cash =
220M
+ 260.06M
- 32M
- 26.01M
- 4M
- 4M

Ending Cash = $414.05M
```

The player now has strong cash flow and can consider development, debt repayment, or new exploration.

---

# 27. Example Bad Economy Turn

## Starting Turn

```text
Cash: $60M
Debt: $300M
Oil Price: $45/bbl
Production: 10,000 bopd
OPEX: $35M
License Fees: $8M
Interest: $18M
```

## Production

```text
Produced Oil =
10,000 × 182.5 × 0.90
= 1,642,500 bbl
```

## Revenue

```text
Revenue =
1,642,500 × $45
= $73.91M
```

## Royalty

```text
Royalty =
$7.39M
```

## Ending Cash

```text
Ending Cash =
60M
+ 73.91M
- 35M
- 7.39M
- 18M
- 8M

Ending Cash = $65.52M
```

The company survives but has limited flexibility.

Recommended AI message:

```text
Your company is financially constrained.
Avoid new exploration wells this turn unless you sell an asset or hedge production.
Debt is already high, and another oil price drop could trigger distress.
```

---

# 28. Balancing Guidelines

## 28.1 Exploration Must Be Affordable but Risky

Players should be able to drill several wells in a match, but not spam drilling without consequences.

Recommended:

```text
Exploration well cost should be 8–12% of starting cash.
```

With $500M starting cash:

```text
Exploration well = $40M to $60M
```

## 28.2 Development Must Be a Major Decision

Development should feel expensive.

Recommended:

```text
Standard development should cost 40–50% of starting cash.
```

With $500M starting cash:

```text
Standard development = $200M to $250M
```

## 28.3 Production Must Feel Rewarding

A successful producing field should be able to transform the company.

A medium field should pay back development cost within several turns if prices are healthy.

## 28.4 Debt Must Be Useful but Dangerous

Debt should help players fund growth.

But overusing debt should create:

```text
High interest
Lower credit rating
Final score penalty
Financial distress risk
```

## 28.5 Hedging Must Be Situational

Hedging should not always be correct.

It should protect during crashes but limit gains during booms.

---

# 29. Design Risks

## 29.1 Economy Too Punishing

If players go broke early, the game becomes frustrating.

Solution:

```text
Use emergency debt.
Allow asset sales.
Avoid permanent elimination in MVP.
Give AI financial warnings.
```

## 29.2 Economy Too Easy

If money is too abundant, decisions lose meaning.

Solution:

```text
Increase license fees.
Increase development cost.
Add OPEX.
Add abandonment liability.
Add commodity price volatility.
```

## 29.3 Players Ignore Abandonment

If abandonment only matters at the end, players may ignore it.

Solution:

```text
Show liability on dashboard.
Add final score penalty.
Add regulator events.
Allow early abandonment planning to reduce cost.
```

## 29.4 Hedging Too Complex

Hedging can confuse players.

Solution:

```text
Start with three simple hedge buttons.
Show projected revenue with and without hedge.
Let AI CFO explain the tradeoff.
```

---

# 30. Open Questions

1. Should development CAPEX be paid immediately or spread across construction turns?
2. Should MVP include gas prices or oil only?
3. Should taxes be revenue-based royalty only at first?
4. Should debt be capped at a fixed number or based on asset value?
5. Should hedging consume an action slot?
6. Should credit rating affect license bids?
7. Should license fees exist in MVP?
8. Should asset sale be player-to-market only or player-to-player?
9. Should company value be fully visible to all players?
10. Should oil price forecasts be public or require paid market analysis?

---

# 31. Recommended MVP Economy Decision

For MVP, use the following economy model:

```text
Commodity:
- Oil only

Starting finance:
- Cash: $500M
- Debt: $0
- Credit rating: 70
- Reputation: 50

Turn time:
- 6 months

Main costs:
- Geological Study: $5M
- 2D Seismic: $15M
- Exploration Well: $40M
- Appraisal Well: $30M
- Small Development: $120M
- Standard Development: $220M
- Large Development: $350M
- Optimize Field: $20M

Revenue:
- Produced Oil × Oil Price

Fiscal:
- 10% royalty on revenue

Debt:
- Max debt: $500M
- Interest based on credit rating
- Emergency debt prevents elimination

Hedging:
- 25%, 50%, or 75% of next turn production

Valuation:
- Cash - Debt + Asset Value - Abandonment Liability + Reputation Bonus
```

This economy is simple enough for the first implementation while still creating strong business decisions.
