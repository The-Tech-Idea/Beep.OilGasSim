# New Section — Gameplay Mode Profiles Architecture

## Purpose

Beep Oil and Gas Sim should support different gameplay modes so the same game engine can serve different audiences.

Not every player wants a complex oil and gas simulation. Some players may want a fast, fun, mobile-style experience that is easy to start. Other players may want a more realistic business and engineering simulation. Training users may want mission-based challenges.

To support this, the technical architecture should include **Gameplay Mode Profiles**.

A Gameplay Mode Profile controls:

* Simulation complexity
* UI complexity
* Number of available actions
* Turn speed
* AI help level
* Financial difficulty
* Geological realism
* Event intensity
* Tutorial guidance
* Victory conditions
* Mobile-friendly simplification

---

# Gameplay Mode Types

The recommended gameplay modes are:

```csharp
public enum GameplayModeType
{
    Fun,
    Balanced,
    Realistic,
    MissionChallenge,
    Training,
    Sandbox
}
```

---

# 1. Fun Mode

## Purpose

Fun Mode is the easiest and fastest way to play Beep Oil and Gas Sim.

This mode should feel like a casual strategy/tycoon game that can be played easily on web or mobile-style screens.

The player should not need deep oil and gas knowledge.

## Target Player

Fun Mode is for:

* New players
* Casual players
* Mobile-style gameplay
* Short sessions
* Younger or non-technical audiences
* Players who want excitement more than realism

## Design Feel

Fun Mode should feel:

```text
Easy to start
Fast to understand
Low complexity
Visually exciting
Forgiving
AI-assisted
Short-session friendly
```

## Fun Mode Rules

Fun Mode should simplify the game:

```text
Fewer actions per turn
Simpler asset stages
Simpler financial model
Fewer penalties
Higher discovery chance
Faster development
Simpler production
Reduced abandonment complexity
More AI guidance
More visual feedback
```

## Recommended Fun Mode Settings

```text
Turns: 10–15
Action slots per turn: 2
Starting cash: High
Debt complexity: Low
Oil price volatility: Medium
Discovery chance: Boosted
Development time: Short
Abandonment penalty: Light
AI advisor: Always available
Tutorial: Strong guidance
UI: Simple view by default
```

## Fun Mode Example

```text
Player starts with $700M.
Blocks show simple risk labels: Low, Medium, High.
The player can Study, Drill, Develop, Produce, or Abandon.
AI recommends the best next action every turn.
Development takes only 1–2 turns.
Abandonment is simplified into one button.
```

## Fun Mode UI Requirements

The UI should hide advanced details by default.

Show simple cards such as:

```text
Block D-08
Risk: Medium
Potential: High
Recommended Action: Drill
Cost: $40M
```

Avoid overwhelming terms like:

```text
Trap integrity
Seal quality
Migration timing
Commerciality score
Fiscal regime
Netback
```

These can be hidden under “Advanced Details”.

---

# 2. Balanced Mode

## Purpose

Balanced Mode is the standard game mode.

It should combine fun gameplay with meaningful oil and gas strategy.

This should be the default mode for most multiplayer matches.

## Target Player

Balanced Mode is for:

* Strategy players
* Tycoon players
* Multiplayer players
* Players who want depth without heavy realism

## Design Feel

Balanced Mode should feel:

```text
Strategic
Competitive
Understandable
Moderately realistic
Good for multiplayer
```

## Recommended Balanced Mode Settings

```text
Turns: 20
Action slots per turn: 3
Starting cash: $500M
Debt: Enabled
Hedging: Enabled
Oil price volatility: Medium
Discovery chance: Normal
Development time: 2–4 turns
Abandonment penalty: Standard
AI advisor: Available
UI: Standard view
```

Balanced Mode should use the main MVP rules defined in the previous design documents.

---

# 3. Realistic Mode

## Purpose

Realistic Mode is for players who want a more serious oil and gas simulation.

This mode should increase uncertainty, cost pressure, project duration, financial discipline, and abandonment responsibility.

## Target Player

Realistic Mode is for:

* Oil and gas professionals
* Advanced strategy players
* Training users
* Simulation-focused players
* Players who want harder decisions

