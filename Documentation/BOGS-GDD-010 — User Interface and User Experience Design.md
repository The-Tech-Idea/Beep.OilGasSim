# Beep Oil and Gas Sim

## User Interface and User Experience Design

**Document ID:** BOGS-GDD-010
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

**Project Name:** Beep Oil and Gas Sim
**Short Name:** Beep O&G Sim

---

# 1. Purpose

This document defines the user interface and user experience design for Beep Oil and Gas Sim.

The goal of the UI is to make a complex oil and gas strategy simulation feel clear, exciting, readable, and easy to control.

The UI must support:

* 2.5D map interaction
* Turn-based decision-making
* Oil and gas asset management
* Financial review
* Exploration risk analysis
* Development and production decisions
* Late-life and abandonment planning
* Multiplayer communication
* Team proposals
* AI Command Center interaction
* Turn results and reports

The interface should feel like a modern strategy game mixed with a professional energy company command dashboard.

---

# 2. UX Vision

The user experience should make the player feel like they are running an oil and gas company from a strategic command center.

The player should always understand:

```text
What do I own?
What stage is each asset in?
How much money do I have?
What are my risks?
What actions can I take?
What is happening this turn?
How am I doing compared to competitors?
What does the AI recommend?
```

The UI should reduce confusion and help players make decisions quickly.

The experience should be:

```text
Strategic
Readable
Modern
Data-rich
Not overwhelming
Competitive
AI-assisted
Team-friendly
```

---

# 3. UX Design Principles

## 3.1 Show the Next Useful Decision

The UI should always guide the player toward meaningful actions.

Example:

For a licensed block:

```text
Recommended next actions:
- Geological Study
- Acquire 2D Seismic
- Drill Exploration Well
- Relinquish License
- Ask AI Geologist
```

For a producing field:

```text
Recommended next actions:
- Optimize Field
- Hedge Production
- Prepare Abandonment
- Ask Production Advisor
```

---

## 3.2 Separate Overview From Detail

The player should be able to see a high-level map and then drill down into details.

Recommended pattern:

```text
Map overview
    ↓
Click block/asset
    ↓
Side panel opens
    ↓
Detailed actions and reports
```

The player should not need to open many menus to understand an asset.

---

## 3.3 Use Progressive Disclosure

Do not show every detail at once.

Basic players should see simple summaries.

Advanced players can open deeper panels.

Example:

```text
Simple View:
Chance of Success: 34%
Confidence: Medium
Main Risk: Trap

Advanced View:
Source: Moderate
Reservoir: Good
Trap: Uncertain
Seal: Moderate
Timing: Unknown
```

---

## 3.4 Make Risk Visible

Risk is central to the game.

The UI should clearly show:

```text
Chance of success
Confidence
Financial exposure
Debt risk
Development risk
Production decline
Abandonment liability
Reputation risk
```

Risk should use colors, icons, and explanation text.

---

## 3.5 Make Results Exciting

Turn results should feel like a reveal.

Drilling results, discoveries, auctions, and production outcomes should be presented with strong visual feedback.

Example:

```text
Major Discovery!
Falcon-1 found commercial oil.
Estimated recoverable volume: 80–160 MMbbl.
```

---

## 3.6 Keep AI Close to Decisions

The AI Command Center should be accessible from every important decision screen.

Examples:

```text
Ask AI about this block
Ask CFO before approving development
Ask HSE about abandonment
Ask Strategy Advisor for turn plan
```

AI should feel embedded, not separate.

---

# 4. Main UI Layout

The recommended main game layout is:

```text
┌─────────────────────────────────────────────────────────────┐
│ Top Bar: Company, Cash, Debt, Oil Price, Turn, Timer, Rank   │
├───────────────┬───────────────────────────────┬─────────────┤
│ Left Sidebar  │ Main 2.5D Map / Game Canvas   │ Right Panel │
│ Navigation    │                               │ Details     │
│               │                               │ Actions     │
├───────────────┴───────────────────────────────┴─────────────┤
│ Bottom Bar: Action Queue, Alerts, Turn Commit                │
└─────────────────────────────────────────────────────────────┘
```

The Command Center can open as a right-side drawer, bottom drawer, or floating panel.

---

# 5. Top Bar

The top bar should always show critical company and match information.

## 5.1 Top Bar Items

```text
Company name
Company color
Cash
Debt
Oil price
Production rate
Turn number
Turn timer
Current rank
Notifications
Settings
```

## 5.2 MVP Top Bar

For MVP, include:

```text
Company name
Cash
Debt
Oil price
Turn number
Rank
Commit Turn button
```

## 5.3 Example Top Bar

```text
Beep Energy | Cash: $420M | Debt: $100M | Oil: $75/bbl | Turn 6/20 | Rank: #2 | Commit Turn
```

---

# 6. Main Map

The map is the main interaction area.

It should show:

```text
License blocks
Ownership
Terrain
Infrastructure
Wells
Discoveries
Fields
Facilities
Pipelines
Events
Competitor assets
```

The map can be 2.5D isometric or low-poly 3D.

---

## 6.1 Map Interaction

Players should be able to:

```text
Click block
Click field
Click well
Click facility
Pan camera
Zoom camera
Switch map layers
Open details panel
Open action menu
Ask AI about selected asset
```

---

## 6.2 Map Layers

Recommended map layers:

```text
Ownership
Geological potential
Infrastructure
Exploration maturity
Production
Environmental sensitivity
Risk
Competitor activity
```

MVP map layers:

```text
Ownership
Asset stage
Infrastructure
Discovery trend
```

---

## 6.3 Map Symbols

Recommended visual symbols:

| Asset            | Icon / Visual          |
| ---------------- | ---------------------- |
| Unowned block    | Neutral outline        |
| Owned block      | Company-colored fill   |
| Studied block    | Document icon          |
| Seismic block    | Wave-line icon         |
| Exploration well | Drill icon             |
| Dry hole         | Small gray well marker |
| Discovery        | Bright oil drop marker |
| Development      | Construction crane     |
| Producing field  | Pumpjack or platform   |
| Late-life field  | Warning icon           |
| Abandoned field  | Sealed well icon       |

---

## 6.4 Map Color Rules

Use color carefully.

Recommended color meanings:

```text
Green: producing / positive
Blue: owned / active company
Yellow: opportunity / warning
Orange: risk / late-life
Red: critical problem
Gray: inactive / abandoned / dry hole
Purple: AI or special analysis
```

Company ownership colors should be distinct and accessible.

---

# 7. Left Sidebar Navigation

The left sidebar provides navigation to major game areas.

## 7.1 Recommended Sidebar Items

```text
Map
Company Dashboard
Assets
Exploration
Development
Production
Finance
Market
Leaderboard
Command Center
Reports
Settings
```

## 7.2 MVP Sidebar Items

```text
Map
Company
Assets
Finance
Leaderboard
Command Center
```

The sidebar should collapse to icons for smaller screens.

---

# 8. Right Detail Panel

The right panel shows details about selected objects.

## 8.1 Dynamic Panel Types

The right panel changes based on selection:

```text
Block Panel
Prospect Panel
Discovery Panel
Development Project Panel
Field Panel
Company Panel
Market Panel
Proposal Panel
Event Panel
```

---

## 8.2 Panel Structure

Recommended panel layout:

```text
Title
Stage
Key metrics
Risk indicators
Summary
Available actions
AI button
Advanced details
History / reports
```

---

# 9. Block Detail Panel

The block panel is used for license and exploration decisions.

## 9.1 Block Panel Fields

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
Infrastructure access
Environmental sensitivity
License status
Available actions
```

## 9.2 Example Block Panel

```text
Block D-08

Owner:
Beep Energy

Stage:
2D Seismic Evaluated

Estimated Chance of Success:
34%

Confidence:
Medium

Estimated Volume:
40–220 MMboe

Main Risk:
Trap closure

Infrastructure:
Moderate access

Available Actions:
- Drill Exploration Well
- Acquire 3D Seismic
- Relinquish License
- Ask AI Geologist
```

---

# 10. Discovery Detail Panel

The discovery panel supports appraisal and development decisions.

## 10.1 Discovery Panel Fields

```text
Discovery name
Fluid type
Estimated recoverable volume
Confidence
Commerciality rating
Development difficulty
Main risk
Estimated development cost
Recommended development concepts
Available actions
```

## 10.2 Example Discovery Panel

```text
Falcon Discovery

Fluid:
Oil

Estimated Recoverable:
80–160 MMbbl

Confidence:
Low

Commerciality:
Commercial

Main Risk:
Reservoir continuity

Available Actions:
- Drill Appraisal Well
- Select Development Concept
- Sell Discovery
- Ask Reservoir Engineer
- Ask CFO
```

---

# 11. Development Project Panel

The development panel shows construction progress.

## 11.1 Development Panel Fields

```text
Field name
Development concept
CAPEX
Construction progress
Turns remaining
Expected first oil
Facility capacity
Expected OPEX
Abandonment liability
Risks
```

## 11.2 Example Development Panel

```text
Falcon Field Development

