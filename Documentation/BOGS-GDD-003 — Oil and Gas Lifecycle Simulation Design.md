# Beep Oil and Gas Sim

## Oil and Gas Lifecycle Simulation Design

**Document ID:** BOGS-GDD-003
**Version:** 0.1
**Status:** Draft
**Parent Document:** BOGS-GDD-001 — Master Game Design Document
**Related Document:** BOGS-GDD-002 — Gameplay Systems Design
**Project Name:** Beep Oil and Gas Sim
**Short Name:** Beep O&G Sim

---

# 1. Purpose

This document defines how Beep Oil and Gas Sim represents the full oil and gas asset lifecycle as game systems.

The purpose is not to create a full petroleum engineering simulator. The goal is to create a simplified but meaningful model that supports fun decisions, competition, uncertainty, and learning.

The lifecycle model should allow players to experience:

* Entering a basin
* Competing for acreage
* Studying geology
* Drilling exploration wells
* Discovering hydrocarbons
* Appraising discoveries
* Developing commercial fields
* Producing oil and gas
* Optimizing late-life assets
* Decommissioning and abandoning fields responsibly

---

# 2. Lifecycle Overview

The full lifecycle in the game is:

```text
Basin Entry
    ↓
License Acquisition
    ↓
Geological Evaluation
    ↓
Seismic Acquisition
    ↓
Prospect Generation
    ↓
Exploration Drilling
    ↓
Discovery or Dry Hole
    ↓
Appraisal
    ↓
Commercial Decision
    ↓
Development Planning
    ↓
Construction
    ↓
First Oil / First Gas
    ↓
Production
    ↓
Optimization
    ↓
Late-Life Management
    ↓
Decommissioning
    ↓
Abandonment
```

For gameplay, this lifecycle is simplified into asset stages.

---

# 3. Asset Stage Model

Each block, prospect, discovery, or field should have a stage.

Recommended stage enum:

```csharp
public enum AssetStage
{
    Unlicensed,
    Licensed,
    Studied,
    SeismicEvaluated,
    ProspectGenerated,
    ExplorationDrilling,
    DryHole,
    Discovery,
    Appraisal,
    CommercialDiscovery,
    DevelopmentPlanning,
    DevelopmentApproved,
    UnderConstruction,
    Producing,
    LateLife,
    Decommissioning,
    Abandoned,
    Sold
}
```

Simplified MVP stages:

```text
Unlicensed
Licensed
Studied
SeismicEvaluated
Discovery
Appraisal
Development
Producing
Abandoned
```

---

# 4. Lifecycle Design Principles

## 4.1 Every Stage Should Create a Decision

The lifecycle should not be a passive sequence.

Each stage should ask the player:

```text
Should I invest more?
Should I stop?
Should I sell?
Should I take more risk?
Should I wait?
Should I ask AI?
Should I beat competitors to the next step?
```

---

## 4.2 Information Should Improve Over Time

The player begins with uncertainty.

As they spend money and take actions, their knowledge improves.

Example:

```text
Before study:
Chance of success unknown.

After geological study:
Chance of success estimated at 18–28%.

After 2D seismic:
Chance of success estimated at 24–34%.

After 3D seismic:
Chance of success estimated at 32–41%.

After exploration well:
Discovery or dry hole result known.

After appraisal:
Recoverable volume confidence improves.
```

---

## 4.3 Risk Should Never Disappear Completely

Even with good data, oil and gas remains uncertain.

The player can reduce risk, but not eliminate it.

Examples:

* A strong seismic structure can still be dry.
* A discovery can be smaller than expected.
* A development can have cost overruns.
* Production can decline faster than expected.
* Abandonment can cost more than planned.

---

## 4.4 Simulation Must Support Competition

The lifecycle should create competitive pressure.

Examples:

* A competitor discovery increases nearby block value.
* Limited rigs delay drilling.
* A shared pipeline has limited capacity.
* Service costs rise if many companies drill at once.
* First mover gets better infrastructure terms.

---

