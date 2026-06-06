# Beep Oil and Gas Sim

## Exploration and Geology Design

**Document ID:** BOGS-GDD-005
**Version:** 0.1
**Status:** Draft
**Parent Document:** BOGS-GDD-001 — Master Game Design Document
**Related Documents:**

* BOGS-GDD-002 — Gameplay Systems Design
* BOGS-GDD-003 — Oil and Gas Lifecycle Simulation Design
* BOGS-GDD-004 — Economy, Finance, and Market Design

**Project Name:** Beep Oil and Gas Sim
**Short Name:** Beep O&G Sim

---

# 1. Purpose

This document defines the exploration and geology systems for Beep Oil and Gas Sim.

Exploration is one of the most important gameplay systems because it creates uncertainty, risk, excitement, and competition.

The system should allow players to:

* Evaluate basins
* Bid for blocks
* Study geology
* Acquire seismic
* Generate prospects
* Estimate chance of success
* Drill exploration wells
* Discover hydrocarbons
* Learn from dry holes
* Improve knowledge over time

The goal is to make exploration feel like smart risk-taking, not random gambling.

---

# 2. Exploration Design Goals

The exploration system should achieve the following goals:

## 2.1 Create Excitement

Drilling an exploration well should feel like a major moment.

The player should feel tension before the result and excitement or disappointment afterward.

---

## 2.2 Reward Smart Preparation

Players who invest in geological studies and seismic should make better decisions.

Buying data should not guarantee success, but it should improve estimates and reduce uncertainty.

---

## 2.3 Preserve Uncertainty

Players should never know the full truth before drilling.

Even strong prospects can fail.

Even weak-looking blocks can occasionally surprise players.

---

## 2.4 Support Competition

Exploration should create multiplayer pressure.

Examples:

* Competitor discoveries increase interest in nearby blocks.
* Public dry holes affect basin perception.
* Players compete for the best acreage.
* Players may overbid based on incomplete information.

---

## 2.5 Keep Geology Understandable

The system should use real oil and gas concepts, but in simplified game terms.

The player should understand:

* Why a block is risky
* What data improved
* Why a well succeeded or failed
* What the next best action might be

---

# 3. Core Exploration Concepts

The game uses simplified petroleum system concepts.

A successful hydrocarbon discovery requires:

```text
Source rock
Reservoir
Trap
Seal
Timing and migration
```

Each factor contributes to geological chance of success.

In game terms:

| Concept            | Gameplay Meaning                                       |
| ------------------ | ------------------------------------------------------ |
| Source Rock        | Is there hydrocarbon generation?                       |
| Reservoir          | Is there rock that can store and flow hydrocarbons?    |
| Trap               | Is there a structure or stratigraphic closure?         |
| Seal               | Can hydrocarbons be trapped and preserved?             |
| Timing / Migration | Did hydrocarbons move into the trap at the right time? |

---

# 4. Exploration Asset Hierarchy

Exploration assets follow this hierarchy:

```text
Basin
  ↓
License Block
  ↓
Lead
  ↓
Prospect
  ↓
Exploration Well
  ↓
Dry Hole or Discovery
```

## 4.1 Basin

The basin is the regional geological setting.

Example:

```text
Desert Frontier Basin
```

## 4.2 License Block

The block is the area a player can bid for and own.

Example:

```text
Block D-14
```

## 4.3 Lead

A lead is an early possible opportunity with weak evidence.

Example:

```text
Northern Closure Lead
```

## 4.4 Prospect

A prospect is a more mature drilling target.

Example:

```text
Falcon Prospect
```

## 4.5 Exploration Well

An exploration well tests the prospect.

Example:

```text
Falcon-1
```

## 4.6 Discovery or Dry Hole

The well result creates either:

```text
Discovery
Dry Hole
Non-commercial discovery
Technical failure
```

---

# 5. Basin Geology Model