## Design Feel

Realistic Mode should feel:

```text
Serious
Detailed
Risky
Financially strict
Closer to real oil and gas logic
```

## Realistic Mode Rules

Realistic Mode can include:

```text
Lower exploration success rates
More expensive wells
Longer development timelines
More detailed appraisal
Staged development CAPEX
More detailed OPEX
Stronger debt consequences
Stronger abandonment liability
More regulatory events
More HSE risk
More detailed production decline
More realistic market volatility
```

## Recommended Realistic Mode Settings

```text
Turns: 30–40
Action slots per turn: 3–4
Starting cash: Normal or lower
Debt: Strict
Credit rating: Important
Oil price volatility: High
Discovery chance: Realistic/lower
Development time: Longer
Abandonment penalty: High
AI advisor: Advisory only, less hand-holding
UI: Advanced view available by default
```

## Realistic Mode UI Requirements

Realistic Mode should expose more details:

```text
Chance of success by risk factor
Confidence level
CAPEX phasing
OPEX breakdown
Netback
Commerciality score
Field decline chart
Abandonment liability breakdown
Debt and interest forecast
```

---

# 4. Mission Challenge Mode

## Purpose

Mission Challenge Mode provides specific objectives instead of open-ended company growth.

Each mission gives the player a scenario, constraints, and victory goals.

## Target Player

Mission Challenge Mode is for:

* Solo players
* Tutorial progression
* Training users
* Players who like objectives
* Short challenge sessions

## Design Feel

Mission Challenge Mode should feel:

```text
Focused
Objective-based
Replayable
Progressive
Scenario-driven
```

## Example Missions

```text
Mission 1: First Discovery
Goal: Discover oil within 5 turns.

Mission 2: Survive the Crash
Goal: Keep company value above $300M after an oil price crash.

Mission 3: Fast First Oil
Goal: Bring a discovery to production before Turn 8.

Mission 4: Responsible Operator
Goal: Produce profitably and abandon all late-life fields.

Mission 5: Debt Discipline
Goal: Develop a field without exceeding $200M debt.

Mission 6: Exploration Race
Goal: Beat competitors to a major discovery.
```

## Mission Challenge Rules

Each mission can define:

```text
Starting assets
Starting cash
Allowed actions
Turn limit
Special events
Victory conditions
Bonus objectives
Failure conditions
AI guidance level
```

---

# 5. Training Mode

## Purpose

Training Mode is designed for education, workshops, and corporate learning.

It should allow a facilitator or instructor to guide the session.

## Target Player

Training Mode is for:

* Oil and gas training
* Business simulation workshops
* University classes
* Internal company learning
* Team decision-making exercises

## Training Features

Training Mode may include:

```text
Facilitator dashboard
Pause/resume match
Manual turn advancement
Inject event
Reveal explanation
View all teams
Export reports
Debrief summary
AI-generated learning notes
```

## Training Mode Rules

Training Mode should emphasize learning, not just winning.

Victory can include:

```text
Best company value
Best safety performance
Best abandonment performance
Best exploration efficiency
Best team collaboration
Best capital discipline
```

---

# 6. Sandbox Mode

## Purpose

Sandbox Mode allows players to experiment freely.

It is useful for testing, learning, and debugging.

## Target Player

Sandbox Mode is for:

* Developers
* Designers
* Advanced players
* Testers
* Players who want creative freedom

## Sandbox Features

Sandbox Mode may allow:

```text
Unlimited or high cash
Custom oil price
Custom basin
Custom number of blocks
Event control
Manual discovery creation
Fast development
No turn timer
Optional AI advice
```

Sandbox Mode should not be used for ranked multiplayer.

---

# Gameplay Mode Profile Model

The backend should store gameplay mode configuration as data.

