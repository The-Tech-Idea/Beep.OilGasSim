# 20 — Player Decision Catalogue

**Status:** draft · **Date:** 2026-08-06

> **Affects:** 00, 06, 08, 12, 13, 14, 16, 17, 18 · **Affected by:** 00, 06, 08, 13, 14, 16, 17, 18
> *(strong couplings only — the full row is in [22](22_DESIGN_COHERENCE.md) §2)*

Every decision the player makes, with its horizon, its inputs, its consequences
and its reversibility.

**Every decision below is Advisor-coverable except where marked.** The reality
system ([18](18_GAME_MODES.md) §5b) automates *doing*, never *choosing what the
game is about*: exploration judgement rows (DEX2, DEX3, DEX6, DEX7, DEX10 and
sanction DDV1) are capped at *Advise* — the Advisor runs their arithmetic and
refuses their call.

**This is a design-verification document.** Its purpose is to answer, before any
code exists: *is this actually a game?* A decision that has an obviously correct
answer, or whose consequence is invisible, or which the player has no information
to make, is a design defect — and this catalogue is where those are caught.

---

## 1. The test applied to every row

A decision earns its place only if **all four** hold:

| # | Criterion | If it fails |
|---|---|---|
| **T1** | There is no universally correct answer — it depends on state the player can read | It is not a decision, it is a step. Automate it |
| **T2** | The player has, or can buy, the information to reason about it | It is a coin flip. Add an information source or remove it |
| **T3** | The consequence is observable and attributable | It is invisible. Add an event, a report, or an audit query |
| **T4** | It composes with other decisions rather than standing alone | It is a side quest. Couple it or cut it |

Every row below has been checked against these. Rows that failed are recorded in
§7 with what was done about them.

**Horizon** = how long until the consequence lands. **Reversibility**: `R`
reversible · `C` costly to reverse · `I` irreversible.

---

## 2. Exploration decisions

| # | Decision | Horizon | Key inputs | Consequence | Rev |
|---|---|---|---|---|---|
| DEX1 | Which basin to enter | Years | Fiscal regime, environment, infrastructure, regional prospectivity | Sets every subsequent cost and constraint | C |
| DEX2 | Bid or pass on a block | Years | POS, volumetrics, terms, work commitment, rivals | Acreage, obligations, bond at risk | I |
| DEX3 | How much to bid | Years | Own valuation, expected rival bids, budget | Win at a loss, or lose at a profit | I |
| DEX4 | Which survey to buy | Months | Which POS factor dominates risk, cost, value of information | Variance reduction on specific factors | I |
| DEX5 | Survey now or drill now | Months | Value of information vs its cost and delay, licence clock | Information versus time and cash | C |
| DEX6 | Which prospect to drill first | Months | POS × volume × development cost; play learning value | **The highest-learning prospect is not always the highest-value one** | I |
| DEX7 | Drill or farm out | Months | Cash, risk appetite, portfolio concentration | Half of something versus all of nothing | I |
| DEX8 | Core, log, or test | Days | Which uncertainty binds the next decision | Data quality versus cost and rig time | I |
| DEX9 | After a dry hole: re-rank or exit | Months | Which element failed, remaining commitment, sunk cost | **The sunk-cost trap, made explicit** | C |
| DEX10 | Appraise more or sanction now | Months–years | Estimate spread, development cost, price outlook, licence clock | Certainty versus time-to-cash | C |
| DEX11 | Re-screen with the new tier, or push the frontier | Months–years | Newly detectable classes over held acreage (free ground, known infrastructure) versus frontier POS; re-processing cost versus new acquisition | **The same map, new eyes — imaging is an exploration lever** ([06](06_WORLD_AND_EXPLORATION.md) §2.3a) | C |

**DEX6 is the catalogue's best decision.** Drilling the prospect that teaches you
most about the play can beat drilling the one with the highest individual value —
and knowing when that is true is real exploration skill.

---

## 3. Development decisions

