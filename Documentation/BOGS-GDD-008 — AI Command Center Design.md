# Beep Oil and Gas Sim

## AI Command Center Design

**Document ID:** BOGS-GDD-008
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

**Project Name:** Beep Oil and Gas Sim
**Short Name:** Beep O&G Sim
**Feature Name:** AI Command Center

---

# 1. Purpose

This document defines the AI Command Center system for Beep Oil and Gas Sim.

The AI Command Center is an in-game assistant and advisory system that helps players understand the game, analyze options, manage company strategy, collaborate with teammates, and create action proposals.

The AI should feel like a team of company advisors inside the player’s oil and gas company.

The AI should not simply answer generic questions. It should understand the current game state and provide useful, contextual advice.

The AI must never cheat. It must only use information the player or team is allowed to know.

---

# 2. AI Feature Vision

The AI Command Center should make the player feel like they are leading a professional energy company with access to internal experts.

The player can ask:

```text
Should we drill this block?
Should we buy seismic first?
Can we afford this development?
Which field should we optimize?
Should we abandon this asset?
What is our biggest risk this turn?
Summarize the team discussion.
Prepare a recommended turn plan.
```

The AI should respond with clear advice, risks, tradeoffs, and recommended actions.

The AI can propose actions, but the player must approve them.

---

# 3. Design Goals

## 3.1 Make Complex Systems Easier to Understand

Oil and gas concepts can be complex.

The AI should explain:

```text
Exploration risk
Geological uncertainty
Development tradeoffs
Production decline
Cash flow
Debt risk
Hedging
Abandonment liability
Final score impact
```

The AI should make the game more accessible without removing strategic depth.

---

## 3.2 Support Better Decisions

The AI should help players compare options.

Example:

```text
Option A: Drill now
Option B: Buy seismic first
Option C: save cash for development
```

The AI should explain when an option is risky, attractive, or financially dangerous.

---

## 3.3 Strengthen Team Gameplay

In team mode, the AI should help players collaborate.

The AI should:

```text
Summarize team chat
Convert discussion into action proposals
Identify disagreements
Prepare a turn plan
Explain risks to the CEO
Create meeting-style summaries
```

---

## 3.4 Keep the Game Fair

The AI must not reveal hidden information.

It must not use:

```text
Hidden geology
Future events
True undiscovered reserves
Competitor private actions
Competitor private financials
Random seeds
Server-only information
```

The AI can only use information that the player or team has legitimately discovered or is allowed to see.

---

## 3.5 Keep the Player in Control

The AI should advise, not play the game automatically.

The AI may:

```text
Recommend an action
Create a draft proposal
Explain tradeoffs
Rank options
Generate a turn summary
```

The AI may not:

```text
Submit final actions without approval
Bid without approval
Drill without approval
Take debt without approval
Sell assets without approval
Abandon fields without approval
```

---

# 4. AI Command Center Overview

The AI Command Center is a UI panel available during gameplay.

It includes:

```text
AI Advisor Chat
Team Chat Summary
Action Proposal Assistant
Turn Strategy Report
Asset Analysis
Advisor Role Selector
```

Recommended panel structure:

```text
Command Center
 ├── AI Advisor
 ├── Team Chat
 ├── Action Board
 ├── Turn Report
 └── Reports
```

---

# 5. AI Advisor Roles

The AI system should support multiple advisor personas.

Each advisor has a specific purpose.

## 5.1 Strategy Advisor

The Strategy Advisor gives overall company-level advice.

Responsibilities:

```text
Recommend turn strategy
Compare major opportunities
Identify biggest risks
Balance exploration, development, production, and finance
Suggest priorities
```

Example questions:

```text
What should we do this turn?
What is our biggest risk?
Are we ahead or behind competitors?
Should we focus on exploration or production?
```

---

## 5.2 AI Geologist

The AI Geologist focuses on exploration and subsurface risk.

Responsibilities:

```text
Explain block risk
Compare prospects
Interpret geological studies
Interpret seismic results
Explain dry holes
Recommend drill or data acquisition decisions
```

Example questions:

```text
Should we drill Falcon Prospect?
Which block has the best upside?
What did the dry hole teach us?
Is the main risk source, trap, or seal?
```

---

## 5.3 Reservoir Engineer

The Reservoir Engineer focuses on discoveries and producing fields.

Responsibilities:

```text
Explain reserves
Assess appraisal needs
Analyze production decline
Recommend optimization
Evaluate remaining potential
Identify late-life triggers
```

Example questions:

```text
Should we appraise this discovery?
Is Falcon Field declining too quickly?
Should we optimize this field?
How much production remains?
```

---

## 5.4 Drilling Engineer

The Drilling Engineer focuses on drilling cost, schedule, and operational risk.

Responsibilities:

```text
Explain well cost
Assess drilling risk
Recommend well budget class
Warn about rig cost inflation
Explain drilling delays
```

Example questions:

```text
Can we drill safely this turn?
Should we use a premium well program?
How does rig inflation affect us?
```

---

## 5.5 CFO Advisor

The CFO focuses on money, debt, valuation, and cash flow.

Responsibilities:

```text
Analyze affordability
Warn about low cash
Recommend debt or repayment
Evaluate development payback
Explain hedging
Estimate final score impact
```

Example questions:

```text
Can we afford this development?
Should we take debt?
Should we hedge production?
Which asset is hurting cash flow?
```

---

## 5.6 HSE Advisor

The HSE Advisor focuses on safety, environmental risk, and abandonment.

Responsibilities:

```text
Warn about late-life risk
Explain abandonment liability
Recommend abandonment timing
Assess environmental events
Protect reputation
```

Example questions:

```text
Should we abandon Falcon Field?
What is our biggest environmental risk?
How much abandonment liability do we have?
What happens if we delay closure?
```

---

## 5.7 Market Analyst

The Market Analyst focuses on commodity prices and market timing.

Responsibilities:

```text
Explain oil price trends
Recommend hedging
Warn about price exposure
Assess timing for development
Explain market events
```

Example questions:

```text
Should we hedge next turn?
Is now a good time to approve development?
How exposed are we to an oil price crash?
```

---

# 6. AI Modes

The AI should support several interaction modes.

## 6.1 Free Chat Mode

The player types a question.

Example:

```text
Should we drill Block D-08 this turn?
```

The AI responds with analysis.

---

## 6.2 Asset Context Mode

The player clicks an asset and asks the AI about it.

Examples:

```text
Ask AI about this block
Ask AI about this discovery
Ask AI about this field
Ask AI about abandonment risk
```

The AI receives context for the selected asset.

---

## 6.3 Turn Planning Mode

The AI reviews the current company position and recommends a turn plan.

Example output:

```text
Recommended Turn Plan:
1. Appraise Falcon Discovery
2. Hedge 50% of production
3. Avoid new license bids this turn
```

---

## 6.4 Proposal Creation Mode

The AI creates a draft action proposal for team approval.

Example:

```text
Proposal:
Acquire 2D seismic on Block D-04.

Reason:
Block D-04 has moderate source potential but low trap confidence.
Seismic should improve the drill/no-drill decision before committing $40M.
```

---

## 6.5 Turn Summary Mode

At the end of each turn, the AI generates a summary.

Example:

```text
Turn 6 Summary:
Your company discovered Falcon Field, lost the auction for Block D-12, and improved geological confidence on Block D-04.

Recommended next turn:
Appraise Falcon Field before development.
```

---

# 7. AI User Interface

## 7.1 Command Center Layout

Recommended UI layout:

```text
┌─────────────────────────────────────────────┐
│ Command Center                              │
├─────────────────────────────────────────────┤
│ Tabs: AI Advisor | Team Chat | Actions      │
├─────────────────────────────────────────────┤
│ Advisor: [Strategy ▼]                       │
│ Context: Falcon Field                       │
│                                             │
│ Player: Should we develop Falcon now?       │
│ AI: Development is possible, but appraisal  │
│     would reduce reserve uncertainty.       │
│                                             │
│ [ Ask a question...                       ] │
│ [Send] [Create Proposal] [Ask CFO]          │
└─────────────────────────────────────────────┘
```

---

## 7.2 Advisor Selector

The player can select:

```text
Strategy
Geologist
Reservoir Engineer
Drilling Engineer
CFO
HSE
Market Analyst
```

The selected advisor changes the response style and focus.

---

## 7.3 Context Chip

The UI should show the selected context.

Examples:

```text
Context: Company
Context: Block D-08
Context: Falcon Prospect
Context: Falcon Field
Context: Turn 7
Context: Proposal #14
```

---

## 7.4 Quick Prompt Buttons

The UI should include quick actions.

Examples:

```text
What should we do next?
Explain this risk
Compare options
Create proposal
Summarize team chat
Estimate financial impact
Ask another advisor
```

---

# 8. AI Response Format

AI responses should be structured and concise.

Recommended format:

```text
Situation
Risks
Recommendation
Suggested Action
```

Example:

```text
Situation:
Block D-08 has a 34% estimated chance of success with medium confidence.

Risks:
The main risk is trap closure. A dry hole would cost $40M.

Recommendation:
Drill only if you want high-risk growth. If cash protection is more important, wait.

Suggested Action:
Acquire 3D seismic when available, or drill Falcon-1 if you accept the risk.
```

---

# 9. AI Advice Categories

The AI should classify advice into categories.

Recommended categories:

```text
Safe
Balanced
Aggressive
High Risk
Emergency
Not Recommended
```

Example:

```text
Recommendation Type: Balanced
```

This helps players understand the tone of the advice quickly.

---

# 10. AI Action Proposal System

The AI can create a draft proposal.

## 10.1 Proposal Structure

```csharp
public sealed class ActionProposal
{
    public Guid Id { get; set; }
    public Guid GameSessionId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid CreatedByPlayerId { get; set; }

    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";

    public string ProposedActionType { get; set; } = "";
    public Guid? TargetAssetId { get; set; }

    public decimal EstimatedCost { get; set; }
    public string ExpectedBenefit { get; set; } = "";
    public string MainRisk { get; set; } = "";

    public ProposalStatus Status { get; set; }
    public List<ProposalComment> Comments { get; set; } = new();
    public List<ProposalVote> Votes { get; set; } = new();
}
```

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

## 10.2 AI Proposal Example

```text
Proposal: Drill Falcon-1

Target:
Falcon Prospect, Block D-08

Estimated Cost:
$40M

Expected Benefit:
Potential commercial discovery of 80–160 MMboe.

Main Risk:
Trap closure remains uncertain.

AI Recommendation:
Balanced to aggressive. Drill if the company wants growth and can absorb a dry hole.
```

---

# 11. AI Context System

The AI must receive carefully prepared context from the server.

The client should not send raw hidden game state.

The server should build an AI-safe context object.

---

## 11.1 AI Context Builder

Responsibilities:

```text
Collect allowed company data
Collect selected asset data
Collect recent turn results
Collect known market data
Collect team discussion summary
Remove hidden information
Limit context size
Format data clearly
```

---

## 11.2 AI Game Context Model

```csharp
public sealed class AiGameContext
{
    public Guid GameSessionId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid PlayerId { get; set; }

    public int CurrentTurn { get; set; }
    public int TotalTurns { get; set; }

    public AiCompanySnapshot Company { get; set; } = new();
    public AiMarketSnapshot Market { get; set; } = new();

    public List<AiAssetSummary> Assets { get; set; } = new();
    public List<AiRecentEvent> RecentEvents { get; set; } = new();
    public List<AiProposalSummary> PendingProposals { get; set; } = new();

    public AiSelectedContext? SelectedContext { get; set; }

    public string KnownLimitations { get; set; } = "";
}
```