```csharp
public sealed class GameplayModeProfile
{
    public Guid Id { get; set; }

    public GameplayModeType ModeType { get; set; }

    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    public int TotalTurns { get; set; }
    public int ActionSlotsPerTurn { get; set; }

    public decimal StartingCash { get; set; }
    public decimal MaxDebt { get; set; }

    public double ExplorationChanceModifier { get; set; }
    public double DevelopmentTimeModifier { get; set; }
    public double CostModifier { get; set; }
    public double OilPriceVolatilityModifier { get; set; }

    public double AbandonmentPenaltyModifier { get; set; }
    public double EventIntensityModifier { get; set; }

    public bool EnableAdvancedFinance { get; set; }
    public bool EnableDetailedGeology { get; set; }
    public bool EnableDetailedAbandonment { get; set; }
    public bool EnableHedging { get; set; }
    public bool EnablePlayerTrading { get; set; }
    public bool EnableTeamMode { get; set; }

    public AiAssistanceLevel AiAssistanceLevel { get; set; }
    public UiComplexityLevel UiComplexityLevel { get; set; }
}
```

---

# AI Assistance Level

```csharp
public enum AiAssistanceLevel
{
    Off,
    BasicHints,
    Guided,
    FullAdvisor,
    TrainingCoach
}
```

## AI Assistance Behavior

```text
Off:
No AI advisor.

BasicHints:
AI gives short suggestions only.

Guided:
AI recommends next actions and explains risks.

FullAdvisor:
AI supports strategy, finance, geology, HSE, and proposals.

TrainingCoach:
AI explains concepts, teaches, summarizes, and creates learning notes.
```

---

# UI Complexity Level

```csharp
public enum UiComplexityLevel
{
    Simple,
    Standard,
    Advanced,
    Expert
}
```

## UI Complexity Behavior

```text
Simple:
Mobile-style simplified UI.
Few buttons.
Big cards.
Clear recommendations.
Minimal technical terms.

Standard:
Default strategy UI.
Shows key metrics and common actions.

Advanced:
Shows deeper technical and financial details.

Expert:
Shows formulas, detailed risk factors, simulation breakdowns, and full reports.
```

---

# Gameplay Mode JSON Example

```json
{
  "modeType": "Fun",
  "name": "Fun Mode",
  "description": "Fast, easy, mobile-style gameplay for casual players.",
  "totalTurns": 12,
  "actionSlotsPerTurn": 2,
  "startingCash": 700000000,
  "maxDebt": 300000000,
  "explorationChanceModifier": 1.35,
  "developmentTimeModifier": 0.6,
  "costModifier": 0.85,
  "oilPriceVolatilityModifier": 0.8,
  "abandonmentPenaltyModifier": 0.5,
  "eventIntensityModifier": 0.7,
  "enableAdvancedFinance": false,
  "enableDetailedGeology": false,
  "enableDetailedAbandonment": false,
  "enableHedging": false,
  "enablePlayerTrading": false,
  "enableTeamMode": false,
  "aiAssistanceLevel": "Guided",
  "uiComplexityLevel": "Simple"
}
```

---

# Scenario Integration

Each scenario should reference a gameplay mode profile.

```csharp
public sealed class ScenarioDefinition
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    public GameplayModeType DefaultGameplayMode { get; set; }

    public List<GameplayModeType> SupportedGameplayModes { get; set; } = new();

    public string BalanceProfileId { get; set; } = "";
    public string MapDefinitionId { get; set; } = "";
}
```

Example:

```json
{
  "id": "desert-frontier",
  "name": "Desert Frontier",
  "defaultGameplayMode": "Balanced",
  "supportedGameplayModes": [
    "Fun",
    "Balanced",
    "Realistic",
    "MissionChallenge"
  ]
}
```

---

# Match Creation Flow

When a match is created, the player should choose:

```text
Scenario
Gameplay Mode
Number of Players
Turn Count, optional
AI Assistance
Team Mode
```

Recommended UX:

```text
Choose how you want to play:

Fun
Fast and easy. Best for new players and mobile-style casual play.

Balanced
Standard competitive strategy mode.

Realistic
Detailed oil and gas simulation with tougher economics.

Mission Challenge
Focused objective-based scenarios.

Training
Instructor-led learning and team exercises.

Sandbox
Experiment freely with relaxed rules.
```

---

# Technical Rules

The selected gameplay mode should be saved in the GameSession.

