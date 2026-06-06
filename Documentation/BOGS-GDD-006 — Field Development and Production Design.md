# Beep Oil and Gas Sim

## Field Development and Production Design

**Document ID:** BOGS-GDD-006
**Version:** 0.1
**Status:** Draft
**Parent Document:** BOGS-GDD-001 — Master Game Design Document
**Related Documents:**

* BOGS-GDD-002 — Gameplay Systems Design
* BOGS-GDD-003 — Oil and Gas Lifecycle Simulation Design
* BOGS-GDD-004 — Economy, Finance, and Market Design
* BOGS-GDD-005 — Exploration and Geology Design

**Project Name:** Beep Oil and Gas Sim
**Short Name:** Beep O&G Sim

---

# 1. Purpose

This document defines the field development and production systems for Beep Oil and Gas Sim.

After a player discovers hydrocarbons, the next major challenge is deciding whether and how to develop the discovery.

This document covers:

* Discovery maturation
* Appraisal
* Commerciality
* Development concepts
* CAPEX and construction
* Facilities
* Wells
* Production startup
* Production decline
* OPEX
* Optimization
* Downtime
* Production events
* Production reporting
* MVP production rules

The goal is to make field development feel like a major strategic investment and production feel like the reward for smart risk-taking.

---

# 2. Design Goals

## 2.1 Make Development a Big Decision

Approving a field development should feel important.

Development requires major capital and can transform the company if successful.

The player should ask:

```text
Is this discovery big enough?
Can we afford development?
Should we appraise more?
Should we choose a small, fast development or a large, expensive one?
Will production pay back before the match ends?
```

---

## 2.2 Reward Good Timing

Developing too early can be risky.

Waiting too long can allow competitors to gain advantage.

Good players should balance:

* Confidence
* Cash
* Market outlook
* Development cost
* Production timing
* Match length
* Competitor pressure

---

## 2.3 Keep Engineering Simple

The system should use real field development concepts, but not become a detailed engineering simulator.

The player should understand:

* CAPEX
* OPEX
* Development duration
* Facility capacity
* Production rate
* Decline
* Uptime
* Optimization
* Abandonment liability

The first version should avoid detailed reservoir engineering, facility design, or wellbore simulation.

---

## 2.4 Make Production Satisfying

Production is the payoff.

When a field starts producing, the player should see:

* Revenue growth
* Production charts
* Cash flow improvement
* Company value increase
* Leaderboard movement

Producing fields should become valuable assets that require ongoing management.

---

## 2.5 Create Mid-Game Decisions

After fields start producing, the game should not become passive.

Players should continue deciding:

```text
Should we optimize this field?
Should we maintain facilities?
Should we drill more producers?
Should we hedge production?
Should we sell the field?
Should we prepare abandonment?
```

---

# 3. Field Development Lifecycle

The field development lifecycle is:

```text
Discovery
    ↓
Appraisal
    ↓
Commerciality Review
    ↓
Development Concept Selection
    ↓
Final Investment Decision
    ↓
Construction
    ↓
First Oil / First Gas
    ↓
Production
    ↓
Optimization
    ↓
Decline
    ↓
Late-Life
```

For MVP, the lifecycle should be simplified:

```text
Discovery
    ↓
Appraisal
    ↓
Approve Development
    ↓
Construction
    ↓
Production
    ↓
Optimize
    ↓
Late-Life
```

---

# 4. Discovery to Field Conversion

A discovery does not automatically become a producing field.

The player must decide whether to invest.

## 4.1 Discovery State

A discovery has:

```text
Estimated recoverable volume
Confidence
Fluid type
Commerciality score
Development difficulty
Distance to infrastructure
Main risk
Estimated development cost
Estimated production potential
```

## 4.2 Discovery Model

