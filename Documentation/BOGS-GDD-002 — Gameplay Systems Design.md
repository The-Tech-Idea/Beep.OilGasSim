# Beep Oil and Gas Sim

## Gameplay Systems Design

**Document ID:** BOGS-GDD-002
**Version:** 0.1
**Status:** Draft
**Parent Document:** BOGS-GDD-001 — Master Game Design Document
**Project Name:** Beep Oil and Gas Sim
**Short Name:** Beep O&G Sim

---

# 1. Purpose

This document defines the core gameplay systems for Beep Oil and Gas Sim.

It explains how the player interacts with the game, how turns work, what actions are available, how decisions are resolved, and how players compete.

This document focuses on the playable rules and game flow. More technical oil and gas simulation details will be covered in later documents.

Related documents:

* BOGS-GDD-003 — Oil and Gas Lifecycle Simulation Design
* BOGS-GDD-004 — Economy, Finance, and Market Design
* BOGS-GDD-005 — Exploration and Geology Design
* BOGS-GDD-006 — Field Development and Production Design
* BOGS-GDD-008 — AI Command Center Design
* BOGS-GDD-009 — Multiplayer and Team Collaboration Design

---

# 2. Gameplay Overview

Beep Oil and Gas Sim is a competitive strategy simulation where each player controls an oil and gas company.

Players compete across a shared map by acquiring license blocks, studying geology, drilling wells, developing fields, producing hydrocarbons, managing finances, and handling abandonment responsibilities.

The game is built around repeated decision cycles.

Each turn, players choose actions. The server resolves those actions and updates the game world.

The core gameplay experience is:

```text
Plan → Commit → Resolve → Learn → Adapt
```

Players do not have perfect information. They must make decisions under uncertainty.

A good player must balance:

* Risk and reward
* Cash and debt
* Exploration and production
* Short-term survival and long-term value
* Fast growth and responsible abandonment
* Competition and collaboration

---

# 3. Core Gameplay Pillars

## 3.1 Strategic Risk

The player should constantly face meaningful risk.

Examples:

* Drill now with low confidence or buy more seismic first
* Bid aggressively for a promising block or preserve cash
* Develop a field quickly or wait for more appraisal data
* Take debt to build infrastructure or sell an asset
* Delay abandonment or protect reputation

Risk should be understandable, not random.

---

## 3.2 Competitive Pressure

Players should feel pressure from other companies.

Competition happens through:

* License auctions
* Nearby discoveries
* Limited rig availability
* Service cost inflation
* Shared infrastructure access
* Asset sales
* Market timing
* Leaderboards
* Final valuation

The game should make players feel that waiting too long can create missed opportunities.

---

## 3.3 Incomplete Information

Players should never know the full truth of a basin at the start.

Information is revealed through:

* Public basin data
* Geological studies
* Seismic surveys
* Exploration wells
* Appraisal wells
* Production history
* Competitor discoveries

The player should be rewarded for spending wisely on information.

---

## 3.4 Business Management

The player is not only a driller. The player manages a company.

Important business factors:

* Cash
* Debt
* Production revenue
* Operating cost
* Capital investment
* Asset value
* Reputation
* Abandonment liability
* Investor confidence

---

## 3.5 Simple Rules, Deep Outcomes

The game should avoid excessive complexity in the first version.

Each action should be easy to understand.

Example:

```text
Buy 3D seismic:
- Costs money
- Takes one turn
- Improves geological confidence
- Reduces dry-hole risk
```

Simple actions can still create deep strategy when combined with uncertainty, competition, and limited resources.

---

# 4. Game Session Structure

A game session is called a **Match**.

Each match contains:

* One map
* A fixed number of players
* A fixed number of turns
* A starting cash amount
* A commodity price model
* Hidden geology
* Public information
* Game events
* Turn actions
* Final scoring

---

## 4.1 Recommended MVP Match Setup

```text
Scenario Name: Desert Frontier
Players: 2–6
Turns: 20
Turn Length in Game Time: 6 months
Starting Cash: $500M
Primary Commodity: Oil
Map Size: 20 license blocks
Victory Condition: Highest final company value
```

---

## 4.2 Game Time

For the MVP, each turn represents **6 months**.

This keeps the game fast while still allowing oil and gas projects to progress over time.

Example:

```text
Turn 1 = Year 1, H1
Turn 2 = Year 1, H2
Turn 3 = Year 2, H1
Turn 4 = Year 2, H2
```

A 20-turn match represents 10 years.

---

# 5. Player Role

Each player controls one company.

The company has:

* Cash
* Debt
* Reputation
* Licenses
* Prospects
* Discoveries
* Fields
* Wells
* Facilities
* Contracts
* Pending actions
* Historical performance

The player acts as the company leader.

In team mode, multiple players may share one company.

---

# 6. Match Flow

A match follows this flow:

```text
1. Match created
2. Players join
3. Companies assigned
4. Starting map revealed
5. Turn 1 planning begins
6. Players submit actions
7. Server resolves actions
8. Results are shown
9. Next turn begins
10. Repeat until final turn
11. Final score calculated
12. Winner announced
```

---

# 7. Turn Structure

Each turn has four major phases:

```text
Planning Phase
    ↓
Commit Phase
    ↓
Resolution Phase
    ↓
Results Phase
```

---

## 7.1 Planning Phase

During planning, players review the current game state and decide what to do.

Players can:

* Inspect blocks
* Review company dashboard
* Review asset details
* Check market conditions
* Read event reports
* Chat with team members
* Ask the AI advisor
* Create action proposals
* Select final actions

In live multiplayer, the planning phase has a timer.

In async mode, the planning phase lasts until the turn deadline.

---

## 7.2 Commit Phase

During commit, the player confirms final actions.

Once actions are committed, they are locked for turn resolution.

For team mode, the CEO or authorized player commits the final company action plan.

---

## 7.3 Resolution Phase

During resolution, the server processes all player actions.

The client does not calculate final outcomes.

The server resolves:

* Auctions
* Exploration studies
* Seismic results
* Drilling outcomes
* Discoveries
* Appraisal updates
* Development progress
* Production
* Revenue
* Costs
* Random events
* Reputation changes
* Leaderboard changes

---

## 7.4 Results Phase

During results, players receive a clear summary of what happened.

Results should include:

* Financial changes
* New discoveries
* Dry holes
* Production results
* Market changes
* Events
* Competitor public actions
* Updated rankings
* AI turn summary

The results phase should be exciting and easy to understand.

---

# 8. Action System

Actions are the main decisions players make each turn.

Each action has:

* Action type
* Target asset or block
* Cost
* Duration
* Requirements
* Expected effect
* Risk
* Result
* Visibility

---

## 8.1 Action Categories

Main action categories:

1. License actions
2. Exploration actions
3. Drilling actions
4. Appraisal actions
5. Development actions
6. Production actions
7. Financial actions
8. Market actions
9. HSE and reputation actions
10. Abandonment actions
11. Team and AI actions

---

## 8.2 Action Points

For the MVP, each company should have a limited number of **Action Slots** per turn.

Recommended MVP rule:

```text
Each company has 3 action slots per turn.
```

This forces strategic choices.

Example:

```text
Turn 4 actions:
1. Buy 2D seismic on Block 7
2. Drill exploration well on Block 3
3. Hedge 30% of production
```

Some major actions may consume more than one slot.

Example:

```text
Develop offshore field = 2 action slots
Acquire small geological study = 1 action slot
```

---

## 8.3 Action Cost

Actions usually cost money.

Some actions may also require:

* Owned license
* Available rig
* Facility capacity
* Minimum reputation
* Previous study
* Appraisal confidence
* Regulatory approval

Example:

```text
Drill Exploration Well
Cost: $40M
Action Slots: 1
Duration: 1 turn
Requirement: Player owns the block
Result: Discovery or dry hole
```

---

# 9. License Actions

License actions allow players to acquire exploration rights.

---

## 9.1 Bid for License

Players bid on open license blocks.

```text
Action: Bid for License
Target: Unowned block
Cost: Bid amount if successful
Duration: Resolved during same turn
Requirement: Sufficient cash or credit
Visibility: Public after auction resolves
```

The highest valid bid wins.

If two players bid the same amount, tie-breakers may be:

1. Higher reputation
2. Earlier submitted bid
3. Random government preference

Recommended MVP rule:

```text
Highest bid wins.
Tie resolved randomly.
```

---

## 9.2 Relinquish License

Players may give up a block.

