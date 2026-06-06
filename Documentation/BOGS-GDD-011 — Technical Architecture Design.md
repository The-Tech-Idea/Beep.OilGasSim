# Beep Oil and Gas Sim

## Technical Architecture Design

**Document ID:** BOGS-GDD-011
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

**Project Name:** Beep Oil and Gas Sim
**Short Name:** Beep O&G Sim
**Recommended Client Stack:** TypeScript, Babylon.js, React, Vite
**Recommended Backend Stack:** ASP.NET Core, SignalR, PostgreSQL
**Recommended Architecture Style:** Server-authoritative multiplayer simulation with browser-based 2.5D client

---

# 1. Purpose

This document defines the technical architecture for Beep Oil and Gas Sim.

The architecture must support:

* Browser-based gameplay
* 2.5D or low-poly 3D map rendering
* Turn-based game simulation
* Multiplayer sessions
* Team collaboration
* AI Command Center
* Server-authoritative game rules
* Hidden geology and anti-cheat protection
* Persistent matches
* Scenario and content data
* Future expansion into async leagues, campaigns, and training mode

The technical architecture should be practical, scalable, and aligned with the development team's .NET experience while still using a browser-native game client.

---
Gameplay Modes:
- Implement GameplayModeProfile from the beginning.
- Ship MVP with Fun Mode and Balanced Mode.
- Fun Mode uses simple mobile-style UI and guided AI.
- Balanced Mode uses the standard strategy simulation rules.

Check  Gameplay Mode Profiles Architecture.md

# 2. Architecture Goals

## 2.1 Server-Authoritative Simulation

The server must be the source of truth.

The client may display and request actions, but the server decides:

```text
Hidden geology
Drilling results
Discovery results
Production results
Market events
Financial calculations
Turn resolution
Leaderboard
Final score
```

This protects multiplayer fairness.

---

## 2.2 Web-Native Client

The client should run directly in a browser.

Recommended:

```text
TypeScript
Babylon.js
React overlay UI
Vite build system
```

The game canvas handles the map. React handles panels, dashboards, chat, action boards, and AI Command Center.

---

## 2.3 .NET-Centric Backend

The backend should use ASP.NET Core because it fits the expected team skills and supports:

```text
Web APIs
SignalR realtime communication
Authentication
Hosted services
Background turn resolution
Dependency injection
Entity Framework Core
Clean architecture
```

---

## 2.4 Modular Domain Design

The backend should be divided into clear systems:

```text
Game Session
Simulation
Exploration
Development
Production
Economy
Events
AI
Multiplayer
Collaboration
Persistence
Content
```

Each system should be testable independently.

---

## 2.5 AI-Safe Context Separation

The AI must never receive hidden game data.

The architecture must enforce:

```text
Game State
    ↓
Visibility Filter
    ↓
AI-Safe Context
    ↓
AI Advisor
```

Hidden geology and future events must stay server-only.

---

## 2.6 Future Expansion

The architecture should support later features:

```text
Async league mode
AI competitors
Scenario editor
Training/facilitator mode
Player-to-player trading
Shared infrastructure
Advanced abandonment
Real-world inspired datasets
Replay system
Mobile/tablet UI
```

---

# 3. High-Level Architecture

```text
Browser Client
 ├── Babylon.js Map Renderer
 ├── React UI Overlay
 ├── SignalR Realtime Client
 └── REST API Client

        ↓ HTTPS / WebSocket

ASP.NET Core Backend
 ├── REST API Controllers
 ├── SignalR Hubs
 ├── Game Session Services
 ├── Turn Simulation Engine
 ├── Domain Services
 ├── AI Advisor Services
 ├── Collaboration Services
 ├── Event Services
 └── Persistence Layer

        ↓

Database / Storage
 ├── PostgreSQL
 ├── Redis, optional
 ├── File/Object Storage, optional
 └── Logs / Telemetry

        ↓

External Services
 ├── AI Provider
 ├── Authentication Provider, optional
 └── Email/Notification Provider, optional
```

---

# 4. Recommended Solution Structure

Recommended repository:

```text
beep-oil-gas-sim/
│
├── client/
│   └── beep-oil-gas-sim-web/
│
├── server/
│   ├── Beep.OilGasSim.Api/
│   ├── Beep.OilGasSim.Application/
│   ├── Beep.OilGasSim.Domain/
│   ├── Beep.OilGasSim.Infrastructure/
│   ├── Beep.OilGasSim.Simulation/
│   ├── Beep.OilGasSim.AI/
│   └── Beep.OilGasSim.Tests/
│
├── shared/
│   ├── contracts/
│   └── content-schemas/
│
├── content/
│   ├── scenarios/
│   ├── basins/
│   ├── events/
│   ├── development-concepts/
│   └── balance/
│
├── docs/
│   ├── gdd/
│   ├── architecture/
│   └── api/
│
└── deploy/
    ├── docker/
    ├── compose/
    └── cloud/
```

