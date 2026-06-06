# Beep Oil and Gas Sim

## Master Game Design Document

**Document ID:** BOGS-GDD-001
**Version:** 0.1
**Status:** Draft
**Project Name:** Beep Oil and Gas Sim
**Short Name:** Beep O&G Sim
**Genre:** Competitive oil and gas strategy / tycoon simulation
**Platform:** Web
**Recommended Gameplay Style:** Turn-based strategy with live animated feedback
**Recommended Visual Style:** 2.5D isometric / low-poly 3D
**Recommended Technology:** TypeScript, Babylon.js, ASP.NET Core, SignalR, PostgreSQL
**Core AI Feature:** Command Center AI

---

# 1. Executive Summary

Beep Oil and Gas Sim is a competitive web-based oil and gas strategy simulation where players manage energy companies through the full petroleum lifecycle.

Players compete to acquire acreage, study geology, drill exploration wells, appraise discoveries, develop fields, produce oil and gas, survive market shocks, optimize assets, and responsibly abandon or repurpose late-life infrastructure.

The game combines:

* Oil and gas business strategy
* Exploration risk
* Field development decisions
* Production management
* Financial planning
* Market volatility
* Multiplayer competition
* Team collaboration
* AI-assisted decision-making

The goal is to create a game that is fun and exciting while still teaching real oil and gas concepts in a simplified and accessible way.

---

# 2. Vision Statement

Beep Oil and Gas Sim should make the player feel like they are leading a real oil and gas company.

The player should experience the excitement of discovering a major field, the stress of dry holes, the pressure of expensive development projects, the opportunity of high oil prices, and the responsibility of safe abandonment.

The game should be realistic enough to be meaningful, but not so complex that it becomes a technical engineering tool.

The experience should feel like:

> Civilization-style strategic territory competition mixed with tycoon business management, oilfield risk, multiplayer rivalry, and AI-powered company advisors.

---

# 3. Core Design Pillars

## 3.1 Full Oil and Gas Lifecycle

The game should cover the full asset lifecycle:

1. Basin entry
2. License bidding
3. Geological study
4. Seismic acquisition
5. Exploration drilling
6. Discovery
7. Appraisal
8. Development planning
9. Field construction
10. Production
11. Optimization
12. Late-life management
13. Decommissioning
14. Abandonment

This makes the game different from simple drilling or idle oil games.

---

## 3.2 Fun Through Uncertainty

Players should never know everything.

They should make decisions based on incomplete information:

* Is this block worth bidding for?
* Is the seismic interpretation reliable?
* Should we drill now or buy more data?
* Is this discovery commercial?
* Will oil prices stay high?
* Should we sell the field before decline gets worse?

The game should make risk feel exciting, not random.

---

## 3.3 Competitive Company Strategy

Players are not just drilling wells. They are running companies.

They must manage:

* Cash
* Debt
* Assets
* Reserves
* Production
* Reputation
* Safety
* Environmental liability
* Competitor behavior

Players can win through different strategies:

* High-risk exploration
* Conservative development
* Mature asset optimization
* Gas infrastructure focus
* Fast production growth
* Strong financial control
* ESG/reputation leadership

---

## 3.4 Simple Simulation, Deep Decisions

The game should not start as a full petroleum engineering simulator.

Instead, it should use simplified models that create meaningful choices.

Example:

* Exploration success uses probability and geological risk factors.
* Production uses simplified decline curves.
* Development uses CAPEX, OPEX, time, capacity, and risk.
* Abandonment uses liability, cost, regulation, and reputation.

The player should understand why a result happened without needing to be an engineer.

---

## 3.5 AI as a Gameplay Feature

The AI system should be part of the game experience.

The player should have a Command Center AI that can act as:

* Strategy Advisor
* Geologist
* Reservoir Engineer
* Drilling Advisor
* CFO
* HSE Advisor
* Market Analyst