```csharp
public sealed class DiscoveryAsset
{
    public Guid Id { get; set; }
    public Guid BlockId { get; set; }
    public Guid CompanyId { get; set; }

    public string Name { get; set; } = "";
    public FluidType FluidType { get; set; }

    public double EstimatedLowVolumeMmboe { get; set; }
    public double EstimatedMidVolumeMmboe { get; set; }
    public double EstimatedHighVolumeMmboe { get; set; }

    public double Confidence { get; set; }
    public double CommercialityScore { get; set; }
    public double DevelopmentDifficulty { get; set; }

    public decimal EstimatedDevelopmentCost { get; set; }
    public double EstimatedInitialRateBoePerDay { get; set; }

    public string MainRisk { get; set; } = "";
    public AssetStage Stage { get; set; }
}
```

---

# 5. Appraisal System

Appraisal improves confidence before development.

## 5.1 Appraisal Purpose

Appraisal answers:

```text
How large is the field?
How good is the reservoir?
How difficult will development be?
What production rate can be expected?
Is the discovery commercial?
```

## 5.2 Appraisal Action

```text
Action: Drill Appraisal Well
Target: Discovery
Cost: High
Duration: 1 turn
Effect:
- Improves reserve confidence
- Updates commerciality
- May increase or decrease estimated reserves
- May reveal development risk
```

Recommended MVP cost:

```text
Appraisal Well: $30M
```

## 5.3 Appraisal Outcomes

Possible outcomes:

```text
Reserve estimate upgraded
Reserve estimate unchanged
Reserve estimate downgraded
Development difficulty reduced
Development difficulty increased
Commerciality improved
Commerciality reduced
Discovery becomes non-commercial
```

## 5.4 Appraisal Confidence

Example:

```text
Before appraisal:
Estimated volume: 50–220 MMboe
Confidence: 35

After appraisal:
Estimated volume: 85–155 MMboe
Confidence: 62
```

## 5.5 MVP Appraisal Rule

For MVP:

```text
Each appraisal well increases confidence by 20–30 points.
The volume range narrows around the true discovered volume.
Commerciality score is recalculated.
```

---

# 6. Commerciality System

Commerciality determines whether a discovery is worth developing.

## 6.1 Commerciality Inputs

```text
Recoverable volume
Oil price
Development cost
Operating cost
Distance to infrastructure
Development difficulty
Fluid type
Abandonment liability
Confidence
```

## 6.2 Commerciality Score

Recommended simplified formula:

```text
Commerciality Score =
Expected Resource Value
- Estimated Development Cost
- Estimated Operating Cost
- Risk Penalty
- Abandonment Liability
```

## 6.3 Commerciality Rating

Display commerciality as a rating:

```text
Poor
Marginal
Commercial
Attractive
World-Class
```

## 6.4 Commerciality Example

```text
Discovery: Falcon
Estimated recoverable volume: 120 MMbbl
Oil price: $75/bbl
Value per barrel in ground: $4
Estimated development cost: $220M
Risk penalty: $60M
Abandonment liability: $40M

Expected resource value = 120M × $4 = $480M

Commerciality Score =
480M - 220M - 60M - 40M
= $160M

Rating: Commercial
```

---

# 7. Development Concepts

A development concept defines how the field will be built and produced.

## 7.1 MVP Development Concepts

MVP should include three development choices:

```text
Small Development
Standard Development
Large Development
```

---

## 7.2 Small Development

```text
Purpose:
Fast, lower-cost development for small or uncertain discoveries.

Benefits:
- Lower CAPEX
- Faster first oil
- Lower financial risk
- Good when cash is limited

Drawbacks:
- Lower production capacity
- May leave value undeveloped
- Lower peak revenue
```

Recommended MVP values:

```text
CAPEX: $120M
Construction Time: 2 turns
Capacity: 12,000 boe/day
Base OPEX: $8M per turn
Abandonment Liability: $25M
```

---

## 7.3 Standard Development

```text
Purpose:
Balanced development for normal commercial discoveries.

Benefits:
- Good production capacity
- Reasonable construction time
- Balanced cost and reward

Drawbacks:
- Requires meaningful cash
- Moderate abandonment liability
```

Recommended MVP values:

```text
CAPEX: $220M
Construction Time: 3 turns
Capacity: 25,000 boe/day
Base OPEX: $14M per turn
Abandonment Liability: $45M
```

---

## 7.4 Large Development