---

## 11.3 Company Snapshot

```csharp
public sealed class AiCompanySnapshot
{
    public string CompanyName { get; set; } = "";

    public decimal Cash { get; set; }
    public decimal Debt { get; set; }
    public decimal CompanyValue { get; set; }

    public double ProductionBoePerDay { get; set; }
    public double ReservesMmboe { get; set; }

    public decimal RevenueLastTurn { get; set; }
    public decimal NetCashFlowLastTurn { get; set; }

    public decimal AbandonmentLiability { get; set; }

    public int Reputation { get; set; }
    public int CreditRating { get; set; }
    public int CurrentRank { get; set; }
}
```

---

## 11.4 Asset Summary

```csharp
public sealed class AiAssetSummary
{
    public Guid AssetId { get; set; }

    public string Name { get; set; } = "";
    public string AssetType { get; set; } = "";
    public string Stage { get; set; } = "";

    public string KnownSummary { get; set; } = "";
    public string MainRisk { get; set; } = "";

    public decimal EstimatedValue { get; set; }
    public decimal? EstimatedCostToNextStep { get; set; }

    public double Confidence { get; set; }
}
```

---

# 12. AI Data Access Rules

## 12.1 Allowed AI Data

The AI can use:

```text
Company cash
Company debt
Company production
Known reserves
Known asset estimates
Known block data
Owned study results
Owned seismic results
Known discovery information
Known production data
Known abandonment liability
Public competitor information
Team chat summaries
Submitted proposals
Current market data
Market forecast visible to player
```

---

## 12.2 Forbidden AI Data

The AI must not receive:

```text
Hidden geology
True undiscovered chance of success
True undiscovered recoverable volume
Future event schedule
Random seeds
Competitor private actions
Competitor private cash/debt
Private competitor AI advice
Server-only valuation truth
```

---

## 12.3 Fairness Rule

The AI should never know more than the player.

Important rule:

```text
If the player could not see it in the UI, the AI should not see it either.
```

---

# 13. AI Safety and Validation

The AI must be treated as an advisor, not an authority.

## 13.1 AI Output Validation

The server should validate AI-created proposals before showing them as actionable.

Validation checks:

```text
Does the company own the target?
Is the action available?
Is the cost correct?
Does the action require a stage that is not reached?
Does the company have enough action slots?
Is the target asset valid?
```

---

## 13.2 AI Cannot Execute Actions Directly

The AI may return:

```text
RecommendedAction
DraftProposal
Analysis
Warning
Summary
```

The AI may not directly call:

```text
SubmitTurnAction
ApproveBid
TakeDebt
SellAsset
AbandonField
```

Those require player confirmation.

---

# 14. AI Tool System

The backend should expose controlled AI tools.

The AI does not directly query the database.

Instead, it calls safe functions.

## 14.1 Recommended AI Tools

```text
get_company_snapshot
get_selected_asset_summary
get_known_block_details
get_known_field_details
get_pending_proposals
estimate_cashflow_visible
compare_known_options
create_draft_proposal
summarize_team_chat
generate_turn_plan
```

---

## 14.2 Tool Example: Estimate Cash Flow

```csharp
public sealed class EstimateCashflowRequest
{
    public Guid CompanyId { get; set; }
    public int TurnsAhead { get; set; }
    public List<Guid> IncludedAssetIds { get; set; } = new();
}
```

```csharp
public sealed class EstimateCashflowResult
{
    public decimal EstimatedRevenue { get; set; }
    public decimal EstimatedOpex { get; set; }
    public decimal EstimatedCapex { get; set; }
    public decimal EstimatedNetCashFlow { get; set; }

    public string Assumptions { get; set; } = "";
}
```

---

# 15. AI Prompt Design

The AI should receive a strong system instruction.

## 15.1 Base System Prompt

