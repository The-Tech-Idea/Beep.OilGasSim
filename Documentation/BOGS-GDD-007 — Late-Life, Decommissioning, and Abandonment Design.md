# Beep Oil and Gas Sim

## Late-Life, Decommissioning, and Abandonment Design

**Document ID:** BOGS-GDD-007
**Version:** 0.1
**Status:** Draft
**Parent Document:** BOGS-GDD-001 — Master Game Design Document
**Related Documents:**

* BOGS-GDD-002 — Gameplay Systems Design
* BOGS-GDD-003 — Oil and Gas Lifecycle Simulation Design
* BOGS-GDD-004 — Economy, Finance, and Market Design
* BOGS-GDD-006 — Field Development and Production Design

**Project Name:** Beep Oil and Gas Sim
**Short Name:** Beep O&G Sim

---

# 1. Purpose

This document defines the late-life, decommissioning, and abandonment systems for Beep Oil and Gas Sim.

Most oil and gas games focus only on exploration, drilling, and production. Beep Oil and Gas Sim should be different by including the full responsibility of asset closure.

Late-life and abandonment systems should create meaningful end-game and mid-game decisions.

Players must decide whether to:

* Continue producing a declining field
* Invest in optimization
* Sell a mature asset
* Shut in production
* Prepare abandonment early
* Delay abandonment and risk penalties
* Repurpose infrastructure
* Properly plug wells and close facilities

The goal is to make abandonment a strategic business decision, not just a boring cost.

---

# 2. Design Goals

## 2.1 Make Late-Life Strategic

Late-life should create meaningful choices.

The player should ask:

```text id="rt4bm2"
Is this field still profitable?
Should I spend money to extend production?
Should I sell this asset?
Should I abandon now or later?
Can I reduce future abandonment cost?
Will keeping this field hurt my final score?
```

---

## 2.2 Make Abandonment Responsible but Fun

Abandonment is a cost, but it should also create gameplay.

Players who plan properly should be rewarded.

Players who ignore liability should face penalties.

Good abandonment planning can provide:

```text id="0up55r"
Reputation bonus
Lower final penalties
Better government relationship
Reduced environmental risk
Lower long-term liability
```

---

## 2.3 Avoid Punishing Players Too Harshly

Abandonment should matter, but it should not destroy the fun.

Players should have time to prepare.

The game should warn players early when abandonment liability is growing.

---

## 2.4 Connect to Company Reputation

Late-life and abandonment decisions should affect:

```text id="le9i7x"
Safety reputation
Environmental reputation
Government relationship
Investor confidence
Final score
Future license opportunities
```

---

## 2.5 Keep MVP Simple

For the first playable version, abandonment should be simple:

```text id="whor7i"
Each producing field has abandonment liability.
The player can abandon a field by paying the liability.
Unresolved liability creates a final score penalty.
```

Expanded decommissioning details can be added later.

---

# 3. Late-Life Lifecycle

The late-life lifecycle begins after a field declines.

```text id="0zvak4"
Producing Field
    ↓
Declining Field
    ↓
Late-Life Field
    ↓
Shut-In Field
    ↓
Decommissioning
    ↓
Abandoned
```

For MVP:

```text id="no398u"
Producing
    ↓
Late-Life
    ↓
Abandoned
```

---

# 4. Late-Life Trigger

A producing field becomes late-life when it is no longer strongly economic or has depleted much of its recoverable reserves.

## 4.1 Trigger Conditions

A field enters late-life when one or more of the following conditions are met:

```text id="f86azb"
Current production falls below 25% of peak production
Remaining reserves fall below 20% of original recoverable reserves
Field net cash flow is negative for 2 consecutive turns
Water cut exceeds threshold
License is near expiry
Major facility integrity issue occurs
```

## 4.2 MVP Late-Life Trigger

For MVP, use two simple triggers:

```text id="62um6s"
1. Production falls below 25% of peak production
or
2. Remaining recoverable reserves fall below 20% of original recoverable reserves
```

---

# 5. Late-Life Field State