# 5. Basin Model

A basin is the large region where the game takes place.

Each basin contains license blocks.

## 5.1 Basin Attributes

```csharp
public sealed class Basin
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";

    public BasinType BasinType { get; set; }

    public double GeologicalPotential { get; set; }
    public double InfrastructureMaturity { get; set; }
    public double PoliticalRisk { get; set; }
    public double EnvironmentalSensitivity { get; set; }
    public double ServiceCostIndex { get; set; }
    public double FiscalAttractiveness { get; set; }

    public List<LicenseBlock> Blocks { get; set; } = new();
}
```

## 5.2 Basin Types

```csharp
public enum BasinType
{
    DesertOnshore,
    MatureOnshore,
    OffshoreShelf,
    Deepwater,
    GasProvince,
    ShalePlay,
    ArcticFrontier
}
```

## 5.3 Basin Gameplay Effects

| Basin Attribute           | Gameplay Effect                            |
| ------------------------- | ------------------------------------------ |
| Geological Potential      | Affects chance of finding hydrocarbons     |
| Infrastructure Maturity   | Affects development cost and time          |
| Political Risk            | Affects taxes, license security, events    |
| Environmental Sensitivity | Affects HSE penalties and abandonment cost |
| Service Cost Index        | Affects drilling and development costs     |
| Fiscal Attractiveness     | Affects profit after tax/royalty           |

---

# 6. License Block Model

A license block is the basic map unit players compete for.

## 6.1 Block Public Data

Public data is visible before bidding.

```csharp
public sealed class PublicBlockData
{
    public string Name { get; set; } = "";
    public double SurfaceRisk { get; set; }
    public double InfrastructureAccess { get; set; }
    public double EnvironmentalSensitivity { get; set; }
    public double NearbyDiscoveryIndex { get; set; }
    public string PublicGeologyHint { get; set; } = "";
}
```

Example public hint:

```text
Block 7 lies on a structural trend near mature source rocks.
Infrastructure access is moderate.
Environmental restrictions are low.
```

## 6.2 Hidden Geological Truth

Hidden truth is known only to the server.

```csharp
public sealed class HiddenGeology
{
    public double SourceRockQuality { get; set; }
    public double ReservoirQuality { get; set; }
    public double TrapIntegrity { get; set; }
    public double SealQuality { get; set; }
    public double TimingMigration { get; set; }

    public FluidType FluidType { get; set; }
    public double RecoverableVolumeMmboe { get; set; }
    public double DepthMeters { get; set; }
    public double PressureIndex { get; set; }
    public double ContaminantRisk { get; set; }
}
```

## 6.3 Fluid Types

```csharp
public enum FluidType
{
    Unknown,
    Dry,
    Oil,
    Gas,
    Condensate,
    OilAndGas
}
```

The AI advisor must not receive hidden geology unless the player has legitimately discovered it.

---

# 7. Knowledge and Confidence Model

The game should track what the player knows separately from the hidden truth.

## 7.1 Knowledge Level

```csharp
public enum KnowledgeLevel
{
    None,
    PublicHint,
    GeologicalStudy,
    TwoDSeismic,
    ThreeDSeismic,
    ExplorationWell,
    AppraisalWell,
    ProductionHistory
}
```

## 7.2 Player Interpretation

```csharp
public sealed class BlockKnowledge
{
    public Guid CompanyId { get; set; }
    public Guid BlockId { get; set; }

    public KnowledgeLevel KnowledgeLevel { get; set; }

    public double EstimatedChanceOfSuccess { get; set; }
    public double Confidence { get; set; }

    public double EstimatedLowVolumeMmboe { get; set; }
    public double EstimatedMidVolumeMmboe { get; set; }
    public double EstimatedHighVolumeMmboe { get; set; }

    public string InterpretationSummary { get; set; } = "";
}
```

## 7.3 Confidence Meaning

Confidence should represent how reliable the estimate is.

```text
0–20: Very uncertain
21–40: Low confidence
41–60: Moderate confidence
61–80: High confidence
81–100: Very high confidence
```