| # | Decision | Horizon | Key inputs | Consequence | Rev |
|---|---|---|---|---|---|
| DDV1 | Sanction or wait | Years | Reserves estimate, price and cost cycle, capital | **Sanctioning at a cycle peak is the classic value-destroying error** | I |
| DDV2 | Development concept | Years | Volumes, setting, export options, phasing | Sets the cost base for the field's whole life | I |
| DDV3 | Facility sizing | Years | Peak rate forecast, plateau length, water forecast, expandability | Undersize and defer production; oversize and waste capital | C |
| DDV4 | Number and type of wells | Years | Reservoir connectivity, recovery, well cost, rig availability | Recovery factor versus capital | C |
| DDV5 | Vertical or horizontal | Months | Reservoir geometry, contact gain, cost, water risk | Productivity versus cost and water risk | I |
| DDV6 | Which zones to perforate | Months | Zone quality, water and gas proximity, commingling | Rate now versus water later | C |
| DDV7 | Tubing size | Months | Expected rate profile over life | Friction-limited or liquid-loaded. **The right answer changes with time** | C |
| DDV8 | Lift method, **which tier**, and when | Months | Rate, depth, deviation, GOR, water cut, power; **tier datasheets — the proven mid-tier versus the early-generation top tier** ([07](07_TECHNOLOGY.md) §4b.3) | Deferred production versus premature capital versus reliability risk | C |
| DDV9 | Export route | Years | Volume, distance, tariffs, capacity, counterparty risk | Own pipeline versus tariff versus trucking | I |
| DDV10 | Gas strategy | Years | Gas volume, market, flaring rules, pressure support value | **Sell, re-inject, or flare — three strategies, all defensible** | C |
| DDV11 | Storage capacity | Years | Production rate, lifting frequency, weather risk | Insurance against shut-in versus capital | C |
| DDV12 | Power source | Years | Grid availability, load, fuel gas value, emissions | Capital, emissions, reliability | C |
| DDV13 | Phasing | Years | Cash constraints, uncertainty, learning | Capital efficiency versus time-to-plateau | C |

**DDV3 deserves emphasis** because it is where the bottleneck report pays off. A
player who undersized a separator sees exactly what it cost them every month, in
barrels, and can compute whether expansion pays.

---

## 4. Production decisions

| # | Decision | Horizon | Key inputs | Consequence | Rev |
|---|---|---|---|---|---|
| DPR1 | Which bottleneck to relieve | Months | Deferred volume by cause, cost to relieve, remaining reserves | **The core operations loop** | R |
| DPR2 | Choke settings | Immediate | Rate, drawdown limits, sand risk, shared-line backpressure | Rate now versus reservoir damage and neighbours | R |
| DPR3 | Shut in a weak well or not | Months | Its rate, its backpressure effect on the shared line | **A weak well can cost more than it makes, via the shared line** | R |
| DPR4 | Zonal shutoff versus water handling | Months | Water cut by zone, treatment capacity, cost | The cheapest rung of the water escalation ladder | C |
| DPR5 | Maintenance strategy per asset | Years | Criticality, intervention cost, failure consequence | **Run-to-failure is right for some assets and wrong for others** | R |
| DPR6 | Repair now or defer | Months | Deferred production, repair cost, cascade risk, cash | **The entry point to the maintenance death spiral** | R |
| DPR7 | Workover or leave it | Months | Expected uplift, cost, rig availability, remaining life | Capital versus decline | C |
| DPR8 | Stimulate or not | Months | Skin from well test, expected uplift, cost | Often the highest-return intervention available | C |
| DPR9 | Convert a producer to injector | Years | Its remaining production versus pressure support value | Loses a well, gains recovery | C |
| DPR10 | Infill drilling | Years | Un-drained volume, interference risk, cost | Recovery versus cannibalisation | I |
| DPR11 | Cargo scheduling | Weeks | Tank levels, production forecast, weather, contracts | Shut-in risk versus demurrage | R |
| DPR12 | Spot or term sales | Months | Price outlook, cash certainty need, delivery confidence | Upside versus certainty; take-or-pay exposure | C |
| DPR13 | Accept off-spec or treat | Months | Rejection cost, treating capital | Build the unit or lose the sales | C |
| DPR14 | Abandon or persist | Months | Economic limit, abandonment cost, shared infrastructure, contracts | **Killing it triggers a large bill; keeping it is a slow bleed** | I |

**DPR3 and DPR6 are the two most instructive.** Both look locally obvious and are
frequently wrong: the weak well seems harmless, and deferring one repair seems
prudent.

---

## 5. Company decisions