Concept:
Standard Development

Construction:
66% complete

Turns Remaining:
1

CAPEX:
$220M

Expected Capacity:
25,000 boe/day

Expected First Oil:
Turn 9

Available Actions:
- Review Project
- Ask CFO
- Ask Production Advisor
```

---

# 12. Producing Field Panel

The field panel is used for production management.

## 12.1 Field Panel Fields

```text
Field name
Stage
Production phase
Current production rate
Peak production rate
Facility capacity
Produced volume this turn
Revenue this turn
OPEX this turn
Net cash flow
Remaining reserves
Decline rate
Uptime
Water cut, optional
Abandonment liability
Available actions
```

## 12.2 Example Field Panel

```text
Falcon Field

Stage:
Producing

Current Rate:
23,000 bopd

Peak Rate:
25,000 bopd

Remaining Reserves:
83 MMbbl

Decline:
8% per turn

Uptime:
94%

Net Cash Flow Last Turn:
$216M

Abandonment Liability:
$45M

Available Actions:
- Optimize Field
- Maintenance Campaign
- Hedge Production
- Sell Field
- Ask Production Advisor
```

---

# 13. Late-Life and Abandonment Panel

Late-life assets require clear warnings.

## 13.1 Late-Life Panel Fields

```text
Late-life status
Current production vs peak
Remaining reserves
Net cash flow
Abandonment liability
Estimated final penalty
Environmental sensitivity
Regulatory pressure
Available actions
```

## 13.2 Example Late-Life Warning

```text
Warning:
Falcon Field is now late-life.

Production is below 25% of peak.
Abandonment liability is $45M.
If unresolved at match end, estimated penalty is $67.5M.
```

Available actions:

```text
Continue Production
Optimize Field
Sell Field
Abandon Field
Ask HSE Advisor
```

---

# 14. Company Dashboard

The company dashboard gives a high-level business view.

## 14.1 Dashboard Sections

```text
Financial Summary
Production Summary
Asset Portfolio
Exploration Portfolio
Development Projects
Risks and Warnings
Reputation
Abandonment Liability
Leaderboard Position
```

## 14.2 MVP Dashboard Metrics

```text
Cash
Debt
Company value
Current rank
Oil production
Reserves
Revenue last turn
Net cash flow last turn
Reputation
Abandonment liability
```

---

# 15. Finance UI

The finance UI should help players understand affordability.

## 15.1 Finance Screen

Show:

```text
Cash
Debt
Credit rating
Revenue
OPEX
CAPEX
Interest
Royalty
Net cash flow
Debt limit
Hedging status
Projected cash next turn
```

## 15.2 Financial Warning Examples

```text
Approving this development will reduce cash below $50M.

Your debt is high. Interest payments are reducing cash flow.

Oil price forecast is bearish. Consider hedging production.

Unresolved abandonment liability may reduce final score by $120M.
```

---

# 16. Market UI

The market UI shows commodity and economic conditions.

## 16.1 Market Screen

Show:

```text
Current oil price
Price trend
Forecast range
Market events
Hedging options
Service cost index
Fiscal events
```

## 16.2 MVP Market UI

```text
Current oil price
Oil price chart
Market trend
Hedge 25%
Hedge 50%
Hedge 75%
```

---

# 17. Leaderboard UI

The leaderboard drives competition.

## 17.1 Leaderboard Columns

MVP columns:

```text
Rank
Company
Company value
Production
Reserves
Reputation
```

Expanded columns:

```text
Cash
Debt risk
Discovery count
Producing fields
Abandonment liability
Safety rating
```

Some details may be hidden from competitors depending on visibility rules.

---

# 18. Action Queue and Turn Commit UI

The bottom bar should show selected actions for the current turn.

## 18.1 Action Queue

Show:

```text
Action slots used
Selected actions
Estimated cost
Warnings
Remove action button
Commit turn button
```

Example:

```text
Actions 2/3

1. Drill Falcon-1 — $40M
2. Acquire 2D Seismic on Block D-04 — $15M

Estimated Spend: $55M
Cash After Actions: $365M