Each basin has high-level geological settings that influence all blocks.

## 5.1 Basin Attributes

```csharp
public sealed class BasinGeology
{
    public Guid BasinId { get; set; }

    public string Name { get; set; } = "";

    public double RegionalSourceQuality { get; set; }
    public double RegionalReservoirQuality { get; set; }
    public double RegionalTrapDensity { get; set; }
    public double RegionalSealQuality { get; set; }
    public double RegionalMigrationEfficiency { get; set; }

    public double AverageDepthMeters { get; set; }
    public double StructuralComplexity { get; set; }
    public double DataAvailability { get; set; }
    public double FrontierRisk { get; set; }
}
```

All values should usually be normalized from 0 to 1, except depth.

---

## 5.2 Basin Type Modifiers

Different basin types affect exploration behavior.

| Basin Type      | Exploration Characteristics                                      |
| --------------- | ---------------------------------------------------------------- |
| Desert Onshore  | Lower drilling cost, moderate infrastructure, variable reservoir |
| Mature Onshore  | Better data, lower risk, smaller discoveries                     |
| Offshore Shelf  | Higher cost, larger prospects, moderate risk                     |
| Deepwater       | Very high cost, high reward, high uncertainty                    |
| Gas Province    | More gas-prone, infrastructure dependent                         |
| Shale Play      | Repeatable drilling, less conventional exploration               |
| Arctic Frontier | High cost, high environmental risk, high political risk          |

---

# 6. License Block Geological Model

Each license block has hidden geological truth.

## 6.1 Hidden Geology

```csharp
public sealed class HiddenGeology
{
    public Guid BlockId { get; set; }

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

    public double DevelopmentComplexity { get; set; }
}
```

## 6.2 Value Ranges

Recommended value interpretation:

```text
0.00–0.20 = Very Poor
0.21–0.40 = Poor
0.41–0.60 = Moderate
0.61–0.80 = Good
0.81–1.00 = Excellent
```

---

# 7. Public Block Information

Before owning or studying a block, the player sees only public information.

## 7.1 Public Block Data

```csharp
public sealed class PublicBlockData
{
    public Guid BlockId { get; set; }

    public string BlockName { get; set; } = "";
    public string PublicGeologyHint { get; set; } = "";

    public double InfrastructureAccess { get; set; }
    public double SurfaceRisk { get; set; }
    public double EnvironmentalSensitivity { get; set; }
    public double NearbyDiscoveryIndex { get; set; }

    public PublicRiskRating PublicRiskRating { get; set; }
}
```

## 7.2 Public Risk Rating

```csharp
public enum PublicRiskRating
{
    Unknown,
    Low,
    Moderate,
    High,
    VeryHigh
}
```

## 7.3 Example Public Block View

```text
Block D-08

Public Information:
- Located on a regional structural trend.
- Moderate distance from export pipeline.
- No wells drilled in block.
- Nearby basin has one small historical discovery.
- Environmental restrictions: Low.

Public Risk: Moderate to High
```

This should be enough to support auction decisions without revealing hidden truth.

---

# 8. Player Knowledge Model

The game must separate hidden truth from player knowledge.

Each company has its own interpretation of each block.

## 8.1 Block Knowledge

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

    public RiskFactorEstimate SourceEstimate { get; set; } = new();
    public RiskFactorEstimate ReservoirEstimate { get; set; } = new();
    public RiskFactorEstimate TrapEstimate { get; set; } = new();
    public RiskFactorEstimate SealEstimate { get; set; } = new();
    public RiskFactorEstimate TimingEstimate { get; set; } = new();

    public string InterpretationSummary { get; set; } = "";
}
```

## 8.2 Risk Factor Estimate

```csharp
public sealed class RiskFactorEstimate
{
    public double EstimatedValue { get; set; }
    public double Confidence { get; set; }
}
```

## 8.3 Knowledge Levels

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

---

# 9. Geological Chance of Success

The true geological chance of success is calculated from hidden geological factors.

## 9.1 Base Formula

```text
True Geological Chance =
Source Rock
× Reservoir
× Trap
× Seal
× Timing / Migration
```

Example:

```text
Source Rock: 0.85
Reservoir: 0.70
Trap: 0.60
Seal: 0.80
Timing: 0.75

