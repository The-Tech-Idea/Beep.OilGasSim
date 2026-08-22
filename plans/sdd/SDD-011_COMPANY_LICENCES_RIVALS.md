# SDD-011 — Company, Licences and Rivals

**Status:** drafted · **Serves:** R16 · **Design docs:** [08](../design/08_ECONOMICS.md) §5b, [R16](../phases/R16_COMPANY.md), [06](../design/06_WORLD_AND_EXPLORATION.md) §6

The licence machinery and — the part that must be pinned before anyone "just
writes an AI" — **the rival model: rivals play by the player's rules, through
the player's machinery, with no access to truth.**

---

## 1. Licences

```csharp
// The live licence — already committed, because a well references one from R6
// onward. Deliberately thin: everything negotiable is in the terms below, and
// everything spatial is in the read model's ExplorationView (SDD-017).
public interface ILicence
{
    EntityId<ILicence> Id { get; }
    ContentId FiscalRegime { get; }
    Tick Expiry { get; }
}

public sealed record CommitmentItem(
    ContentId Kind,                          // e.g. exploration-well, seismic-2d
    double Quantity,                         // wells, or km²
    Tick Due);

public sealed record RelinquishmentStep(double Fraction, Tick Due);

public sealed record LicenceTerms(           // from jurisdiction content
    int TermMonths,                          // /30ths-grid whole months, not a Duration
    IReadOnlyList<CommitmentItem> WorkCommitment,
    Money Bond,
    IReadOnlyList<RelinquishmentStep> Relinquishment,
    ContentId FiscalRegime,
    ContentId HseRegime);
```

> **Contract pass 10.** `LicenceTerms` referenced `CommitmentItem` and
> `RelinquishmentSchedule`, neither declared anywhere; the schedule is a list of
> (fraction, date) steps rather than a type of its own, which is what §1's own
> "fraction at dates" comment describes. `Term` was a `Duration` — a `double` of
> days (SDD-001 §1) — where a licence term is a whole number of months on the
> calendar; the same defect SDD-007 §1 had with `BaseDuration`.
>
> `ILicence` itself is declared here for the first time although it has been in
> the code since the contract layer, because `IWell.Licence` needs it from R6.
> **It currently sits in `InformationContracts.cs`, which is the wrong file** —
> it is a company/licence type, not a belief one. Moving it is an R16 task and
> is noted in §7 rather than done now: the type is right, only its address is
> wrong, and moving a public type is a change worth making where the tests that
> cover it are being written.

Commitment tracking is mechanical: qualifying completed operations decrement
items; at deadline, unmet ⇒ bond forfeit + licence loss (`licence.*` events,
all `D`-severity with the EM7 default: forfeit — announced twice, never silent).
Rounds: generated on the jurisdiction's cadence; blocks are polygon partitions
of open acreage.