---

# 5. Backend Project Structure

## 5.1 Beep.OilGasSim.Api

The API project exposes HTTP endpoints and SignalR hubs.

Responsibilities:

```text
Authentication
REST endpoints
SignalR hubs
Request validation
Response DTOs
API versioning
Health checks
```

Recommended folders:

```text
Beep.OilGasSim.Api/
├── Controllers/
├── Hubs/
├── Middleware/
├── Filters/
├── Contracts/
├── Authentication/
├── Configuration/
└── Program.cs
```

---

## 5.2 Beep.OilGasSim.Domain

The domain project contains core business entities and rules.

Responsibilities:

```text
Core entities
Value objects
Domain enums
Domain events
Business invariants
No database-specific logic
No web-specific logic
```

Recommended folders:

```text
Beep.OilGasSim.Domain/
├── GameSessions/
├── Companies/
├── Basins/
├── Blocks/
├── Exploration/
├── Development/
├── Production/
├── Economy/
├── Events/
├── Collaboration/
├── AI/
├── Common/
└── DomainEvents/
```

---

## 5.3 Beep.OilGasSim.Application

The application project coordinates use cases.

Responsibilities:

```text
Command handlers
Query handlers
Application services
Validation
Authorization checks
Transaction boundaries
DTO mapping
```

Recommended folders:

```text
Beep.OilGasSim.Application/
├── GameSessions/
├── Turns/
├── Companies/
├── Actions/
├── Proposals/
├── Chat/
├── Leaderboards/
├── Reports/
├── Interfaces/
└── Common/
```

---

## 5.4 Beep.OilGasSim.Simulation

The simulation project contains deterministic game resolution logic.

Responsibilities:

```text
Turn resolution
Exploration simulation
Appraisal simulation
Development simulation
Production simulation
Economy simulation
Market events
Scoring
Balancing rules
```

Recommended folders:

```text
Beep.OilGasSim.Simulation/
├── TurnEngine/
├── Exploration/
├── Appraisal/
├── Development/
├── Production/
├── Economy/
├── Market/
├── Events/
├── Scoring/
├── Randomness/
└── Reports/
```

---

## 5.5 Beep.OilGasSim.AI

The AI project contains AI advisor services.

Responsibilities:

```text
AI context building
AI-safe visibility filtering
Prompt library
Advisor role management
AI tool registry
AI response validation
Turn summaries
Team summaries
Draft proposal generation
```

Recommended folders:

```text
Beep.OilGasSim.AI/
├── Advisors/
├── Context/
├── Prompts/
├── Tools/
├── Reports/
├── Safety/
├── Providers/
└── Contracts/
```

---

## 5.6 Beep.OilGasSim.Infrastructure

The infrastructure project connects to external systems.

Responsibilities:

```text
Database access
Entity Framework Core
Repository implementations
AI provider implementation
Caching
File storage
Email/notification integrations
Logging integrations
```

Recommended folders:

```text
Beep.OilGasSim.Infrastructure/
├── Persistence/
├── Repositories/
├── Caching/
├── AI/
├── Storage/
├── Messaging/
├── Telemetry/
└── Configuration/
```

---

## 5.7 Beep.OilGasSim.Tests

Testing project.

Recommended test areas:

```text
Simulation tests
Economy tests
Exploration probability tests
Turn resolution tests
Visibility tests
AI context safety tests
Action validation tests
API integration tests
```

---

# 6. Client Architecture

The client should be a browser app.

Recommended stack:

```text
TypeScript
Vite
Babylon.js
React
SignalR client
State management library
```

The client has two main layers:

```text
Game rendering layer
UI application layer
```

---

## 6.1 Client Responsibilities

The client should:

```text
Render the map
Show blocks and assets
Display panels and dashboards
Let players select actions
Send actions to server
Receive turn updates
Display results
Handle chat
Handle AI Command Center UI
Display leaderboards
Animate events
```

The client should not:

```text
Resolve drilling outcomes
Store hidden geology
Calculate final production truth
Generate future events
Decide auction results
Calculate final scoring authoritatively
```

---

## 6.2 Client Folder Structure