True Chance = 0.85 × 0.70 × 0.60 × 0.80 × 0.75
True Chance = 0.2142
True Chance = 21.42%
```

## 9.2 Gameplay Adjustment

Pure geological multiplication may create many low probabilities.

For gameplay, apply a balancing adjustment:

```text
Gameplay Chance =
Base Geological Chance × Basin Opportunity Modifier × Game Balance Modifier
```

Recommended starting values:

```text
Basin Opportunity Modifier: 1.0–1.5
Game Balance Modifier: 1.1–1.4
```

Then clamp the result:

```text
Minimum Chance: 2%
Maximum Chance: 75%
```

Recommended MVP clamp:

```text
Minimum Chance: 5%
Maximum Chance: 60%
```

This prevents impossible or guaranteed exploration outcomes.

---

# 10. Estimated Chance of Success

The player does not see the true chance.

They see an estimate based on available data.

## 10.1 Estimate Accuracy by Knowledge Level

| Knowledge Level  | Typical Confidence |          Estimate Error |
| ---------------- | -----------------: | ----------------------: |
| Public Hint      |              10–20 |               Very High |
| Geological Study |              20–35 |                    High |
| 2D Seismic       |              35–55 |                  Medium |
| 3D Seismic       |              55–75 |                     Low |
| Exploration Well |              70–90 | Low for tested interval |
| Appraisal Well   |              75–95 |  Very Low for discovery |

## 10.2 Example Estimate Evolution

```text
True Chance: 35%

Public Hint:
Displayed Estimate: 18–48%
Confidence: 15

Geological Study:
Displayed Estimate: 24–42%
Confidence: 30

2D Seismic:
Displayed Estimate: 29–39%
Confidence: 48

3D Seismic:
Displayed Estimate: 32–37%
Confidence: 68
```

---

# 11. Exploration Data Actions

Players can buy data to improve estimates.

## 11.1 Geological Study

```text
Action: Geological Study
Cost: Low
Duration: 1 turn
Effect:
- Improves source rock estimate
- Improves regional risk estimate
- Adds public-style geological summary
- Slightly improves chance-of-success estimate
```

Recommended MVP cost:

```text
$5M
```

Example output:

```text
Geological Study Result:
Block D-08 appears to sit on a mature source trend.
Reservoir quality remains uncertain.
Estimated chance of success: 22%
Confidence: 28
```

---

## 11.2 2D Seismic

```text
Action: Acquire 2D Seismic
Cost: Medium
Duration: 1 turn
Effect:
- Improves trap estimate
- Improves structural confidence
- May generate a named prospect
```

Recommended MVP cost:

```text
$15M
```

Example output:

```text
2D Seismic Result:
A possible four-way closure is visible in the northern part of the block.
Trap confidence improved.
Estimated chance of success: 31%
Confidence: 45
```

---

## 11.3 3D Seismic

3D seismic should be Phase 2, not MVP.

```text
Action: Acquire 3D Seismic
Cost: High
Duration: 1–2 turns
Effect:
- Strongly improves trap and reservoir interpretation
- Reduces chance estimate error
- Improves volume estimate
```

Recommended future cost:

```text
$35M–$60M
```

---

## 11.4 AI Prospect Interpretation

```text
Action: AI Prospect Interpretation
Cost: Low to medium
Requirement: Geological study or seismic data
Effect:
- Produces explanation and recommendation
- Slightly improves confidence
- Helps player understand risk
```

This should not reveal hidden truth.

Example:

```text
AI Geologist:
Block D-08 is drillable but still risky.
The main uncertainty is seal quality.
If cash is limited, acquire more seismic before drilling.
```

---

# 12. Prospect Generation

A prospect is created when the company has enough information to identify a drillable target.

## 12.1 Prospect Creation Rules

A prospect may be generated after:

```text
Geological Study + 2D Seismic
```

or after:

```text
3D Seismic
```

For MVP:

```text
A prospect is automatically generated after 2D seismic.
```

## 12.2 Prospect Model

```csharp
public sealed class Prospect
{
    public Guid Id { get; set; }
    public Guid BlockId { get; set; }
    public Guid CompanyId { get; set; }