> **R20d.9 amendment. The join, against ONE licence — because this
> composition has one field.**
>
> `Licence` has been complete and constructed nowhere: no test, no
> composition. §1's machinery is real and untouched by this amendment; what
> was missing is everything downstream of "a company holds one."
>
> **`Well.Licence` returns the composition's ONE licence directly**, rather
> than each well carrying an independent reference that happens to agree.
> §1's own words are "already committed, because a well references one from
> R6 onward" — written for a company that may hold several blocks. This
> composition generates one field ([R20d.8](../phases/R20d_INTEGRATION.md)),
> so there is exactly one licence to reference; a per-well field would be a
> second place storing an answer the composition already knows, and a
> multi-licence company is R20's content, not a reason to shape the join
> around a case this engine cannot produce yet.
>
> **Terms are ONE hand-authored `Defaults.LicenceTerms`**, on the same
> footing as `Defaults.Climate` and `Defaults.Eras`: this composition ships a
> single instance of every mechanic before R20's content pass gives it a
> catalogue, and a licence is not different in kind from a climate. The
> numbers are chosen proportionally to what already exists rather than
> invented from nothing — the bond is a multiple of one well's cost
> (`Defaults.DrillWellTerms.Cost`), the commitment is drilling one well
> within a runway a real campaign needs, the term spans the composition's own
> forty-year test horizon. **`Relinquishment` is the empty list, stated
> rather than padded**: this field is `DeclareKnownField`d from the first
> tick (SDD-010 §4b), so there is no unexplored acreage to hand back, and a
> non-empty schedule would be a control this composition has nothing for it
> to control. **`HseRegime` carries a placeholder id with no consumer yet** —
> R16.6's own row already says the rules it would name are R23's; the field
> is required by the record and this states plainly that naming it does not
> mean enforcing it (rule 7's own test: what reads this to make a decision).
>
> **`RecordDelivery` fires from the activity that completes the commitment**,
> matched by `CommitmentItem.Kind == ActivityTerms.Template` — the same
> convention a facility rung's `requiresTech` matches a registry node's id
> by. `DrillWellActivity.Complete` calls it on a successful hole; a dry hole
> delivers nothing, which is correct — the commitment is to drill a well
> that stands, not to spend the money trying.
>
> **Assessment runs at `StageId.Company` (11), which no module has ever
> contributed to.** Not a new stage: the fourteen-stage order has carried
> this slot since design 03 §6 and it has sat empty. On loss: the bond posts
> as `Account.Penalty` against `Account.Cash` under
> `MovementCategory.Contractual` — both declared in the ledger's own
> `Causes` list since R21 §2.4b and posted to by nothing until now.
>
> **"Announced twice" is scoped down FURTHER than first written here, and the
> reason is worth recording alongside the first cut.** This amendment
> originally proposed publishing one `Severity.Decision` `EngineEvent` at the
> moment of forfeiture. Checked while implementing rather than assumed:
> **no concrete `EngineEvent` subtype exists anywhere in this engine.**
> `EngineEvent` is abstract, `EventBus.Publish` has never been called by any
> module, and design 16's own ordering fields — `Day`, `LoopRole`,
> `IsSegmentBoundary` — have no worked example to build the first one
> against. Building the engine's FIRST concrete event correctly, from a
> design section this task has not fully absorbed, is its own unscoped
> undertaking and not a footnote — bolting one on half-understood risks
> violating design 21 §5.3's ordering invariants in a way nothing would
> catch, which is worse than not publishing one.
>
> **So this join uses the ESTABLISHED door instead: the audit trail.** The
> bond forfeit posts as a `Movement` whose `Cause` is an `AuditId` from
> `_audit.Record(AuditCategory.Financial, ...)` — the same mechanism every
> other financial consequence in this composition already goes through, and
> `Financial` is one of the categories `AuditTrail.Prune` keeps forever
> (finding 236). "Never silent" is satisfied by a channel that demonstrably
> works today, rather than claimed of one this task would be inventing from
> nothing. The `EventBus`/EM7 half — for THIS or any other mechanic — is
> genuinely unbuilt and is design 16's own task.
>
> **A lost licence refuses further drilling and nothing already standing.**
> `DrillWellActivity.OwnRefusals` checks `licence.IsLive`; wells already
> producing keep producing, because losing the right to develop further is
> not the same fact as losing what has already been developed, and design
> 02 §3.4's diagram routes every terminal state through `Abandoned` — a
> licence loss is not one of its edges into that state, and inventing a
> forced-abandonment consequence here would be a second, uninvited
> mechanic.

> **R16 amendment (finding 254): `Expiry` was in the contract from the first
> draft and had no reader.** §1 declares `ILicence.Expiry` and `Licence`
> computes it (`Granted + TermMonths`) and answers `HasExpiredBy(Tick)`
> correctly — proved by its own unit test — but nothing in `OGSim.Composition`
> ever calls it. `Defaults.LicenceTerms.TermMonths` is 480, chosen because "the
> term spans the composition's own forty-year test horizon" (the R20d.9
> amendment above), which makes the coincidence pointed: the clock was sized
> to matter and then never started.
>
> **Expiry is not a second name for commitment loss, and the two must not
> share one meaning.** §1's prose — "at deadline, unmet ⇒ bond forfeit +
> licence loss" — describes ONE commitment item reaching its OWN due date,
> which for the shipped licence is tick 60, far inside the 480-month term.
> Reaching `Expiry` with every commitment met is the opposite case: the
> promise was kept and the clock still ran out, which is not a broken promise
> and forfeits no bond — a company that drilled its one well by tick 60 and is
> still producing at tick 480 did nothing wrong. Reusing
> `DrillWellActivity`'s existing refusal text ("the work commitment went
> unmet and the bond was forfeited") for this case would tell a compliant
> company it broke a promise it kept.
>
> **The consequence is the same as commitment loss, and that reuse IS
> correct**: `IsLive` already means "can this licence still authorise new
> development", not "did the company keep its promise" — nothing reads it as
> the second question, and refusing further drilling is the one edge
> [design 02](../design/02_DOMAIN_MODEL.md) §3.4 gives a licence loss into
> `Abandoned`, exactly as the R20d.9 amendment above already argued for
> commitment loss. Wells already producing keep producing.
>
> `Licence` gains a reason and a second, independent transition:
>
> ```csharp
> public enum LicenceLossReason { CommitmentUnmet, Expired }
>
> // Null while IsLive; set exactly once, by whichever transition fires first.
> public LicenceLossReason? LossReason { get; }
>
> // Mirrors AssessAt's own shape: a one-time transition, called every tick,
> // safe to call after the licence is already lost (a no-op then). Checked
> // AFTER AssessAt, never before — the same "failure before expiry"
> // precedence Scenario.Overall already uses for objectives (SDD-014 §5a),
> // because a company that broke its promise the same month the clock ran
> // out is told the truer of the two reasons.
> public bool ExpireIfDue(Tick now);
> ```
>
> `LicenceStage.Execute` calls `AssessAt` first as it does today, then
> `ExpireIfDue` only if the licence is still live — and records an
> `AuditCategory.Financial` entry on a fresh expiry the same way commitment
> loss does, minus the `Movement`: nothing is owed, so there is nothing to
> post, but "never silent" (EM7) applies to the fact of losing the licence,
> not only to the cost of losing it. `DrillWellActivity`'s refusal reads
> `licence.LossReason` and states the one that actually happened.

