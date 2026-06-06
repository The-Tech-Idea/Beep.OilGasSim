# Beep Oil and Gas Sim

## MVP Development Plan

**Document ID:** BOGS-GDD-012
**Version:** 0.1
**Status:** Draft
**Parent Document:** BOGS-GDD-001 — Master Game Design Document
**Related Documents:**

* BOGS-GDD-002 — Gameplay Systems Design
* BOGS-GDD-003 — Oil and Gas Lifecycle Simulation Design
* BOGS-GDD-004 — Economy, Finance, and Market Design
* BOGS-GDD-005 — Exploration and Geology Design
* BOGS-GDD-006 — Field Development and Production Design
* BOGS-GDD-007 — Late-Life, Decommissioning, and Abandonment Design
* BOGS-GDD-008 — AI Command Center Design
* BOGS-GDD-009 — Multiplayer and Team Collaboration Design
* BOGS-GDD-010 — User Interface and User Experience Design
* BOGS-GDD-011 — Technical Architecture Design

**Project Name:** Beep Oil and Gas Sim
**Short Name:** Beep O&G Sim
**Document Purpose:** Define the minimum playable version, build phases, milestone order, feature scope, and delivery plan.

---

# 1. Purpose

This document defines the MVP development plan for Beep Oil and Gas Sim.

The MVP should prove that the core game is fun, understandable, technically stable, and expandable.

The MVP is not the final full simulation. It is the first playable version that demonstrates:

* Oil and gas lifecycle gameplay
* Competitive strategy
* Simple exploration risk
* Field development and production
* Economy and cash flow
* Turn-based resolution
* Basic multiplayer readiness
* AI Command Center foundation
* Fun Mode and Balanced Mode support
* Clear web-based UI

The MVP should be small enough to build, test, and balance, but complete enough to feel like a real game.

---

# 2. MVP Vision

The MVP version of Beep Oil and Gas Sim should allow a player to start a match, acquire blocks, study geology, drill wells, discover oil, appraise discoveries, develop fields, produce revenue, manage cash, and win by building the highest company value.

The first MVP should focus on a single scenario:

```text
Scenario: Desert Frontier
Mode Options: Fun Mode and Balanced Mode
Players: 1–6
Turns: 12 in Fun Mode, 20 in Balanced Mode
Primary Commodity: Oil
Map: 20 license blocks
Objective: Build the highest company value
```

The MVP should answer these questions:

```text
Is the game loop fun?
Do players understand what to do?
Is exploration exciting?
Does production feel rewarding?
Does cash management create pressure?
Does AI help players make decisions?
Is the architecture strong enough for multiplayer?
Can the game be expanded into deeper modes later?
```

---

# 3. MVP Product Goals

## 3.1 Prove the Core Loop

The MVP must prove this loop:

```text
Bid → Study → Drill → Discover → Appraise → Develop → Produce → Score
```

The player should be able to take at least one asset from unowned license block to producing field within one match.

---

## 3.2 Support Easy Entry Through Fun Mode

The MVP must include Fun Mode so new players can start quickly.

Fun Mode should feel like a casual mobile-style strategy game:

```text
Simple UI
Short turns
Fewer actions
Higher forgiveness
Clear recommendations
Guided AI
Less technical complexity
```

Fun Mode is important because the game should not only serve oil and gas experts. It should also be approachable for general players.

---

## 3.3 Support Standard Strategy Through Balanced Mode

Balanced Mode should be the standard strategy version.

Balanced Mode should include:

```text
More complete economy
3 action slots per turn
Standard discovery chance
Standard development timing
Standard abandonment penalty
More visible metrics
More strategic pressure
```

Balanced Mode is the foundation for future multiplayer and competitive play.

---

## 3.4 Build a Server-Authoritative Foundation

The MVP must not be a client-only prototype.

Even if the first build is solo, the simulation should run through the backend.

This ensures that later multiplayer, AI, async leagues, and team mode can reuse the same foundation.

---

## 3.5 Include AI as a First-Class Feature

The AI Command Center should be included early, at least in basic form.

The MVP AI should support:

```text
Strategy Advisor
Geologist
CFO
HSE Advisor
Asset-aware questions
Turn summary
Recommended next actions
```

The AI should make the game easier to understand and should support Fun Mode guidance.

---

# 4. MVP Non-Goals

The MVP should intentionally avoid overbuilding.

The following are not required for MVP:

```text
Full 3D realism
Real-world oilfield datasets
Complex reservoir simulation
Detailed well trajectories
Advanced 3D seismic
Gas and LNG systems
Player-to-player asset trading
Shared infrastructure negotiation
Detailed regulatory systems
Full facilitator training dashboard
Voice AI
Mobile app store deployment
Ranked matchmaking
Tournament mode
Complex AI competitors
Scenario editor
Advanced decommissioning engineering
```

These can be added in later releases.

---

# 5. MVP Gameplay Modes

The MVP should support two gameplay modes.

---

## 5.1 Fun Mode

Fun Mode is the beginner-friendly mode.

### Purpose

Fun Mode should be fast, easy, forgiving, and visually exciting.

It should be suitable for:

```text
New players
Casual players
Mobile-style sessions
Short gameplay sessions
Players without oil and gas knowledge
```

### Fun Mode Rules

```text
Turns: 12
Action slots per turn: 2
Starting cash: $700M
Max debt: $300M
Discovery chance modifier: +35%
Development time modifier: faster
Cost modifier: cheaper
Abandonment penalty: light
AI assistance: guided
UI complexity: simple
```

### Fun Mode Player Experience

The player sees simple labels:

```text
Risk: Low / Medium / High
Potential: Low / Medium / High
Recommended Action: Study / Drill / Develop
```

Advanced technical details are hidden by default.

Example Fun Mode block card:

```text
Block D-08
Risk: Medium
Potential: High
Recommended Action: Drill
Cost: $40M
AI says: This is a good growth opportunity.
```

---

## 5.2 Balanced Mode

Balanced Mode is the standard strategy mode.

### Purpose

Balanced Mode should be the main competitive strategy experience.

It should be suitable for:

```text
Strategy players
Tycoon players
Multiplayer players
Players who want more depth
```

### Balanced Mode Rules

```text
Turns: 20
Action slots per turn: 3
Starting cash: $500M
Max debt: $500M
Discovery chance modifier: normal
Development time: standard
Costs: standard
Abandonment penalty: standard
AI assistance: full advisor
UI complexity: standard
```

Balanced Mode uses the main MVP rules from the previous design documents.

---

# 6. MVP Scenario

## 6.1 Scenario Name

```text
Desert Frontier
```

## 6.2 Scenario Description

Desert Frontier is a fictional onshore basin with moderate infrastructure, mixed geology, and strong exploration potential.

Players compete to acquire blocks, discover oil, develop fields, and build the most valuable company.

## 6.3 Scenario Settings

```text
Map Type: Desert onshore basin
Blocks: 20
Commodity: Oil only
Starting Oil Price: $75/bbl
Main Risk: Exploration uncertainty
Main Opportunity: Medium to large oil discoveries
Infrastructure: One export pipeline corridor
Environmental Sensitivity: Mostly low to medium
```

## 6.4 Scenario Goals

The scenario should teach:

```text
How license blocks work
How geological study works
How seismic improves confidence
How drilling risk works
How discoveries become fields
How production creates revenue
How abandonment affects final score
```

---

# 7. MVP Feature Scope

## 7.1 Core Game Features

The MVP must include:

```text
Create game session
Choose gameplay mode
Load Desert Frontier scenario
Display 20-block map
Create company
Show company dashboard
Select blocks
Bid for licenses
Submit turn actions
Resolve turns server-side
Show turn results
Show leaderboard
Calculate final score
```

---

## 7.2 Exploration Features

The MVP must include:

```text
Public block hints
Block ownership
Geological study
2D seismic
Estimated chance of success
Confidence level
Drill exploration well
Dry hole result
Commercial discovery result
Major discovery result
Basic dry-hole learning
AI Geologist advice
```

---

## 7.3 Appraisal Features

The MVP must include:

```text
Drill appraisal well
Increase confidence
Narrow volume estimate
Update commerciality
Recommend development
```

---

## 7.4 Development Features

The MVP must include:

```text
Small Development
Standard Development
Large Development
Development CAPEX
Construction duration
Construction progress
First oil after construction
```

---

## 7.5 Production Features

The MVP must include:

```text
Production rate
Produced volume per turn
Oil revenue
OPEX
Royalty
Production decline
Uptime
Remaining reserves
Optimize Field action
Field production report
```

---

## 7.6 Economy Features

The MVP must include:

```text
Cash
Debt
Credit rating
Revenue
CAPEX
OPEX
Royalty
Interest
License fees
Company valuation
Asset valuation
Final score
Emergency debt
```