```text
client/beep-oil-gas-sim-web/
├── src/
│   ├── app/
│   │   ├── App.tsx
│   │   ├── routes.tsx
│   │   └── providers.tsx
│   │
│   ├── game/
│   │   ├── engine/
│   │   ├── map/
│   │   ├── camera/
│   │   ├── assets/
│   │   └── animation/
│   │
│   ├── ui/
│   │   ├── layout/
│   │   ├── panels/
│   │   ├── command-center/
│   │   ├── lobby/
│   │   ├── leaderboard/
│   │   ├── results/
│   │   └── common/
│   │
│   ├── state/
│   │   ├── gameStore.ts
│   │   ├── companyStore.ts
│   │   ├── actionStore.ts
│   │   ├── aiStore.ts
│   │   ├── chatStore.ts
│   │   └── uiStore.ts
│   │
│   ├── net/
│   │   ├── ApiClient.ts
│   │   ├── GameHubClient.ts
│   │   ├── ChatHubClient.ts
│   │   ├── AiHubClient.ts
│   │   └── dto/
│   │
│   ├── models/
│   ├── content/
│   ├── utils/
│   └── main.tsx
│
├── public/
├── package.json
├── vite.config.ts
└── tsconfig.json
```

---

# 7. Rendering Architecture

## 7.1 Babylon.js Scene

Babylon.js should handle:

```text
Basin terrain
License block meshes
Asset markers
Rigs
Pumpjacks
Platforms
Pipelines
Production animations
Camera
Selection highlights
Map layer visualization
```

---

## 7.2 React Overlay

React should handle:

```text
Top bar
Left navigation
Right detail panel
Bottom action queue
Command Center
Chat
Proposal board
Turn results
Leaderboard
Charts
Forms
Modals
Tooltips
```

---

## 7.3 Scene-to-UI Communication

Selection flow:

```text
Player clicks block mesh in Babylon.js
    ↓
Babylon layer emits SelectedBlockChanged
    ↓
UI state updates selectedBlockId
    ↓
React right panel loads block details
    ↓
Action buttons become available
```

Recommended event bridge:

```typescript
export interface GameSelectionEvent {
  type: "Block" | "Field" | "Well" | "Facility";
  id: string;
}
```

---

# 8. Server-Authoritative Game State

The server stores the authoritative game state.

## 8.1 Game State Categories

```text
Public game state
Company-private game state
Server-hidden game state
```

---

## 8.2 Public State

Visible to all players:

```text
Map layout
Block ownership
Public discoveries
Producing fields
Public events
Oil price
Leaderboard
Company rank
Public reputation
```

---

## 8.3 Company-Private State

Visible only to the company:

```text
Cash
Debt
Detailed asset economics
Seismic interpretation
Private drilling reports
Team chat
AI advice
Pending action plan
Private proposal board
```

---

## 8.4 Server-Hidden State

Never sent to client:

```text
Hidden geology
True undiscovered volumes
True undiscovered chance of success
Future random events
Random seeds
Competitor private actions before resolution
Private competitor finances
```

---

# 9. Domain Model Overview

## 9.1 Core Entities

```text
GameSession
Scenario
Turn
Company
Player
CompanyPlayer
Basin
LicenseBlock
BlockKnowledge
Prospect
Well
Discovery
DevelopmentProject
ProducingField
Facility
TurnAction
TurnResult
MarketState
GameEvent
ActionProposal
TeamMessage
AiConversation
LeaderboardSnapshot
```

---

## 9.2 GameSession

```csharp
public sealed class GameSession
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "";
    public Guid ScenarioId { get; set; }

    public GameSessionState State { get; set; }

    public int CurrentTurnNumber { get; set; }
    public int TotalTurns { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public List<Company> Companies { get; set; } = new();
    public List<Turn> Turns { get; set; } = new();
}
```

---

## 9.3 Company

```csharp
public sealed class Company
{
    public Guid Id { get; set; }
    public Guid GameSessionId { get; set; }

    public string Name { get; set; } = "";
    public string ColorHex { get; set; } = "";

    public CompanyFinance Finance { get; set; } = new();
    public CompanyReputation Reputation { get; set; } = new();

    public List<CompanyPlayer> Players { get; set; } = new();
}
```

---

## 9.4 LicenseBlock

```csharp
public sealed class LicenseBlock
{
    public Guid Id { get; set; }
    public Guid GameSessionId { get; set; }
    public Guid BasinId { get; set; }

    public string Name { get; set; } = "";

    public Guid? OwnerCompanyId { get; set; }

    public BlockPublicData PublicData { get; set; } = new();
    public HiddenGeology HiddenGeology { get; set; } = new();

    public AssetStage Stage { get; set; }

    public int GridX { get; set; }
    public int GridY { get; set; }
}
```

