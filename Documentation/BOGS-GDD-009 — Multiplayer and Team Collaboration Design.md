# Beep Oil and Gas Sim

## Multiplayer and Team Collaboration Design

**Document ID:** BOGS-GDD-009
**Version:** 0.1
**Status:** Draft
**Parent Document:** BOGS-GDD-001 — Master Game Design Document
**Related Documents:**

* BOGS-GDD-002 — Gameplay Systems Design
* BOGS-GDD-004 — Economy, Finance, and Market Design
* BOGS-GDD-008 — AI Command Center Design

**Project Name:** Beep Oil and Gas Sim
**Short Name:** Beep O&G Sim

---

# 1. Purpose

This document defines the multiplayer and team collaboration systems for Beep Oil and Gas Sim.

The game should support competitive multiplayer where each player controls an oil and gas company. It should also support team-based company management, where multiple players cooperate inside the same company and compete against other companies.

The multiplayer system should make the game feel competitive, social, strategic, and fair.

The team collaboration system should make players feel like they are working inside a real oil and gas company, with roles, proposals, approvals, team chat, AI summaries, and shared decision-making.

---

# 2. Multiplayer Vision

Beep Oil and Gas Sim should support multiple multiplayer styles:

```text
Live competitive matches
Async league matches
Team company matches
Private friend sessions
Training / classroom sessions
```

The ideal multiplayer experience is:

> “Our company is competing against other companies in the same basin. We must coordinate as a team, make better decisions than competitors, and win through exploration, development, production, financial discipline, and responsible abandonment.”

---

# 3. Design Goals

## 3.1 Competitive Pressure

Players should feel pressure from competitors.

Competition should come from:

```text
License auctions
Nearby discoveries
Limited rigs
Shared infrastructure
Commodity market timing
Asset sales
Leaderboard movement
Public reputation
Final company value
```

---

## 3.2 Fair Play

The server must be authoritative.

The client should never decide:

```text
Hidden geology
Drilling results
Discovery size
Production result
Market events
Final scoring
Competitor private data
```

The server validates all actions.

---

## 3.3 Flexible Session Types

The multiplayer design should support both short sessions and long-running games.

Examples:

```text
Live Match:
2–6 players, 45–90 minutes

Async League:
10–50 players, one turn every 12 or 24 hours

Team Training Match:
Multiple players per company, facilitator-controlled scenario
```

---

## 3.4 Team-Based Decision-Making

In team mode, players should not just chat. They should collaborate through structured tools:

```text
Team roles
Action proposals
Proposal comments
Voting
CEO approval
AI summaries
Turn plan board
```

---

## 3.5 AI-Supported Collaboration

The AI Command Center should support multiplayer by:

```text
Summarizing team chat
Creating proposal drafts
Explaining risks
Preparing turn plans
Highlighting disagreement
Generating end-turn reports
```

---

# 4. Multiplayer Modes

## 4.1 Solo With Multiplayer Architecture

Even solo mode should use the same server-side simulation architecture.

This allows:

```text
Easier testing
Future AI competitors
Consistent simulation logic
Replay support
Scenario validation
```

---

## 4.2 Live Multiplayer Match

A real-time session where players submit turn actions within a time limit.

Recommended format:

```text
Players: 2–6
Turns: 12–30
Turn timer: 2–5 minutes
Session length: 45–90 minutes
```

Live matches are best for fast competitive gameplay.

---

## 4.3 Async League Match

A long-running match where players submit actions before a deadline.

Recommended format:

```text
Players: 10–50
Turn deadline: every 12 or 24 hours
Server resolves all actions at deadline
Leaderboard updates after resolution
```

Async mode is ideal for larger groups and long-term strategy.

---

## 4.4 Team Company Mode

Multiple players cooperate inside one company.

Example:

```text
Company: Beep Energy
Players:
- CEO
- Exploration Manager
- Drilling Manager
- Production Manager
- CFO
- HSE Manager
```

Each role can propose actions and discuss strategy. The CEO or authorized player approves final actions.

---

## 4.5 Training / Classroom Mode

A facilitator can create a session for learning or corporate training.

Possible facilitator features:

```text
Pause match
Advance turn manually
Inject events
View all companies
Review team decisions
Export reports
Run debrief session
```

This can be Phase 2 or Phase 3.

---

# 5. Match Structure

A multiplayer match contains:

```text
Game session
Map
Scenario
Companies
Players
Teams
Turns
Actions
Chat messages
Proposals
AI conversations
Turn results
Leaderboard
Final score
```

---

## 5.1 Match Setup

Match creator chooses:

```text
Scenario
Number of companies
Players per company
Turn count
Turn timer
AI advisor enabled/disabled
Team mode enabled/disabled
Private/public match
```

MVP setup:

```text
Scenario: Desert Frontier
Players: 2–6
Players per company: 1
Turns: 20
Turn timer: optional
AI: enabled
Team chat: enabled
```

---

## 5.2 Match States

```csharp
public enum GameSessionState
{
    Lobby,
    Preparing,
    Planning,
    Committing,
    Resolving,
    Results,
    Completed,
    Cancelled
}
```

---

# 6. Lobby System

The lobby is where players join before the match starts.

## 6.1 Lobby Features

```text
Create match
Join match
Select company
Invite players
Assign teams
Select roles
Ready button
Chat
Start match
```

---

## 6.2 Lobby Player State

```csharp
public sealed class LobbyPlayer
{
    public Guid PlayerId { get; set; }
    public Guid GameSessionId { get; set; }

    public Guid? CompanyId { get; set; }
    public PlayerCompanyRole Role { get; set; }

    public bool IsReady { get; set; }
    public bool IsHost { get; set; }
}
```

---

## 6.3 Lobby Start Rule

Recommended MVP rule:

```text
The host can start when at least 2 companies have ready players.
```

For solo testing:

```text
The host can start with 1 company and AI or dummy competitors disabled.
```

---

# 7. Company and Player Structure

## 7.1 Company

A company is the main competitive entity.

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

## 7.2 Company Player

```csharp
public sealed class CompanyPlayer
{
    public Guid PlayerId { get; set; }
    public Guid CompanyId { get; set; }

    public PlayerCompanyRole Role { get; set; }
    public CompanyPermissionSet Permissions { get; set; } = new();

    public bool IsOnline { get; set; }
    public DateTime LastSeenUtc { get; set; }
}
```

---

# 8. Team Roles

Team roles create structure for company collaboration.

## 8.1 Recommended Roles

```csharp
public enum PlayerCompanyRole
{
    CEO,
    ExplorationManager,
    DrillingManager,
    ProductionManager,
    FinanceManager,
    HseManager,
    Observer
}
```

---

## 8.2 Role Responsibilities

| Role                | Main Responsibility                            |
| ------------------- | ---------------------------------------------- |
| CEO                 | Final approval and turn submission             |
| Exploration Manager | Blocks, prospects, seismic, drilling targets   |
| Drilling Manager    | Well planning, drilling risk, rig decisions    |
| Production Manager  | Producing fields, optimization, maintenance    |
| Finance Manager     | Cash, debt, hedging, development affordability |
| HSE Manager         | Safety, environment, abandonment, reputation   |
| Observer            | View-only access                               |

---

## 8.3 MVP Role Recommendation

For MVP, roles can be simple.

```text
CEO
Member
Observer
```

Expanded roles can come after the basic team flow works.

---

# 9. Permission System

Permissions define what each player can do.

## 9.1 Permission Types

```csharp
public sealed class CompanyPermissionSet
{
    public bool CanCreateProposal { get; set; }
    public bool CanCommentOnProposal { get; set; }
    public bool CanVoteOnProposal { get; set; }
    public bool CanApproveProposal { get; set; }
    public bool CanCommitTurn { get; set; }

    public bool CanUseAiAdvisor { get; set; }
    public bool CanManageFinance { get; set; }
    public bool CanManageExploration { get; set; }
    public bool CanManageProduction { get; set; }
    public bool CanManageAbandonment { get; set; }
}
```

---

## 9.2 Recommended Defaults

| Role     | Create | Vote |  Approve | Commit |       AI |
| -------- | -----: | ---: | -------: | -----: | -------: |
| CEO      |    Yes |  Yes |      Yes |    Yes |      Yes |
| Manager  |    Yes |  Yes | Optional |     No |      Yes |
| Member   |    Yes |  Yes |       No |     No |      Yes |
| Observer |     No |   No |       No |     No | Optional |