    public string Name { get; set; } = "";

    public double EstimatedChanceOfSuccess { get; set; }
    public double Confidence { get; set; }

    public double EstimatedLowVolumeMmboe { get; set; }
    public double EstimatedMidVolumeMmboe { get; set; }
    public double EstimatedHighVolumeMmboe { get; set; }

    public string MainRisk { get; set; } = "";
    public bool IsDrillReady { get; set; }
}
```

## 12.3 Prospect Naming

The game can auto-generate names.

Example names:

```text
Falcon Prospect
Oryx Prospect
Dune Prospect
Sahara North
Crescent Lead
Palm Structure
Horizon Prospect
```

---

# 13. Volume Estimation

Before drilling, volume is estimated as a range.

## 13.1 Volume Estimate

```text
Low Estimate
Mid Estimate
High Estimate
```

Example:

```text
Estimated Recoverable Volume:
Low: 40 MMboe
Mid: 110 MMboe
High: 240 MMboe
```

## 13.2 Estimate Range by Confidence

Low confidence creates a wide range.

High confidence creates a narrower range.

Example:

```text
Confidence 25:
40–240 MMboe

Confidence 50:
70–180 MMboe

Confidence 75:
90–140 MMboe
```

---

# 14. Exploration Well Design

The exploration well tests a prospect.

## 14.1 Drill Exploration Well Action

```text
Action: Drill Exploration Well
Target: Prospect or owned block
Cost: High
Duration: 1 turn
Result: Discovery, dry hole, or technical issue
```

Recommended MVP cost:

```text
$40M
```

---

## 14.2 Well Budget Classes

Players may select a well budget class.

```text
Cheap Well
Standard Well
Premium Well
```

## 14.3 Budget Effects

| Budget Class | Cost Modifier | Operational Risk | Data Quality |
| ------------ | ------------: | ---------------: | -----------: |
| Cheap        |          -20% |           Higher |        Lower |
| Standard     |            0% |           Normal |       Normal |
| Premium      |          +25% |            Lower |       Higher |

Recommended MVP:

```text
Use Standard Well only.
Add budget classes in Phase 2.
```

---

# 15. Exploration Result Types

An exploration well may result in:

```text
Dry Hole
Technical Failure
Non-commercial Discovery
Commercial Discovery
Major Discovery
Giant Discovery
```

## 15.1 MVP Result Types

MVP should use:

```text
Dry Hole
Non-commercial Discovery
Commercial Discovery
Major Discovery
```

---

## 15.2 Result Meaning

### Dry Hole

No commercial hydrocarbons found.

### Non-commercial Discovery

Hydrocarbons found, but not currently economic.

### Commercial Discovery

Recoverable volume and economics are good enough to consider appraisal/development.

### Major Discovery

Large discovery that can strongly change the game.

---

# 16. Exploration Resolution

The server resolves exploration wells.

## 16.1 Resolution Steps

```text
1. Get hidden geology
2. Calculate true chance of success
3. Apply drilling modifiers
4. Generate random roll
5. Determine success or failure
6. If success, generate discovery estimate
7. If failure, generate dry-hole lesson
8. Update company knowledge
9. Update public information if applicable
10. Update player report
```

## 16.2 Example Pseudocode

```csharp
public ExplorationWellResult ResolveExplorationWell(
    Company company,
    LicenseBlock block,
    Prospect prospect,
    DrillingProgram program,
    Random rng)
{
    var trueChance = CalculateTrueChance(block.HiddenGeology);

    trueChance += program.SuccessModifier;
    trueChance -= GetDepthPenalty(block.HiddenGeology.DepthMeters);
    trueChance -= GetOperationalRiskPenalty(program);

    trueChance = Math.Clamp(trueChance, 0.05, 0.60);

    var roll = rng.NextDouble();

    if (roll <= trueChance)
    {
        return CreateDiscoveryResult(company, block, prospect, rng);
    }

    return CreateDryHoleResult(company, block, prospect, rng);
}
```

---

# 17. Dry Hole Design

Dry holes are important.

They should hurt financially but still give information.

## 17.1 Dry Hole Cost

The player loses the well cost.

```text
Exploration Well Cost: $40M
Revenue: $0
```

## 17.2 Dry Hole Learning

A dry hole should reveal one or more failure reasons.

Example failure reasons:

```text
No effective reservoir
Poor seal
No trap closure
No hydrocarbon charge
Migration timing failure
Water-bearing reservoir
Technical well problem
```

## 17.3 Dry Hole Report Example

```text
Well Result: Dry Hole