## 2. Rivals — the architectural rule

> **A rival is a policy over beliefs, never a reader of truth.** Rivals hold
> their own `TechnologyState` (S005-1), buy surveys through the same
> `IInformationSource` door, maintain beliefs with the same SDD-008 machinery,
> and act through the same command bus. The architecture test that keeps truth
> `internal` to Information protects the player from cheating rivals *by
> construction* — there is no rival-specific data path to audit.

This costs little (rivals are few, their belief sets coarse) and buys the
fairness claim outright: `rival.result` events are real drilling outcomes of
real beliefs.

### 2.1 Rival policy (deliberately simple — R16 §2.2)

```text
Per rival, content personality: {aggressiveness a, techLag ticks, budget policy}
Rival tech (TD2): a rival acquires each node by DIFFUSION at (node era start +
techLag) — deterministic, no draws, personality-differentiated. Leaders have
small lags; laggards large; the player races real clocks, not dice.
Each round: for each block, EMV from THEIR beliefs (SDD-008 §5 volumetrics ×
their POS × a coarse development template NPV) → bid = EMV · a · U where
U ~ Uniform(0.8, 1.2) from the `market` stream (audited).
Sealed-bid, highest wins, ties by lowest company id. They survey/drill on a
simple value-ranking loop with their budget; their results publish (§3).
```

## 3. Public data

A rival's completed well/survey publishes an **observation available to all
companies** (industry disclosure): same source machinery, a `public` flag, a
declared extra σ (you read their press release, not their logs). Updates the
player's play-shared Betas exactly like own data — R16-V5 with no new
mechanism.

## 4. The asset market (08 §5b)

```text
Offer trigger: rival RRR < 1 for k years, or cash below policy floor —
evaluated on THEIR books.
Price: their P50 NPV of the asset × (1 − distress discount from policy).
Data room: on purchase, THEIR observations for the asset REPLAY into the
buyer's beliefs through the normal door — and the mechanism already exists:
every observation is audited (SDD-008 §3), so the data room is a filtered
replay of the seller's audit entries. No second observation store, no new
machinery; what you bought is their information, not their conclusions.
Acquisition ⇒ operatorship + obligations transfer (IObligationRegistry re-keys).
Minority stake ⇒ a WorkingInterest row: passive cost/revenue share, no commands.
Carried interest (the farm-out's "carried well"): the carrying party funds the
carried party's cost share until the carry amount (integer cents) exhausts —
tracked per WorkingInterest as remainingCarry, drawn before the carried party's
own cash, ledgered via PartnerPayable. Revenue shares are NEVER carried — only
costs. One field, one rule; the classic "free well" is carry == the well's AFE.
rival.assetOffer: D-severity, deadline, EM7 default = decline.
```