A late-life field should still be active but under pressure.

## 5.1 Late-Life Field Attributes

```csharp id="edxs6c"
public sealed class LateLifeFieldState
{
    public Guid FieldId { get; set; }

    public bool IsLateLife { get; set; }
    public int TurnsInLateLife { get; set; }

    public double CurrentProductionBoePerDay { get; set; }
    public double PeakProductionBoePerDay { get; set; }
    public double RemainingRecoverableMmboe { get; set; }

    public decimal NetCashFlowThisTurn { get; set; }
    public decimal AbandonmentLiability { get; set; }

    public double EnvironmentalRisk { get; set; }
    public double IntegrityRisk { get; set; }
    public double RegulatoryPressure { get; set; }

    public string LateLifeSummary { get; set; } = "";
}
```

---

# 6. Late-Life Decisions

When a field enters late-life, the player should have several choices.

## 6.1 Continue Production

```text id="17v47v"
Action: Continue Production
Effect:
- Field keeps producing
- Revenue continues
- OPEX continues
- Abandonment liability remains
Risk:
- Future liability may increase
- Environmental or integrity event risk may increase
```

This is the default behavior if the player does nothing.

---

## 6.2 Optimize Late-Life Field

```text id="gffyzm"
Action: Optimize Late-Life Field
Cost: Medium
Effect:
- Temporarily improves production
- May slow decline
- May delay abandonment
Risk:
- Poor value if reserves are too low
```

Example:

```text id="dmded4"
Cost: $20M
Effect:
+10% production next turn
+3% uptime next turn
```

---

## 6.3 Reduce OPEX

```text id="2y5wza"
Action: Reduce OPEX
Cost: Low
Effect:
- Reduces fixed operating cost
Risk:
- May increase downtime or safety risk
```

This should be Phase 2, not MVP.

---

## 6.4 Shut In Field

```text id="q7d33p"
Action: Shut In Field
Effect:
- Stops production
- Reduces OPEX
- Keeps abandonment liability
Risk:
- Reputation or regulatory pressure may increase if left unresolved
```

This is useful when oil price is low or production is uneconomic.

---

## 6.5 Sell Mature Asset

```text id="lb2i64"
Action: Sell Mature Asset
Effect:
- Generates cash
- Transfers abandonment liability to buyer or partially discounts price
Risk:
- Sale value may be low
- Buyer may benefit from future recovery
```

For MVP, asset sales can be simplified as selling to the market.

---

## 6.6 Prepare Abandonment

```text id="wcxgtk"
Action: Prepare Abandonment
Cost: Low to medium
Effect:
- Reduces future abandonment liability
- Reduces regulatory penalty risk
- Improves reputation
```

This should be Phase 2.

---

## 6.7 Abandon Field

```text id="8zco3h"
Action: Abandon Field
Cost: Current abandonment liability
Effect:
- Field is removed from active production
- Liability is cleared
- Reputation protected
```

This is the key MVP abandonment action.

---

# 7. Abandonment Liability

Every developed field creates abandonment liability.

Abandonment liability represents the future cost of safely closing the asset.

## 7.1 Liability Sources

Abandonment liability is created by:

```text id="5c7qi2"
Number of wells
Facility size
Development type
Environmental sensitivity
Offshore/onshore complexity
Contaminant risk
Regulatory strictness
Field age
Deferred maintenance
```

## 7.2 MVP Liability by Development Type

Recommended MVP values:

```text id="m79nqx"
Small Development:
Base Abandonment Liability = $25M

Standard Development:
Base Abandonment Liability = $45M

Large Development:
Base Abandonment Liability = $80M
```

---

## 7.3 Environmental Multiplier

Fields in environmentally sensitive areas should cost more to abandon.

```text id="annptf"
Final Abandonment Liability =
Base Liability × Environmental Multiplier
```

Recommended values:

```text id="docfna"
Low sensitivity: 1.0
Medium sensitivity: 1.2
High sensitivity: 1.5
Very high sensitivity: 2.0
```

Example:

```text id="qzmbx8"
Standard Development Base Liability: $45M
High Environmental Sensitivity Multiplier: 1.5

Final Liability:
$45M × 1.5 = $67.5M
```

---

# 8. Liability Growth

If a field remains in late-life without action, liability may grow.

## 8.1 Growth Reasons

```text id="q951zw"
Aging equipment
Deferred maintenance
Environmental monitoring failures
Regulatory pressure
Well integrity problems
Facility deterioration
```

## 8.2 MVP Rule

For MVP, keep liability stable until final scoring.

Recommended MVP:

```text id="xlncy2"
Abandonment liability does not grow each turn.
It is created at development approval and remains until paid or penalized.
```

## 8.3 Phase 2 Rule

After MVP:

```text id="n3cm2b"
If a field stays late-life for more than 3 turns:
Abandonment liability increases by 5% per turn.
```

Example:

```text id="1yxqdp"
Starting liability: $45M
Late-life for 4 turns
1 turn beyond grace period
New liability = $45M × 1.05 = $47.25M
```

---

# 9. Abandonment Action

The Abandon Field action is the main way to clear liability.

## 9.1 MVP Abandon Field Action

```text id="4txl3p"
Action: Abandon Field
Target: Late-life or producing field
Action Slots: 1
Cost: Current abandonment liability
Duration: 1 turn
Effect:
- Field status becomes Abandoned
- Production stops
- OPEX stops
- Abandonment liability becomes 0
- Reputation may improve
```

## 9.2 Abandonment Eligibility

For MVP, any producing or late-life field can be abandoned.

Recommended rule:

```text id="91gloa"
A field can be abandoned if the company can pay the abandonment cost.
```

If the player cannot pay, they may need debt or asset sale.

---

# 10. Abandonment Score Effects

Abandonment affects final score.

## 10.1 Cleared Liability

If the player abandons properly:

```text id="ii9n2j"
Liability is removed.
No final penalty.
Small reputation bonus may be awarded.
```

Recommended MVP bonus:

```text id="ijlbxs"
+2 reputation for each properly abandoned field.
```

---

## 10.2 Unfunded Liability

If the player ends the match with unresolved abandonment liability:

```text id="rhjymk"
Final Score Penalty =
Remaining Abandonment Liability × Penalty Multiplier
```

Recommended MVP penalty multiplier:

```text id="9omg7n"
1.5
```

Example:

```text id="3655oe"
Remaining liability: $80M
Penalty multiplier: 1.5
Final penalty: $120M
```

---

# 11. Reputation Effects

Late-life and abandonment decisions affect reputation.

## 11.1 Positive Reputation Effects

```text id="o4u49m"
Proper abandonment
Early abandonment planning
Good environmental record
No late-life incidents
Responsible asset sale
```

## 11.2 Negative Reputation Effects

```text id="4j8co1"
Ignoring late-life liability
Environmental incident
Regulatory violation
Repeated shut-in without plan
End-game unfunded abandonment liability
```

## 11.3 MVP Reputation Rules

```text id="r7nqlv"
Properly abandon field: +2 reputation
End match with unresolved liability above $100M: -5 reputation
End match with unresolved liability above $250M: -10 reputation
Environmental event: -5 to -15 reputation
```

---

# 12. Regulatory Pressure

Regulatory pressure represents government attention on late-life assets.

## 12.1 Regulatory Pressure Sources

```text id="tdl8t8"
Field is late-life
Field is shut in
Environmental sensitivity is high
Company reputation is low
Abandonment delayed too long
Previous incidents
```

## 12.2 Regulatory Events

Examples:

```text id="gnmy94"
Regulator Inspection
Abandonment Deadline
Environmental Compliance Audit
Mandatory Well Integrity Test
Penalty Notice
License Renewal Blocked
```

## 12.3 MVP Recommendation

Regulatory pressure should be mostly represented through final score and simple events.

MVP event:

```text id="sf458z"
Regulatory Inspection:
A company with high abandonment liability may receive a penalty or warning.
```

---

# 13. Environmental Risk

Environmental risk increases when late-life fields are poorly managed.