Falcon-1 did not find commercial hydrocarbons.

Finding:
The well encountered reservoir-quality sandstone, but trap closure appears ineffective.

Effect:
- Trap risk increased for this block.
- Nearby structural prospects are now considered slightly riskier.
- Your basin knowledge improved.
```

---

# 18. Discovery Design

A discovery is created when the well succeeds.

## 18.1 Discovery Report Example

```text
Well Result: Commercial Discovery

Falcon-1 discovered light oil.

Initial Estimate:
- Recoverable volume: 80–160 MMbbl
- Mid case: 115 MMbbl
- Confidence: Low
- Development difficulty: Moderate
- Main risk: Reservoir continuity

Recommended Next Action:
Drill appraisal well before approving development.
```

## 18.2 Discovery Model

```csharp
public sealed class Discovery
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

    public string MainRisk { get; set; } = "";
    public AssetStage Stage { get; set; }
}
```

---

# 19. Discovery Size Classification

| Size Class     |  Recoverable Volume |
| -------------- | ------------------: |
| Non-commercial |  Less than 20 MMboe |
| Small          |         20–75 MMboe |
| Medium         |        75–200 MMboe |
| Large          |       200–500 MMboe |
| Giant          | More than 500 MMboe |

For MVP:

```text
Non-commercial: Less than 30 MMboe
Commercial: 30–150 MMboe
Major: More than 150 MMboe
```

---

# 20. Nearby Block Effects

A discovery should affect nearby blocks.

## 20.1 Public Discovery Effect

When a major discovery is announced:

```text
Nearby block public value increases.
Auction bids may become more aggressive.
Nearby geological chance estimates may improve.
```

## 20.2 Dry Hole Effect

When a dry hole is public:

```text
Nearby block perceived value may decrease.
Specific geological risk may increase.
```

## 20.3 MVP Recommendation

For MVP:

```text
Major discoveries affect nearby block public interest.
Dry holes affect only the drilling company’s knowledge.
```

This keeps the first version simpler.

---

# 21. Geological Risk Types

Each prospect should have a main risk.

Examples:

```text
Source Risk
Reservoir Risk
Trap Risk
Seal Risk
Timing Risk
Depth Risk
Pressure Risk
Contaminant Risk
Commercial Risk
```

## 21.1 Risk Display

The UI should show:

```text
Main Risk: Trap
Secondary Risk: Seal
Confidence: Medium
```

Example:

```text
Falcon Prospect
Estimated chance of success: 34%
Main Risk: Trap closure
Confidence: 45/100
```

---

# 22. Exploration UI Requirements

The exploration UI should make risk understandable.

## 22.1 Block Panel

The block panel should show:

```text
Block name
Owner
Stage
Public geology hint
Knowledge level
Estimated chance of success
Confidence
Estimated volume range
Main risk
Available actions
AI advisor button
```

## 22.2 Prospect Card

The prospect card should show:

```text
Prospect name
Estimated chance of success
Volume range
Main risk
Drilling cost
Recommended action
```

## 22.3 Exploration Map Layers

Recommended map layers:

```text
Ownership layer
Geological potential layer
Infrastructure layer
Environmental sensitivity layer
Discovery trend layer
Risk layer
```

MVP should include:

```text
Ownership layer
Discovery trend layer
Risk color layer
```

---

# 23. AI Geologist Integration

The AI Geologist is a major part of this system.

## 23.1 AI Geologist Responsibilities

The AI Geologist should help players:

```text
Understand exploration risk
Compare blocks
Decide whether to buy seismic
Decide whether to drill
Interpret dry holes
Explain discoveries
Recommend next exploration action
```

## 23.2 Example Questions

Players may ask:

```text
Should we drill Block D-08?
Which block has the best exploration upside?
Is Falcon Prospect too risky?
What did the dry hole teach us?
Should we buy seismic or drill now?
```

## 23.3 AI Data Rules

The AI Geologist may use:

```text
Public block data
Player-owned study results
Player-owned seismic interpretation
Known discoveries
Known dry-hole results
Company financial context
```

The AI Geologist must not use:

```text
Hidden geology
True chance of success
Undiscovered volumes
Future events
Competitor private data
```

---

# 24. Example AI Geologist Response

Player asks:

```text
Should we drill Falcon Prospect this turn?
```

AI response:

```text
Situation:
Falcon Prospect has an estimated 34% chance of success with medium confidence.
The main risk is trap closure.