---

## 7.7 Market Features

The MVP must include:

```text
Oil price
Oil price movement
Basic market trend
Oil price chart
Hedging in Balanced Mode
Simple market events
```

Fun Mode may hide hedging initially or present it as a simple “Protect Revenue” button.

---

## 7.8 Late-Life and Abandonment Features

The MVP must include:

```text
Abandonment liability
Late-life trigger
Abandon Field action
Unresolved liability penalty
Reputation bonus for abandonment
AI HSE warning
```

Detailed decommissioning steps are not required.

---

## 7.9 AI Command Center Features

The MVP must include:

```text
AI Strategy Advisor
AI Geologist
AI CFO
AI HSE Advisor
Selected asset context
Ask AI button from asset panels
Turn summary
Recommended next actions
AI-safe context builder
No hidden data exposure
```

Optional MVP addition:

```text
Draft action proposal
```

---

## 7.10 Multiplayer Features

The MVP should be developed in a way that supports multiplayer.

The first playable version may begin as solo, but the server architecture should support multiplayer.

MVP multiplayer target:

```text
2–6 companies
One player per company
Basic lobby
Commit turn
Server resolves all players
Leaderboard
Basic public chat
```

Team mode can be Phase 2 after MVP core loop is stable.

---

# 8. MVP Build Strategy

The recommended build strategy is:

```text
Build simulation first.
Build solo UI second.
Add multiplayer third.
Add AI fourth.
Add polish and balancing fifth.
```

Reason:

The game must be fun and correct before polishing the visuals.

A strong simulation engine will make the rest of the project easier.

---

# 9. Development Phases

---

## Phase 0 — Project Foundation

### Goal

Create the repository, solution structure, client app, backend app, database, and development environment.

### Features

```text
Repository setup
ASP.NET Core solution
TypeScript/Vite client
PostgreSQL database
Docker Compose
Basic API health endpoint
Basic client shell
Shared coding conventions
Initial CI pipeline
```

### Deliverables

```text
Backend runs locally
Client runs locally
Database runs locally
Client can call backend health endpoint
```

### Success Criteria

```text
Developer can clone repo and run full local environment.
```

---

## Phase 1 — Core Domain and Simulation Prototype

### Goal

Build the core simulation without relying on the final UI.

### Features

```text
GameSession entity
Company entity
Basin and block entities
Hidden geology
GameplayModeProfile
BalanceProfile
TurnAction model
TurnEngine
Action validation
License auction
Geological study
2D seismic
Exploration drilling
Discovery/dry hole
Basic economy
Turn result generation
```

### Deliverables

```text
Server can create a Desert Frontier match.
Server can run a full turn.
Server can resolve exploration actions.
Server can produce turn results.
Automated tests cover core formulas.
```

### Success Criteria

```text
A developer can simulate a 20-turn match through backend tests or API calls.
```

---

## Phase 2 — Economy, Development, and Production Simulation

### Goal

Complete the asset lifecycle from discovery to production.

### Features

```text
Appraisal
Commerciality update
Small/Standard/Large development
Construction progress
Producing field creation
Production volume
OPEX
Revenue
Royalty
Decline
Uptime
Optimize Field
Company valuation
Leaderboard calculation
Final score
Abandonment liability
Abandon Field action
```

### Deliverables

```text
A discovered field can be appraised.
A discovery can be developed.
A development can become a producing field.
A producing field generates revenue.
A field can decline and be abandoned.
Final score can be calculated.
```

### Success Criteria

```text
A complete oilfield lifecycle works server-side from license to abandonment.
```

---

## Phase 3 — Basic Web Game UI

### Goal

Create the first playable browser interface.

### Features

```text
Main game shell
Top bar
2.5D or simplified map view
20 block display
Block selection
Right detail panel
Action buttons
Action queue
Commit turn
Turn result screen
Company dashboard
Leaderboard
```

### Deliverables

```text
Player can start a solo match from browser.
Player can select a block.
Player can add actions.
Player can commit turn.
Player can view turn results.
Player can play multiple turns.
```

### Success Criteria

```text
A player can complete a solo match through the browser UI.
```

---

## Phase 4 — Gameplay Mode Support

### Goal

Add Fun Mode and Balanced Mode as first-class gameplay profiles.

### Features