```text
You are the AI Command Center inside Beep Oil and Gas Sim.

You advise a player-controlled oil and gas company in a competitive strategy simulation.

Use only the game state provided to you.

Do not invent hidden geology, future events, competitor private actions, or undiscovered reserves.

When information is uncertain, clearly say it is uncertain.

You may recommend actions, but you must not claim that an action has been executed.

Always structure important advice using:
1. Situation
2. Risks
3. Recommendation
4. Suggested Action

Keep responses useful, concise, and game-focused.
```

---

## 15.2 Advisor-Specific Prompt: Geologist

```text
You are the company's AI Geologist.

Focus on exploration risk, block interpretation, prospects, chance of success, data confidence, seismic, dry holes, and discoveries.

Do not reveal hidden geology. Discuss only the known or estimated information provided.
```

---

## 15.3 Advisor-Specific Prompt: CFO

```text
You are the company's AI CFO.

Focus on cash, debt, credit rating, project affordability, hedging, asset value, cash flow, and final score risk.

Warn the player when a decision may create financial distress.
```

---

## 15.4 Advisor-Specific Prompt: HSE

```text
You are the company's AI HSE Advisor.

Focus on safety, environmental risk, abandonment liability, late-life fields, regulatory pressure, and reputation.

Encourage responsible closure when it improves long-term company value.
```

---

# 16. AI Memory and Conversation History

The AI should remember recent conversation context within a match.

## 16.1 Stored Conversation Data

Store:

```text
Conversation ID
Game session ID
Company ID
Player ID
Advisor type
Messages
Selected context
Created proposals
Timestamp
```

## 16.2 Conversation Limits

The AI should not receive unlimited chat history.

Use:

```text
Last 10–20 messages
Current game context
Summary of older chat
Relevant proposals
```

---

# 17. Team Chat Summarization

Team mode should include AI summaries.

## 17.1 Summary Types

```text
Short summary
Decision summary
Risk summary
Disagreement summary
Turn plan summary
```

## 17.2 Example Team Summary

```text
Team Discussion Summary:

The Exploration Manager prefers drilling Block D-08 this turn.
The CFO is concerned that a dry hole would reduce cash below $100M.
The AI Geologist notes that the main uncertainty is trap closure.

Main decision:
Drill now or acquire additional seismic first.

Recommended compromise:
Delay drilling one turn and improve confidence if the team wants a lower-risk strategy.
```

---

# 18. AI Turn Report

At the end of each turn, the AI should generate a report.

## 18.1 Turn Report Sections

```text
Financial Summary
Exploration Summary
Development Summary
Production Summary
Risk Summary
Recommended Next Turn Actions
```

## 18.2 Example Turn Report

```text
Turn 8 AI Report

Financial:
Cash increased from $210M to $248M due to strong Falcon production.

Exploration:
Block D-04 remains attractive, but confidence is still low.

Development:
Falcon Field construction is 66% complete and remains on schedule.

Production:
No major production issues occurred this turn.

Risks:
Debt remains manageable, but cash could become tight if development cost overruns occur.

Recommended Next Turn:
1. Complete Falcon development.
2. Avoid new exploration wells until first oil.
3. Consider hedging once production starts.
```

---

# 19. AI Cost Control

AI usage should be controlled to avoid excessive cost.

## 19.1 Cost Control Methods

```text
Limit context size
Use summaries instead of full history
Cache system prompts
Cache static game rules
Rate-limit AI questions
Use cheaper model for summaries
Use stronger model for complex strategy
Use server-side batching for turn reports
```

## 19.2 MVP AI Limits

Recommended MVP limits:

```text
Free advisor questions per turn: 5 per company
Extra questions: optional cooldown or paid in-game analytics cost
Turn report: 1 per company per turn
Team summary: on demand or every turn
```

For testing, usage can be unlimited.

---

# 20. AI Response Quality Rules

The AI should:

```text
Be clear
Be concise
Explain uncertainty
Use game numbers when available
Recommend practical actions
Avoid overconfidence
Avoid hidden assumptions
Use player-known data only
```

The AI should not:

```text
Write long essays during live matches
Use real-world investment advice language
Pretend to know hidden outcomes
Guarantee success
Auto-play the game
Confuse game money with real money
```

---

# 21. AI Error Handling

The AI may fail or give an invalid suggestion.

The system should handle this gracefully.

## 21.1 Possible Errors

```text
AI service unavailable
Invalid response
Proposal validation failed
Context missing
Advisor timeout
Rate limit exceeded
```

## 21.2 User-Friendly Messages

Examples:

```text
The AI Advisor is unavailable right now. You can still submit actions manually.

The AI suggested an action that is not currently valid, so it was not converted into a proposal.

Not enough known data is available to analyze this asset yet.
```

---

# 22. Backend Architecture

Recommended backend structure:

```text
server/
└── OilGasRivals.Api/
    ├── AI/
    │   ├── AiAdvisorService.cs
    │   ├── AiContextBuilder.cs
    │   ├── AiPromptLibrary.cs
    │   ├── AiToolRegistry.cs
    │   ├── AiToolExecutor.cs
    │   ├── AiResponseValidator.cs
    │   ├── AiConversationStore.cs
    │   └── AiTurnReportService.cs
    │
    ├── Collaboration/
    │   ├── ActionProposalService.cs
    │   ├── TeamChatService.cs
    │   └── TeamSummaryService.cs
    │
    └── Hubs/
        ├── AiAdvisorHub.cs
        └── TeamChatHub.cs
```

---

# 23. AI Advisor Service

## 23.1 Service Responsibilities

```text
Receive AI request
Validate player access
Build AI-safe context
Select advisor prompt
Call AI provider
Validate response
Create proposal if requested
Save conversation
Stream response to client
```

## 23.2 Request Model

```csharp
public sealed class AiAdvisorRequest
{
    public Guid GameSessionId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid PlayerId { get; set; }

    public string AdvisorType { get; set; } = "Strategy";
    public string Message { get; set; } = "";

    public Guid? SelectedAssetId { get; set; }
    public Guid? ProposalId { get; set; }

    public bool AllowProposalCreation { get; set; }
}
```

## 23.3 Response Model

```csharp
public sealed class AiAdvisorResponse
{
    public string AdvisorType { get; set; } = "";
    public string Message { get; set; } = "";

    public string RecommendationType { get; set; } = "";
    public Guid? DraftProposalId { get; set; }

    public List<string> Warnings { get; set; } = new();
    public List<string> SuggestedQuickActions { get; set; } = new();
}
```

---

# 24. SignalR AI Hub

AI responses should be streamable to the client.

## 24.1 Hub Responsibilities

```text
Join company AI room
Send advisor question
Stream AI response tokens
Notify proposal created
Notify turn report ready
```

## 24.2 Example Hub Skeleton

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BeepOilAndGasSim.Api.Hubs;

[Authorize]
public sealed class AiAdvisorHub : Hub
{
    private readonly IAiAdvisorService _aiAdvisorService;

    public AiAdvisorHub(IAiAdvisorService aiAdvisorService)
    {
        _aiAdvisorService = aiAdvisorService;
    }