```text
Purpose:
High-capacity development for large discoveries.

Benefits:
- High production rate
- Strong revenue potential
- Good for major discoveries

Drawbacks:
- Very expensive
- Longer construction
- Higher financial risk
- Higher abandonment liability
```

Recommended MVP values:

```text
CAPEX: $350M
Construction Time: 4 turns
Capacity: 45,000 boe/day
Base OPEX: $25M per turn
Abandonment Liability: $80M
```

---

# 8. Development Concept Model

```csharp
public sealed class DevelopmentConcept
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    public decimal Capex { get; set; }
    public int ConstructionTurns { get; set; }

    public double FacilityCapacityBoePerDay { get; set; }
    public decimal FixedOpexPerTurn { get; set; }
    public decimal VariableOpexPerBoe { get; set; }

    public double Reliability { get; set; }
    public double SafetyRisk { get; set; }
    public double EmissionsIndex { get; set; }

    public decimal AbandonmentLiability { get; set; }

    public double MinimumRecommendedVolumeMmboe { get; set; }
    public double MaximumRecommendedVolumeMmboe { get; set; }
}
```

---

# 9. Development Selection Rules

## 9.1 Requirements

To approve development, a discovery should require:

```text
Commercial discovery
Sufficient cash or debt capacity
Minimum confidence threshold
No blocking regulatory event
```

Recommended MVP requirement:

```text
Discovery confidence must be at least 40.
```

This encourages at least one appraisal well for uncertain discoveries, but still allows risky fast-track development.

---

## 9.2 Overdevelopment Risk

If the player chooses a development too large for the field, the project may underperform.

Example:

```text
Discovery mid-case: 45 MMbbl
Player chooses Large Development

Risk:
- Facility capacity may be underused
- CAPEX payback may be poor
- Company value may fall
```

## 9.3 Underdevelopment Risk

If the player chooses a development too small for a large field, the player may leave value behind.

Example:

```text
Discovery mid-case: 250 MMbbl
Player chooses Small Development

Risk:
- Production bottleneck
- Slower monetization
- Lower match-end value
```

---

# 10. Final Investment Decision

Approving development is the game equivalent of a final investment decision.

## 10.1 Approve Development Action

```text
Action: Approve Development
Target: Commercial discovery
Cost: Development CAPEX
Duration: Construction period
Effect:
- Discovery becomes development project
- Cash decreases
- Construction begins
```

## 10.2 MVP CAPEX Payment

For MVP:

```text
Development CAPEX is paid immediately when approved.
```

Example:

```text
Standard Development:
Cash decreases by $220M immediately.
Construction takes 3 turns.
```

Post-MVP can spread CAPEX across construction turns.

---

# 11. Construction System

Development projects take time to complete.

## 11.1 Construction Progress

Each project has:

```text
Construction Turns Required
Construction Turns Completed
Progress Percentage
Expected First Oil Turn
```

## 11.2 Development Project Model

```csharp
public sealed class DevelopmentProject
{
    public Guid Id { get; set; }
    public Guid DiscoveryId { get; set; }
    public Guid CompanyId { get; set; }

    public string FieldName { get; set; } = "";

    public DevelopmentConcept Concept { get; set; } = new();

    public int ConstructionTurnsRequired { get; set; }
    public int ConstructionTurnsCompleted { get; set; }

    public decimal CapexCommitted { get; set; }
    public decimal CapexSpent { get; set; }

    public bool IsDelayed { get; set; }
    public string StatusSummary { get; set; } = "";
}
```

## 11.3 Construction Resolution

Each turn:

```text
If no delay:
ConstructionTurnsCompleted += 1

If completed:
Create Producing Field next turn or immediately after resolution.
```

Recommended MVP:

```text
Production starts at the beginning of the next turn after construction reaches 100%.
```

---

# 12. Construction Events

Construction may experience events.

## 12.1 Event Examples

```text
Cost overrun
Construction delay
Supply chain shortage
Safety incident
Fast-track success
Regulatory inspection
```

## 12.2 MVP Recommendation

For MVP, construction should be mostly deterministic.

Use only occasional simple events:

```text
Development Delay:
Construction takes +1 turn.

Cost Overrun:
Additional cost = 10% of CAPEX.
```