```text
Gameplay mode selection
Fun Mode profile
Balanced Mode profile
Mode-specific settings
Mode-specific UI complexity
Mode-specific AI assistance level
Mode-specific simulation modifiers
```

### Deliverables

```text
Player can choose Fun Mode or Balanced Mode.
Fun Mode has simpler UI and easier rules.
Balanced Mode uses standard MVP rules.
Simulation uses GameplayModeProfile modifiers.
```

### Success Criteria

```text
Fun Mode feels easy and fast.
Balanced Mode feels strategic and deeper.
Both modes use the same core engine.
```

---

## Phase 5 — AI Command Center MVP

### Goal

Add basic game-aware AI assistance.

### Features

```text
AI Command Center panel
Advisor selector
Strategy Advisor
Geologist
CFO
HSE Advisor
Selected asset context
AI-safe context builder
Ask AI from block/discovery/field
Turn summary report
Recommended next action
```

### Deliverables

```text
Player can ask AI about a block.
Player can ask AI about finances.
Player can ask AI about abandonment.
AI summarizes turn results.
AI does not receive hidden geology.
```

### Success Criteria

```text
AI advice helps the player understand decisions without cheating.
```

---

## Phase 6 — Multiplayer MVP

### Goal

Allow multiple players to compete in the same match.

### Features

```text
Lobby
Create match
Join match
Company assignment
2–6 companies
SignalR GameHub
Action submission per company
Turn commit
Server turn resolution for all companies
Realtime turn results
Leaderboard update
Basic public chat
```

### Deliverables

```text
Multiple players can join one match.
Each player controls one company.
All players submit actions.
Server resolves the turn.
Leaderboard updates for all players.
```

### Success Criteria

```text
2–6 players can complete a live multiplayer match.
```

---

## Phase 7 — MVP Polish, Tutorial, and Balancing

### Goal

Make the MVP understandable, playable, and testable by external users.

### Features

```text
Tutorial flow
Tooltips
Improved results cards
Financial warnings
Risk badges
Discovery reveal effects
Production chart
Oil price chart
Balance tuning
Bug fixes
UX improvements
Basic sound effects, optional
```

### Deliverables

```text
First-time player can understand Fun Mode.
Balanced Mode is playable and competitive.
Core game metrics are tuned.
Major bugs are resolved.
```

### Success Criteria

```text
External testers can play a full match with minimal explanation.
```

---

# 10. MVP Milestone Summary

| Milestone | Name                      | Main Result                                  |
| --------- | ------------------------- | -------------------------------------------- |
| M0        | Foundation                | Repo, client, backend, database running      |
| M1        | Core Simulation           | Backend can resolve basic turns              |
| M2        | Full Lifecycle Simulation | Discovery to production to abandonment works |
| M3        | Solo Web Playable         | Browser solo match playable                  |
| M4        | Gameplay Modes            | Fun and Balanced modes implemented           |
| M5        | AI Command Center         | AI advisors provide game-aware help          |
| M6        | Multiplayer MVP           | 2–6 player match works                       |
| M7        | Polish and Balance        | MVP ready for testing                        |

---

# 11. MVP Feature Priority

## Must Have

```text
Game session
Scenario loading
GameplayModeProfile
Fun Mode
Balanced Mode
20-block map
Company cash and debt
License auction
Geological study
2D seismic
Exploration drilling
Discovery/dry hole
Appraisal
Development
Production
Oil price
Revenue and OPEX
Company value
Final score
Turn results
Basic UI
AI Strategy Advisor
AI Geologist
AI-safe context
Server-side simulation
```

---

## Should Have

```text
Leaderboard
Hedging
Abandonment action
Late-life trigger
AI CFO
AI HSE Advisor
Oil price chart
Production chart
Basic multiplayer lobby
SignalR updates
Public chat
Tutorial hints
```

---

## Could Have

```text
Team chat
Action proposal board
AI draft proposal
3D map effects
Event cards
Sound effects
Scenario intro screen
Asset sale
Emergency debt UI
Advanced dashboard charts
```

---

## Not MVP

```text
Full team mode
Player-to-player trading
Training facilitator dashboard
Advanced regulatory simulation
Detailed decommissioning steps
Gas production
LNG
Carbon storage
Shared infrastructure
Ranked matchmaking
Mobile app version
Voice AI
```

---

# 12. MVP Data Scope

## 12.1 Scenario Data

The MVP needs one scenario:

```text
Desert Frontier
```

Required scenario data:

```text
20 blocks
Public hints
Hidden geology
Infrastructure access
Environmental sensitivity
Starting oil price
Oil price movement settings
Starting company settings
Development concept settings
Event settings
Gameplay mode profiles
```

---

## 12.2 Block Data

Each block should include:

```text
Block ID
Block name
Grid position
Public geology hint
Infrastructure rating
Environmental sensitivity
Hidden source quality
Hidden reservoir quality
Hidden trap quality
Hidden seal quality
Hidden timing/migration
Hidden fluid type
Hidden recoverable volume
Depth
Development complexity
```

---

## 12.3 Event Data

MVP events:

```text
Oil Price Crash
Oil Price Boom
Rig Cost Inflation
Seismic Breakthrough
Equipment Failure
Reservoir Outperformance
Reservoir Underperformance
Regulatory Inspection
Late-Life Leak
```

Fun Mode should use fewer and less punishing events.

Balanced Mode should use the standard event set.

---

## 12.4 Development Concepts

MVP concepts:

```text
Small Development
Standard Development
Large Development
```

---

# 13. MVP Gameplay Rules

## 13.1 Fun Mode Rules

```text
Turns: 12
Action slots: 2
Starting cash: $700M
Starting debt: $0
Max debt: $300M
Exploration chance modifier: 1.35
Cost modifier: 0.85
Development time modifier: 0.6
Abandonment penalty modifier: 0.5
AI assistance: Guided
UI complexity: Simple
```

## 13.2 Balanced Mode Rules

```text
Turns: 20
Action slots: 3
Starting cash: $500M
Starting debt: $0
Max debt: $500M
Exploration chance modifier: 1.0
Cost modifier: 1.0
Development time modifier: 1.0
Abandonment penalty modifier: 1.0
AI assistance: FullAdvisor
UI complexity: Standard
```

---

# 14. MVP Action List

## 14.1 Fun Mode Actions

Fun Mode should expose fewer actions:

```text
Bid for Block
Study Block
Drill Well
Appraise Discovery
Develop Field
Optimize Field
Protect Revenue
Abandon Field
Ask AI
```

Fun Mode action names should be simple.

Example:

```text
“Study Block” instead of “Geological Study”
“Protect Revenue” instead of “Hedge Production”
```

---

## 14.2 Balanced Mode Actions

Balanced Mode should expose standard actions:

```text
Bid for License
Geological Study
Acquire 2D Seismic
Drill Exploration Well
Drill Appraisal Well
Approve Development
Optimize Field
Hedge Production
Take Debt
Repay Debt
Sell Asset
Abandon Field
Ask AI Advisor
```

---

# 15. MVP Technical Deliverables

## 15.1 Backend Deliverables

```text
ASP.NET Core API
PostgreSQL database
Entity Framework Core models
GameSession service
Scenario loader
GameplayModeProfile loader
TurnEngine
ActionValidator
AuctionResolver
ExplorationResolver
AppraisalResolver
DevelopmentResolver
ProductionResolver
EconomyResolver
MarketResolver
ScoringService
AI Context Builder
AI Advisor Service
SignalR GameHub
Basic authentication or test identity
```

---

## 15.2 Client Deliverables

```text
Vite TypeScript project
Babylon.js or simplified map rendering
React UI shell
Mode selection screen
Lobby / start screen
Main game screen
Top bar
Map area
Right detail panel
Bottom action queue
Company dashboard
Leaderboard
Turn results screen
AI Command Center panel
Basic charts
```

---

## 15.3 Content Deliverables

```text
Desert Frontier scenario JSON
Fun Mode profile JSON
Balanced Mode profile JSON
MVP balance JSON
20 block definitions
Development concept definitions
Event card definitions
Prospect name list
Company name list
```

---

## 15.4 Test Deliverables

```text
Simulation unit tests
Turn resolution tests
Exploration probability tests
Economy tests
Production tests
Abandonment penalty tests
GameplayModeProfile tests
AI context safety tests
API integration tests
Basic client smoke tests
```

---

# 16. MVP Architecture Build Order

The recommended technical build order is:

```text
1. Domain models
2. Scenario and balance loading
3. GameplayModeProfile support
4. TurnAction model and validation
5. TurnEngine shell
6. License auction
7. Exploration simulation
8. Economy simulation
9. Development and production simulation
10. Abandonment and scoring
11. REST API endpoints
12. Basic client UI
13. SignalR updates
14. AI Command Center
15. Multiplayer lobby
16. Polish and testing
```