---

## 9.5 TurnAction

```csharp
public sealed class TurnAction
{
    public Guid Id { get; set; }
    public Guid GameSessionId { get; set; }
    public Guid CompanyId { get; set; }

    public int TurnNumber { get; set; }

    public TurnActionType ActionType { get; set; }
    public Guid? TargetBlockId { get; set; }
    public Guid? TargetAssetId { get; set; }

    public decimal EstimatedCost { get; set; }
    public int ActionSlotCost { get; set; }

    public TurnActionStatus Status { get; set; }

    public string ParametersJson { get; set; } = "";
}
```

---

## 9.6 TurnActionType

```csharp
public enum TurnActionType
{
    BidForLicense,
    RelinquishLicense,

    GeologicalStudy,
    Acquire2DSeismic,
    Acquire3DSeismic,

    DrillExplorationWell,
    DrillAppraisalWell,

    ApproveDevelopment,
    OptimizeField,

    HedgeProduction,
    TakeDebt,
    RepayDebt,
    SellAsset,

    AbandonField
}
```

---

# 10. Turn Resolution Architecture

The turn engine should resolve all companies together.

## 10.1 Turn Resolution Flow

```text
Start Turn Resolution
    ↓
Load game session state
    ↓
Load all committed company actions
    ↓
Validate actions
    ↓
Resolve actions in deterministic order
    ↓
Update game state
    ↓
Calculate financials
    ↓
Update leaderboard
    ↓
Generate reports
    ↓
Persist results
    ↓
Notify clients
End Turn Resolution
```

---

## 10.2 Turn Engine Interface

```csharp
public interface ITurnEngine
{
    Task<TurnResolutionResult> ResolveTurnAsync(
        Guid gameSessionId,
        int turnNumber,
        CancellationToken cancellationToken);
}
```

---

## 10.3 Turn Engine Implementation

```csharp
public sealed class TurnEngine : ITurnEngine
{
    private readonly IActionValidator _actionValidator;
    private readonly IAuctionResolver _auctionResolver;
    private readonly IExplorationResolver _explorationResolver;
    private readonly IDevelopmentResolver _developmentResolver;
    private readonly IProductionResolver _productionResolver;
    private readonly IEconomyResolver _economyResolver;
    private readonly IMarketResolver _marketResolver;
    private readonly IEventResolver _eventResolver;
    private readonly IScoringService _scoringService;

    public async Task<TurnResolutionResult> ResolveTurnAsync(
        Guid gameSessionId,
        int turnNumber,
        CancellationToken cancellationToken)
    {
        // 1. Load state
        // 2. Validate actions
        // 3. Resolve systems in correct order
        // 4. Persist results
        // 5. Return reports
        throw new NotImplementedException();
    }
}
```

---

## 10.4 Resolution Order

```text
1. Validate submitted actions
2. Process license auctions
3. Apply license fees
4. Process geological studies
5. Process seismic actions
6. Resolve exploration wells
7. Resolve appraisal wells
8. Approve and advance development projects
9. Start completed production projects
10. Calculate production volumes
11. Apply commodity prices
12. Calculate revenue
13. Apply OPEX, CAPEX, royalty, interest, debt
14. Apply optimization effects
15. Apply market and random events
16. Apply HSE and reputation effects
17. Process abandonment actions
18. Update asset values
19. Update company values
20. Update leaderboard
21. Generate turn results
```

---

# 11. Deterministic Randomness

Simulation randomness must be controlled.

## 11.1 Random Seed Strategy

Each game session should have a server-generated seed.

Each turn can derive deterministic sub-seeds.

Example:

```text
GameSeed
    ↓
TurnSeed
    ↓
SystemSeed
    ↓
ActionSeed
```

This supports:

```text
Debugging
Replay
Testing
Auditability
Fairness
```

---

## 11.2 Random Service

```csharp
public interface IGameRandom
{
    double NextDouble();
    int NextInt(int minInclusive, int maxExclusive);
}
```

```csharp
public interface IGameRandomFactory
{
    IGameRandom CreateForTurn(Guid gameSessionId, int turnNumber, string systemName);
}
```

---

# 12. Simulation Services

## 12.1 Exploration Resolver

Responsibilities:

```text
Calculate true exploration success
Resolve exploration wells
Create discoveries
Create dry holes
Update knowledge
Generate drilling reports
```

Interface:

```csharp
public interface IExplorationResolver
{
    Task ResolveExplorationActionsAsync(
        TurnResolutionContext context,
        CancellationToken cancellationToken);
}
```

---

## 12.2 Development Resolver