[Commit Turn]
```

---

## 18.2 Action Validation Warnings

Examples:

```text
Not enough cash.
Action requires owned block.
Discovery confidence too low.
Action slots full.
Field is not producing yet.
```

---

# 19. Turn Results UI

Turn results should be presented clearly and dramatically.

## 19.1 Results Sections

```text
Headline events
Exploration results
Auction results
Development progress
Production report
Financial summary
Market changes
Reputation changes
Leaderboard changes
AI summary
```

## 19.2 Result Card Example

```text
Major Discovery!

Falcon-1 discovered commercial oil in Block D-08.

Initial Estimate:
80–160 MMbbl

Main Risk:
Reservoir continuity

Recommended Next Action:
Drill appraisal well.
```

---

## 19.3 Financial Result Example

```text
Financial Summary

Starting Cash: $420M
Revenue: $0
CAPEX: $55M
OPEX: $0
Ending Cash: $365M
Debt: $0
```

---

# 20. Command Center UI

The Command Center contains AI and collaboration tools.

## 20.1 Command Center Tabs

```text
AI Advisor
Team Chat
Action Board
Turn Report
Reports
```

## 20.2 AI Advisor Panel

Fields:

```text
Advisor selector
Context chip
Message list
Quick prompt buttons
Input box
Create Proposal button
```

## 20.3 Example AI Panel

```text
Advisor:
Geologist

Context:
Block D-08

Player:
Should we drill?

AI:
Block D-08 is drillable but still risky.
The chance of success is 34% with medium confidence.
The main risk is trap closure.

Recommendation:
Drill only if you can absorb a dry hole.
```

---

# 21. Team Chat UI

Team chat supports company collaboration.

## 21.1 Chat Features

```text
Company chat
Public match chat
AI summaries
Proposal links
System messages
```

## 21.2 MVP Chat UI

```text
Message list
Player names
Timestamps
Input box
Send button
```

---

# 22. Action Proposal Board UI

The proposal board supports team decisions.

## 22.1 Board Columns

Recommended columns:

```text
Draft
Proposed
Approved
Committed
Rejected
```

MVP can use a simple list.

## 22.2 Proposal Card

Each proposal card shows:

```text
Title
Target
Estimated cost
Expected benefit
Main risk
Votes
AI recommendation
Status
Approve button
Reject button
```

---

# 23. Notifications UI

Notifications should appear for important events.

## 23.1 Notification Types

```text
Info
Success
Warning
Critical
AI
Team
Market
```

## 23.2 Examples

```text
Success:
You won Block D-08 for $32M.

Warning:
Cash will be below $50M after selected actions.

Critical:
Falcon Field has entered late-life.

AI:
Turn 7 AI Report is ready.
```

---

# 24. Onboarding and Tutorial UX

The game needs onboarding because oil and gas concepts may be new to some players.

## 24.1 Tutorial Goals

Teach:

```text
How turns work
How to bid for blocks
How exploration risk works
How to buy data
How to drill
How to develop discoveries
How production creates revenue
How debt and cash work
How abandonment affects score
How to ask AI for help
```

## 24.2 Tutorial Method

Use guided missions:

```text
Step 1: Select a block
Step 2: Buy geological study
Step 3: Acquire 2D seismic
Step 4: Drill exploration well
Step 5: Appraise discovery
Step 6: Approve development
Step 7: Review production
Step 8: Ask AI for turn plan
```

---

# 25. Accessibility Requirements

The UI should be readable and accessible.

## 25.1 Accessibility Guidelines

```text
High contrast text
Readable font sizes
Color is not the only indicator
Icons have tooltips
Keyboard-friendly navigation where possible
Clear error messages
Scalable UI
Avoid tiny text in panels
```

## 25.2 Color Accessibility

Risk colors should also include labels or icons.

Bad:

```text
Only red means high risk.
```

Good:

```text
Red warning icon + "High Risk" label.
```

---

# 26. Responsive Web Design

The first version should target desktop web.

## 26.1 Primary Target

```text
Desktop browser
Laptop browser
Large tablet optional
```

## 26.2 Not Primary for MVP

```text
Small mobile phone layout
Console controls
Touch-only gameplay
```

The game can later support tablets with simplified layout.

---

# 27. Visual Style Direction

Recommended style:

```text
Modern strategy dashboard
2.5D low-poly map
Clean panels
Strong icons
Readable charts
Company color identity
Subtle animations
Industrial-energy theme
```

## 27.1 Tone

The UI should feel:

```text
Professional
Strategic
Modern
Slightly playful
Not cartoonish
Not overly corporate
```

---

# 28. Animation and Feedback

Animations should make the game feel alive.

## 28.1 Useful Animations

```text
Rig drilling animation
Seismic survey movement
Oil discovery reveal
Production flow animation
Pipeline activity
Construction progress
Leaderboard rank movement
Notification slide-in
AI typing indicator
Turn resolution sequence
```

## 28.2 MVP Animations

```text
Map selection highlight
Drilling result reveal
Production pulse
Notification animation
AI typing indicator
```

---

# 29. Charts and Data Visualization

Charts should help players understand trends.

## 29.1 Recommended Charts

```text
Oil price over time
Company value over time
Cash over time
Production over time
Reserves over time
Field production decline
Debt over time
Leaderboard trend
```

## 29.2 MVP Charts

```text
Oil price chart
Company value chart
Production chart
Field decline chart
```

---

# 30. Tooltip System

Tooltips are important for learning.

## 30.1 Tooltip Examples

```text
Chance of Success:
The estimated probability that drilling will find commercial hydrocarbons. This is uncertain and improves with more data.