This order reduces risk because the simulation is proven before UI and multiplayer are added.

---

# 17. MVP API Scope

Recommended REST endpoints:

```text
POST   /api/game-sessions
GET    /api/game-sessions/{id}
POST   /api/game-sessions/{id}/join
POST   /api/game-sessions/{id}/start

GET    /api/game-modes
GET    /api/scenarios

GET    /api/game-sessions/{id}/map
GET    /api/game-sessions/{id}/companies/{companyId}
GET    /api/game-sessions/{id}/companies/{companyId}/assets

POST   /api/game-sessions/{id}/actions
GET    /api/game-sessions/{id}/actions/current
DELETE /api/game-sessions/{id}/actions/{actionId}

POST   /api/game-sessions/{id}/turns/{turnNumber}/commit
GET    /api/game-sessions/{id}/turns/{turnNumber}/results

GET    /api/game-sessions/{id}/leaderboard

POST   /api/game-sessions/{id}/ai/ask
GET    /api/game-sessions/{id}/ai/conversations
```

---

# 18. MVP SignalR Scope

Recommended MVP hubs:

```text
GameHub
ChatHub
AiAdvisorHub
```

## GameHub Events

```text
GameStarted
TurnStarted
ActionSubmitted
TurnCommitted
TurnResolving
TurnResolved
LeaderboardUpdated
NotificationReceived
```

## ChatHub Events

```text
PublicMessageReceived
CompanyMessageReceived
```

## AiAdvisorHub Events

```text
AiMessageStarted
AiMessageDelta
AiMessageCompleted
AiTurnReportReady
```

---

# 19. MVP Database Scope

Required tables:

```text
Users
PlayerProfiles
GameSessions
Scenarios
GameplayModeProfiles
Companies
CompanyPlayers
Turns
Basins
LicenseBlocks
BlockKnowledge
Prospects
Wells
Discoveries
DevelopmentProjects
ProducingFields
TurnActions
TurnResults
MarketStates
CompanyFinances
CompanyReputations
LeaderboardSnapshots
AiConversations
AiMessages
TeamMessages
Notifications
```

Optional MVP tables:

```text
ActionProposals
ProposalVotes
ProposalComments
```

---

# 20. MVP UI Screens

## 20.1 Start Screen

Purpose:

```text
Start new game
Choose scenario
Choose gameplay mode
Choose solo or multiplayer
```

MVP fields:

```text
Scenario: Desert Frontier
Mode: Fun or Balanced
Players: Solo or multiplayer
Company name
Start button
```

---

## 20.2 Mode Selection Screen

Mode cards:

```text
Fun Mode
Fast and easy. Best for new players.

Balanced Mode
Standard competitive strategy mode.
```

Each card should show:

```text
Difficulty
Match length
AI help level
Complexity
Recommended for
```

---

## 20.3 Main Game Screen

Required:

```text
Top bar
Map
Right detail panel
Bottom action queue
Commit turn button
Command Center button
Notifications
```

---

## 20.4 Company Dashboard

Required:

```text
Cash
Debt
Company value
Production
Reserves
Revenue last turn
Net cash flow
Abandonment liability
Rank
```

---

## 20.5 Turn Results Screen

Required sections:

```text
Headline results
Exploration results
Development results
Production results
Financial summary
Market changes
Leaderboard changes
AI summary
```

---

## 20.6 AI Command Center

Required:

```text
Advisor selector
Context chip
AI chat
Quick prompt buttons
Ask input
Turn summary
```

---

# 21. MVP User Flow

## 21.1 Fun Mode First-Time Flow

```text
1. Player opens game.
2. Player chooses Fun Mode.
3. Game starts Desert Frontier.
4. Tutorial highlights a recommended block.
5. Player clicks Study Block.
6. Turn resolves.
7. AI explains result.
8. Player drills a recommended prospect.
9. Discovery reveal appears.
10. Player develops the field.
11. Field starts producing.
12. Player sees cash and company value increase.
13. Match ends with simple score summary.
```

---

## 21.2 Balanced Mode Flow

```text
1. Player chooses Balanced Mode.
2. Player reviews map and company dashboard.
3. Player bids for license blocks.
4. Player studies and acquires seismic.
5. Player drills exploration wells.
6. Player appraises discoveries.
7. Player chooses development concept.
8. Field produces revenue.
9. Player manages debt, hedging, optimization, and abandonment.
10. Final company value determines winner.
```