    public async Task JoinCompanyAiRoom(Guid gameSessionId, Guid companyId)
    {
        // Validate that the current player belongs to this company.
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            $"game:{gameSessionId}:company:{companyId}:ai");
    }

    public async Task AskAdvisor(AiAdvisorRequest request)
    {
        // Validate player access inside service.
        await _aiAdvisorService.ProcessAdvisorRequestAsync(
            request,
            Context.ConnectionAborted);
    }
}
```

---

# 25. Client Architecture

Recommended client files:

```text
client/
└── src/
    ├── game/
    │   ├── ui/
    │   │   ├── command-center/
    │   │   │   ├── CommandCenterPanel.tsx
    │   │   │   ├── AiAdvisorPanel.tsx
    │   │   │   ├── AdvisorSelector.tsx
    │   │   │   ├── AiMessageList.tsx
    │   │   │   ├── AiInputBox.tsx
    │   │   │   ├── AiQuickActions.tsx
    │   │   │   └── AiProposalPreview.tsx
    │   │   │
    │   │   └── reports/
    │   │       └── AiTurnReportPanel.tsx
    │   │
    │   ├── net/
    │   │   ├── AiAdvisorClient.ts
    │   │   └── AiAdvisorHubClient.ts
    │   │
    │   └── state/
    │       ├── aiStore.ts
    │       ├── commandCenterStore.ts
    │       └── proposalStore.ts
```

---

# 26. Client AI State

```typescript
export type AdvisorType =
  | "Strategy"
  | "Geologist"
  | "ReservoirEngineer"
  | "DrillingEngineer"
  | "CFO"
  | "HSE"
  | "MarketAnalyst";

export interface AiMessage {
  id: string;
  role: "player" | "ai" | "system";
  advisorType?: AdvisorType;
  content: string;
  createdAtUtc: string;
  selectedAssetId?: string;
}

export interface AiAdvisorState {
  activeAdvisor: AdvisorType;
  selectedAssetId?: string;
  messages: AiMessage[];
  isStreaming: boolean;
  error?: string;
}
```

---

# 27. AI MVP Scope

The first AI version should be focused.

## 27.1 MVP Includes

```text
AI Strategy Advisor
AI Geologist
AI CFO
AI HSE Advisor
Asset-aware chat
Turn summary report
Draft action proposal creation
Team chat summary
Server-side context filtering
No hidden information access
```

## 27.2 MVP Does Not Include

```text
Voice AI
Full autonomous planning
AI-controlled companies
Advanced tool chaining
Long-term player coaching memory
Real-world oil data integration
Full document generation
Complex multi-agent debate
```

---

# 28. MVP AI Questions

The MVP AI should handle these questions well:

```text
What should we do this turn?
Should we drill this block?
Should we buy seismic first?
Can we afford this development?
Should we take debt?
Should we hedge production?
Which field should we optimize?
Should we abandon this field?
What is our biggest risk?
Summarize our team discussion.
Create a proposal for this action.
```

---

# 29. Example AI Workflows

## 29.1 Ask About a Block

```text
Player selects Block D-08.
Player asks: Should we drill?

Server sends:
- Company cash
- Owned block status
- Known study and seismic results
- Estimated chance of success
- Confidence
- Drilling cost
- Recent market conditions

AI responds:
- Situation
- Risks
- Recommendation
- Suggested action
```

---

## 29.2 Create Proposal

```text
Player asks:
Create a proposal to drill Falcon-1.

AI creates:
Draft proposal with target, cost, expected benefit, risk, and recommendation.

Player reviews:
Clicks "Submit Proposal".

Team reviews:
Votes and comments.

CEO approves:
Proposal becomes committed action.
```

---

## 29.3 End Turn Report

```text
Turn resolves.
Server generates company turn data.
AI creates summary.
Report appears in Command Center.
```

---

# 30. Example AI Responses

## 30.1 Strategy Advisor

```text
Situation:
Your company has $180M cash, one commercial discovery, and no producing fields yet.

Risks:
Approving a standard development would leave only $-40M cash unless you borrow.
Drilling another exploration well before first oil would increase financial risk.

Recommendation:
Prioritize development funding over new exploration.

Suggested Action:
Take $100M debt and approve small or standard development depending on your risk appetite.
```

---

## 30.2 Geologist

```text
Situation:
Block D-04 has a 29% estimated chance of success with low-to-medium confidence.

Risks:
The main uncertainty is trap integrity. The current estimate is based on geological study only.

Recommendation:
Do not drill yet unless you need a high-risk move. Acquire 2D seismic first.