The AI should help players understand risk, compare options, summarize team discussion, and propose actions.

The AI should not cheat. It must only use information the player is allowed to know.

---

# 4. Target Audience

## 4.1 Primary Audience

* Strategy game players
* Tycoon game players
* Simulation game players
* Oil and gas professionals
* Energy students
* Corporate training users
* Business simulation users

## 4.2 Secondary Audience

* Multiplayer board-game style players
* Educational institutions
* Energy transition learners
* Management training teams
* Web game players interested in realistic economic games

---

# 5. Player Fantasy

The player fantasy is:

> “I am running an oil and gas company. I must discover resources, make smart investments, beat competitors, survive uncertainty, and build the most valuable company.”

Important emotional moments:

* Winning an important license auction
* Discovering a large field
* Getting a dry hole after a risky bet
* Surviving an oil price crash
* Beating a competitor to first oil
* Selling an asset at the perfect time
* Making a bad development decision and paying for it
* Abandoning responsibly while competitors receive penalties

---

# 6. High-Level Game Loop

The core loop is:

```text
Review company status
    ↓
Review map, assets, and market
    ↓
Choose strategy
    ↓
Submit actions
    ↓
Resolve turn
    ↓
Receive results
    ↓
React to new information
    ↓
Improve company value
```

The oilfield loop is:

```text
Acquire acreage
    ↓
Study geology
    ↓
Drill exploration well
    ↓
Discover or fail
    ↓
Appraise discovery
    ↓
Develop field
    ↓
Produce oil and gas
    ↓
Optimize or sell
    ↓
Abandon or repurpose
```

---

# 7. Recommended Game Format

The recommended format is a **hybrid turn-based game**.

Players make decisions during a planning phase. The server then resolves all decisions together.

This works well because oil and gas decisions naturally happen over months and years.

## 7.1 Turn Duration

Each turn can represent:

* 3 months
* 6 months
* 1 year

For MVP, each turn should represent **6 months**.

## 7.2 Match Length

Recommended match length:

```text
Short match: 12 turns
Standard match: 20 turns
Long match: 30 turns
Campaign: Scenario-based progression
```

For MVP, use **20 turns**.

---

# 8. Main Game Modes

## 8.1 Solo Scenario Mode

Single-player scenarios designed to teach the game.

Example scenarios:

1. First Discovery
2. License Auction
3. Desert Frontier
4. Offshore Gamble
5. Oil Price Crash
6. Mature Field Rescue
7. Responsible Abandonment

---

## 8.2 Live Multiplayer Match

A session where players compete in real time.

Recommended format:

```text
Players: 2–6
Turns: 12–30
Turn timer: 2–5 minutes
Estimated session length: 45–90 minutes
```

---

## 8.3 Async League Mode

A slower multiplayer mode.

Recommended format:

```text
Players: 10–50
Turn deadline: every 12 or 24 hours
Players submit actions before deadline
Server resolves all actions together
Leaderboard updates after each turn
```

---

## 8.4 Team Company Mode

Multiple players manage one company together.

Example roles:

* CEO
* Exploration Manager
* Drilling Manager
* Production Manager
* Finance Manager
* HSE Manager

Each role can propose actions. The CEO approves the final turn plan.

---

# 9. Core Game Stages

## 9.1 Basin Entry

Players enter a basin or region.

Each basin has:

* Geological potential
* Political risk
* Infrastructure maturity
* Environmental sensitivity
* Service cost level
* Market access
* Fiscal terms

Example basins:

* Desert Frontier
* Offshore Shelf
* Deepwater Basin
* Mature Onshore Basin
* Gas Province
* Shale Basin

---

## 9.2 License Auction

Players bid for blocks.

Each block has public information and hidden truth.

Public information may include:

* Location
* Surface risk
* Distance to infrastructure
* Known nearby discoveries
* Geological hints
* Environmental restrictions

Hidden information may include:

* Source rock quality
* Reservoir quality
* Trap integrity
* Seal quality
* Fluid type
* Recoverable volume
* Depth
* Pressure
* Development difficulty

Players must decide how much to bid without knowing the full value.

---

## 9.3 Exploration

Players can reduce uncertainty by buying data or drilling.

Exploration actions:

* Geological study
* 2D seismic
* 3D seismic
* AI prospect interpretation
* Exploration well

Exploration should feel like smart gambling.

The more data a player buys, the better their probability estimates become.

---

## 9.4 Discovery

If an exploration well succeeds, a discovery is created.

A discovery includes:

* Fluid type
* Estimated recoverable volume
* Confidence range
* Depth
* Pressure
* Reservoir quality estimate
* Development complexity
* Commerciality risk

The discovery should still have uncertainty until appraisal is completed.

---

## 9.5 Appraisal

Players drill appraisal wells or run technical studies to reduce uncertainty.

Appraisal can:

* Increase estimated reserves
* Reduce estimated reserves
* Reveal technical issues
* Confirm commerciality
* Prove the discovery is uneconomic

---

## 9.6 Development

Players choose how to develop a commercial discovery.

Development concepts may include:

* Onshore central processing facility
* Offshore fixed platform
* FPSO
* Subsea tieback
* Modular early production system
* Gas-to-power project
* LNG export project
* Pipeline connection

Each development concept affects:

* CAPEX
* OPEX
* Construction time
* First oil date
* Production capacity
* Risk
* Emissions
* Abandonment liability

---

## 9.7 Production

Fields generate oil and gas revenue.

Production is affected by:

* Initial production rate
* Decline rate
* Facility capacity
* Uptime
* Maintenance
* Water cut
* Gas handling
* Optimization investments
* Commodity prices

Production should be simple but satisfying.

---

## 9.8 Optimization

Players can improve field performance.

Optimization actions:

* Workover
* Artificial lift
* Water injection
* Gas injection
* Facility debottlenecking
* Maintenance campaign
* Digital field optimization
* Pipeline tariff negotiation

Optimization should give players meaningful mid-game decisions.

---

## 9.9 Late-Life Management

As fields decline, players decide whether to continue, sell, repurpose, or abandon.

Late-life actions:

* Continue production
* Reduce OPEX
* Sell asset
* Convert infrastructure
* Prepare abandonment
* Delay abandonment
* Plug wells

---

## 9.10 Abandonment

Abandonment is a major part of the game.

Players must:

* Plug wells
* Remove facilities
* Restore sites
* Pay abandonment costs
* Manage regulatory compliance

Ignoring abandonment may increase short-term cash but creates penalties.

---

# 10. Main Player Resources

## 10.1 Financial

* Cash
* Debt
* Credit rating
* CAPEX budget
* OPEX burden
* Revenue
* Profit
* Asset NPV

## 10.2 Technical

* Geological knowledge
* Seismic coverage
* Drilling capability
* Facility capacity
* Production technology
* Abandonment capability

## 10.3 Assets

* Licenses
* Prospects
* Discoveries
* Fields
* Wells
* Facilities
* Pipelines
* Contracts

## 10.4 Reputation

* Safety reputation
* Environmental reputation
* Government relationship
* Investor confidence
* Community trust

---

# 11. Victory and Scoring

The winner should not be decided only by cash.

Recommended final score:

```text
Final Score =
    Cash
  + Asset NPV
  + Proven Reserves Value
  + Production Performance
  + Technology Bonus
  + Reputation Bonus
  - Debt Penalty
  - Safety Penalty
  - Environmental Penalty
  - Unfunded Abandonment Liability
```

This creates balanced gameplay.

A player who produces aggressively but ignores abandonment should not automatically win.

---

# 12. Competition Systems

Players compete through:

* License auctions
* Limited rigs
* Shared infrastructure
* Commodity market timing
* Asset sales
* Service cost inflation
* Public discoveries
* Reputation
* Final valuation
* Leaderboards

Competitor discoveries can affect nearby block values.

For example, if one player discovers oil in Block 12, nearby blocks may become more attractive.

---

# 13. Event System

Events create drama and uncertainty.

## 13.1 Market Events

* Oil price crash
* Gas price spike
* Demand boom
* Service cost inflation
* Rig shortage
* Currency shock

## 13.2 Technical Events

* Dry hole
* Better-than-expected reservoir
* Water breakthrough
* Drilling delay
* Equipment failure
* Facility bottleneck

## 13.3 Political Events

* Tax increase
* New environmental law
* License deadline
* Export restriction
* Local content requirement

## 13.4 Safety and Environmental Events

* Near miss
* Spill
* Blowout risk
* Community protest
* Abandonment inspection

## 13.5 Competitive Events

* Competitor discovery
* Farm-in offer
* Asset auction
* Infrastructure conflict

---

# 14. AI Command Center

The AI Command Center is an in-game advisory and collaboration feature.

Players can chat with AI advisors that understand the current game state.

AI advisor roles:

* Strategy Advisor
* Geologist
* Reservoir Engineer
* Drilling Engineer
* CFO
* HSE Advisor
* Market Analyst

Players can ask:

* Should we drill this block?
* Should we buy seismic first?
* Can we afford this development?
* What is our best move next turn?
* Which field should we optimize?
* Should we sell or abandon this asset?
* Summarize our team discussion.
* Create an action proposal.

The AI can recommend actions but cannot execute them without player approval.

---

# 15. Team Collaboration

Team mode allows multiple players to manage one company.

Main features:

* Team chat
* AI advisor chat
* Action proposal board
* Voting
* CEO approval
* Role permissions
* Turn summary
* Shared reports

Example workflow:

```text
Exploration Manager proposes drilling Block 8.
CFO says cash is too low.
AI Geologist recommends 3D seismic first.
CEO approves seismic purchase.
The action is submitted for turn resolution.
```

---

# 16. User Interface Overview

The UI should include:

## Main Game Screen

* 2.5D map
* Company status bar
* Turn timer
* Resource indicators
* Leaderboard
* Event notifications

## Side Panels

* Block details
* Field details
* Well details
* Facility details
* Company dashboard
* Market dashboard
* Action list

## Command Center

* Team chat
* AI advisor chat
* Action proposals
* Turn summary
* Reports

## Decision Screens

* License auction screen
* Exploration planning screen
* Development planning screen
* Production optimization screen
* Abandonment planning screen

---

# 17. Technical Architecture

## 17.1 Client

Recommended client stack:

* TypeScript
* Babylon.js
* Vite
* React or HTML overlay UI

Client responsibilities:

* Render map
* Display UI
* Show animations
* Submit player actions
* Display server results
* Handle chat and AI panel
* Show leaderboards and reports

---

## 17.2 Server

Recommended server stack:

* ASP.NET Core Web API
* SignalR
* PostgreSQL
* Redis optional

Server responsibilities:

* Authentication
* Game session management
* Hidden geology
* Turn resolution
* Asset state
* Multiplayer synchronization
* Chat
* AI context generation
* Action validation
* Database persistence

The server must be authoritative.

The client should never resolve hidden results such as dry holes, discoveries, reserves, production, or random events.

---

## 17.3 AI Layer

The AI layer should be server-side.

Main components:

* AI Advisor Service
* AI Context Builder
* AI Tool Registry
* AI Conversation Store
* AI Safety Guard
* AI Report Generator

The AI should only receive data that the player or team is allowed to know.

---

# 18. MVP Scope

The MVP should be small but playable.

## MVP Scenario

```text
Scenario Name: Desert Frontier
Players: 2–6
Turns: 20
Starting Cash: $500M
Map: 20 license blocks
Primary Commodity: Oil
Victory: Highest final company value
```