Responsibilities:

```text
Approve development projects
Advance construction
Apply delays
Create producing fields
```

---

## 12.3 Production Resolver

Responsibilities:

```text
Calculate production volume
Apply uptime
Apply decline
Update remaining reserves
Determine late-life triggers
```

---

## 12.4 Economy Resolver

Responsibilities:

```text
Calculate revenue
Calculate OPEX
Apply CAPEX
Apply royalties
Apply interest
Apply debt
Apply cash changes
Handle financial distress
```

---

## 12.5 Market Resolver

Responsibilities:

```text
Update oil price
Apply market trend
Apply market events
Resolve hedging
```

---

## 12.6 Event Resolver

Responsibilities:

```text
Trigger random events
Apply event effects
Generate event reports
Update reputation
```

---

## 12.7 Scoring Service

Responsibilities:

```text
Calculate company value
Calculate final score
Apply abandonment penalties
Apply reputation bonus
Update leaderboard
```

---

# 13. Action Validation Architecture

Every action must be validated server-side.

## 13.1 Action Validator Interface

```csharp
public interface IActionValidator
{
    Task<ActionValidationResult> ValidateAsync(
        TurnAction action,
        GameSessionStateSnapshot snapshot,
        CancellationToken cancellationToken);
}
```

---

## 13.2 Validation Checks

```text
Player belongs to company
Company owns target asset, if required
Asset is in correct stage
Action is allowed this turn
Company has action slots
Company can afford cost or borrow
Target block exists
Target asset exists
Turn is open
Action was not already committed
```

---

## 13.3 Validation Result

```csharp
public sealed class ActionValidationResult
{
    public bool IsValid { get; set; }

    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();

    public decimal ConfirmedCost { get; set; }
    public int ActionSlotCost { get; set; }
}
```

---

# 14. API Design

The backend should expose REST APIs for request/response operations and SignalR for realtime updates.

## 14.1 REST API Areas

```text
Auth
Player Profile
Game Sessions
Lobby
Company
Map
Assets
Actions
Turn Results
Leaderboard
Proposals
AI
Reports
```

---

## 14.2 Example Endpoints

```text
POST   /api/game-sessions
GET    /api/game-sessions/{id}
POST   /api/game-sessions/{id}/join
POST   /api/game-sessions/{id}/start

GET    /api/game-sessions/{id}/map
GET    /api/game-sessions/{id}/companies/{companyId}
GET    /api/game-sessions/{id}/companies/{companyId}/assets

POST   /api/game-sessions/{id}/actions
GET    /api/game-sessions/{id}/actions/current
POST   /api/game-sessions/{id}/turns/{turnNumber}/commit

GET    /api/game-sessions/{id}/turns/{turnNumber}/results
GET    /api/game-sessions/{id}/leaderboard

POST   /api/game-sessions/{id}/ai/ask
POST   /api/game-sessions/{id}/proposals
```

---

# 15. SignalR Design

SignalR should support realtime multiplayer.

## 15.1 Recommended Hubs

```text
GameHub
LobbyHub
TeamChatHub
ProposalHub
AiAdvisorHub
NotificationHub
```

For MVP, these may be combined:

```text
GameHub
ChatHub
AiAdvisorHub
```

---

## 15.2 SignalR Events

```text
LobbyUpdated
PlayerJoined
PlayerReadyChanged
GameStarted
TurnStarted
TurnTimerUpdated
ActionSubmitted
TurnCommitted
TurnResolving
TurnResolved
LeaderboardUpdated
ChatMessageReceived
ProposalCreated
ProposalUpdated
AiMessageReceived
NotificationReceived
```

---

## 15.3 Group Naming

```text
game:{gameSessionId}
game:{gameSessionId}:lobby
game:{gameSessionId}:company:{companyId}
game:{gameSessionId}:company:{companyId}:chat
game:{gameSessionId}:company:{companyId}:ai
game:{gameSessionId}:company:{companyId}:proposals
```

---

# 16. Database Architecture

PostgreSQL is recommended.

## 16.1 Main Tables

```text
Users
PlayerProfiles
GameSessions
Scenarios
Turns
Companies
CompanyPlayers
Basins
LicenseBlocks
BlockKnowledge
Prospects
Wells
Discoveries
DevelopmentProjects
ProducingFields
Facilities
TurnActions
TurnResults
MarketStates
GameEvents
CompanyFinances
CompanyReputations
ActionProposals
ProposalVotes
ProposalComments
TeamMessages
AiConversations
AiMessages
LeaderboardSnapshots
Notifications
```

---

## 16.2 JSON Columns