Upside:
The mid-case recoverable volume is 115 MMboe, which could support a standard development.

Risks:
Your estimate is based only on 2D seismic.
The volume range is still wide: 40–220 MMboe.
A dry hole would cost about $40M.

Recommendation:
Drill only if you are comfortable with the risk and need a major growth opportunity.
If you want a safer approach, acquire 3D seismic first when available.

Suggested Action:
Drill Falcon-1 using a standard well program.
```

---

# 25. Exploration Events

Exploration events add variety.

## 25.1 Event Examples

```text
Rig Shortage
Seismic Crew Delay
Better Data Processing
Regulatory Drilling Delay
Unexpected High Pressure
Lost Circulation
Competitor Discovery Nearby
Government Opens New Blocks
```

## 25.2 MVP Exploration Events

Use only a few at first:

```text
Rig Cost Inflation
Competitor Discovery Nearby
Seismic Interpretation Breakthrough
Drilling Delay
```

---

# 26. MVP Exploration Rules

The MVP exploration model should be simple.

## 26.1 MVP Flow

```text
Bid for block
    ↓
Geological study
    ↓
2D seismic
    ↓
Drill exploration well
    ↓
Dry hole or discovery
    ↓
Appraise discovery
```

## 26.2 MVP Actions

```text
Geological Study
Acquire 2D Seismic
Drill Exploration Well
Drill Appraisal Well
Ask AI Geologist
```

## 26.3 MVP Costs

```text
Geological Study: $5M
2D Seismic: $15M
Exploration Well: $40M
Appraisal Well: $30M
```

## 26.4 MVP Chance Range

```text
Minimum exploration chance: 5%
Maximum exploration chance: 60%
Typical prospect chance: 15%–40%
Strong prospect chance: 40%–55%
```

## 26.5 MVP Result Types

```text
Dry Hole
Non-commercial Discovery
Commercial Discovery
Major Discovery
```

---

# 27. Example Exploration Turn

## Starting Situation

```text
Company: Beep Energy
Cash: $420M
Owned Blocks: D-04, D-08
Action Slots: 3
```

Block D-08:

```text
Stage: 2D Seismic Complete
Estimated Chance of Success: 34%
Confidence: 45
Estimated Volume: 40–220 MMboe
Main Risk: Trap
Drilling Cost: $40M
```

## Player Actions

```text
Action 1: Drill Falcon-1 on Block D-08
Action 2: Geological Study on Block D-04
Action 3: Bid $25M on Block D-12
```

## Resolution

```text
Falcon-1 Result:
Commercial Discovery