Example:

```text
Block 11
Estimated chance of success: 32%
Estimated recoverable volume: 40–180 MMboe
Confidence: 38/100
```

---

# 8. Exploration Simulation

Exploration determines whether a block contains a discovery.

## 8.1 Geological Chance Components

The hidden chance of success can be built from five petroleum system factors:

```text
Source Rock
Reservoir
Trap
Seal
Timing / Migration
```

Simplified formula:

```text
Geological Chance of Success =
Source × Reservoir × Trap × Seal × Timing
```

Each factor is between 0 and 1.

Example:

```text
Source: 0.80
Reservoir: 0.70
Trap: 0.60
Seal: 0.75
Timing: 0.65

Chance = 0.80 × 0.70 × 0.60 × 0.75 × 0.65
Chance = 0.1638
Chance = 16.38%
```

For gameplay, this can be adjusted upward slightly to avoid too many dry holes.

---

## 8.2 Gameplay Chance of Success

The displayed chance to the player is not always the true chance.

It is an estimate based on knowledge level.

```text
True Chance: known only to server
Estimated Chance: shown to player
Confidence: reliability of estimate
```

With low confidence, the estimate may be wrong.

Example:

```text
True chance: 35%
Displayed after public hints: 18–45%
Displayed after 2D seismic: 28–39%
Displayed after 3D seismic: 32–37%
```

---

## 8.3 Exploration Well Resolution

When the player drills, the server uses the true chance.

Basic logic:

```csharp
public ExplorationResult ResolveExplorationWell(Block block, DrillingProgram program, Random rng)
{
    var chance = block.HiddenGeology.CalculateTrueChanceOfSuccess();

    chance += program.SuccessModifier;
    chance -= block.HiddenGeology.DepthMeters > 4500 ? 0.03 : 0;
    chance -= block.HiddenGeology.ContaminantRisk * 0.02;

    chance = Math.Clamp(chance, 0.02, 0.75);

    var roll = rng.NextDouble();

    if (roll <= chance)
    {
        return CreateDiscovery(block);
    }

    return CreateDryHole(block);
}
```

---

# 9. Discovery Model

A discovery occurs when an exploration well finds hydrocarbons.

## 9.1 Discovery Attributes

```csharp
public sealed class Discovery
{
    public Guid Id { get; set; }
    public Guid BlockId { get; set; }
    public Guid OwnerCompanyId { get; set; }

    public string Name { get; set; } = "";
    public FluidType FluidType { get; set; }

    public double EstimatedLowVolumeMmboe { get; set; }
    public double EstimatedMidVolumeMmboe { get; set; }
    public double EstimatedHighVolumeMmboe { get; set; }

    public double Confidence { get; set; }
    public double CommercialChance { get; set; }

    public double DevelopmentDifficulty { get; set; }
    public double ContaminantRisk { get; set; }
    public double PressureRisk { get; set; }

    public AssetStage Stage { get; set; }
}
```

## 9.2 Discovery Size Classes

| Class          |  Recoverable Volume |
| -------------- | ------------------: |
| Non-commercial |  Less than 20 MMboe |
| Small          |         20–75 MMboe |
| Medium         |        75–200 MMboe |
| Large          |       200–500 MMboe |
| Giant          | More than 500 MMboe |

For MVP, use:

```text
Non-commercial
Commercial
Major discovery
```

---

# 10. Dry Hole Model

Dry holes should not feel useless.

A dry hole should provide geological learning.

## 10.1 Dry Hole Results

A dry hole may reveal:

* No reservoir
* No hydrocarbons
* Poor seal
* No trap closure
* Wrong migration timing
* High water saturation

Example result:

```text
Falcon-1 was a dry hole.
The well encountered reservoir-quality sand, but no effective trap closure.
Nearby structural prospects are now considered higher risk.
```

## 10.2 Dry Hole Benefits

Dry holes can:

* Improve basin knowledge
* Reduce uncertainty in nearby blocks
* Help competitors if public
* Unlock better geological interpretation
* Reduce future wasted spending

This makes failure educational and strategic.

---

# 11. Appraisal Simulation

Appraisal improves the estimate of a discovery.

## 11.1 Appraisal Purpose

Appraisal answers:

```text
How big is the discovery?
Is it commercial?
What development concept fits?
What is the production potential?
What are the risks?
```

## 11.2 Appraisal Result Types

Possible outcomes:

```text
Volume upgraded
Volume unchanged
Volume downgraded
Commerciality improved
Commerciality reduced
Development difficulty increased
Field becomes non-commercial
```

## 11.3 Confidence Increase

Each appraisal well increases confidence.

Example:

```text
Before appraisal:
Volume estimate: 50–220 MMboe
Confidence: 35

After one appraisal:
Volume estimate: 80–160 MMboe
Confidence: 58

After two appraisals:
Volume estimate: 95–140 MMboe
Confidence: 75
```

## 11.4 Appraisal Formula

```text
New Confidence =
Old Confidence + Appraisal Quality + Data Bonus - Complexity Penalty
```

Example values:

```text
Standard appraisal well: +20 confidence
Premium appraisal well: +28 confidence
Complex reservoir penalty: -5
Good 3D seismic bonus: +8
```

---

# 12. Commerciality Model

A discovery is commercial if expected value is attractive.

## 12.1 Commerciality Inputs

```text
Recoverable volume
Fluid type
Oil/gas price
Development cost
Operating cost
Distance to infrastructure
Fiscal terms
Technical risk
Environmental restrictions
Abandonment liability
```

## 12.2 Simplified Commerciality Score

```text
Commerciality Score =
Resource Value
- Development Cost
- Operating Cost
- Risk Penalty
- Abandonment Liability
```

For gameplay, show a simple rating:

```text
Poor
Marginal
Commercial
Attractive
World-class
```

---

# 13. Development Simulation

Development converts a commercial discovery into a producing field.

## 13.1 Development Options

MVP options:

```text
Small Development
Standard Development
Large Development
```

Expanded options:

```text
Onshore central processing facility
Offshore fixed platform
FPSO
Subsea tieback
Pipeline connection
Gas-to-power
LNG export
Early production system
```

## 13.2 Development Attributes

```csharp
public sealed class DevelopmentConcept
{
    public string Name { get; set; } = "";

    public decimal Capex { get; set; }
    public decimal OpexPerTurn { get; set; }

    public int ConstructionTurns { get; set; }
    public double ProductionCapacityBoePerDay { get; set; }

    public double Reliability { get; set; }
    public double EmissionsIndex { get; set; }
    public double SafetyRisk { get; set; }
    public decimal AbandonmentLiability { get; set; }
}
```

## 13.3 Development Tradeoffs

| Option               | Benefit                   | Drawback                       |
| -------------------- | ------------------------- | ------------------------------ |
| Small Development    | Cheap, fast               | Lower peak production          |
| Standard Development | Balanced                  | Medium cost                    |
| Large Development    | High production           | Expensive, slower, risky       |
| Tieback              | Lower cost                | Requires nearby infrastructure |
| FPSO                 | Good offshore flexibility | High lease cost                |
| LNG                  | Monetizes gas             | Very high CAPEX                |

---

# 14. Construction Simulation

Development takes multiple turns.

## 14.1 Construction Progress

Each development project has progress:

```text
0% → 100%
```

Example:

```text
Standard Development:
Turn 1: 33%
Turn 2: 66%
Turn 3: 100%
Turn 4: First oil
```

## 14.2 Construction Events

Possible events:

```text
Cost overrun
Construction delay
Safety incident
Fast-track success
Supply chain shortage
Regulatory delay
```

MVP should keep construction simple:

```text
Project progresses by fixed amount each turn.
Events may delay by 1 turn.
```

---

# 15. Production Simulation

Production starts after development completion.

## 15.1 Field Production Attributes