| # | Decision | Horizon | Key inputs | Consequence | Rev |
|---|---|---|---|---|---|
| DCO1 | Capital allocation | Years | Portfolio returns, risk, reserve replacement need | **The master decision — everything else competes here** | C |
| DCO2 | Explore or develop | Years | RRR, current cash flow, prospect inventory | **Below RRR 1.0 the company is liquidating itself** | C |
| DCO3 | Debt or equity or farm-out | Years | Borrowing base, cost of capital, dilution, risk | Control versus risk versus cost | C |
| DCO4 | Hedge or ride the market | Months | Price outlook, cash needs, covenant headroom | Certainty versus upside | C |
| DCO5 | Technology: build, license, or contract | Years | Capability need, scale, cost profile | Own it, rent it forever, or never own it | C |
| DCO6 | R&D direction and funding | Years | Portfolio needs, era, competitive position | Long-horizon capability, uncertain outcome | R |
| DCO7 | HSE investment level | Years | Barrier status, incident exposure, ESG effect on capital | **Cheap until it isn't; a fat tail the player controls** | C |
| DCO8 | Emissions: reduce, offset, or accept | Years | Carbon price, caps, ESG effect, capital cost | Cost now versus constraint later | C |
| DCO9 | Crew size and rotation | Months | Workload, fatigue, cost | **Lean crewing is cheaper and raises the human-error rate** | R |
| DCO10 | Rig contracting strategy | Years | Programme, day rates, market tightness | Committed cost versus availability when needed | C |
| DCO11 | Which jurisdictions to operate in | Years | Fiscal terms, HSE regime, stability, prospectivity | Sets the rules you play under | C |
| DCO12 | Community investment | Years | Social licence standing, permit needs | Access and permits versus cost | R |
| DCO13 | Relinquish or retain acreage | Years | Remaining prospectivity, commitment cost, rentals | Optionality versus carrying cost | I |
| DCO14 | Abandonment timing | Years | Provision adequacy, regulatory deadline, asset reuse | Cash timing versus obligation | I |
| DCO15 | Buy reserves, or find them | Years | `rival.assetOffer` price versus own valuation, finding cost history, RRR gap | **Acquisition adds reserves at market price; exploration at finding cost — the gap is the skill** | I |

**DCO2 is the decision the whole game is built to make interesting.** Cash flow
always argues for development; the future always argues for exploration; and RRR
is the number that says which one is currently wrong.

---

## 5b. Environment and HSE decisions

Added in the third pass. These were absent, which meant two whole subsystems had
no entry in the catalogue that proves the game has depth.

| # | Decision | Horizon | Key inputs | Consequence | Rev |
|---|---|---|---|---|---|
| DEN1 | Commit to a seasonal window, or wait | Weeks–1 year | Window remaining, readiness, licence clock, forecast confidence | **Miss it and lose a year while the licence clock runs** | I |
| DEN2 | Buy schedule buffer, or accept weather risk | Months | Forecast spread, downtime cost, standby rates | Certainty versus cost | C |
| DEN3 | Design for the setting, or minimise capital | Years | Winterisation, foundations, corrosion allowance, cooling margin | Cheap now, expensive for forty years | I |
| DEN4 | Mitigate flow assurance, or manage it | Months | Ambient temperature, water, composition, blockage hazard | Insulation and inhibitor capital versus pigging and blockage risk | C |
| DHS1 | Which barriers to fund, and to what strength | Years | Barrier status, threat rates, consequence severity | **The fat tail the player controls** | C |
| DHS2 | Investigate a near miss, or carry on | Weeks | Near-miss pattern, investigation cost, barrier status | **The cheapest information in the game, and the easiest to ignore** | R |
| DHS3 | Metallurgy for current service, or for likely future service | Years | Current H₂S, souring risk from planned waterflood, cost premium | **Souring years later makes the cheap choice wrong** | I |
| DHS4 | Reduce emissions, buy allowance, or curtail | Years | Carbon price trajectory, cap headroom, abatement cost, ESG effect on capital | Cost now versus constraint and cost-of-capital later | C |

**DHS2 and DHS3 are the two worth emphasising.** A near miss costs nothing and
points precisely at a weakening barrier — ignoring it is locally free and exactly
how the maintenance spiral is entered. And DHS3 is the game's longest-arc
decision: choosing carbon steel for a sweet reservoir is correct today and wrong
after fifteen years of waterflood, and the information to see it coming
(water chemistry) is purchasable in advance.

---

## 6. Decision density by stage

A check that no stage is empty and no stage is overloaded.