Discovery:
Falcon Field
Fluid: Oil
Initial Estimate: 80–160 MMbbl
Confidence: 35
Main Risk: Reservoir continuity

Block D-04 Study:
Source rock appears mature.
Estimated chance of success increased from unknown to 24%.

Auction:
Lost Block D-12 to competitor bid of $30M.

Financial:
Cash reduced by $45M for drilling and study.
```

---

# 28. Balancing Guidelines

## 28.1 Avoid Too Many Dry Holes

If early play creates too many failures, players may quit.

Recommendation:

```text
Average drill-ready prospect chance should be around 25%–35%.
Strong prospects should reach 40%–55%.
```

## 28.2 Data Must Be Worth Buying

If seismic rarely changes decisions, players will skip it.

Recommendation:

```text
2D seismic should often improve confidence enough to affect drill/no-drill decisions.
```

## 28.3 Discoveries Must Be Varied

Not every discovery should be equally valuable.

Discovery outcomes should include:

```text
Small discoveries
Marginal discoveries
Commercial discoveries
Major discoveries
Occasional giant discoveries
```

## 28.4 Failure Must Teach

Dry holes should reveal useful information.

This helps the player feel they learned something even after losing money.

---

# 29. Design Risks

## 29.1 Exploration Feels Random

Solution:

```text
Show chance of success.
Show confidence.
Explain main risk.
Give dry-hole lessons.
Allow data purchases to reduce uncertainty.
```

## 29.2 Too Much Technical Language

Solution:

```text
Use plain explanations.
Add AI Geologist summaries.
Use icons and color-coded risk.
Provide tooltips for petroleum terms.
```

## 29.3 Players Always Buy All Data

Solution:

```text
Make data useful but costly.
Add time pressure.
Add competitor pressure.
Make some players drill early for first-mover advantage.
```

## 29.4 Players Always Drill Immediately

Solution:

```text
Make low-confidence drilling risky.
Reward seismic with better estimates.
Make dry holes financially painful.
```

---

# 30. Open Questions

1. Should MVP include 3D seismic?
2. Should geological study be allowed on unowned blocks?
3. Should dry-hole information become public or stay private?
4. Should players see exact chance percentages or risk bands?
5. Should competitors see that a player acquired seismic?
6. Should prospects be auto-generated or manually selected?
7. Should well budget classes be available in MVP?
8. Should some blocks have multiple prospects?
9. Should gas discoveries exist in MVP?
10. Should basin-wide knowledge improve as more wells are drilled?

---

# 31. Recommended MVP Exploration Decision

For the first playable version, use this exploration system:

```text
Basin:
- One desert basin

Blocks:
- 20 license blocks
- Each block has hidden geology

Player knowledge:
- Public hint
- Geological study
- 2D seismic
- Exploration well result

Actions:
- Geological Study
- 2D Seismic
- Drill Exploration Well
- Drill Appraisal Well

Chance system:
- True chance calculated from hidden petroleum system factors
- Player sees estimated chance and confidence
- Minimum chance: 5%
- Maximum chance: 60%

Results:
- Dry Hole
- Non-commercial Discovery
- Commercial Discovery
- Major Discovery

AI:
- AI Geologist can explain risk and recommend next action
```

This model gives Beep Oil and Gas Sim a strong exploration foundation while keeping the MVP achievable.