Suggested Action:
Acquire 2D seismic on Block D-04.
```

---

## 30.3 CFO

```text
Situation:
You have $95M cash and $250M debt. Your credit rating is 48.

Risks:
Another dry hole could trigger emergency debt. Interest is already reducing cash flow.

Recommendation:
Avoid new exploration wells this turn. Use production cash to repay debt or hedge revenue.

Suggested Action:
Hedge 50% of next turn production and repay $50M debt if cash allows.
```

---

## 30.4 HSE Advisor

```text
Situation:
Falcon Field is late-life and has $45M abandonment liability.

Risks:
If unresolved at match end, the estimated penalty is $67.5M.
The field generated only $6M net cash flow last turn.

Recommendation:
Abandon the field unless you need one more turn of cash flow.

Suggested Action:
Abandon Falcon Field.
```

---

# 31. AI Balancing Guidelines

## 31.1 AI Should Help, Not Replace Skill

The AI should support decision-making but not always give the perfect answer.

It should:

```text
Explain tradeoffs
Point out obvious risks
Recommend based on known data
Acknowledge uncertainty
```

It should not:

```text
Know hidden truth
Guarantee the best move
Always optimize perfectly
Remove player responsibility
```

---

## 31.2 AI Should Be Useful for New and Expert Players

For new players:

```text
Explain concepts
Suggest safe actions
Clarify risks
```

For expert players:

```text
Compare options
Summarize data
Highlight overlooked risks
Estimate financial impact
```

---

## 31.3 AI Should Be Fast Enough for Live Matches

Responses should be short during timed turns.

Recommended live response length:

```text
100–250 words
```

Detailed reports can be longer.

---

# 32. Design Risks

## 32.1 AI Gives Bad Advice

Solution:

```text
Validate proposals.
Explain uncertainty.
Let player decide.
Improve prompts and context.
Log feedback.
```

---

## 32.2 AI Feels Like Cheating

Solution:

```text
Never provide hidden data.
Show that advice is based on known information.
Use uncertainty language.
Allow all players equal AI access.
```

---

## 32.3 AI Becomes Too Expensive

Solution:

```text
Limit questions per turn.
Summarize context.
Cache prompts.
Use smaller models for summaries.
Use stronger models only for strategy.
```

---

## 32.4 AI Slows Down Turns

Solution:

```text
Stream responses.
Add quick summaries.
Pre-generate turn reports.
Allow players to submit actions without AI.
```

---

## 32.5 AI Overwhelms UI

Solution:

```text
Keep Command Center collapsible.
Use short answers by default.
Use tabs.
Show suggested actions as buttons.
```

---

# 33. Open Questions

1. Should AI usage be unlimited or limited per turn?
2. Should AI questions consume in-game analytics budget?
3. Should all players have the same AI quality?
4. Should premium company upgrades improve AI advisor accuracy?
5. Should AI be allowed to create proposals automatically after turn reports?
6. Should AI chat be private to each player or shared across company team?
7. Should AI summaries be visible to all team members?
8. Should advisor roles have different personalities or only different expertise?
9. Should the AI be available during auctions?
10. Should AI advice be stored for replay and audit?

---

# 34. Recommended MVP AI Decision

For MVP, implement the AI Command Center as follows:

```text
AI Roles:
- Strategy Advisor
- Geologist
- CFO
- HSE Advisor

Core Features:
- Text chat
- Selected asset context
- Turn summary report
- Team chat summary
- Draft proposal creation

Safety:
- Server-side context builder
- No hidden geology
- No future events
- No competitor private information
- AI cannot execute actions

Usage:
- 5 advisor questions per company per turn
- 1 AI turn report per company per turn
- Unlimited during development/testing

UI:
- Command Center side panel
- Advisor selector
- Context chip
- Quick prompt buttons
- Create Proposal button
```

This gives Beep Oil and Gas Sim a distinctive AI-powered gameplay layer while keeping the first implementation focused and achievable.