| Stage | Primary decisions | Density | Character |
|---|---|---|---|
| Startup | DEX1–DEX5, DCO3, DCO11 | Low count, very high stakes | Every choice is existential |
| Exploration | DEX4–DEX9, DCO2 | Moderate, repeating | A learning loop |
| Appraisal & sanction | DEX10, DDV1–DDV2, DCO1, DCO3 | Low count, highest stakes | The biggest bets of the game |
| Development | DDV3–DDV13, DEN1–DEN4, DCO10 | **High** | The design and optimisation game |
| Plateau | DPR1–DPR3, DPR5, DPR11–DPR12, DHS1–DHS2, DCO4, DCO7 | Moderate, steady | Management |
| Decline | DPR4, DPR6–DPR10, DPR13–DPR14, DHS3–DHS4, DCO2 | **High** | Triage and reinvestment |
| Maturity | DCO1–DCO2, DCO13–DCO14, DEX1 | Moderate, strategic | Replace reserves or wind down |

**Two peaks — development and decline — with quieter periods between.** That
rhythm is what real-time-with-pause ([15_TIME](15_TIME_AND_EXECUTION.md) §2) is
designed to serve: the quiet stretches pass quickly, the dense ones are paused
through.

---

## 7. Candidates that failed the test

Recorded so the exclusion is a decision, not an omission.

| Candidate | Failed | Resolution |
|---|---|---|
| "Choose mud weight while drilling" | T2 — the player has no basis to reason about it | Folded into the drilling operation's risk profile and its equipment/competency inputs |
| "Set separator operating pressure" | T1 — a near-universally correct answer given the fluid | **Automated**, with multi-stage design (DDV3) as the real decision |
| "Approve each purchase order" | T1, T4 — no judgement, no coupling | Removed. Costs accrue from operations |
| "Choose a pipeline route metre by metre" | T1 — the optimum is computable | Reduced to a corridor choice with cost/environment trade-offs (DDV9) |
| "Hire individual employees" | T3 — individual effect is invisible | Replaced by crew size, discipline mix and rotation (DCO9) |
| "Set flare pilot gas rate" | T3 — consequence too small to see | Removed. Absorbed into fuel gas |
| "Pick a seismic contractor" | T1 — a price/quality choice with a dominant answer | Removed. Survey quality is a technology and content matter |
| "Respond to each regulatory letter" | T4 — stands alone | Folded into HSE findings, which gate restart (DPR6 adjacency) |

**The pattern in the failures:** almost every one was a decision with a correct
answer the player would learn once and then execute forever. That is a chore.
**A decision belongs in a game only if a skilled player still has to think about
it on the hundredth encounter.**

---

## 8. Verification

| # | Test | Passes when |
|---|---|---|
| PD1 | Every decision has a command | Each row maps to at least one `ICommand` |
| PD2 | Every decision has inputs in the read model | The information named is available to the host |
| PD3 | Every decision has an observable consequence | Each maps to at least one event or report ([16](16_EVENT_MATRIX.md) EM8) |
| PD4 | No dominant strategy | For each decision, at least two scripted playthroughs exist where different choices win |
| PD5 | Density profile | Decision counts across SC1 match §6 within tolerance |
| PD6 | Irreversibility is real | `I` decisions cannot be undone except by save reload |
| PD7 | Information sufficiency | For every `T2`, a purchasable information source exists that materially improves the decision |

**PD4 is the hardest and most valuable test.** It is what proves the game has
depth rather than a solution, and it is worth the effort of writing the scripted
playthroughs.

---

## 9. Open decisions

| # | Decision | Options | Recommendation |
|---|---|---|---|
| PD-D1 | Automation of repetitive choices | (a) none, (b) player-defined rules for routine decisions | **✅ Resolved — subsumed by the Advisor** ([18](18_GAME_MODES.md) §5b): per-domain Manual / Advise / Confirm / Auto levels; player-defined rules are the *Auto* level's policy |
| PD-D2 | Decision support depth | (a) raw data, (b) computed recommendations | **✅ Resolved — the Advisor's *Advise* level** ([18](18_GAME_MODES.md) §5b.2), keeping this row's line intact: arithmetic is recommended with reasoning shown; exploration judgement is never automated |
| PD-D3 | Undo | (a) none, (b) undo within the current tick before advancing | **(b)** — mis-clicks are not gameplay; committed ticks are final |
| PD-D4 | Difficulty via decision count | (a) same decisions at all levels, (b) more automated at lower levels | **✅ Resolved — the assist axis** ([18](18_GAME_MODES.md) §5b): presets set per-domain Advisor levels, so lower difficulty *is* more automation over the same decisions — never fewer real decisions in the world |