Confidence:
How reliable the estimate is. Low confidence means the real result may be very different.

Abandonment Liability:
The estimated cost to safely close wells and facilities at the end of field life.
```

---

# 31. Empty States

The UI should handle empty states gracefully.

Examples:

## No Producing Fields

```text
You do not have producing fields yet.
Explore, appraise, and develop discoveries to start production.
```

## No Proposals

```text
No proposals yet.
Create a proposal from an asset action or ask AI to suggest one.
```

## No AI Context

```text
Select a block, discovery, or field to get asset-specific advice.
```

---

# 32. Error Messages

Error messages should explain what happened and how to fix it.

Bad:

```text
Invalid action.
```

Good:

```text
You cannot drill Block D-08 because your company does not own this block.
Bid for the license first or choose an owned block.
```

---

# 33. MVP UI Scope

## 33.1 MVP Screens

```text
Login / player entry
Create or join match
Lobby
Main map
Block detail panel
Discovery detail panel
Field detail panel
Company dashboard
Finance summary
Leaderboard
Command Center
Turn results
```

## 33.2 MVP Panels

```text
Top bar
Left navigation
Right asset detail panel
Bottom action queue
Command Center side panel
Notification stack
```

## 33.3 MVP UI Features

```text
Map block selection
Action buttons
Action queue
Commit turn
Turn result cards
Leaderboard
AI advisor chat
Basic team chat
Basic proposal list, if team mode included
```

---

# 34. UI Architecture Recommendation

The game client should use:

```text
Babylon.js for map rendering
React for UI overlay
TypeScript for shared models and UI logic
SignalR client for real-time updates
State store for company/game state
```

## 34.1 Recommended Client Structure

```text
client/
└── src/
    ├── game/
    │   ├── engine/
    │   │   ├── BabylonGame.ts
    │   │   ├── SceneFactory.ts
    │   │   └── CameraController.ts
    │   │
    │   ├── map/
    │   │   ├── BasinMap.ts
    │   │   ├── BlockMeshFactory.ts
    │   │   ├── AssetMarkers.ts
    │   │   └── MapLayerController.ts
    │   │
    │   ├── ui/
    │   │   ├── layout/
    │   │   │   ├── GameShell.tsx
    │   │   │   ├── TopBar.tsx
    │   │   │   ├── LeftSidebar.tsx
    │   │   │   ├── RightPanel.tsx
    │   │   │   └── BottomActionBar.tsx
    │   │   │
    │   │   ├── panels/
    │   │   │   ├── BlockPanel.tsx
    │   │   │   ├── DiscoveryPanel.tsx
    │   │   │   ├── FieldPanel.tsx
    │   │   │   ├── CompanyDashboard.tsx
    │   │   │   ├── FinancePanel.tsx
    │   │   │   ├── MarketPanel.tsx
    │   │   │   └── LeaderboardPanel.tsx
    │   │   │
    │   │   ├── command-center/
    │   │   │   ├── CommandCenterPanel.tsx
    │   │   │   ├── AiAdvisorPanel.tsx
    │   │   │   ├── TeamChatPanel.tsx
    │   │   │   └── ActionBoardPanel.tsx
    │   │   │
    │   │   └── common/
    │   │       ├── MetricCard.tsx
    │   │       ├── RiskBadge.tsx
    │   │       ├── ConfidenceMeter.tsx
    │   │       ├── ActionButton.tsx
    │   │       └── Tooltip.tsx
    │   │
    │   ├── state/
    │   │   ├── gameStore.ts
    │   │   ├── companyStore.ts
    │   │   ├── actionStore.ts
    │   │   ├── aiStore.ts
    │   │   └── uiStore.ts
    │   │
    │   └── net/
    │       ├── ApiClient.ts
    │       ├── GameHubClient.ts
    │       └── AiAdvisorClient.ts