## 13.1 Environmental Risk Sources

```text id="8yi2z6"
High environmental sensitivity
Late-life age
Poor maintenance
High integrity risk
Low reputation
Delayed abandonment
```

## 13.2 Environmental Event Examples

```text id="jjqi1t"
Minor leak
Produced water issue
Well integrity problem
Community complaint
Site restoration failure
```

## 13.3 MVP Environmental Event

```text id="avgjup"
Late-Life Leak:
A late-life field suffers an environmental incident.
Effects:
- Cash penalty
- Reputation loss
- Abandonment liability increases
```

Recommended MVP values:

```text id="6jqz14"
Cash penalty: $10M–$30M
Reputation loss: 5–10 points
Liability increase: 10%–20%
```

---

# 14. Decommissioning System

Decommissioning is the process before final abandonment.

For the MVP, decommissioning can be abstracted into the Abandon Field action.

For expanded versions, decommissioning can be broken into steps.

## 14.1 Expanded Decommissioning Steps

```text id="vyh3q8"
1. Engineering study
2. Regulatory approval
3. Well plugging
4. Facility shutdown
5. Equipment removal
6. Site restoration
7. Final inspection
8. Liability release
```

## 14.2 Expanded Decommissioning Actions

```text id="6sj2sz"
Prepare Decommissioning Plan
Plug Wells
Remove Facilities
Restore Site
Complete Regulatory Closeout
```

## 14.3 Expanded Decommissioning Benefits

Detailed decommissioning can allow:

```text id="kymcuy"
Lower cost through planning
Reduced incident risk
Reputation bonus
ESG score improvement
Government relationship improvement
```

---

# 15. Repurposing System

Repurposing gives players alternatives to abandonment.

This should be added after MVP.

## 15.1 Repurposing Options

Possible repurposing options:

```text id="d7fsnr"
Gas storage
Carbon storage
Hydrogen hub
Offshore wind support base
Training facility
Pipeline reuse
Water disposal facility
Energy transition hub
```

## 15.2 Repurposing Design Goal

Repurposing should allow a player to convert a late-life liability into a new strategic asset.

Example:

```text id="9d3ib1"
Old Gas Field → Gas Storage Facility
Old Platform → Carbon Storage Hub
Pipeline Network → Hydrogen Transport Corridor
```

## 15.3 Repurposing Tradeoff

```text id="fcjf8g"
Benefits:
- Reduces abandonment liability
- Creates new revenue
- Improves reputation
- Supports energy transition strategy

Drawbacks:
- Requires investment
- Not always available
- May require high technology level
- May depend on field type and location
```

---

# 16. Late-Life Asset Sale

Players may sell late-life assets.

## 16.1 Mature Asset Sale Logic

Sale value should account for liability.

```text id="xayrbz"
Sale Price =
Remaining Economic Value
- Abandonment Liability Discount
- Buyer Risk Discount
```

## 16.2 Example

```text id="mlq8ow"
Remaining field value: $90M
Abandonment liability: $45M
Buyer risk discount: $15M

Sale price:
$90M - $45M - $15M = $30M
```

## 16.3 Negative Value Assets

Some late-life assets may have negative value.

Example:

```text id="84h846"
Remaining field value: $20M
Abandonment liability: $60M

Market value: -$40M
```

In this case, the player may need to pay someone to take the asset.

This should be Phase 2 or later.

---

# 17. Late-Life UI Requirements

The UI should make abandonment liability visible early.

## 17.1 Company Dashboard

Show:

```text id="g7qee7"
Total abandonment liability
Late-life field count
Fields near late-life
Estimated final penalty
Reputation impact
```

## 17.2 Field Panel

Show:

```text id="rp0up9"
Current production
Peak production
Remaining reserves
Late-life status
Net cash flow
Abandonment liability
Environmental sensitivity
Regulatory pressure
Available actions
```

## 17.3 Warning Messages

Examples:

```text id="lmtwgt"
Falcon Field is approaching late-life.
Production is below 30% of peak.

Abandonment liability is $45M.
If unresolved at match end, estimated penalty is $67.5M.
```