```text
Action: Relinquish License
Target: Owned block
Cost: Small administrative fee
Effect: Removes annual license cost
Risk: May lose future opportunity
```

This is useful if the player owns too many poor blocks.

---

## 9.3 Extend License

Players may pay to extend a license nearing expiry.

```text
Action: Extend License
Target: Owned block
Cost: Extension fee
Effect: Adds extra turns before expiry
Requirement: Good compliance record
```

This can be added after MVP.

---

# 10. Exploration Actions

Exploration actions reduce uncertainty before drilling.

---

## 10.1 Geological Study

```text
Action: Geological Study
Target: Owned or open block
Cost: Low
Duration: 1 turn
Effect: Reveals broad geological hints
Accuracy: Low to medium
```

Example result:

```text
Block 5 has moderate source rock potential and high structural uncertainty.
```

---

## 10.2 2D Seismic

```text
Action: Acquire 2D Seismic
Target: Owned block
Cost: Medium
Duration: 1 turn
Effect: Improves trap and structure confidence
Accuracy: Medium
```

Example result:

```text
2D seismic suggests a possible structural trap in the northern part of the block.
Estimated chance of success improved from 18% to 27%.
```

---

## 10.3 3D Seismic

```text
Action: Acquire 3D Seismic
Target: Owned block
Cost: High
Duration: 1–2 turns
Effect: Strongly improves prospect definition
Accuracy: High
```

Example result:

```text
3D seismic confirms a closed structure.
Estimated chance of success improved from 27% to 41%.
```

---

## 10.4 AI Prospect Interpretation

```text
Action: AI Prospect Interpretation
Target: Block with seismic data
Cost: Medium
Duration: Same turn or 1 turn
Effect: Improves confidence and generates AI recommendation
Requirement: At least geological study or seismic
```

This action should feel like an advanced analytics feature inside the game.

It should not reveal hidden truth directly.

---

# 11. Drilling Actions

Drilling actions create the biggest excitement and risk.

---

## 11.1 Drill Exploration Well

```text
Action: Drill Exploration Well
Target: Owned block or prospect
Cost: High
Duration: 1 turn
Result: Discovery or dry hole
Risk: High
```

Possible outcomes:

* Dry hole
* Non-commercial discovery
* Small commercial discovery
* Medium discovery
* Large discovery
* Giant discovery
* Technical problem
* Cost overrun

MVP outcome categories:

```text
Dry Hole
Non-Commercial Discovery
Commercial Discovery
Major Discovery
```

---

## 11.2 Drilling Budget Class

Players may choose drilling budget class.

```text
Cheap Well
- Lower cost
- Higher operational risk
- Lower data quality

Standard Well
- Balanced cost and risk

Premium Well
- Higher cost
- Lower operational risk
- Better data quality
```

Recommended MVP:

```text
Cheap Well: -20% cost, +10% failure risk
Standard Well: normal cost, normal risk
Premium Well: +25% cost, -10% failure risk
```

---

## 11.3 Drill Appraisal Well

```text
Action: Drill Appraisal Well
Target: Discovery
Cost: High
Duration: 1 turn
Effect: Improves reserve confidence
Risk: May reduce estimated value
```

Possible outcomes:

* Discovery grows
* Discovery stays similar
* Discovery shrinks
* Development risk increases
* Discovery becomes non-commercial

---

# 12. Development Actions

Development actions convert discoveries into producing fields.

---

## 12.1 Select Development Concept

```text
Action: Select Development Concept
Target: Commercial discovery
Cost: Study cost
Duration: 1 turn
Effect: Presents development options
```

The player chooses between options such as:

* Fast low-capacity development
* Balanced development
* Large high-capacity development
* Shared infrastructure tieback

---

## 12.2 Approve Development

```text
Action: Approve Development
Target: Appraised discovery
Cost: Major CAPEX
Duration: Multiple turns
Effect: Starts construction
Requirement: Minimum commercial confidence
```

MVP development options:

```text
Small Development
- Lower CAPEX
- Faster first oil
- Lower production capacity

Standard Development
- Medium CAPEX
- Medium time
- Balanced capacity

Large Development
- High CAPEX
- Longer time
- Higher capacity
```

---

## 12.3 Development Progress

Development projects take multiple turns.

Example:

```text
Small Development: 2 turns
Standard Development: 3 turns
Large Development: 4 turns
```