Some flexible data can be stored as JSON.

Good candidates:

```text
Turn action parameters
Event effects
Scenario configuration
AI context snapshots
Turn report details
Balance parameters
```

Important rule:

```text
Use normal relational columns for core searchable data.
Use JSON for flexible configuration and snapshots.
```

---

## 16.3 Migrations

Use database migrations for:

```text
Schema evolution
Seed data
Scenario data, if appropriate
Balance version references
```

---

# 17. Persistence Strategy

## 17.1 Game State Persistence

Persist after each important state change:

```text
Game created
Player joined
Turn actions submitted
Turn committed
Turn resolved
Chat message sent
Proposal created
AI message saved
```

---

## 17.2 Turn Snapshot

At the end of each turn, store a snapshot.

Snapshot should include:

```text
Company values
Asset states
Market state
Leaderboard
Turn results
Public events
Private company reports
```

This supports:

```text
Replay
Debugging
Async mode
Audit
Training debrief
```

---

# 18. Content Architecture

Game content should be data-driven.

## 18.1 Content Files

```text
content/
├── scenarios/
│   └── desert-frontier.json
├── basins/
│   └── desert-basin.json
├── events/
│   ├── market-events.json
│   ├── exploration-events.json
│   └── production-events.json
├── development-concepts/
│   └── mvp-development-concepts.json
├── balance/
│   └── mvp-balance.json
└── names/
    ├── prospect-names.json
    └── company-names.json
```

---

## 18.2 Scenario File Example

```json
{
  "id": "desert-frontier",
  "name": "Desert Frontier",
  "turns": 20,
  "turnLengthMonths": 6,
  "startingCash": 500000000,
  "startingOilPrice": 75,
  "blockCount": 20,
  "maxPlayers": 6,
  "primaryCommodity": "Oil"
}
```

---

## 18.3 Balance File Example

```json
{
  "actionSlotsPerTurn": 3,
  "costs": {
    "geologicalStudy": 5000000,
    "twoDSeismic": 15000000,
    "explorationWell": 40000000,
    "appraisalWell": 30000000,
    "smallDevelopment": 120000000,
    "standardDevelopment": 220000000,
    "largeDevelopment": 350000000,
    "optimizeField": 20000000
  },
  "economy": {
    "royaltyRate": 0.10,
    "maxDebt": 500000000
  }
}
```

---

# 19. AI Architecture

The AI system must be server-side.

## 19.1 AI Request Flow

```text
Player asks AI question
    ↓
Client sends request to server
    ↓
Server validates player access
    ↓
AI Context Builder loads allowed data
    ↓
Visibility filter removes hidden/private forbidden data
    ↓
Prompt is built
    ↓
AI provider is called
    ↓
Response is validated
    ↓
Optional proposal is created
    ↓
Response is streamed or returned to client
    ↓
Conversation is saved
```

---

## 19.2 AI Components

```text
AiAdvisorService
AiContextBuilder
AiVisibilityFilter
AiPromptLibrary
AiToolRegistry
AiToolExecutor
AiResponseValidator
AiConversationStore
AiTurnReportService
AiTeamSummaryService
```

---

## 19.3 AI Context Safety Tests

Create automated tests to ensure hidden fields are not exposed.

Example tests:

```text
AI context must not include HiddenGeology
AI context must not include true undiscovered volume
AI context must not include future events
AI context must not include competitor private cash/debt
AI context must only include company-owned seismic results
```

---

# 20. Security Architecture

## 20.1 Authentication

The system should support authenticated users.

MVP options:

```text
Simple email/password
External identity provider
Developer test login
```

Production should use secure authentication.

---

## 20.2 Authorization

Authorization must check:

```text
Player belongs to game session
Player belongs to company
Player has permission for action
Player can view requested asset
Player can access chat/proposals
Player can use AI for company
```

---

## 20.3 Anti-Cheat Rules

```text
Never send hidden geology to client.
Never send future events to client.
Never accept client-calculated results.
Validate every action server-side.
Keep competitor private data isolated.
Use server logs for turn resolution.
```

---

# 21. Caching Strategy

Caching is optional for MVP.

Possible cache targets:

```text
Public scenario data
Static content files
Public map data
Leaderboard
AI prompt templates
AI-safe context fragments
```

Recommended MVP:

```text
Start without Redis.
Add Redis when async leagues or higher concurrency require it.
```

---

# 22. Background Jobs

Some operations may run in background services.

## 22.1 Background Job Types

```text
Async league turn resolution
AI turn report generation
Notification delivery
Expired lobby cleanup
Old match archival
Analytics aggregation
```