---

# 22. MVP Acceptance Criteria

## 22.1 Gameplay Acceptance Criteria

The MVP is acceptable when:

```text
Player can complete a full match.
Player can discover oil.
Player can develop at least one field.
Player can produce revenue.
Player can finish with a final score.
Player can understand why they won or lost.
Fun Mode is easy enough for new players.
Balanced Mode has meaningful strategic choices.
```

---

## 22.2 Technical Acceptance Criteria

The MVP is acceptable when:

```text
Server resolves all game outcomes.
Hidden geology is never sent to client.
AI does not receive hidden geology.
Turn resolution is repeatable and testable.
Game state persists in database.
Client can recover current state after refresh.
Basic multiplayer works or architecture is ready for it.
```

---

## 22.3 AI Acceptance Criteria

The AI MVP is acceptable when:

```text
AI can answer about company strategy.
AI can answer about selected blocks.
AI can answer about cash/debt risk.
AI can answer about abandonment risk.
AI produces useful turn summaries.
AI does not expose hidden truth.
AI advice is understandable for Fun Mode.
```

---

## 22.4 UI Acceptance Criteria

The UI MVP is acceptable when:

```text
Player can see company status at all times.
Player can select blocks and assets easily.
Player can identify available actions.
Player can see action costs and warnings.
Player can commit turns clearly.
Player can understand turn results.
Player can ask AI from relevant screens.
```

---

# 23. MVP Balancing Targets

Initial balancing targets:

```text
Fun Mode:
- Player should usually discover oil within first 3–5 turns.
- Player should usually reach production before turn 8.
- Player should rarely go bankrupt.
- AI should guide the player strongly.

Balanced Mode:
- Player should usually need to evaluate risk before drilling.
- Dry holes should happen often enough to matter.
- At least one company should reach production by mid-match.
- Development should be a major cash decision.
- Abandonment should affect final score.
```

---

# 24. Automated Simulation Targets

Before external testing, run automated simulations.

Track:

```text
Discovery rate
Dry-hole rate
Average company value
Average ending cash
Average debt
Average production
Average number of producing fields
Average unresolved abandonment liability
Frequency of financial distress
Frequency of player reaching production
```

Recommended targets:

```text
Fun Mode:
Discovery rate should be high.
Financial distress should be rare.
At least 80% of test companies should reach production.

Balanced Mode:
Discovery rate should be moderate.
Financial distress should be possible but not common.
At least 50%–70% of companies should reach production.
```

---

# 25. MVP Risk Register

## 25.1 Risk: Game Is Too Complex

Impact:

```text
New players may not understand what to do.
```

Mitigation:

```text
Fun Mode
Guided AI
Simple UI
Tutorial
Recommended actions
Tooltips
```

---

## 25.2 Risk: Game Is Too Random

Impact:

```text
Players may feel they cannot make smart decisions.
```

Mitigation:

```text
Show chance of success
Show confidence
Let studies improve knowledge
Give dry-hole learning
AI explains risk
```

---

## 25.3 Risk: Simulation Is Hard to Balance

Impact:

```text
Game may become too easy, too hard, or too slow.
```

Mitigation:

```text
Data-driven balance files
Automated simulation runs
Separate Fun and Balanced profiles
Telemetry
Frequent playtests
```

---

## 25.4 Risk: AI Reveals Hidden Data

Impact:

```text
Breaks fairness and trust.
```

Mitigation:

```text
AI-safe context builder
Strict DTOs
Hidden data tests
Never send hidden geology to AI
Logging
```

---

## 25.5 Risk: Multiplayer Adds Too Much Complexity Early

Impact:

```text
Could delay core gameplay.
```

Mitigation:

```text
Build server-authoritative solo first
Add multiplayer after core loop works
Reuse same backend simulation
```

---

## 25.6 Risk: UI Overwhelms Players

Impact:

```text
Players may quit before understanding the game.
```

Mitigation:

```text
Simple UI in Fun Mode
Progressive disclosure
Risk badges
Recommended action cards
AI explanations
```

---

# 26. MVP Testing Plan

## 26.1 Developer Testing

Focus:

```text
Turn resolution
Formulas
Action validation
State transitions
API endpoints
AI context filtering
```

---

## 26.2 Internal Playtesting

Focus:

```text
Is Fun Mode understandable?
Is Balanced Mode strategic?
Are actions clear?
Are results exciting?
Is cash pressure balanced?
Is AI helpful?
```