These events should be rare at first.

---

# 13. Producing Field Model

When construction completes, the development becomes a producing field.

```csharp
public sealed class ProducingField
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid BlockId { get; set; }

    public string Name { get; set; } = "";

    public double OriginalRecoverableMmboe { get; set; }
    public double RemainingRecoverableMmboe { get; set; }

    public double CurrentProductionBoePerDay { get; set; }
    public double PeakProductionBoePerDay { get; set; }
    public double FacilityCapacityBoePerDay { get; set; }

    public double DeclineRatePerTurn { get; set; }
    public double Uptime { get; set; }
    public double WaterCut { get; set; }

    public decimal FixedOpexPerTurn { get; set; }
    public decimal VariableOpexPerBoe { get; set; }

    public decimal AbandonmentLiability { get; set; }

    public ProductionPhase ProductionPhase { get; set; }
    public AssetStage Stage { get; set; }
}
```

---

# 14. Production Phases

```csharp
public enum ProductionPhase
{
    RampUp,
    Plateau,
    Decline,
    LateLife,
    ShutIn,
    Abandoned
}
```

## 14.1 Ramp-Up

The field is newly producing.

Characteristics:

```text
Production starts below capacity.
Operational risk is slightly higher.
Revenue begins.
```

Recommended MVP:

```text
First production turn produces at 70% of target rate.
Second production turn reaches full target rate.
```

---

## 14.2 Plateau

The field produces near planned capacity.

Characteristics:

```text
Highest revenue period
Stable production
Strong cash flow
```

---

## 14.3 Decline

The field begins to naturally decline.

Characteristics:

```text
Production decreases each turn
Water cut may increase
OPEX per barrel rises
Optimization becomes valuable
```

---

## 14.4 Late-Life

The field has low remaining production.

Characteristics:

```text
Low production
High OPEX per barrel
Abandonment liability becomes important
Sell, optimize, or abandon decisions become critical
```

---

# 15. Initial Production Rate

Initial production depends on:

```text
Field size
Development concept capacity
Reservoir quality
Development difficulty
Production confidence
```

## 15.1 MVP Initial Rate Formula

```text
Initial Production Rate =
Minimum of:
- Facility Capacity
- Field Potential Rate
```

Simplified field potential:

```text
Field Potential Rate =
Estimated Recoverable Mmboe × Productivity Factor
```

Recommended MVP productivity factor:

```text
Small fields: 100–180 boe/day per MMboe
Medium fields: 150–250 boe/day per MMboe
Large fields: 180–300 boe/day per MMboe
```

Example:

```text
Discovery: 120 MMboe
Productivity Factor: 220 boe/day per MMboe

Field Potential = 120 × 220
= 26,400 boe/day

Standard Development Capacity = 25,000 boe/day

Initial Production Rate = 25,000 boe/day
```

---

# 16. Production Volume Formula

For a 6-month turn:

```text
Produced Volume =
Current Production Rate × 182.5 × Uptime
```

Example:

```text
Current rate: 25,000 boe/day
Uptime: 94%

Produced Volume =
25,000 × 182.5 × 0.94
= 4,288,750 boe
= 4.29 MMboe
```

---

# 17. Revenue Link

Production creates revenue through the economy system.

```text
Revenue =
Produced Volume × Realized Commodity Price
```

For MVP:

```text
Revenue =
Produced Oil × Oil Price
```

Gas revenue should be added later.

---

# 18. Decline System

Production should decline over time.

## 18.1 Decline Formula

```text
Next Turn Production Rate =
Current Production Rate × (1 - Decline Rate)
```

Example:

```text
Current Rate: 25,000 boe/day
Decline Rate: 8%

Next Rate =
25,000 × 0.92
= 23,000 boe/day
```

## 18.2 Decline Rate Ranges

Recommended MVP values:

```text
Low decline field: 4% per turn
Normal decline field: 7% per turn
High decline field: 12% per turn
Very high decline field: 16% per turn
```

Since each turn is 6 months, this creates noticeable but manageable decline.

---

# 19. Remaining Reserves

Produced volume reduces remaining recoverable reserves.