## 22.2 MVP

For MVP, turn resolution can run inside the API process.

Later, move heavy jobs to hosted services or queue workers.

---

# 23. Deployment Architecture

## 23.1 MVP Deployment

Recommended MVP deployment:

```text
Frontend:
Static web hosting

Backend:
ASP.NET Core API container or app service

Database:
Managed PostgreSQL

Optional:
Object storage for logs/reports
```

---

## 23.2 Docker Compose for Development

Development environment:

```text
Web client
API server
PostgreSQL
Optional Redis
```

Example services:

```text
beep-oilgas-web
beep-oilgas-api
beep-oilgas-db
beep-oilgas-redis
```

---

## 23.3 Environment Configuration

Use environment variables for:

```text
Database connection string
AI provider API key
JWT/auth settings
Allowed origins
Logging level
Feature flags
```

Do not store secrets in source control.

---

# 24. Feature Flags

Feature flags help control rollout.

Recommended flags:

```text
AI_ENABLED
MULTIPLAYER_ENABLED
TEAM_MODE_ENABLED
ASYNC_LEAGUE_ENABLED
PLAYER_TRADING_ENABLED
ADVANCED_ABANDONMENT_ENABLED
THREE_D_SEISMIC_ENABLED
```

---

# 25. Logging and Telemetry

The system should log important events.

## 25.1 Logs

Log:

```text
User login
Game created
Player joined
Action submitted
Turn committed
Turn resolution started
Turn resolution completed
AI request
AI proposal created
Validation errors
Simulation errors
```

---

## 25.2 Game Analytics

Track:

```text
Most used actions
Average cash by turn
Dry-hole rate
Discovery rate
Development choices
Bankruptcy/distress rate
AI usage
Turn duration
Player drop-off
Abandonment completion rate
```

These metrics are important for balancing.

---

# 26. Testing Strategy

Testing is critical because the simulation has many rules.

## 26.1 Unit Tests

Test:

```text
Exploration chance calculations
Drilling result boundaries
Production formulas
Decline formulas
Revenue calculations
Debt interest
Abandonment penalties
Company valuation
Action validation
```

---

## 26.2 Integration Tests

Test:

```text
Create game session
Join match
Submit actions
Commit turn
Resolve turn
Generate results
Update leaderboard
AI context filtering
Proposal workflow
```

---

## 26.3 Simulation Balance Tests

Run many simulated matches automatically.

Track:

```text
Average number of discoveries
Average cash at end
Average debt
Average production
Dry-hole frequency
How often players reach late-life
How often abandonment matters
```

---

## 26.4 AI Safety Tests

Test that AI context never includes hidden data.

Example:

```csharp
[Fact]
public void AiContext_Should_Not_Include_HiddenGeology()
{
    // Arrange game with hidden geology
    // Build AI context for company
    // Assert hidden properties are not present
}
```

---

# 27. MVP Technical Scope

## 27.1 MVP Client

```text
Vite TypeScript app
Babylon.js map
React UI overlay
Main game screen
Block panel
Discovery panel
Field panel
Company dashboard
Action queue
Turn results
Leaderboard
AI Command Center panel
Basic chat
```

---

## 27.2 MVP Backend

```text
ASP.NET Core API
SignalR GameHub
PostgreSQL database
Game session creation
Player/company setup
Map/scenario loading
Action submission
Turn resolution
Company valuation
Leaderboard
AI advisor endpoint
Basic chat
```

---

## 27.3 MVP Simulation

```text
License auction
Geological study
2D seismic
Exploration drilling
Discovery/dry hole
Appraisal
Development approval
Construction
Production
Decline
OPEX/revenue
Debt
Hedging
Abandonment
Final score
```

---

## 27.4 MVP AI

```text
Strategy Advisor
Geologist
CFO
HSE Advisor
AI-safe context builder
Text chat
Turn summary
Draft proposal creation
```

---

# 28. Technical Implementation Phases

## Phase 0 — Foundation

```text
Repository setup
Solution setup
Client setup
Database setup
Basic CI
Shared coding conventions
```

---

## Phase 1 — Core Simulation Prototype

```text
Domain models
Scenario loading
Turn engine
Action validation
Exploration resolution
Economy calculation
Production calculation
Console or API-based simulation tests
```

Goal:

```text
Resolve a full 20-turn test match without UI.
```

---

## Phase 2 — Web Client Prototype

```text
Babylon.js map
React layout
Block selection
Action queue
Commit turn
Turn results display
Company dashboard
```

Goal:

```text
Play a solo browser match.
```

---

## Phase 3 — Multiplayer MVP