---

# 10. Turn Collaboration Flow

In team mode, a company turn should follow this process:

```text
1. Team reviews company status
2. Players discuss in team chat
3. Players ask AI advisors
4. Players create action proposals
5. Team comments and votes
6. CEO approves selected proposals
7. Approved proposals become planned actions
8. CEO commits final turn plan
9. Server resolves turn
10. AI generates turn report
```

---

# 11. Action Proposal Board

The proposal board is the core collaboration feature.

## 11.1 Proposal Board Purpose

The board helps teams organize decisions.

Instead of losing ideas in chat, every important decision becomes a proposal.

Examples:

```text
Drill Falcon-1
Acquire 2D seismic on Block D-04
Approve Standard Development for Falcon Field
Hedge 50% of next turn production
Abandon North Field
Take $100M debt
```

---

## 11.2 Proposal Model

```csharp
public sealed class ActionProposal
{
    public Guid Id { get; set; }
    public Guid GameSessionId { get; set; }
    public Guid CompanyId { get; set; }

    public Guid CreatedByPlayerId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";

    public TurnActionType ProposedActionType { get; set; }
    public Guid? TargetAssetId { get; set; }
    public Guid? TargetBlockId { get; set; }

    public decimal EstimatedCost { get; set; }
    public string ExpectedBenefit { get; set; } = "";
    public string MainRisk { get; set; } = "";

    public ProposalStatus Status { get; set; }

    public List<ProposalComment> Comments { get; set; } = new();
    public List<ProposalVote> Votes { get; set; } = new();
}
```

---

## 11.3 Proposal Status

```csharp
public enum ProposalStatus
{
    Draft,
    Proposed,
    Approved,
    Rejected,
    Committed,
    Cancelled
}
```

---

## 11.4 Proposal Vote

```csharp
public sealed class ProposalVote
{
    public Guid ProposalId { get; set; }
    public Guid PlayerId { get; set; }

    public ProposalVoteType VoteType { get; set; }
    public string? Reason { get; set; }

    public DateTime VotedAtUtc { get; set; }
}
```

```csharp
public enum ProposalVoteType
{
    Support,
    Oppose,
    Neutral
}
```

---

# 12. Proposal UI

Each proposal card should show:

```text
Title
Action type
Target asset/block
Estimated cost
Expected benefit
Main risk
Created by
AI recommendation, if available
Votes
Comments
Status
Approve button
Reject button
Commit status
```

Example:

```text
Proposal: Drill Falcon-1

Target:
Block D-08 / Falcon Prospect

Cost:
$40M

Expected Benefit:
Potential commercial discovery of 80–160 MMboe

Main Risk:
Trap closure uncertainty

Votes:
3 Support, 1 Oppose

AI Recommendation:
Balanced to aggressive

Status:
Awaiting CEO approval
```

---

# 13. Turn Action Plan

Approved proposals become the company’s turn action plan.

## 13.1 Turn Action Plan Model

```csharp
public sealed class CompanyTurnPlan
{
    public Guid Id { get; set; }
    public Guid GameSessionId { get; set; }
    public Guid CompanyId { get; set; }
    public int TurnNumber { get; set; }

    public List<TurnAction> Actions { get; set; } = new();

    public bool IsCommitted { get; set; }
    public Guid? CommittedByPlayerId { get; set; }
    public DateTime? CommittedAtUtc { get; set; }
}
```

---

## 13.2 Action Slot Validation

The turn plan must respect action slots.

MVP rule:

```text
Each company has 3 action slots per turn.
```

Example:

```text
Approved proposals:
1. Drill Falcon-1
2. Acquire 2D seismic on Block D-04
3. Hedge 50% production
```

This is valid.

If a fourth action is approved, the CEO must choose which 3 to commit.

---

# 14. Team Chat

Team chat allows private communication inside a company.

## 14.1 Chat Channels

Recommended channels:

```text
Company Team Chat
Match Public Chat
System Notifications
AI Advisor Chat
Proposal Comments
```