```text
Remaining Recoverable =
Previous Remaining Recoverable - Produced Volume
```

Example:

```text
Remaining reserves: 80 MMboe
Produced this turn: 4.3 MMboe

New remaining reserves:
75.7 MMboe
```

If remaining reserves are low, production rate should be reduced and the field may enter late-life.

---

# 20. OPEX System

Producing fields have operating costs.

## 20.1 OPEX Formula

```text
OPEX =
Fixed OPEX
+
Produced Volume × Variable OPEX Per Boe
```

Example:

```text
Fixed OPEX: $14M
Variable OPEX: $9/boe
Produced Volume: 4.29 MMboe

Variable OPEX =
4,290,000 × $9
= $38.61M

Total OPEX =
$14M + $38.61M
= $52.61M
```

---

## 20.2 OPEX by Development Type

Recommended MVP:

```text
Small Development:
Fixed OPEX: $8M/turn
Variable OPEX: $10/boe

Standard Development:
Fixed OPEX: $14M/turn
Variable OPEX: $9/boe

Large Development:
Fixed OPEX: $25M/turn
Variable OPEX: $8/boe
```

Large developments have higher fixed cost but lower variable cost due to scale.

---

# 21. Uptime and Downtime

Uptime represents how much of the turn the field was operational.

## 21.1 Uptime

```text
Uptime 1.00 = 100%
Uptime 0.95 = 95%
Uptime 0.80 = 80%
```

## 21.2 Base Uptime

Recommended MVP:

```text
Small Development: 92%
Standard Development: 94%
Large Development: 93%
```

## 21.3 Downtime Events

Events can reduce uptime.

Examples:

```text
Facility outage
Maintenance delay
Pipeline outage
Storm disruption
Equipment failure
Safety shutdown
```

Example:

```text
Base uptime: 94%
Pipeline outage penalty: -15%
Final uptime: 79%
```

---

# 22. Optimization System

Optimization lets the player improve production performance.

## 22.1 MVP Optimization Action

```text
Action: Optimize Field
Target: Producing field
Cost: $20M
Duration: 1 turn
Effect:
- +10% production rate next turn
- +3% uptime for next turn
- Slightly delays late-life trigger
```

## 22.2 Optimization Tradeoff

Optimization should be useful but not always correct.

It is best when:

```text
Field has enough remaining reserves
Oil price is high
Production is constrained
Decline is manageable
Cash is available
```

It is poor when:

```text
Field is nearly depleted
Oil price is low
OPEX is too high
Abandonment is near
```

---

# 23. Advanced Optimization Options

These should be added after MVP.

## 23.1 Workover

```text
Effect:
Restores lost production from well damage or decline.
Best for mature fields.
```

## 23.2 Artificial Lift

```text
Effect:
Improves late-life oil production.
Reduces decline impact.
```

## 23.3 Water Injection

```text
Effect:
Slows decline.
Can increase recovery.
Costs more OPEX.
```

## 23.4 Facility Debottlenecking

```text
Effect:
Increases facility capacity.
Best when reservoir potential exceeds facility capacity.
```

## 23.5 Maintenance Campaign

```text
Effect:
Improves uptime.
Reduces safety and downtime events.
```

---

# 24. Production Events

Production events add uncertainty and operational drama.

## 24.1 Event Examples

```text
Water breakthrough
Equipment failure
Pipeline outage
Unexpected pressure support
Reservoir underperformance
Reservoir outperformance
Maintenance success
Safety shutdown
Storm disruption
Service crew shortage
```

## 24.2 MVP Events

Recommended MVP production events:

```text
Equipment Failure
Pipeline Outage
Reservoir Outperformance
Reservoir Underperformance
Maintenance Success
```

## 24.3 Event Example

```text
Event: Equipment Failure

Effect:
Falcon Field uptime reduced from 94% to 78% this turn.

Result:
Production decreased by 0.73 MMbbl.
Revenue decreased by $54M at current oil price.
```

---

# 25. Field Performance Reporting

The player needs clear feedback.

## 25.1 Production Report

Each producing field should generate a turn report:

```text
Field name
Production rate
Produced volume
Uptime
Revenue
OPEX
Net cash flow
Remaining reserves
Decline rate
Events
Recommended action
```

Example:

```text
Falcon Field Production Report

Current Rate: 23,000 bopd
Produced This Turn: 3.94 MMbbl
Uptime: 94%
Revenue: $295.5M
OPEX: $49.5M
Royalty: $29.5M
Net Cash Flow: $216.5M
Remaining Reserves: 83.1 MMbbl
Decline Rate: 8%
Recommendation: Continue production. Consider optimization if oil price remains above $70/bbl.
```

---

# 26. Production Dashboard

The company dashboard should include:

```text
Total production rate
Total produced this turn
Revenue this turn
OPEX this turn
Net cash flow
Remaining reserves
Top producing field
Weakest field
Abandonment liability
```

## 26.1 MVP Dashboard Values

```text
Total Production
Total Revenue
Total OPEX
Net Cash Flow
Remaining Reserves
Company Value
```

---

# 27. Field UI Requirements

The field detail panel should show:

```text
Field name
Stage
Development concept
Current production rate
Facility capacity
Remaining reserves
Decline rate
Uptime
OPEX
Revenue
Abandonment liability
Production chart
Available actions
AI advisor button
```

## 27.1 Field Status Colors

Recommended colors:

```text
Green: Strong producing field
Blue: Development/construction
Yellow: Declining field
Orange: Late-life field
Red: Shut-in or distressed field
Gray: Abandoned
```

---

# 28. AI Reservoir and Production Advisor

The AI should help players manage fields.

## 28.1 AI Reservoir Engineer

The AI Reservoir Engineer should answer:

```text
Is this field declining too fast?
Should we optimize?
Should we drill more producers later?
Is water cut becoming a problem?
Is the field entering late-life?
```

## 28.2 AI Production Advisor

The AI Production Advisor should answer:

```text
Which field generated the most cash?
Which field has the worst OPEX?
Should we maintain or optimize?
What caused production to drop?
```

## 28.3 AI CFO Link

The CFO should connect production to company finances:

```text
Can this field pay back development cost?
Should we use production cash to repay debt?
Should we hedge next turn production?
```

---

# 29. Example AI Production Response

Player asks:

```text
Should we optimize Falcon Field this turn?
```

AI response:

```text
Situation:
Falcon Field is producing 22,000 bopd with 76 MMbbl remaining.
Decline is 8% per turn and uptime is 93%.

Economics:
At the current oil price of $78/bbl, optimization is likely to pay back quickly.

Risks:
The field is not yet late-life, so optimization has good value.
However, your cash balance is only $90M, and you also have a pending appraisal opportunity.

Recommendation:
Optimize Falcon Field if your goal is short-term cash flow.
Delay optimization if you need cash for appraisal or debt protection.

Suggested Action:
Optimize Falcon Field.
```

---

# 30. MVP Development and Production Rules

For the first playable version, use the following rules.

## 30.1 Development Concepts

```text
Small Development
CAPEX: $120M
Construction: 2 turns
Capacity: 12,000 boe/day
Fixed OPEX: $8M/turn
Variable OPEX: $10/boe
Abandonment Liability: $25M

Standard Development
CAPEX: $220M
Construction: 3 turns
Capacity: 25,000 boe/day
Fixed OPEX: $14M/turn
Variable OPEX: $9/boe
Abandonment Liability: $45M

Large Development
CAPEX: $350M
Construction: 4 turns
Capacity: 45,000 boe/day
Fixed OPEX: $25M/turn
Variable OPEX: $8/boe
Abandonment Liability: $80M
```

---

## 30.2 Production Formula

```text
Produced Volume =
Current Production Rate × 182.5 × Uptime
```

---

## 30.3 Decline Formula

```text
Next Production Rate =
Current Production Rate × (1 - Decline Rate)
```

---

## 30.4 Decline Rates

```text
Low Decline: 4% per turn
Normal Decline: 8% per turn
High Decline: 12% per turn
```

---

## 30.5 Uptime

```text
Base Uptime:
Small Development: 92%
Standard Development: 94%
Large Development: 93%
```

---

## 30.6 Optimization