```

---

# 35. Example Main Screen Wireframe

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│ Beep Energy | Cash $420M | Debt $100M | Oil $75 | Turn 6/20 | Rank #2      │
├──────────────┬───────────────────────────────────────────────┬──────────────┤
│ Map          │                                               │ Block D-08   │
│ Company      │              2.5D Basin Map                   │ Owner: You   │
│ Assets       │                                               │ Stage: 2D    │
│ Finance      │       [D-01] [D-02] [D-03]                    │ CoS: 34%     │
│ Market       │       [D-04] [D-05] [D-06]                    │ Risk: Trap   │
│ Leaderboard  │       [D-07] [D-08] [D-09]                    │              │
│ Command      │                                               │ Actions:     │
│ Center       │                                               │ Drill Well   │
│              │                                               │ Ask AI       │
├──────────────┴───────────────────────────────────────────────┴──────────────┤
│ Actions 2/3: Drill Falcon-1 | 2D Seismic D-04 | Cash After: $365M | Commit  │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

# 36. Example Command Center Wireframe

```text
┌──────────────────────────────────────────────┐
│ Command Center                              │
├──────────────────────────────────────────────┤
│ Tabs: AI Advisor | Team Chat | Action Board │
├──────────────────────────────────────────────┤
│ Advisor: Geologist                          │
│ Context: Block D-08                         │
│                                              │
│ Player: Should we drill this turn?          │
│                                              │
│ AI: Situation: Block D-08 has a 34%         │
│ estimated chance of success.                │
│                                              │
│ Risks: Main risk is trap closure.           │
│                                              │
│ Recommendation: Drill only if you can       │
│ absorb a $40M dry hole.                     │
│                                              │
│ [Create Proposal] [Ask CFO]                 │
├──────────────────────────────────────────────┤
│ Ask a question...                     Send  │
└──────────────────────────────────────────────┘
```

---

# 37. Design Risks

## 37.1 UI Becomes Too Complex

Solution:

```text
Use progressive disclosure.
Show summaries first.
Use panels and tabs.
Use AI to explain.
Keep MVP screens limited.
```

---

## 37.2 Players Do Not Know What To Do Next

Solution:

```text
Show recommended next actions.
Use tutorial missions.
Use AI strategy advisor.
Use clear action buttons.
Highlight available actions.
```

---

## 37.3 Too Much Data, Not Enough Meaning

Solution:

```text
Use interpretation text.
Use risk badges.
Use simple charts.
Use AI summaries.
Prioritize decision-relevant data.
```

---

## 37.4 Turn Results Feel Boring

Solution:

```text
Use result cards.
Use reveal animations.
Highlight discoveries.
Show financial impact.
Show leaderboard movement.
```

---

## 37.5 AI Panel Feels Detached

Solution:

```text
Add Ask AI buttons to every asset panel.
Pass selected context to AI.
Let AI create proposals.
Show AI reports after turn resolution.
```

---

# 38. Open Questions

1. Should the first UI use React, plain TypeScript, or another UI framework?
2. Should Command Center be a right drawer or bottom drawer?
3. Should the map be isometric 2D or low-poly 3D for MVP?
4. Should financial details be shown in one dashboard or separate finance screen?
5. Should the action queue always be visible?
6. Should the tutorial be mandatory for first-time players?
7. Should competitors’ cash and debt be hidden?
8. Should the UI support tablets in MVP?
9. Should the AI panel be available during turn resolution?
10. Should turn results appear as cards, timeline, or report page?

---

# 39. Recommended MVP UI Decision

For MVP, implement this UI:

```text
Main Layout:
- Top bar
- Left sidebar
- Main 2.5D map
- Right detail panel
- Bottom action queue
- Command Center drawer

Core Screens:
- Lobby
- Main game screen
- Company dashboard
- Leaderboard
- Turn results

Core Panels:
- Block panel
- Discovery panel
- Field panel
- Finance summary
- AI advisor panel
- Team chat panel

Core UX:
- Click asset → show panel
- Select action → add to action queue
- Review cost and warnings
- Commit turn
- View results
- Ask AI from any asset
```

This UI direction supports strategy gameplay, multiplayer, team collaboration, and AI-assisted decision-making without overwhelming the first version.