```text
Lobby
GameHub
Company assignment
Action submission per player
Turn commit
Realtime results
Leaderboard
Basic chat
```

Goal:

```text
2–6 players can complete a live match.
```

---

## Phase 4 — AI Command Center MVP

```text
AI context builder
Strategy Advisor
Geologist
CFO
HSE Advisor
AI chat panel
Turn summary
Draft proposal creation
```

Goal:

```text
Players can ask AI for game-aware advice without hidden data leakage.
```

---

## Phase 5 — Team Collaboration

```text
Company team mode
Roles
Proposal board
Voting
CEO approval
Team chat summaries
```

Goal:

```text
Multiple players manage one company together.
```

---

## Phase 6 — Content and Balancing

```text
Scenario tuning
Event cards
Balance testing
Tutorial
Polished UI
Charts
Reports
```

Goal:

```text
MVP is fun, understandable, and balanced enough for external testing.
```

---

# 29. Recommended Namespace Strategy

Use clear namespaces.

```csharp
Beep.OilGasSim.Domain
Beep.OilGasSim.Application
Beep.OilGasSim.Simulation
Beep.OilGasSim.AI
Beep.OilGasSim.Infrastructure
Beep.OilGasSim.Api
```

Example:

```csharp
namespace Beep.OilGasSim.Simulation.Exploration;

public sealed class ExplorationResolver : IExplorationResolver
{
}
```

---

# 30. Recommended Coding Principles

```text
Keep simulation deterministic.
Keep hidden data server-side.
Keep AI context filtered.
Keep domain logic testable.
Avoid putting business rules in controllers.
Avoid putting simulation logic in UI.
Use clear DTOs for client communication.
Use content files for balance values.
Version balance and scenario data.
Log turn resolution results.
```

---

# 31. Design Risks

## 31.1 Too Much Built at Once

Problem:

```text
The project includes game rendering, multiplayer, AI, and simulation.
```

Solution:

```text
Build simulation first.
Build solo UI second.
Build multiplayer third.
Build AI fourth.
```

---

## 31.2 Hidden Data Leakage

Problem:

```text
Client or AI may accidentally receive hidden geology.
```

Solution:

```text
Strict DTO separation.
AI-safe context builder.
Automated tests.
Never reuse database entities directly as API responses.
```

---

## 31.3 Simulation Becomes Hard to Balance

Problem:

```text
Many formulas interact.
```

Solution:

```text
Use data-driven balance files.
Create automated simulation runs.
Log metrics.
Keep MVP formulas simple.
```

---

## 31.4 Multiplayer Race Conditions

Problem:

```text
Players submit actions while turn is resolving.
```

Solution:

```text
Use game session state transitions.
Lock turn during resolution.
Validate turn state before accepting actions.
Use transactions.
```

---

## 31.5 AI Cost or Latency

Problem:

```text
AI responses may be expensive or slow.
```

Solution:

```text
Limit context size.
Cache static prompts.
Stream responses.
Limit questions per turn.
Use summaries.
```

---

# 32. Open Technical Questions

1. Should the first prototype use PostgreSQL immediately or in-memory storage first?
2. Should the client use React or plain TypeScript UI for the first prototype?
3. Should turn resolution be synchronous in API or background job from the beginning?
4. Should the game support guest players in MVP?
5. Should shared DTOs be generated from C# contracts or manually maintained in TypeScript?
6. Should scenario content be stored in JSON files or database tables?
7. Should AI conversations be saved permanently or only per match?
8. Should replay support be designed from MVP?
9. Should Redis be introduced early for SignalR scale-out?
10. Should game state snapshots be full JSON snapshots or normalized turn result records?

---

# 33. Recommended MVP Technical Decision

For MVP, use this architecture:

```text
Client:
- TypeScript
- Vite
- Babylon.js
- React overlay UI
- SignalR client

Backend:
- ASP.NET Core Web API
- SignalR
- Entity Framework Core
- PostgreSQL

Simulation:
- Server-authoritative
- Deterministic turn engine
- Data-driven balance values
- Automated simulation tests

AI:
- Server-side AI Advisor Service
- AI-safe context builder
- Strategy Advisor, Geologist, CFO, HSE
- No hidden data access

Deployment:
- Static frontend hosting
- ASP.NET Core backend
- PostgreSQL database
- Docker Compose for local development

MVP Build Order:
1. Simulation engine
2. Solo web client
3. Multiplayer loop
4. AI Command Center
5. Team collaboration
6. Balancing and polish
```

This architecture gives Beep Oil and Gas Sim a strong technical foundation while keeping the first implementation realistic and achievable.