During each turn, the project may experience:

* On schedule
* Delay
* Cost overrun
* Safety incident
* Improved efficiency

For MVP, keep development deterministic unless affected by events.

---

# 13. Production Actions

Production actions improve or maintain producing fields.

---

## 13.1 Start Production

When development is complete, the field automatically begins production.

Production generates:

* Oil revenue
* Gas revenue, if applicable
* Operating costs
* Taxes or royalties
* Decline
* Reputation effects if incidents occur

---

## 13.2 Optimize Production

```text
Action: Optimize Production
Target: Producing field
Cost: Medium
Duration: 1 turn
Effect: Temporarily or permanently improves production
```

Possible optimization types:

* Workover
* Artificial lift
* Water injection
* Facility debottlenecking
* Maintenance campaign

MVP should start with one general action:

```text
Optimize Field
Cost: $20M
Effect: +10% production next turn and reduces downtime risk
```

---

## 13.3 Maintenance Campaign

```text
Action: Maintenance Campaign
Target: Producing field or facility
Cost: Medium
Effect: Reduces downtime risk and incident probability
```

This gives players a way to prevent future losses.

---

# 14. Financial Actions

Financial actions help players manage risk and funding.

---

## 14.1 Take Debt

```text
Action: Take Debt
Target: Company
Effect: Increases cash
Cost: Interest payments
Risk: Debt penalty and lower credit rating
```

Debt allows players to fund development but creates long-term pressure.

---

## 14.2 Repay Debt

```text
Action: Repay Debt
Target: Company
Effect: Reduces debt and interest burden
Benefit: Improves credit rating and final score
```

---

## 14.3 Hedge Production

```text
Action: Hedge Production
Target: Producing company
Effect: Locks price for part of future production
Benefit: Reduces downside risk
Cost: Limits upside during price boom
```

MVP hedge rule:

```text
Player can hedge 25%, 50%, or 75% of next turn production.
Hedged production receives fixed price.
Unhedged production receives market price.
```

---

# 15. Market and Trading Actions

These actions create competition and business strategy.

---

## 15.1 Sell Asset

```text
Action: Sell Asset
Target: License, discovery, or field
Effect: Generates cash
Risk: Buyer may profit later
```

For MVP, asset sales can be to the market/NPC.

Player-to-player asset trading can be added later.

---

## 15.2 Farm-Out

```text
Action: Farm-Out
Target: License or discovery
Effect: Another company pays part of the cost for a share of ownership
```

This is not required in MVP but is important for later depth.

---

## 15.3 Farm-In

```text
Action: Farm-In
Target: Another company asset
Effect: Buy into an opportunity
```

This should be added after core multiplayer is stable.

---

# 16. HSE and Reputation Actions

HSE systems make the game more responsible and strategic.

---

## 16.1 Safety Investment

```text
Action: Safety Investment
Target: Company or asset
Cost: Medium
Effect: Reduces incident probability
Benefit: Improves reputation
```

---

## 16.2 Environmental Program

```text
Action: Environmental Program
Target: Company, block, or field
Cost: Medium
Effect: Reduces environmental risk
Benefit: Improves government and community relationship
```

---

## 16.3 Community Engagement

```text
Action: Community Engagement
Target: Basin or block
Cost: Low to medium
Effect: Reduces social risk and protest chance
```

These actions are optional for MVP but important for later design.

---

# 17. Abandonment Actions

Abandonment actions become important in late game.

---

## 17.1 Prepare Abandonment Plan

```text
Action: Prepare Abandonment Plan
Target: Late-life field
Cost: Low
Effect: Reduces future abandonment cost and penalty risk
```

---

## 17.2 Plug and Abandon Well

```text
Action: Plug and Abandon Well
Target: Inactive or late-life well
Cost: Medium
Effect: Reduces abandonment liability
Benefit: Improves regulatory compliance
```

---

## 17.3 Decommission Facility

```text
Action: Decommission Facility
Target: Field facility
Cost: High
Effect: Removes future liability
Benefit: Avoids end-game penalty
```

For MVP, abandonment may be simplified into one action:

```text
Abandon Field
Cost: abandonment liability
Effect: Removes field and liability
```

---

# 18. AI and Team Actions

These actions support collaboration and decision-making.

---

## 18.1 Ask AI Advisor