## MVP Features

* Create or join game
* One basin map
* 20 license blocks
* License auction
* Geological study
* 2D seismic
* Exploration well
* Discovery or dry hole
* Basic appraisal
* Simple development option
* Basic production decline
* Oil price event
* Leaderboard
* Team chat
* AI Strategy Advisor
* Turn summary
* Final score

## Not in MVP

* Full reservoir simulation
* Full 3D realism
* Complex offshore engineering
* Advanced AI competitors
* Real commodity data integration
* Mobile-first layout
* Voice AI
* Full asset trading marketplace
* Advanced abandonment engineering

---

# 19. Recommended Development Phases

## Phase 1: Prototype

Goal: prove the game loop.

Includes:

* Simple map
* Blocks
* Auctions
* Exploration drilling
* Discovery/dry hole
* Basic production
* Turn resolution

---

## Phase 2: MVP

Goal: playable multiplayer match.

Includes:

* 2–6 player game
* UI panels
* Leaderboard
* Basic AI advisor
* Team chat
* Final scoring

---

## Phase 3: Expanded Simulation

Goal: deeper oil and gas systems.

Includes:

* Appraisal
* Multiple development concepts
* Production optimization
* More events
* Late-life management
* Abandonment

---

## Phase 4: Team Company Mode

Goal: support collaborative gameplay.

Includes:

* Company roles
* Proposal board
* Voting
* CEO approval
* AI meeting summary

---

## Phase 5: Campaign and Content

Goal: improve retention and learning.

Includes:

* Solo scenarios
* Tutorials
* Multiple basins
* Scenario editor
* More event cards

---

# 20. Design Document Set

This master document is the top-level reference.

The detailed documents should be created in this order:

## BOGS-GDD-001

Master Game Design Document

## BOGS-GDD-002

Gameplay Systems Design

## BOGS-GDD-003

Oil and Gas Lifecycle Simulation Design

## BOGS-GDD-004

Economy, Finance, and Market Design

## BOGS-GDD-005

Exploration and Geology Design

## BOGS-GDD-006

Field Development and Production Design

## BOGS-GDD-007

Late-Life, Decommissioning, and Abandonment Design

## BOGS-GDD-008

AI Command Center Design

## BOGS-GDD-009

Multiplayer and Team Collaboration Design

## BOGS-GDD-010

User Interface and User Experience Design

## BOGS-GDD-011

Technical Architecture Design

## BOGS-GDD-012

MVP Development Plan

## BOGS-GDD-013

Content and Scenario Design

## BOGS-GDD-014

Data Model and Backend Domain Design

## BOGS-GDD-015

Balancing and Scoring Design

---

# 21. Key Open Questions

The following decisions should be confirmed during detailed design:

1. Should the first MVP be single-player, multiplayer, or both?
2. Should the first map be desert, offshore, or mixed?
3. Should production focus only on oil first, or include gas?
4. Should the match length be 12, 20, or 30 turns?
5. Should the graphics be isometric 2D, 2.5D, or low-poly 3D?
6. Should team mode be included in MVP or Phase 2?
7. Should AI be available from the beginning or added after core gameplay?
8. Should the game use fictional data only, or allow real-world inspired basins?
9. Should the game support educational/training mode?
10. Should the backend be built first with simulation tests before the full UI?

---

# 22. Recommended Initial Decision

The recommended initial build direction is:

```text
Game Name: Beep Oil and Gas Sim
First Scenario: Desert Frontier
Game Mode: Turn-based multiplayer with solo testing support
Players: 2–6
Turns: 20
Graphics: 2.5D low-poly/isometric
Frontend: TypeScript + Babylon.js
Backend: ASP.NET Core + SignalR
Database: PostgreSQL
AI: Basic Strategy Advisor in MVP
```

This direction gives the project a strong foundation while keeping the first version achievable.