MVP should include:

```text
Company Team Chat
Match Public Chat
AI Advisor Chat
```

---

## 14.2 Team Message Model

```csharp
public sealed class TeamMessage
{
    public Guid Id { get; set; }
    public Guid GameSessionId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid SenderPlayerId { get; set; }

    public string Message { get; set; } = "";
    public DateTime SentAtUtc { get; set; }

    public TeamMessageType MessageType { get; set; }
}
```

```csharp
public enum TeamMessageType
{
    User,
    System,
    AiSummary,
    AiAdvisor,
    ProposalUpdate
}
```

---

# 15. Public Match Chat

Public chat allows all companies to communicate.

It can support:

```text
Friendly competition
Auction banter
Training discussions
Host announcements
```

Public chat should not reveal private company data automatically.

---

# 16. SignalR Real-Time Design

SignalR should handle live updates.

## 16.1 Recommended Hubs

```text
GameHub
LobbyHub
TeamChatHub
ProposalHub
AiAdvisorHub
NotificationHub
```

For MVP, these can be combined into fewer hubs if simpler.

---

## 16.2 SignalR Groups

Use groups to isolate messages.

Recommended group names:

```text
game:{gameSessionId}
game:{gameSessionId}:company:{companyId}
game:{gameSessionId}:company:{companyId}:chat
game:{gameSessionId}:company:{companyId}:ai
game:{gameSessionId}:company:{companyId}:proposals
game:{gameSessionId}:lobby
```

---

## 16.3 Real-Time Events

Server sends events such as:

```text
PlayerJoinedLobby
PlayerReadyChanged
MatchStarted
TurnStarted
TurnTimerUpdated
ProposalCreated
ProposalVoted
ProposalApproved
TurnPlanCommitted
TurnResolved
LeaderboardUpdated
ChatMessageReceived
AiResponseReceived
```

---

# 17. GameHub Skeleton

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BeepOilAndGasSim.Api.Hubs;

[Authorize]
public sealed class GameHub : Hub
{
    public async Task JoinGame(Guid gameSessionId)
    {
        // Validate player belongs to this game.
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            $"game:{gameSessionId}");
    }

    public async Task JoinCompany(Guid gameSessionId, Guid companyId)
    {
        // Validate player belongs to this company.
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            $"game:{gameSessionId}:company:{companyId}");
    }
}
```

---

# 18. TeamChatHub Skeleton

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BeepOilAndGasSim.Api.Hubs;

[Authorize]
public sealed class TeamChatHub : Hub
{
    private readonly ITeamChatService _teamChatService;

    public TeamChatHub(ITeamChatService teamChatService)
    {
        _teamChatService = teamChatService;
    }

    public async Task SendTeamMessage(Guid gameSessionId, Guid companyId, string message)
    {
        // Service validates membership and saves message.
        var savedMessage = await _teamChatService.CreateTeamMessageAsync(
            gameSessionId,
            companyId,
            message,
            Context.UserIdentifier,
            Context.ConnectionAborted);

        await Clients
            .Group($"game:{gameSessionId}:company:{companyId}:chat")
            .SendAsync("TeamMessageReceived", savedMessage);
    }
}
```

---

# 19. ProposalHub Skeleton

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BeepOilAndGasSim.Api.Hubs;

[Authorize]
public sealed class ProposalHub : Hub
{
    private readonly IActionProposalService _proposalService;

    public ProposalHub(IActionProposalService proposalService)
    {
        _proposalService = proposalService;
    }

    public async Task CreateProposal(CreateProposalRequest request)
    {
        var proposal = await _proposalService.CreateProposalAsync(
            request,
            Context.UserIdentifier,
            Context.ConnectionAborted);

        await Clients
            .Group($"game:{request.GameSessionId}:company:{request.CompanyId}:proposals")
            .SendAsync("ProposalCreated", proposal);
    }