```csharp
public sealed class GameSession
{
    public Guid Id { get; set; }

    public Guid ScenarioId { get; set; }

    public GameplayModeType GameplayMode { get; set; }

    public string GameplayModeProfileId { get; set; } = "";

    public int CurrentTurnNumber { get; set; }
    public int TotalTurns { get; set; }

    public GameSessionState State { get; set; }
}
```

The simulation engine should receive the GameplayModeProfile during turn resolution.

```csharp
public sealed class TurnResolutionContext
{
    public Guid GameSessionId { get; set; }
    public int TurnNumber { get; set; }

    public GameplayModeProfile GameplayModeProfile { get; set; } = new();

    public BalanceProfile BalanceProfile { get; set; } = new();
}
```

Simulation systems should use gameplay mode modifiers.

Examples:

```text
ExplorationResolver uses ExplorationChanceModifier.
DevelopmentResolver uses DevelopmentTimeModifier.
EconomyResolver uses CostModifier.
MarketResolver uses OilPriceVolatilityModifier.
ScoringService uses AbandonmentPenaltyModifier.
EventResolver uses EventIntensityModifier.
UI uses UiComplexityLevel.
AI system uses AiAssistanceLevel.
```

---

# Fun Mode Technical Requirements

Fun Mode must be treated as a first-class mode, not an afterthought.

Fun Mode should include technical support for:

```text
Simplified action list
Simplified UI panels
Shorter matches
Guided AI recommendations
Reduced number of visible metrics
Simplified results cards
Less punishing penalties
Fast development timing
Higher success feedback frequency
Mobile-friendly card layout
```

The client should be able to ask:

```typescript
if (gameplayMode.uiComplexityLevel === "Simple") {
    showSimpleBlockPanel();
    showRecommendedActions();
    hideAdvancedGeology();
}
```

The server should still remain authoritative.

Even in Fun Mode, the client must not calculate hidden results.

---

# Realistic Mode Technical Requirements

Realistic Mode should enable deeper systems.

Realistic Mode may enable:

```text
3D seismic
Detailed risk factor display
Staged CAPEX
Detailed OPEX
Advanced debt
More severe abandonment penalties
More events
Longer construction
Lower discovery probabilities
Detailed reports
```

Realistic Mode should use the same core simulation engine, but with stricter modifiers and more enabled systems.

---

# Mission Challenge Technical Requirements

Mission Challenge Mode requires an objective system.

```csharp
public sealed class MissionObjective
{
    public Guid Id { get; set; }

    public string Title { get; set; } = "";
    public string Description { get; set; } = "";

    public MissionObjectiveType ObjectiveType { get; set; }

    public decimal TargetValue { get; set; }
    public int? TargetTurn { get; set; }

    public bool IsRequired { get; set; }
    public bool IsCompleted { get; set; }
}
```

```csharp
public enum MissionObjectiveType
{
    DiscoverOil,
    ReachProductionRate,
    MaintainCashAbove,
    KeepDebtBelow,
    AchieveCompanyValue,
    CompleteAbandonment,
    SurviveMarketCrash,
    WinAuction,
    DevelopField,
    FinishWithReputationAbove
}
```

Mission Challenge Mode should evaluate objectives at the end of each turn.

---

# Recommended MVP Decision

For MVP, implement gameplay modes in this order:

```text
1. Fun Mode
2. Balanced Mode
3. Mission Challenge Mode
4. Realistic Mode
5. Training Mode
6. Sandbox Mode
```

However, the architecture should support all modes from the beginning through GameplayModeProfile.

The first playable build should include:

```text
Fun Mode:
- Very easy to start
- Simple UI
- Short match
- Guided AI
- Fewer actions
- Higher forgiveness

Balanced Mode:
- Standard rules from the main MVP
- Multiplayer-ready
- Full action slots
- Standard economy and scoring
```

Realistic, Training, Mission Challenge, and Sandbox can be expanded later.

---

# Important Design Rule

Beep Oil and Gas Sim should not force every player into a complex simulation.

The game should support two clear experiences:

```text
Fun Mode:
A fast, simple, exciting game that feels easy like a mobile strategy game.

Realistic Mode:
A deeper simulation for players who want serious oil and gas decision-making.
```

Both modes should use the same core architecture, but different profiles, UI complexity, AI guidance, and balance settings.