```csharp
public sealed class ProducingField
{
    public Guid Id { get; set; }
    public Guid OwnerCompanyId { get; set; }

    public double RemainingRecoverableMmboe { get; set; }
    public double CurrentProductionBoePerDay { get; set; }
    public double FacilityCapacityBoePerDay { get; set; }
    public double DeclineRatePerTurn { get; set; }
    public double Uptime { get; set; }
    public double WaterCut { get; set; }

    public decimal OpexPerTurn { get; set; }
    public decimal AbandonmentLiability { get; set; }

    public AssetStage Stage { get; set; }
}
```

## 15.2 Simplified Production Formula

```text
Produced Volume This Turn =
Current Daily Rate × Days Per Turn × Uptime
```

For a 6-month turn:

```text
Days Per Turn = 182.5
```

Example:

```text
20,000 boe/day × 182.5 × 0.95 = 3.47 MMboe
```

## 15.3 Decline Formula

```text
Next Turn Rate =
Current Rate × (1 - Decline Rate) × Optimization Modifier
```

Example:

```text
20,000 × (1 - 0.08) × 1.05 = 19,320 boe/day
```

---

# 16. Production Phases

A field can move through production phases.

```text
Ramp-Up
Plateau
Decline
Late-Life
Shut-In
Abandoned
```

## 16.1 Ramp-Up

Field is newly started.

* Production increases toward capacity.
* Higher early operational risk.

## 16.2 Plateau

Field produces near capacity.

* Best revenue period.
* Optimization has high value.

## 16.3 Decline

Production gradually falls.

* Water cut increases.
* OPEX per barrel rises.

## 16.4 Late-Life

Production is low.

* Profit margins shrink.
* Abandonment pressure increases.

## 16.5 Shut-In

Field is temporarily or permanently stopped.

* No production revenue.
* Some holding costs remain.
* Abandonment liability remains.

---

# 17. Optimization Simulation

Players can improve asset performance.

## 17.1 Optimization Actions

```text
Workover
Artificial lift
Water injection
Gas injection
Facility debottlenecking
Digital optimization
Maintenance campaign
```

## 17.2 MVP Optimization

For MVP, use one action:

```text
Optimize Field
Cost: $20M
Effect:
- +10% production next turn
- -5% downtime risk for 2 turns
```

## 17.3 Expanded Optimization Effects

| Optimization    | Effect                               |
| --------------- | ------------------------------------ |
| Workover        | Restores lost production             |
| Artificial Lift | Improves late-life oil rate          |
| Water Injection | Slows decline                        |
| Gas Injection   | Improves recovery                    |
| Debottlenecking | Increases facility capacity          |
| Maintenance     | Improves uptime and lowers incidents |

---

# 18. Late-Life Simulation

A field enters late-life when production becomes low or remaining reserves are limited.

## 18.1 Late-Life Trigger

A field enters late-life when one or more conditions apply:

```text
Production below 25% of peak
Remaining reserves below 20%
OPEX per barrel too high
Water cut above threshold
Field is near license expiry
```

## 18.2 Late-Life Decisions

Players can:

```text
Keep producing
Optimize field
Reduce OPEX
Sell asset
Shut in field
Prepare abandonment
Repurpose infrastructure
Abandon field
```

## 18.3 Late-Life Risk

Ignoring late-life obligations creates:

```text
Higher abandonment liability
Reputation penalty
Regulatory penalty
Environmental risk
Final score penalty
```

---

# 19. Abandonment Simulation

Abandonment should be meaningful but simple.

## 19.1 Abandonment Scope

Abandonment may include:

```text
Plugging wells
Removing facilities
Cleaning site
Restoring land/seabed
Regulatory closeout
```

## 19.2 Abandonment Liability

Each field has abandonment liability.

```text
Abandonment Liability =
Well Count Cost
+ Facility Removal Cost
+ Environmental Sensitivity Cost
+ Regulatory Complexity Cost
```

MVP simplified formula:

```text
Abandonment Liability =
Base Field Liability × Environmental Multiplier
```