    public async Task VoteProposal(Guid proposalId, ProposalVoteType voteType, string? reason)
    {
        var result = await _proposalService.VoteAsync(
            proposalId,
            voteType,
            reason,
            Context.UserIdentifier,
            Context.ConnectionAborted);

        await Clients
            .Group($"game:{result.GameSessionId}:company:{result.CompanyId}:proposals")
            .SendAsync("ProposalUpdated", result);
    }
}
```

---

# 20. Turn Timer

Live matches need a timer.

## 20.1 Timer Behavior

```text
Turn starts
Timer begins
Players plan and submit actions
Warnings appear near end
Uncommitted teams auto-submit current approved actions or no actions
Turn resolves
```

---

## 20.2 Recommended Timer Values

```text
Fast Match: 2 minutes per turn
Standard Match: 5 minutes per turn
Training Match: manual advance
Async Match: 12 or 24 hours per turn
```

MVP recommendation:

```text
No strict timer for first prototype.
Add optional 5-minute timer after core turn flow works.
```

---

# 21. Turn Commit Rules

## 21.1 Solo Company

If one player controls the company:

```text
Player selects actions
Player clicks Commit Turn
Actions are locked
```

---

## 21.2 Team Company

If multiple players control the company:

```text
Players create proposals
CEO approves proposals
CEO commits final action plan
```

Optional rule:

```text
If CEO is offline, a delegated manager may commit.
```

---

## 21.3 Auto-Commit

For timed live matches:

```text
If timer expires:
- Use current committed plan if available
- Otherwise use approved proposals up to action slot limit
- Otherwise submit no actions
```

---

# 22. Visibility Rules

Multiplayer requires strict information boundaries.

## 22.1 Public to All Players

```text
Block ownership
Auction results
Major discoveries
Producing fields
Public company rank
Public reputation
Public production ranking
Market price
Public events
```

---

## 22.2 Private to Company

```text
Cash
Debt
Detailed asset economics
Owned seismic interpretation
Internal AI advice
Team chat
Action proposals
Hedging decisions before resolution
Pending turn plan
```

---

## 22.3 Server-Only Hidden Data

```text
Hidden geology
True undiscovered volumes
Future event schedule
Random seeds
Unresolved competitor actions
Private competitor finances
```

---

# 23. Anti-Cheat and Validation

The server must validate every action.

## 23.1 Validation Examples

```text
Does the player belong to the company?
Can this role submit this action?
Does the company own the target block?
Is the asset in the correct stage?
Does the company have enough action slots?
Can the company afford the action or borrow?
Is the proposal valid?
Has the turn already been committed?
```

---

## 23.2 Client Trust Rule

Important rule:

```text
Never trust the browser client for game results or hidden information.
```

The client can request actions. The server resolves them.

---

# 24. Reconnection and Offline Handling

Multiplayer web games need reconnection support.

## 24.1 Reconnection Behavior

When a player reconnects:

```text
Authenticate player
Restore game session
Rejoin SignalR groups
Load current turn state
Load team chat
Load proposals
Load pending action plan
Load latest results
```

---

## 24.2 Offline Player in Team Mode

If a team member goes offline:

```text
Other team members can continue
CEO can still commit
Offline player can catch up later
```

If CEO goes offline:

```text
Use delegated commit permission
or
Auto-commit approved actions at timer expiry
```

---

# 25. Notifications

Notifications keep players aware of important changes.

## 25.1 Notification Types

```text
Proposal created
Proposal approved
Vote received
Turn committed
Turn resolved
Auction won/lost
Discovery made
Field started production
Cash warning
Abandonment warning
AI report ready
```

---

## 25.2 Notification Priority

```text
Info
Warning
Critical
Success
```

Example:

```text
Critical:
Cash will fall below zero if this development is approved.

Success:
Falcon-1 discovered a commercial oil field.
```

---

# 26. Leaderboard

The leaderboard drives competition.

## 26.1 MVP Leaderboard

```text
Rank
Company Name
Company Value
Production
Reserves
Reputation
```

---

## 26.2 Hidden or Partial Financials

The game may hide some details from competitors.

Example:

```text
Visible:
Company rank, production, reserves, reputation

