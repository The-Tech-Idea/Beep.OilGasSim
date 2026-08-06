# Phase R16 — Company, Licences and Regulation

**Arc III** · Status ⬜ · Depends on: R13, R15 · Enables: R20

---

## 0. Purpose

The wrapper around everything: the right to operate, the obligation that comes
with it, the rivals competing for it, and the regulator watching.

**This is the phase that makes exploration urgent** rather than optional.

---

## 1. Goals

| # | Goal | Acceptance |
|---|---|---|
| G1 | Licences expire and oblige | Term, work commitment and relinquishment all bind; failing a commitment forfeits the bond |
| G2 | Rounds are competitive | Rivals bid; losing a wanted block is possible and stings |
| G3 | Rival results are public data | A rival's well updates the player's play beliefs |
| G4 | Regulation has teeth | Inspections, penalties and licence risk are real consequences |
| G5 | Flaring caps limit production | The R9-V8 behaviour is driven by real jurisdictional content |
| G6 | Environmental rules bind | Emissions caps, flaring rules and discharge standards are jurisdiction content that constrains the HSE state R23 owns |

---

## 2. Design decisions

### 2.1 The licence clock is the game's pacing mechanism

A licence has a term, a work commitment and a relinquishment schedule. Sitting on
acreage doing nothing loses it and forfeits the bond.

*Rationale:* it converts "should I explore?" into "I must explore — where, and
how much can I afford?" That is a much better question, and it manufactures
urgency without an artificial timer.

### 2.2 Rivals are simple and their *results* are the point

Rivals bid using a valuation model over their own beliefs, drill, and succeed or
fail. **They do not need to be sophisticated operators** — their function is to
make rounds competitive and to generate public data.

*Rationale:* full AI operators are a large subsystem for modest gameplay return
(open decision D4). Bidding rivals plus public results deliver most of the value
at a fraction of the cost, and a fuller AI can be added later behind the same
contract.

### 2.3 The regulator is a jurisdiction property

Inspection frequency, penalty severity, flaring rules, emissions limits, spill
liability and abandonment requirements are all `jurisdiction` content.

**Consequence:** jurisdictions become a real strategic axis. A permissive
jurisdiction with a harsh fiscal regime versus a strict one with generous terms
is a genuine choice, and it is authored entirely in content.

### 2.4 Environmental liability is accrued, not just fined

A spill creates a cleanup obligation — an operation with a cost and a duration —
not merely a one-off penalty. Emissions accrue against a cap. **Damage persists**
and affects licence renewal and future round eligibility.

*Rationale:* one-off fines are trivially absorbed by a profitable company and
teach nothing. A persistent record that affects access to acreage has real
weight.

### 2.5 Working interests and farm-outs run through the licence

A licence is held by one or more parties with declared interests. Costs and
revenues split accordingly (R13.9); the operator is one party. Farm-outs transfer
interest for cash, a carry, or both.

### 2.6 The jurisdiction owns the regimes

A `jurisdiction` is where the fiscal regime, the **HSE regime**
([R23](R23_HSE.md)) and the environmental rules all attach. R16 owns the
jurisdiction entity and the regulator; R23 owns the barrier and emissions
machinery those rules govern.

**The split follows law L5:** one owner per fact. The rule set is
company/jurisdiction state; the safety and emissions state it constrains is HSE
state. Two documents naming different owners for one fact is the defect this
separation prevents.

### 2.6b The asset market

R16 implements [08](../design/08_ECONOMICS.md) §5b: rivals periodically offer
discovered-but-undeveloped assets and stakes, priced from their own noisy
beliefs. Acquisitions transfer operatorship to the player; minority stakes are
passive cost-and-revenue lines. This is the *acquire / farm in* exit the
liquidation spiral requires (CI4) — without it, a company below RRR 1.0 whose
exploration is failing has only one exit, and CI-V12 would fail.

### 2.7 Events this phase raises

`licence.roundAnnounced` · `.bidResult` · `.commitmentDue` · `.expiring` /
`rival.result` · `reg.*`. Four are `D` severity — they carry a deadline, and
**an expired `D` event applies its declared default and publishes that fact**
(EM7), so a licence is never lost silently.

---

## 3. Deliverables

`OGSim.Company` extension: `ICompany`, `ILicence`, `IWorkCommitment`, licence
rounds and bidding, rival operators and public data publication, `IRegulator`,
inspections and penalties, flaring caps and their production coupling, and the
`jurisdiction` rule set that governs them.

**Not here:** the emissions ledger, incident record, barriers and ESG standing
live in `OGSim.Hse` ([R23](R23_HSE.md)). R16 owns the *rules*; R23 owns the
*state those rules constrain*.
Content: `jurisdiction` catalogue with regimes, rules and round schedules.

---

## 4. Verification

| # | Test | Passes when |
|---|---|---|
| R16-V1 | Licence expiry | An expired licence is lost; held assets are handled per the declared rules |
| R16-V2 | Work commitment | Failing it forfeits the bond; satisfying it does not |
| R16-V3 | Relinquishment | Unretained acreage is released on schedule |
| R16-V4 | Competitive bidding | Rivals bid; the player can lose a block; bids reflect rival beliefs |
| R16-V5 | Public data | A rival's result updates the player's beliefs about the shared play |
| R16-V6 | Inspection | Occurs at the declared frequency; violations are detected and penalised |
| R16-V7 | Flaring cap (SC-link to R9-V8) | A jurisdictional cap limits oil production through the gas system |
| R16-V8 | Spill | Creates a cleanup obligation and a persistent record |
| R16-V9 | Emissions accrual | Emissions accumulate against the cap; exceeding it has the declared consequence |
| R16-V10 | Jurisdiction variation | The same field in two jurisdictions produces materially different outcomes |
| R16-V11 | Working interests | Costs and revenues split correctly and sum to 100% |

---

## 5. Out of scope

Full AI operators running their own developments (deferred). Political risk,
expropriation, war (a possible later addition). Corporate M&A.

---

## 6. Risks

| Risk | Mitigation |
|---|---|
| Rival bidding is exploitable | Rivals bid from their own noisy beliefs with a valuation spread; the player cannot see their number |
| Regulation feels like arbitrary punishment | Every rule is visible in the jurisdiction before a bid; the player chooses their jurisdiction |
| Licence clocks create unwinnable positions | Relinquishment is partial and staged; farm-out is always available as an exit |
| Environmental modelling becomes a morality system | It is modelled as cost, liability and access — not as approval. The player's incentive to be clean is economic |