Example:

```text
Base Liability: $40M
Environmental Multiplier: 1.25
Final Liability: $50M
```

## 19.3 Abandonment Action

```text
Action: Abandon Field
Cost: Current abandonment liability
Effect:
- Field removed from active assets
- Liability cleared
- Reputation protected or improved
```

## 19.4 Delayed Abandonment Penalty

If a player ends the match with unhandled abandonment liability:

```text
Final Score Penalty = Unfunded Abandonment Liability × Penalty Multiplier
```

Recommended MVP:

```text
Penalty Multiplier = 1.5
```

---

# 20. Asset Repurposing

Repurposing can be added after MVP.

Possible repurposing options:

```text
Gas storage
Carbon storage
Hydrogen hub
Offshore wind support
Training facility
Pipeline reuse
```

This can make late-life decisions more interesting and connect the game to energy transition themes.

---

# 21. Lifecycle State Transitions

Recommended transitions:

```text
Unlicensed
    → Licensed

Licensed
    → Studied
    → SeismicEvaluated
    → ExplorationDrilling

ExplorationDrilling
    → DryHole
    → Discovery

Discovery
    → Appraisal
    → CommercialDiscovery
    → Sold

CommercialDiscovery
    → DevelopmentPlanning
    → DevelopmentApproved
    → UnderConstruction

UnderConstruction
    → Producing

Producing
    → LateLife
    → Sold
    → Decommissioning

LateLife
    → Optimized
    → Sold
    → Decommissioning

Decommissioning
    → Abandoned
```

---

# 22. Server Resolution by Lifecycle Stage

The server should process lifecycle updates during turn resolution.

Recommended lifecycle processing order:

```text
1. Update license ownership
2. Apply studies and seismic results
3. Resolve exploration wells
4. Create discoveries or dry holes
5. Resolve appraisal wells
6. Update commerciality
7. Advance development projects
8. Start production for completed projects
9. Calculate production and decline
10. Apply optimization effects
11. Check late-life triggers
12. Update abandonment liability
13. Process abandonment actions
14. Update asset valuation
```

---

# 23. MVP Lifecycle Scope

For MVP, include:

```text
License
Geological Study
2D Seismic
Exploration Well
Discovery or Dry Hole
Appraisal
Simple Development
Production
Optimization
Abandonment
```

Do not include in MVP:

```text
3D seismic
Complex petroleum system modeling
Multiple reservoir layers
Detailed well trajectories
Enhanced oil recovery
Subsea engineering
LNG chain
Carbon storage
Detailed abandonment engineering
```

---

# 24. Example Lifecycle Scenario

## Turn 1

Player bids on Block 5 and wins.

```text
Block 5 stage: Licensed
```

## Turn 2

Player buys geological study.

```text
Block 5 stage: Studied
Estimated chance of success: 20%
Confidence: 25
```

## Turn 3

Player acquires 2D seismic.

```text
Block 5 stage: SeismicEvaluated
Estimated chance of success: 32%
Confidence: 45
```

## Turn 4

Player drills exploration well.

Result:

```text
Commercial Discovery
Estimated volume: 80–160 MMboe
Confidence: 35
```

## Turn 5

Player drills appraisal well.

Result:

```text
Updated volume: 100–140 MMboe
Confidence: 60
Commerciality: Attractive
```

## Turn 6

Player approves standard development.

```text
Development cost: $220M
Construction duration: 3 turns
```

## Turns 7–9

Development progresses.

```text
Turn 7: 33%
Turn 8: 66%
Turn 9: 100%
```

## Turn 10

Field starts production.

```text
Initial production: 25,000 boe/day
```

## Turns 11–17

Field produces and declines.

Player optimizes field twice.

## Turn 18

Field enters late-life.

## Turn 19

Player abandons field.

## Turn 20

Player avoids abandonment penalty and gains reputation bonus.

---

# 25. AI Integration With Lifecycle

The AI Command Center should understand the lifecycle stage of every asset.

Example AI behavior:

## Licensed Block

```text
The AI should discuss exploration risk and whether to buy data.
```

## Seismic Evaluated Block

```text
The AI should compare drill-now versus more study.
```

## Discovery

```text
The AI should recommend appraisal, sale, or development delay.
```

## Producing Field

```text
The AI should discuss optimization, cash flow, and decline.
```

## Late-Life Field

```text
The AI should discuss abandonment, sale, or repurposing.
```

The AI must never reveal hidden lifecycle truth.

---

# 26. Lifecycle UI Requirements

The UI should clearly show where each asset is in the lifecycle.

Recommended visual indicators:

```text
Block color by ownership
Stage icon
Confidence meter
Risk meter
Estimated value range
Next recommended action
AI advisor button
```

Example block panel:

```text
Block 8
Owner: Beep Energy
Stage: Seismic Evaluated
Estimated Chance of Success: 34%
Confidence: Medium
Next Actions:
- Drill Exploration Well
- Acquire 3D Seismic
- Relinquish License
- Ask AI Advisor
```

Example field panel:

```text
Falcon Field
Stage: Producing
Current Rate: 22,000 boe/day
Remaining Reserves: 74 MMboe
Decline Rate: 8% per turn
Uptime: 94%
Abandonment Liability: $42M
Next Actions:
- Optimize Field
- Maintenance Campaign
- Sell Asset
- Prepare Abandonment
```

---

# 27. Balancing Guidelines

Lifecycle pacing must feel satisfying.

Recommended MVP pacing:

```text
License acquisition: same turn
Geological study: 1 turn
2D seismic: 1 turn
Exploration well: 1 turn
Appraisal well: 1 turn
Development: 2–4 turns
Production: starts after development
Late-life: after several production turns
Abandonment: 1 turn
```

A player should be able to take one asset from license to production within a 20-turn match.

---

# 28. Design Risks

## 28.1 Lifecycle Too Slow

If development takes too long, players may not see production before the match ends.

Solution:

* Use compressed time
* Use fast development options
* Start some scenarios with existing discoveries
* Use 20-turn or longer matches

## 28.2 Too Many Dry Holes

If players fail too often, the game becomes frustrating.

Solution:

* Improve early estimates
* Give dry holes useful information
* Allow smaller discoveries
* Allow asset sales and debt recovery

## 28.3 Too Much Hidden Information

If everything is hidden, players feel powerless.

Solution:

* Show estimated probabilities
* Show confidence levels
* Let studies improve knowledge
* Let AI explain uncertainty

## 28.4 Abandonment Feels Like Punishment Only

If abandonment is only a cost, players may dislike it.

Solution:

* Add reputation benefits
* Add final score protection
* Add repurposing options later
* Make early planning reduce cost

---

# 29. Open Questions

1. Should 3D seismic be included in MVP or Phase 2?
2. Should dry holes improve nearby block estimates for all players or only the drilling company?
3. Should appraisal be required before development?
4. Should there be non-commercial discoveries in MVP?
5. Should gas be included from the first version?
6. Should abandonment be required before final scoring?
7. Should late-life fields allow repurposing in the first full release?
8. Should nearby discoveries modify public block values?
9. Should development delays be random in MVP?
10. Should production decline be linear, exponential, or simplified percentage-based?

---

# 30. Recommended MVP Lifecycle Decision

For the MVP, use this lifecycle:

```text
Unlicensed
    ↓
Licensed
    ↓
Studied
    ↓
Seismic Evaluated
    ↓
Exploration Well
    ↓
Discovery or Dry Hole
    ↓
Appraisal
    ↓
Development
    ↓
Producing
    ↓
Late-Life
    ↓
Abandoned
```

Recommended MVP simplifications:

```text
- Oil only
- One basin
- 20 blocks
- 2D seismic only
- Simple exploration success model
- Simple appraisal confidence model
- Three development sizes
- Simple production decline
- One optimization action
- One abandonment action
```

This creates a complete lifecycle while keeping the first implementation achievable.