Hidden:
Cash, debt, detailed asset economics
```

This allows strategic uncertainty.

---

# 27. Auction Multiplayer Design

License auctions are one of the main competitive systems.

## 27.1 Auction Flow

```text
Blocks available
Players submit bids during planning
Bids are hidden until resolution
Server resolves auction
Highest valid bid wins
Results announced publicly
```

---

## 27.2 Auction Result Visibility

Public result:

```text
Beep Energy won Block D-08 for $32M.
```

Private result:

```text
You lost Block D-08.
Your bid: $25M.
Winning bid: $32M.
```

---

## 27.3 Tie-Breaking

MVP rule:

```text
Highest bid wins.
Tie is resolved randomly.
```

Future options:

```text
Higher reputation wins
Government relationship wins
Earlier bid wins
Host-selected fiscal preference
```

---

# 28. Shared Infrastructure

Shared infrastructure can be a future multiplayer feature.

Examples:

```text
Pipeline capacity
Export terminal
Processing plant
Gas plant
LNG terminal
Port
Power supply
```

Players may compete for capacity or negotiate access.

This should be Phase 2 or later.

---

# 29. Player-to-Player Asset Trading

Asset trading can increase multiplayer depth.

Possible trades:

```text
Sell license
Sell discovery
Sell producing field
Farm-in
Farm-out
Share development cost
Buy infrastructure access
```

MVP recommendation:

```text
Use market/NPC asset sales first.
Add player-to-player trading later.
```

---

# 30. AI in Multiplayer

The AI Command Center supports multiplayer but must follow fairness rules.

## 30.1 AI Access

Each company can ask AI using only company-known data.

The AI must not access:

```text
Competitor private finances
Competitor private actions
Hidden geology
Future events
```

---

## 30.2 Shared AI Chat

In team mode, AI advisor responses should normally be visible to the company team.

Possible setting:

```text
Private AI questions: disabled in team mode
Company AI chat: enabled
```

This keeps teamwork transparent.

---

## 30.3 AI Team Summary

The AI can summarize:

```text
Team chat
Proposal debate
Current turn plan
Risks before commit
End-turn results
```

Example:

```text
The team agrees that Falcon Field should be appraised.
The main disagreement is whether to also bid for Block D-12.
The CFO is concerned that doing both may require debt.
```

---

# 31. Host and Facilitator Controls

For private or training matches, a host may need extra controls.

## 31.1 Host Controls

```text
Start match
Pause match
Resume match
Kick player
Assign role
Advance turn
Change timer
End match
```

## 31.2 Facilitator Controls

Future training mode:

```text
View all companies
Inject event
Reveal educational explanation
Export team decisions
Generate debrief report
```

---

# 32. Database Entities

Recommended multiplayer-related entities:

```text
GameSession
Scenario
Company
CompanyPlayer
PlayerProfile
Turn
TurnAction
CompanyTurnPlan
ActionProposal
ProposalVote
ProposalComment
TeamMessage
PublicMessage
AiConversation
TurnResult
LeaderboardSnapshot
Notification
```

---

# 33. Client Multiplayer Architecture

Recommended client structure:

```text
client/
└── src/
    ├── multiplayer/
    │   ├── lobby/
    │   │   ├── LobbyPage.tsx
    │   │   ├── CompanySelector.tsx
    │   │   ├── PlayerList.tsx
    │   │   └── ReadyButton.tsx
    │   │
    │   ├── team/
    │   │   ├── TeamChatPanel.tsx
    │   │   ├── TeamRoleBadge.tsx
    │   │   └── TeamMemberList.tsx
    │   │
    │   ├── proposals/
    │   │   ├── ActionBoardPanel.tsx
    │   │   ├── ProposalCard.tsx
    │   │   ├── ProposalEditor.tsx
    │   │   └── VoteButtons.tsx
    │   │
    │   ├── turn/
    │   │   ├── TurnTimer.tsx
    │   │   ├── TurnPlanPanel.tsx
    │   │   └── CommitTurnButton.tsx
    │   │
    │   └── net/
    │       ├── GameHubClient.ts
    │       ├── LobbyHubClient.ts
    │       ├── TeamChatHubClient.ts
    │       └── ProposalHubClient.ts