---

# 18. AI HSE and Late-Life Advisor

The AI Command Center should help players with late-life and abandonment decisions.

## 18.1 AI HSE Advisor Responsibilities

The AI HSE Advisor should answer:

```text id="lamcfx"
Which fields need abandonment planning?
What is our abandonment liability?
Which field is our biggest environmental risk?
Should we abandon this field now?
What happens if we delay abandonment?
How can we reduce reputation penalties?
```

## 18.2 AI CFO Responsibilities

The CFO should answer:

```text id="wy9a7e"
Can we afford abandonment this turn?
Should we borrow to abandon?
Is this field still profitable?
Should we sell or abandon?
How will abandonment affect final score?
```

## 18.3 AI Reservoir Advisor Responsibilities

The Reservoir Advisor should answer:

```text id="uhjxc6"
Is the field still worth optimizing?
How much production remains?
Is decline too steep to justify investment?
```

---

# 19. Example AI Late-Life Response

Player asks:

```text id="cg7u32"
Should we abandon Falcon Field now?
```

AI response:

```text id="l5p8ae"
Situation:
Falcon Field is in late-life. Production is now 22% of peak and remaining reserves are below 18% of original recoverable reserves.

Economics:
The field generated only $8M net cash flow last turn.
Abandonment liability is $45M.
If unresolved at match end, estimated penalty is $67.5M.

Risks:
Keeping the field active may generate small cash flow, but it exposes the company to late-life environmental events.

Recommendation:
If you have enough cash, abandon Falcon Field this turn.
If cash is tight, consider selling a non-core license or taking limited debt to fund abandonment.

Suggested Action:
Abandon Falcon Field.
```

---

# 20. Late-Life Events

Late-life events should create tension.

## 20.1 Event Examples

```text id="5igkg4"
Well Integrity Warning
Regulatory Inspection
Minor Environmental Leak
Facility Corrosion Issue
Community Complaint
Abandonment Cost Inflation
Successful Cost Reduction Program
```

## 20.2 MVP Late-Life Events

Use only a small set:

```text id="2t38q3"
Regulatory Inspection
Late-Life Leak
Abandonment Cost Inflation
```

## 20.3 Event Example: Regulatory Inspection

```text id="gnggqx"
Event:
Regulatory Inspection

Trigger:
Company has more than $100M unresolved abandonment liability.

Effect:
- Company receives warning.
- If reputation is below 40, pay $20M penalty.
- If reputation is above 70, no penalty but warning remains.
```

---

# 21. MVP Late-Life and Abandonment Rules

For MVP, use simple rules.

## 21.1 Field Becomes Late-Life

```text id="md3pu0"
Field enters late-life if:
- Production is below 25% of peak
or
- Remaining reserves are below 20% of original recoverable reserves
```

## 21.2 Abandonment Liability

```text id="muwj1k"
Small Development: $25M
Standard Development: $45M
Large Development: $80M
```

Apply environmental multiplier:

```text id="ugvjpy"
Low: 1.0
Medium: 1.2
High: 1.5
Very High: 2.0
```

## 21.3 Abandon Field Action

```text id="5yze88"
Cost: Current abandonment liability
Action Slots: 1
Duration: 1 turn
Effect:
- Field becomes Abandoned
- Production stops
- OPEX stops
- Liability cleared
- Reputation +2
```

## 21.4 Final Penalty

```text id="utprgh"
Unresolved Abandonment Penalty =
Remaining Liability × 1.5
```

---

# 22. Example Late-Life Scenario

## Field Status

```text id="bw1t68"
Falcon Field
Peak Production: 25,000 bopd
Current Production: 5,800 bopd
Remaining Reserves: 15 MMbbl
Original Recoverable: 110 MMbbl
Abandonment Liability: $45M
Cash: $160M
Turns Remaining: 3
```

## Late-Life Trigger

```text id="9hfpsx"
Current production is 23.2% of peak.
Field enters late-life.
```

## Player Choices