```text
Action: Ask AI Advisor
Target: Company, block, field, or action proposal
Cost: No game cost or small analytics cost
Effect: Generates recommendation
```

This does not consume an action slot in MVP.

---

## 18.2 Create Proposal

```text
Action: Create Proposal
Target: Team action board
Effect: Adds proposed action for discussion
```

This is a collaboration action, not a game-world action.

---

## 18.3 Vote on Proposal

```text
Action: Vote on Proposal
Target: Team proposal
Effect: Shows support or rejection
```

---

## 18.4 Approve Proposal

```text
Action: Approve Proposal
Target: Team proposal
Effect: Converts proposal into committed turn action
Requirement: CEO or authorized player
```

---

# 19. Turn Resolution Order

The server should resolve each turn in a consistent order.

Recommended order:

```text
1. Validate submitted actions
2. Process license auctions
3. Process license fees and expiries
4. Process exploration studies
5. Process seismic acquisition
6. Resolve drilling actions
7. Update discoveries and appraisal results
8. Advance development projects
9. Start production from completed projects
10. Calculate production volumes
11. Apply commodity prices
12. Calculate revenue
13. Apply OPEX, CAPEX, debt, tax, and fees
14. Resolve production optimization
15. Apply market and random events
16. Apply safety, environmental, and reputation effects
17. Update abandonment liabilities
18. Update company valuation
19. Update leaderboard
20. Generate turn report
```

This order should be implemented server-side.

---

# 20. Company Dashboard

The player should always be able to see company status.

Recommended dashboard values:

```text
Cash
Debt
Revenue
OPEX
CAPEX committed
Net income
Production rate
Reserves
Asset value
Reputation
Safety rating
Environmental rating
Abandonment liability
Rank
```

MVP dashboard values:

```text
Cash
Debt
Production
Reserves
Asset value
Reputation
Rank
```

---

# 21. Player Feedback

Every important result should be shown clearly.

Bad feedback:

```text
Your action failed.
```

Good feedback:

```text
Falcon-1 was a dry hole.
The well found good reservoir quality but no trap closure.
Your geological confidence in nearby Block 6 increased slightly.
Cash decreased by $42M.
```

The player should learn from outcomes.

---

# 22. Visibility Rules

The game should control what information is public, private, and hidden.

---

## 22.1 Public Information

Visible to all players:

* License ownership
* Auction winners
* Major discoveries
* Producing fields
* Public production ranking
* Company rank
* Some event outcomes
* Public reputation

---

## 22.2 Private Company Information

Visible only to company/team:

* Cash details
* Debt details
* Internal technical studies
* Seismic interpretation
* Field economics
* AI recommendations
* Team chat
* Action proposals

---

## 22.3 Hidden Server Information

Visible only to server:

* True geology
* Exact recoverable volume before discovery
* Future random events
* Competitor private strategy
* Undiscovered reservoir truth
* Hidden event seeds

The AI must not receive hidden server-only information.

---

# 23. Leaderboard

The leaderboard should update every turn.

Recommended leaderboard values:

```text
Rank
Company Name
Company Value
Cash
Production
Reserves
Reputation
Debt Risk
```

In competitive mode, not all details need to be visible.

MVP leaderboard:

```text
Rank
Company Name
Company Value
Production
Reserves
```

---

# 24. Company Valuation

Company value is the main in-game score during a match.

Simplified MVP valuation:

```text
Company Value =
Cash
- Debt
+ Producing Field Value
+ Discovery Value
+ License Option Value
- Abandonment Liability
+ Reputation Bonus
```

The exact formulas will be defined in BOGS-GDD-004 and BOGS-GDD-015.

---

# 25. Failure States

The game should avoid eliminating players too early.

If a company runs out of cash, it enters financial distress.

Possible consequences:

* Cannot bid for new blocks
* Cannot drill new wells
* Must sell assets
* Must take expensive debt
* Reputation drops
* Final score penalty

Bankruptcy can be added later, but MVP should allow players to continue.

---

# 26. MVP Gameplay Rules

The MVP should use simple, clear rules.

## 26.1 MVP Action Slots

```text
Each company gets 3 action slots per turn.
```

## 26.2 MVP Actions

Available MVP actions:

```text
Bid for License
Geological Study
Acquire 2D Seismic
Drill Exploration Well
Drill Appraisal Well
Approve Development
Optimize Field
Take Debt
Repay Debt
Hedge Production
Sell Asset
Abandon Field
Ask AI Advisor
```

---

## 26.3 MVP Resource Values

Starting values:

```text
Starting Cash: $500M
Starting Debt: $0
Starting Reputation: 50/100
Starting Production: 0
Starting Reserves: 0
```

---

## 26.4 MVP Costs

Initial balancing values:

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

These are placeholder values and must be balanced through testing.

---

## 26.5 MVP Turn Count

```text
20 turns
Each turn = 6 months
Total game time = 10 years
```

---

# 27. Example Turn

## Turn 3 Planning

Company: Beep Energy
Cash: $430M
Owned blocks: Block 4, Block 7
Known information:

```text
Block 4:
- Geological study completed
- Chance of success: 22%

Block 7:
- 2D seismic completed
- Chance of success: 34%
```

Player chooses:

```text
Action 1: Drill exploration well on Block 7
Action 2: Buy 2D seismic on Block 4
Action 3: Bid $25M for Block 12
```

---

## Turn 3 Resolution

Results:

```text
Block 7 exploration well:
Commercial discovery found.
Estimated recoverable volume: 95 MMbbl.
Confidence: Low.

Block 4 seismic:
Chance of success improved from 22% to 31%.

Block 12 auction:
Bid lost. Competitor won with $30M.

Financial:
Cash decreased from $430M to $375M.

Leaderboard:
Beep Energy moved from rank 3 to rank 2.
```

---

# 28. Example Player Decision

The player owns a discovery with uncertain reserves.

```text
Falcon Discovery:
Estimated recoverable volume: 60–180 MMbbl
Confidence: Low
Development cost estimate: $220M
Cash: $260M
```

The player can:

```text
Option A: Drill appraisal well
- Costs $30M
- Improves confidence
- Delays development

Option B: Approve development now
- Faster first oil
- Higher risk of overbuilding

Option C: Sell discovery
- Immediate cash
- Lose future upside
```

This type of decision should be central to the game.

---

# 29. Game Feel Requirements

The game should feel:

* Strategic
* Competitive
* Risky
* Clear
* Business-like
* Rewarding
* Educational but not boring
* Serious but still fun

The player should feel tension before drilling and satisfaction after good decisions.

---

# 30. Design Risks

## 30.1 Too Much Complexity

Oil and gas is complex. The game must simplify.

Solution:

* Use layered complexity
* Start with simple actions
* Add advanced options later
* Use AI to explain decisions

---

## 30.2 Too Much Randomness

If players feel outcomes are random, the game will be frustrating.

Solution:

* Show probabilities
* Let players reduce risk through studies
* Explain why results happened
* Use partial information from dry holes

---

## 30.3 Slow Gameplay

Oil and gas projects take many years, but the game must stay exciting.

Solution:

* Use 6-month turns
* Resolve actions quickly
* Use events
* Show animations
* Provide meaningful choices every turn

---

## 30.4 AI Feels Like Cheating

If the AI knows hidden truth, the game becomes unfair.

Solution:

* AI receives only player-known data
* AI gives probabilistic advice
* AI explains uncertainty
* AI cannot execute without approval

---

# 31. Open Design Questions

The following questions should be finalized during prototype testing:

1. Should MVP have 3 or 4 action slots per turn?
2. Should 3D seismic be included in MVP or Phase 2?
3. Should development require appraisal first?
4. Should players be allowed to sell assets in MVP?
5. Should abandonment be required before final scoring?
6. Should dry holes give useful geological information?
7. Should license blocks expire?
8. Should rigs be limited in MVP?
9. Should debt have a hard limit?
10. Should AI advisor usage be free or limited?

---

# 32. Recommended MVP Gameplay Decision

For the first playable version, the recommended gameplay rules are:

```text
Game Mode: Turn-based multiplayer
Players: 2–6
Turns: 20
Action Slots: 3 per company per turn
Map: 20 blocks
Primary Resource: Cash
Primary Commodity: Oil
First Actions: Bid, Study, Seismic, Drill, Appraise, Develop, Produce, Optimize, Hedge, Abandon
AI: Strategy Advisor only
Team Mode: Basic chat and proposal board
Victory: Highest company value
```

This is enough to create a complete, playable, competitive first version.
