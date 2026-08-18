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
> **"Announced twice" is scoped down, and stated as a limit rather than
> claimed in full.** Design 16's EM7 describes a `Decision`-severity event
> published AHEAD of a deadline, that later EXPIRES into its declared
> default and publishes THAT too — an advance-warning-then-expiry pattern.
> Checked rather than assumed: `Severity.Decision` is referenced nowhere in
> the engine except `EventBus`'s own cause-required guard, so no module
> tracks a live deadline and publishes ahead of it — EM7 in full is its own
> unbuilt mechanic, not a small addition to this join. What ships here is the
> half that is achievable without it: the outcome publishes ONCE, at the
> moment of forfeiture, as a `Severity.Decision` event carrying the
> forfeiture as its audited cause, which is genuinely "never silent" — a
> host cannot miss it — without the second, advance half EM7's full text
> promises. Building that is design 16's own task, not this one's.
>
> **A lost licence refuses further drilling and nothing already standing.**
> `DrillWellActivity.OwnRefusals` checks `licence.IsLive`; wells already
> producing keep producing, because losing the right to develop further is
> not the same fact as losing what has already been developed, and design
> 02 §3.4's diagram routes every terminal state through `Abandoned` — a
> licence loss is not one of its edges into that state, and inventing a
> forced-abandonment consequence here would be a second, uninvited
> mechanic.

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