> **Amendment (finding 275) — the PLAYER as the distressed party, the second
> of R13.10's three restructuring findings.** The block above is the
> RIVAL-SIDE asset market — a player buying a distressed rival's assets —
> and it stays exactly as blocked as finding 274's own amendment (SDD-009
> §5) found it: `Rival` is a complete, real design (`OGSim.Company/Rivals.cs`)
> never instantiated anywhere in the composed engine, and the data room's own
> "replay a seller's audit entries" claim does not hold up independently
> either — `IBeliefStore` has no bulk-import path by design, and an audit
> entry does not record enough of an observation to replay one (no subject
> id, no observed value). Named again rather than solved here.
>
> **What IS built is the mirror transaction: the player selling a working
> interest to an abstract partner, design 08 §7's "asset sales"/"forced
> farm-outs" merged from the seller's own side** — the same decision either
> name describes: give up a share of the field's future economics for cash
> now. `WorkingInterest` (`OGSim.Company`) is the SAME row this section's
> own text already named for the rival-side "minority stake" case — one
> type, read from whichever side of the transaction is real. `PartnerPayable`
> gets its first real producer, the same account this section's own text
> already named for the carried-interest case.
>
> **Priced off the SAME DCF walk `Bank`'s own borrowing base already runs**
> (SDD-009 §5): `Bank.Terms.ReserveValue` — the reserves' present value
> BEFORE the advance-rate haircut, `ReserveBasedLending.PresentValueCents`
> exposed uncut since finding 262 — times the fraction sold, at a 25%
> distress discount. **A stated simplification of this section's own "P50
> NPV" wording**: the walk prices 1P/Proved, not a second P50 reserves class
> this composition has never built (SDD-009 §4's own reserves booking is
> 1P/2P/3P by class, and a genuine P50 figure is a materially larger further
> task, named rather than invented here) — the same simplification finding
> 274 used for the covenant's own borrowing base, now reused a second time
> rather than diverging from it.
>
> **`SellWorkingInterestCommand(double Fraction)`** (`OGSim.Composition`):
> refused above a cumulative 50% cap (past it the company has given up
> operatorship in substance, which is R13.10's third finding — a takeover —
> rather than a sale), and refused unless the company is already financially
> distressed — the covenant reading `Curing`/`Amortising`, or cash below
> zero (read the same way `ObjectiveStage.Insolvent` derives it, without
> depending on that stage's own persisted latch, which exists for the
> scenario's verdict and not for gating a command). Both numbers — the 50%
> cap and the 25% discount — were confirmed with Fahad before landing, the
> same gate every other invented number this session has gone through; the
> discount sits inside the real 20-40% range distressed oil and gas asset
> sales commonly trade at.
>
> **The sale proceeds are booked as a capital transaction, not revenue**:
> `Account.Cash` against `Account.Equity`, the same distinction this
> engine's own opening balance draws — the company is not selling a barrel,
> it is selling a share of itself. **Ongoing production is then split every
> tick** (`ProductionLoop.PostEconomics`): every revenue account this stage
> posts — oil sale gross AND gas sale, since a partner's interest is in the
> field and not in one hydrocarbon stream — and the field's own operating
> cost both scale by `(1 − PartnerShare)` for the company's own books; the
> partner's share posts to `Account.PartnerPayable` instead, credited by
> their revenue share and debited by their cost share, so the balance
> tracks what the company owes them net rather than two running totals.
> Royalty and tax stay on the FULL gross, unaffected by any private
> split — a licence's fiscal terms are assessed against the whole field, not
> the company's ownership structure, and the licence holder pays them in
> full. Demurrage stays out of scope, named rather than split — a logistics
> penalty, not the field's own operating cost this amendment prices.
>
> **`WorkingInterest` is the first of R13.10's three levers that needs its
> own persisted state**: unlike the hedge or insurance (finding 272/273,
> stateless — nothing about one tick's settlement depends on another's), a
> sold stake is permanent, so `PartnerShare` is an `IStateOwner`
> (`company.working-interest`) rather than recomputed. `PartnerShare` only
> grows — there is no mechanism to buy a share back, named as a further task
> rather than built, and carried interest's `remainingCarry` deferred-cost
> mechanic this section's own text describes for the rival-side case stays
> unbuilt on the player-side too: this amendment ships a straight
> proportional split, not a carry.
>
> **With no sale ever made, `PartnerShare` is zero and every changed line
> reduces to exactly the single movement it replaces** — proven by the
> entire existing suite staying green unchanged, the same proof finding
> 271's quality differential used for its own "unchanged default" claim.

## 5. Regulator

Inspection scheduling per `hse-regime` cadence ± jitter from `operations`
stream; findings read R23's barrier/violation state; orders gate restart
(R23-V17). Penalties are Movements with cause. Nothing here decides physics —
the regulator observes state and issues consequences.

## 6. Test mapping

R16-V1..V3 (terms mechanics) · V4 (bidding: seeded rivals produce the sealed-
bid outcome; the player can lose) · V5 (public data via the σ-inflated
observation) · V6 (inspections) · V7 (flaring cap end-to-end) · V8/V9
(liability/emissions accrual against R23 state) · V10 (jurisdiction variation)
· V11 (interest splits) · **new R16-V12**: architecture — rival assemblies/types
have no truth access path (the Information `internal` boundary covers them).

## 7. Open items

| # | Item | Trigger |
|---|---|---|
| S011-1 | Rival belief coarseness (full per-prospect vs play-level only) — start play-level + per-bid prospect sampling; measure cost | R16.4 |
| S011-2 | Whether rivals participate in the same licence's working interests as partners (farm-in TO the player) | post-R20; the data model (WorkingInterest) already permits it |
| S011-3 | ~~`ILicence` is declared in the wrong file~~ **Closed at R16.** It lives in `OGSim.Contracts/CompanyContracts.cs`, where the tests that cover it are. The type was always right; only its address was wrong | ✅ |