```text
Optimize Field
Cost: $20M
Effect:
+10% production next turn
+3% uptime next turn
```

---

## 30.7 Late-Life Trigger

A field enters late-life when:

```text
Production falls below 25% of peak production
or
Remaining reserves fall below 20% of original recoverable reserves
```

---

# 31. Example Field Development Scenario

## Discovery

```text
Falcon Discovery
Estimated recoverable volume: 110 MMbbl
Confidence: 58
Commerciality: Commercial
Cash: $300M
```

## Player Decision

The player chooses Standard Development.

```text
CAPEX: $220M
Construction: 3 turns
Capacity: 25,000 boe/day
Abandonment Liability: $45M
```

## Result

```text
Cash drops from $300M to $80M.
Development begins.
```

## Construction

```text
Turn 6: 33%
Turn 7: 66%
Turn 8: 100%
Turn 9: First oil
```

## Production Turn

```text
Initial rate: 25,000 boe/day
Ramp-up factor: 70%
Uptime: 94%

Produced volume:
25,000 × 0.70 × 182.5 × 0.94
= 3.00 MMbbl
```

At $75/bbl:

```text
Revenue:
3.00M × $75 = $225M
```

The field gives the player a major financial boost.

---

# 32. Balancing Guidelines

## 32.1 Development Should Be Expensive

Development should require commitment.

Recommended:

```text
Standard development should cost about 40–50% of starting cash.
```

With $500M starting cash:

```text
Standard Development = $200M–$250M
```

---

## 32.2 Production Should Pay Back

A good field should be able to pay back development cost in a few strong turns.

A marginal field should struggle to pay back if oil prices fall.

---

## 32.3 Large Development Should Be Risky

Large development should be powerful but dangerous.

It should punish:

```text
Overconfidence
Low appraisal confidence
High debt
Low oil prices
Small discoveries
```

---

## 32.4 Small Development Should Be Useful

Small development should support:

```text
Fast first oil
Cash-constrained players
Small discoveries
Short matches
Risk reduction
```

---

# 33. Design Risks

## 33.1 Production Becomes Too Passive

Solution:

```text
Add optimization actions.
Add downtime events.
Add decline.
Add OPEX pressure.
Add late-life decisions.
```

---

## 33.2 Development Choices Are Obvious

Solution:

```text
Make each concept fit different discovery sizes.
Add cash constraints.
Add market uncertainty.
Add construction time.
Add abandonment liability.
```

---

## 33.3 Players Always Choose Large Development

Solution:

```text
Make large development expensive.
Increase construction time.
Increase abandonment liability.
Punish overdevelopment.
```

---

## 33.4 Players Avoid Development

Solution:

```text
Make production financially rewarding.
Make undeveloped discoveries lose value over time.
Add license deadlines later.
Reward first oil in scoring.
```

---

# 34. Open Questions

1. Should MVP require appraisal before development?
2. Should development CAPEX be paid immediately or over time?
3. Should construction delays exist in MVP?
4. Should the game include gas production in MVP?
5. Should fields have multiple wells in the first version?
6. Should optimization consume an action slot?
7. Should large fields require large development?
8. Should production start immediately after construction or next turn?
9. Should OPEX increase as fields decline?
10. Should player-to-player infrastructure sharing exist later?

---

# 35. Recommended MVP Decision

For MVP, use this field development and production system:

```text
Discovery:
- Must have commercial discovery

Appraisal:
- Recommended but not always mandatory
- Confidence threshold: 40

Development:
- Small, Standard, and Large concepts
- CAPEX paid immediately
- Construction takes 2, 3, or 4 turns

Production:
- Oil only
- Production based on daily rate × 182.5 × uptime
- Decline applied each turn
- OPEX deducted each turn
- Revenue based on oil price

Optimization:
- One Optimize Field action
- Costs $20M
- Boosts production and uptime next turn

Late-Life:
- Triggered by low production or low remaining reserves

Abandonment:
- Liability created during development
- Detailed abandonment handled in BOGS-GDD-007
```

This gives Beep Oil and Gas Sim a complete and understandable development-to-production system while keeping the first version achievable.