```text id="p7p946"
Option A: Continue production
- Earn small cash flow
- Keep liability

Option B: Optimize
- Spend $20M
- May improve final production
- Not likely to pay back fully

Option C: Abandon
- Spend $45M
- Clear liability
- Avoid final penalty

Option D: Sell
- Receive discounted value
- Transfer or reduce liability
```

## Recommended Decision

```text id="4kt1df"
Since only 3 turns remain and the field is close to depletion, abandonment is likely the best choice.
```

---

# 23. Example Final Score Impact

## Company A

```text id="dpulnp"
Cash: $300M
Debt: $100M
Asset Value: $500M
Unresolved Abandonment Liability: $0

Company Value:
$300M - $100M + $500M = $700M
```

## Company B

```text id="c4tvoc"
Cash: $370M
Debt: $100M
Asset Value: $500M
Unresolved Abandonment Liability: $80M
Penalty Multiplier: 1.5

Penalty:
$80M × 1.5 = $120M

Company Value:
$370M - $100M + $500M - $120M = $650M
```

Company B has more cash, but Company A wins because it handled abandonment responsibly.

---

# 24. Balancing Guidelines

## 24.1 Abandonment Must Matter

If abandonment liability is too small, players ignore it.

Recommended:

```text id="ivku2s"
Abandonment liability should be large enough to affect final score.
```

## 24.2 Abandonment Must Not Dominate Everything

If abandonment is too expensive, players may avoid development.

Recommended:

```text id="hbfwfs"
Abandonment liability should usually be 15%–25% of development CAPEX.
```

Example:

```text id="gs4qi5"
Standard Development CAPEX: $220M
Abandonment Liability: $45M
Ratio: 20%
```

## 24.3 Late-Life Should Arrive Before Game End

In a 20-turn match, at least some fields should reach late-life before the end.

This creates abandonment decisions.

## 24.4 Give Early Warnings

Players should not be surprised by final penalties.

The UI and AI should warn them repeatedly.

---

# 25. Design Risks

## 25.1 Players Ignore Abandonment

Solution:

```text id="x3dmnu"
Show liability on dashboard.
Include final penalty.
Add AI warnings.
Add reputation effects.
```

## 25.2 Abandonment Feels Like a Tax

Solution:

```text id="6572zz"
Add reputation reward.
Add repurposing options later.
Allow abandonment planning to reduce cost.
Make good closure part of winning strategy.
```

## 25.3 Too Many Systems Too Early

Solution:

```text id="hku7bc"
MVP uses one Abandon Field action.
Expanded decommissioning comes later.
```

## 25.4 Players Avoid Developing Fields

Solution:

```text id="6agmd3"
Keep production profitable.
Balance liability around 15%–25% of development CAPEX.
Make abandonment manageable with planning.
```

---

# 26. Open Questions

1. Should abandonment be possible before late-life?
2. Should abandonment consume one action slot or more?
3. Should liability grow during late-life in MVP?
4. Should field sale transfer all liability or only part of it?
5. Should poor abandonment affect future license bids?
6. Should repurposing be included in the first full release?
7. Should abandonment costs vary by onshore/offshore map type?
8. Should a field be shut in before abandonment?
9. Should abandonment planning reduce cost?
10. Should late-life environmental events be random or based on risk score?

---

# 27. Recommended MVP Decision

For MVP, use this late-life and abandonment system:

```text id="jxnhv5"
Late-Life Trigger:
- Production below 25% of peak
or
- Remaining reserves below 20% of original recoverable

Abandonment Liability:
- Created when development is approved
- Based on development type
- Modified by environmental sensitivity

Abandon Field Action:
- Costs current abandonment liability
- Takes 1 action slot
- Resolves in 1 turn
- Stops production and OPEX
- Clears liability
- Gives +2 reputation

Final Penalty:
- Unresolved liability × 1.5

AI Support:
- HSE Advisor warns about liability and recommends action
```

This gives Beep Oil and Gas Sim a strong, distinctive full-lifecycle system while keeping the first version easy to implement.