---

## 26.3 External Playtesting

Use two groups:

```text
Casual players
Oil and gas / strategy-oriented players
```

Questions to answer:

```text
Did players understand the goal?
Did players enjoy drilling?
Did they understand risk?
Did AI help?
Was Fun Mode easy?
Was Balanced Mode too complex?
Did they want to play again?
```

---

# 27. MVP Quality Checklist

Before MVP release:

```text
Full match playable
No blocking crashes
Turn resolution stable
Game state persists
Hidden data protected
AI context safe
Fun Mode tested
Balanced Mode tested
Basic tutorial present
UI readable on desktop/laptop
Core actions validated
Final scoring works
Leaderboard works
Main formulas covered by tests
```

---

# 28. Suggested Team Roles for Development

Recommended development roles:

```text
Game Designer
Simulation Developer
Backend Developer
Frontend/UI Developer
Babylon.js/Rendering Developer
AI Integration Developer
QA/Playtest Coordinator
Content/Balancing Designer
```

For a small team, roles can be combined.

Minimum practical team:

```text
1 backend/simulation developer
1 frontend/game UI developer
1 designer/content owner
1 AI/backend helper, part-time
```

---

# 29. MVP Development Sequence by Workstream

## 29.1 Backend Workstream

```text
Domain model
Database schema
Scenario loader
GameplayModeProfile
Turn engine
Action validation
Simulation resolvers
API endpoints
SignalR hubs
AI context builder
```

---

## 29.2 Frontend Workstream

```text
Vite app
Game shell
Map renderer
Block selection
Panels
Action queue
Turn results
Dashboard
Leaderboard
AI panel
Mode selection
```

---

## 29.3 Content Workstream

```text
Desert Frontier map
20 block definitions
Hidden geology tuning
Action costs
Development concepts
Market settings
Event cards
Tutorial text
AI prompt templates
```

---

## 29.4 Testing Workstream

```text
Unit tests
Integration tests
Simulation batch tests
AI safety tests
Manual playtests
Balance reports
Bug tracking
```

---

# 30. MVP Release Definition

The MVP release is ready when the following statement is true:

```text
A new player can open Beep Oil and Gas Sim in a browser, choose Fun Mode, play the Desert Frontier scenario, receive AI guidance, discover and develop oil, produce revenue, complete the match, and understand their final score.

A strategy player can choose Balanced Mode, make deeper decisions around exploration, development, finance, production, and abandonment, and complete a competitive match using the same core system.
```

---

# 31. Post-MVP Roadmap

After MVP, the next releases should be:

## Release 1.1 — Team Collaboration

```text
Team company mode
Roles
Team chat
Proposal board
Voting
CEO approval
AI team summaries
```

## Release 1.2 — Mission Challenge Mode

```text
Mission objectives
Scenario-specific goals
Tutorial missions
Challenge scoring
Mission rewards
```

## Release 1.3 — Realistic Mode Expansion

```text
3D seismic
Staged CAPEX
Detailed finance
Advanced production decline
More HSE events
Stronger abandonment system
```

## Release 1.4 — Multiplayer Expansion

```text
Async league
Player-to-player trading
Shared infrastructure
Ranked matches
Replay and debrief
```

## Release 1.5 — Training Mode

```text
Facilitator dashboard
Pause/resume
Inject events
View all teams
Export learning reports
AI debrief summaries
```

## Release 1.6 — Content Expansion

```text
Offshore basin
Gas province
Mature field rescue scenario
Oil price crash campaign
Responsible abandonment campaign
```

---

# 32. Recommended MVP Decision

The MVP should be built as follows:

```text
First Scenario:
Desert Frontier

First Gameplay Modes:
Fun Mode and Balanced Mode

First Platform:
Web browser desktop/laptop

First Visual Style:
Simple 2.5D/isometric map with clean UI panels

First Backend:
ASP.NET Core server-authoritative simulation

First Client:
TypeScript + Babylon.js + React

First Database:
PostgreSQL

First AI:
Command Center with Strategy Advisor, Geologist, CFO, and HSE Advisor

First Multiplayer Target:
Architecture-ready from the start, live 2–6 player mode after solo loop works

First Success Goal:
Complete fun and balanced playable matches from start to final score.
```

This MVP provides a practical first version of Beep Oil and Gas Sim while keeping the architecture ready for realistic simulation, mission challenges, team collaboration, and training use cases.