```

---

# 34. MVP Multiplayer Scope

## 34.1 MVP Includes

```text
Create match
Join match
Lobby
2–6 companies
One player per company
Basic public chat
Basic company chat
Submit actions
Server turn resolution
Leaderboard
Turn results
AI advisor per company
```

---

## 34.2 Team MVP Includes

If team mode is included early:

```text
Multiple players per company
CEO/member roles
Team chat
Proposal board
Proposal voting
CEO approval
Commit turn plan
AI team summary
```

---

## 34.3 MVP Does Not Include

```text
Player-to-player asset trading
Shared infrastructure negotiation
Advanced role permissions
Training facilitator controls
Spectator replay
Tournament system
Ranked matchmaking
Voice chat
Complex alliances
```

---

# 35. Example Multiplayer Turn

## Planning Phase

```text
Turn 5 begins.
All companies receive new market outlook.
Beep Energy team discusses Falcon Discovery.
Exploration Manager proposes appraisal.
CFO proposes hedging 50% production.
CEO approves both.
```

---

## Commit Phase

```text
CEO commits:
1. Drill appraisal well on Falcon Discovery
2. Hedge 50% next turn production
3. Bid $20M on Block D-14
```

---

## Resolution Phase

```text
Server resolves all companies:
- Auctions
- Appraisal
- Hedging
- Production
- Revenue
- Events
```

---

## Results Phase

```text
Beep Energy wins Block D-14.
Falcon appraisal increases confidence.
Oil price drops, but hedge protects revenue.
Leaderboard updates.
AI generates turn report.
```

---

# 36. Example Team Proposal Flow

```text
1. Exploration Manager clicks Block D-08.
2. Creates proposal: Acquire 2D seismic.
3. AI Geologist adds recommendation.
4. CFO comments: "Affordable, low risk."
5. Team votes 4 support, 0 oppose.
6. CEO approves.
7. Proposal is added to turn plan.
8. CEO commits turn.
```

---

# 37. Design Risks

## 37.1 Multiplayer Waiting Time

Problem:

```text
Players may wait too long for others.
```

Solution:

```text
Use timers.
Allow auto-commit.
Use async mode.
Show ready status.
Let players continue analysis while waiting.
```

---

## 37.2 Team Mode Creates Confusion

Problem:

```text
Too many players may create too many proposals.
```

Solution:

```text
Use roles.
Use proposal statuses.
Limit final action slots.
Let CEO approve final plan.
Use AI summaries.
```

---

## 37.3 Cheating Through Client Data

Problem:

```text
Players may inspect browser data.
```

Solution:

```text
Never send hidden geology to client.
Server resolves all outcomes.
Only send player-visible data.
```

---

## 37.4 AI Reveals Too Much

Problem:

```text
AI may accidentally expose hidden data if context builder is wrong.
```

Solution:

```text
Centralized AI-safe context builder.
Strict data filters.
Logging and audits.
Automated tests for hidden fields.
```

---

## 37.5 Players Miss Turn Deadline

Problem:

```text
Async players may forget to submit.
```

Solution:

```text
Notifications.
Auto-submit saved plan.
Allow default strategy.
Allow delegated team commit.
```

---

# 38. Open Questions

1. Should MVP support multiplayer from day one, or start solo with server architecture?
2. Should team mode be in MVP or Phase 2?
3. Should live match timer be mandatory?
4. Should bids be hidden or visible during auctions?
5. Should company cash and debt be visible to competitors?
6. Should AI advisor chat be shared with team or private?
7. Should CEO be the only role that commits turns?
8. Should players be able to join mid-match?
9. Should there be spectators?
10. Should async mode be designed early or after live mode?

---

# 39. Recommended MVP Multiplayer Decision

For MVP, implement multiplayer in this order:

```text
Step 1:
Server-authoritative solo test match

Step 2:
2–6 player live multiplayer match

Step 3:
Basic company chat

Step 4:
Basic AI advisor per company

Step 5:
Team company mode with CEO/member roles

Step 6:
Proposal board and voting

Step 7:
Async league mode
```

Recommended MVP scope:

```text
Mode:
Live multiplayer

Players:
2–6 companies

Team:
One player per company first

Turns:
20

Actions:
3 action slots per turn

Communication:
Public chat and company chat

AI:
Company AI advisor

Turn Commit:
Each player commits their own company turn

Leaderboard:
Updated after every turn
```

Team collaboration should be added immediately after the basic multiplayer loop is stable.
