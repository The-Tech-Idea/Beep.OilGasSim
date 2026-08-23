# SDD-007 — Operations Engine

**Status:** drafted · **Serves:** R12 · **Design docs:** [02](../design/02_DOMAIN_MODEL.md) §7.1, [15](../design/15_TIME_AND_EXECUTION.md) §7, [R12](../phases/R12_OPERATIONS.md), [07](../design/07_TECHNOLOGY.md) §2c

The one scheduled-activity engine: state machine, reservation, accrual
arithmetic, and — the part an implementer would otherwise invent — **when
stochastic outcomes are drawn and how they apply.**

---

## 1. The operation

```csharp
public interface IRig { }                    // resource marker; calendars live in the scheduler

public sealed record ResourceNeeds(
    EntityId<IRig>? Rig,                     // null: no rig needed (a survey, a study)
    IReadOnlyList<(ContentId Discipline, int Count)> Crew);

public sealed record OperationSpec(          // from an operation-template content entry
    ContentId Template,
    EntityRef Target,
    int BaseDurationDays,                    // whole days on the /30ths grid
    CostProfile Costs,                       // §3
    ResourceNeeds Resources,                 // rig, crew disciplines + counts
    Requirements Requirements,               // SDD-005 §3 — validated at scheduling ONLY
    IReadOnlyList<ServiceRental> Rentals,
    OutcomeTable Outcomes);                  // §4

public enum OperationState { Scheduled, Active, Standby, Completed, Failed, Cancelled }

public interface IOperation
{
    EntityId<IOperation> Id { get; }
    OperationSpec Spec { get; }
    OperationState State { get; }
    int ProgressDays { get; }
    Money Accrued { get; }
}
```

> **Contract pass 10.** `BaseDuration` was typed `Duration` with the comment
> "whole days". `Duration` carries a `double` (SDD-001 §1), so the type and its
> own comment disagreed — and an operation duration must be an integer day count
> or §4's `disasterDay` cannot be drawn uniform over `{0 .. effectiveDuration−1}`
> and the segment boundary it creates would not land on the /30ths grid.
> `int BaseDurationDays`, as committed.
>
> `IRig`, `ResourceNeeds` and `IOperation` itself were all referenced here and
> declared nowhere — this document specified an operations engine without ever
> declaring an operation.

State transitions are engine-driven except `Cancelled` (a command). `Standby`
means committed-but-not-progressing (weather, suspension order): **cost without
progress** ([15](../design/15_TIME_AND_EXECUTION.md) §7).

## 2. Scheduling and reservation

```text
On Submit (a command):
  1. Gating.Check(Requirements, caps, Rentals, ctx)      → reject listing all misses
  2. Prerequisite state checks (target exists, status legal)
  3. Resource reservation against calendars:
       rig calendar: day-granular intervals on the /30ths grid;
       REJECTION includes the computed next-free date
       ("no rig of class L3 until 1978-03") — the reason must be actionable
  4. Reserve: resources committed for [start, start + maxDuration] where
     maxDuration = BaseDuration · worst outcome durationFactor — reservations
     are made for the worst case so a delayed operation NEVER finds its rig
     double-booked (pinned; the alternative is a cascading re-plan solver)
```

## 3. Cost accrual

```csharp
public sealed record CostProfile(
    Money Mobilisation,                       // on Active entry
    Money PerActiveDay,                       // day rates + consumables
    Money PerStandbyDay,                      // day rates ONLY — consumables stop, pinned
    Money Completion);                        // on Completed
```

```text
Each tick: activeDays  = tick days − standby days (from segment/weather data)
           accrued    += activeDays · PerActiveDay + standbyDays · PerStandbyDay
Progress  += activeDays              — progress and accrual use the SAME day counts
Rates are base-year content values, escalated by costIndex(t) at accrual —
rounded half-even at the ledger boundary (SDD-009 §1), once.
Cancellation: resources released at once; accrued stays spent (sunk, R12-V8);
              Completion cost not charged.
```

**Why standby excludes consumables:** a rig on weather standby still bills its
day rate but burns no mud and runs no bits. The distinction is visible in the
cost report and is the honest price of a missed weather window.

> **Amendment (finding 289): no activity has a price — it has a RATE, and the
> world supplies the quantity.** Every `content/activities/` entry carried
> `costMillions` and `durationTurns` as flat totals, so the same order cost
> the same money on every map: a 1 km spur priced like a 30 km trunk, a
> 3,000 m well like a 1,500 m one, a first gas train like the expansion twice
> its size. The owner's standing rule — the game generates its own
> environment; no fixed numbers — makes that a defect, and it is repaired at
> the schema:
>
> ```json
> "unit": "metre",                  // the physical dimension the rate is per
> "costMillionsPerUnit": 0.004,     // replaces costMillions
> "turnsPerUnit": 0.002             // replaces durationTurns
> ```
>
> - **The engine owns WHICH quantity an activity measures**; content owns the
>   rate. Each activity declares its `QuantityUnit` and computes its quantity
>   at submit from the world, the plant or the order itself: drilling and
>   every per-well job measure the hole's METRES; the block survey and 3-D
>   measure the generated block's SQUARE KILOMETRES; the export expansion and
>   the early production facility measure the generated route's KILOMETRES
>   (the laid flowline's length, or the field's distance to market); each
>   install measures the NEXT RUNG'S OWN CAPACITY in that ladder's physical
>   unit (kg/s, tonnes, slots, percentage points of water removed); upkeep
>   verbs measure the standing plant's ELEMENT count. Composition refuses an
>   entry whose declared unit is not the activity's, naming both — a rate in
>   the wrong dimension must not load.
> - **`price = rate × quantity`, quoted once** through the existing cost
>   index at scheduling (§3's escalation and finding 215's
>   contract-at-quote are unchanged); **`turns = max(1, round-half-even
>   (quantity × turnsPerUnit))`** — duration scales with the same quantity.
>   The contracted QUANTITY is saved beside the depth, so a reload
>   reproduces the job exactly.
> - **Rates derive from the measured economy at its reference points**
>   (F-2): each rate = the old flat total ÷ the reference quantity it was
>   authored against — 2,000 m for well work, the measured configuration's
>   64 km² block for surveys, its ~1 km market routes for lines, the ladder
>   rung each install price bought, the fixture chain's fifteen elements for
>   upkeep. At the references the economy is unchanged; away from them the
>   world finally prices the work.
> - The equipment catalogue's `costMillions` (plans 28, load-validated and
>   consumed by nothing yet) converts when its consumer lands, with that
>   consumer — not speculatively here.

```csharp
public enum OutcomeGrade { OnTime, Delayed, OverBudget, Partial, Failure, Disaster }

public sealed record OutcomeRow(
    OutcomeGrade Grade,
    double Probability,
    double DurationFactor,
    double CostFactor,
    int? DisasterDay);                       // INTEGER day index — /30ths-grid exact

public sealed record OutcomeTable(IReadOnlyList<OutcomeRow> Rows);
// Probabilities sum to 1.0, checked at CONTENT LOAD (stage 5 consistency,
// SDD-004 §5) rather than at draw time: a table that cannot produce an outcome
// is a broken content file, and the engine refuses to start rather than
// discovering it on the first spud of a campaign.
```

```text
At Active entry, ONE draw from the `operations` stream (audited: stream,
position, value — R12-V7) selects a row of the OutcomeTable:

  grade ∈ { OnTime, Delayed, OverBudget, Partial, Failure, Disaster }
  row = (grade, durationFactor, costFactor, disasterDay?)   // INTEGER day index
  // disasterDay drawn uniform over {0 .. effectiveDuration−1} — day-granular,
  // so its segment boundary lands on the /30ths grid (SDD-001 §9) exactly

Applied:
  effectiveDuration = ceil(BaseDuration · durationFactor)      — fixed from day one
  PerActiveDay     ·= costFactor
  Disaster: at disasterDayFraction of effective duration, the operation raises
  its incident hook (a threat into the R23 bow-tie — NOT a direct incident:
  barriers still apply, which is why well-control competence matters)
  Partial/Failure: outcome applied at termination (well not at TD; hole lost)
```

**Why draw-at-start rather than per-tick hazard:** one audited draw is
replayable, explainable ("the trouble was determined when you spudded — here is
the draw"), cheap, and immune to the exploit of cancelling operations that
start rolling badly — the player cannot observe the draw, only its unfolding.

**Stated acceptance:** the drawn grade persists in the save, and saves are
deliberately inspectable (PR6) — so a player who opens the JSON can read their
drilling outcome early. This is inherent to deterministic-plus-inspectable and
is accepted, not defended against: the protections are ironman (PSD4) for those
who want teeth, and the single-player social contract for everyone else.
Obfuscation would cost inspectability (the most valuable debugging property the
save has) to stop only the players who could equally re-roll by save-scumming.
Per-tick trouble *hazards* still exist, but they belong to R18's
equipment/barrier machinery, not to the operation grade.

### 4.1 Crew effect — and why it is not a forbidden multiplier

Crew skill and fatigue modify `durationFactor` distributions and the threat
rate into the bow-tie (R12-V9, R23). These are **operational variability
parameters declared in content**, not technology effects — [07](../design/07_TECHNOLOGY.md)
§1's no-multiplier rule governs *what technology nodes may do*, and is
untouched: a crew is not a tech node. Recorded here because the two rules look
similar enough that an implementer (or reviewer) would conflate them.

> **Amendment (finding 265) — R12.7's own row named the model as
> undeclared, and it stayed that way through R23's whole bow-tie build.**
> `Barrier.StrengthGiven` has taken a `crewCompetency` argument since R23.1
> and `ThreatStage` has supplied it every tick since — as
> `Defaults.CrewCompetency = 0.9`, a bare literal with no rationale and no
> lever, the same shape findings 233/249/261/262 already found for a
> compressor's ambient input, a top event's cost, a material catalogue and a
> reserve's own value. `OperationScheduler.Draw`'s `durationFactor` term
> reads no crew input at all — the OTHER half R12-V9 asks for ("higher skill
> reduces duration and risk by the declared amount") has never existed.
>
> **Scoped to a company-wide COMPETENCY LEVEL, not a per-discipline skill
> system.** `ResourceNeeds.Crew` already names which disciplines an
> operation needs and how many; grading each discipline separately is a
> materially larger feature this amendment does not attempt. One scalar,
> raised once, is what "declared in content" and R12-V9's singular "the
> declared amount" actually ask for.
>
> ```csharp
> // OGSim.Company — a company fact, the same layer EsgStanding and Bank live
> // at, not a per-operation one.
> public sealed class CrewState : IStateOwner
> {
>     public CrewState(
>         double baseCompetency, double trainedCompetency,
>         double baseDurationFactor, double trainedDurationFactor,
>         Money trainingCost);
>
>     public double Competency { get; }        // feeds Barrier.StrengthGiven,
>                                               // replacing Defaults.CrewCompetency
>     public double DurationFactor { get; }     // feeds OperationScheduler.Draw,
>                                               // multiplying spec.BaseDurationDays
>                                               // alongside the outcome table's own
>                                               // factor — an ADDITIONAL term, not a
>                                               // replacement for the stochastic grade
>     public bool Trained { get; }
>     public Money TrainingCost { get; }
>     public void Train();                      // one-way, like a technology acquisition
> }
> ```
>
> **A ONE-TIME INVESTMENT, not a per-operation lever.** §4.1's "declared in
> content" already rules out an invented technology multiplier; it does not
> by itself say whether a player trains once or tunes continuously. A
> continuous dial (mirroring `SetVoidageReplacementCommand`) would need a
> cost curve nothing pins; a one-way step (mirroring how a technology
> acquisition is bought once and held) needs only the two named amounts
> above, which is the smaller commitment and the one this amendment makes.
> **Expensed, not capitalised** (SDD-009 §1's own distinction for a vessel
> being PP&E): training buys a permanently better crew, but unlike a vessel
> it is not a balance-sheet asset in the accounting this engine follows, so
> it posts `Account.Opex`/`MovementCategory.Operating` rather than
> `Capex_PPE`/`Development`.
>
> **`ProcedureCompliance` is untouched and stays a bare literal.** SDD-012
> §4b's own arithmetic block already names its real source — "open-findings
> backlog (content map)" — which is R16.5's regulator, not R12's crew; fixing
> one placeholder is not licence to guess at the other's blocker.

## 5. Completion

Completion **day** within the tick = the day cumulative active days reach
`effectiveDuration` (deterministic from the weather/standby day calendar,
SDD-016) — that grid day is the segment boundary a new well lands on.
At `progress ≥ effectiveDuration`: apply the outcome record (well online,
facility commissioned, survey delivered to the observation pipeline…), charge
`Completion`, release resources, publish `operation.completed` with the grade.
Outcome application is a stage-3 state change — a well coming online **is** a
segment-boundary event ([21](../design/21_INTEGRATION.md) §5) for the tick it
lands in.

> **R12b amendment (finding 149) — one activity, one class.** The list above
> ("well online, facility commissioned, survey delivered…") named what completion
> *does* and declared nothing that does it. Built from that, the first two
> templates came out scattered across three files each: an `ActivityTerms` entry
> in the composition's content block, a bespoke `ICommandValidator` beside the
> command, and an effect lambda in a dictionary in the module. Nothing held a
> template together, so nothing could check one was whole — a template composed
> with no effect registered was caught by an `InvariantFault` *at completion*,
> which is to say after the player had already paid for it and waited.
>
> **An activity is one object**, in `OGSim.Composition` — the only layer entitled
> to know that a well, a compartment and a belief are all real (03 §2):
>
> ```csharp
> internal interface IActivity                 // the register holds these
> {
>     ActivityTerms Terms { get; }             // §1 spec, §3 costs, §4 outcome table
>     ContentId Template { get; }              // == Terms.Template, never a second copy
>     bool LeavesAnAsset { get; }              // capex if it does, opex if it leaves only knowledge
>     void Complete(CompletedActivity done, Tick tick);         // THIS section, executed
>     void Register(IModuleComposition composition, ActivityOrders orders);
> }
>
> internal abstract class Activity<TCommand> : IActivity where TCommand : Command
> {
>     public abstract (EntityRef Target, Length Depth) Aim(TCommand command);
>     public abstract void Refuse(TCommand command, List<RejectionReason> reasons);
>     public abstract void Complete(CompletedActivity done, Tick tick);
> }
> ```
>
> The generic parameter is the point: `Register` is the only place that knows
> `TCommand`, so an activity wires its own `ICommandValidator<TCommand>` /
> `ICommandApplier<TCommand>` pair and the module never switches on a concrete
> command type to do it.
>
> **What is shared stays shared, and a target is not shared.** One
> `ActivityOrders` holds the refusals every activity really does have — the cash,
> reachability, and the scheduler's own answer on contention — and books the
> operation when there are none; `Refuse` adds only what is true of *that*
> activity, including **what it is aimed at**. One generic validator asks both, in
> that order, and reports every reason (R1 §2.5).
>
> **AMENDMENT (R20d, finding GC-4). The shared refusals must not ask whether the
> FIELD has a compartment.** They did, and it was wrong in the way that is hardest
> to see: the check reads as a sensible "is there anything to work on", and for
> well-work it is — a well test, a wireline log and a core all name an
> `EntityId<IReservoirCompartmentEntity>` and cannot mean anything without one.
> But it was applied to **every** activity, and for the two that open a game it is
> exactly backwards.
>
> **DRILL AND SEISMIC HAVE NO WORLD PRE-REQUISITE.** Drilling a structure is how a
> compartment comes to exist, and a survey is what a company does *before* there
> is anything downhole. Gating either on a compartment already existing means a
> player may only drill once they have already found something — which inverts the
> loop this whole engine is built around: commit capital under uncertainty, wait,
> find out.
>
> Measured: on 2 of 12 generated basins nothing was charged, so no accumulation
> produced a compartment, so `CompartmentCount` was zero — while the read model
> published **eleven and twelve structures with probabilities of success
> attached**. Every seismic and every drill was refused for ten years with "there
> is nothing here to work on", against structures the same engine was advertising.
> The run reported a tidy `Expired` and was indistinguishable from a balance
> result.
>
> **A basin with no charge is a legitimate outcome and must stay playable.** The
> company should be able to spend its money finding that out — that is the game —
> rather than being told the answer for free by a validator. An order refused
> because the world is empty is the engine answering the question the player is
> paying to ask.
>
> So: the target check moves into `OwnRefusals`, where each activity refuses on
> **its own** unresolvable subject. Well-work refuses a compartment id that does
> not resolve, and says so in those words. Drill and seismic refuse nothing on
> these grounds at all.
>
> **The register holds activities, not template ids.** `InFlight` carries the
> `IActivity`, so completion calls `activity.Complete(…)` directly and §3's
> capex/opex split reads `activity.LeavesAnAsset`. The parallel
> `ContentId → effect` dictionary is gone, and with it the class of defect it
> existed to detect: an activity with no effect is now unconstructable rather
> than faulted at run time (law L3, structurally).
>
> *Open:* `Aim` returns a `Length Depth` because drilling is the only template so
> far that is aimed at more than an entity. A workover is aimed at a wellbore;
> the per-template parameter block that generalises this is **not** to be
> invented at a call site (F-4). Until it lands, a template with no depth passes
> zero and says so.
>
> **S1 amendment — the survey case is answered, and not by this open item.**
> This paragraph read "a survey is aimed at an area", and it was the reason an
> area survey looked blocked: an area is a centre and a radius, neither of which
> fits an `EntityRef`. The answer was to stop treating the area as a parameter.
> Acreage is licensed in **blocks**, a block is an entity, and an activity aimed
> at a block is aimed at exactly one `EntityRef` — so `seismic-2d` needs nothing
> this item would provide ([SDD-010](SDD-010_WORLD_GENERATION.md) §4b).
>
> **The phase id was stale, which is worth recording.** The text attributed the
> parameter block to R12b.16; R12b.16 shipped, and what it shipped was "one
> activity, one class" (`IActivity`/`Activity<TCommand>`). The generalisation has
> no phase and is not scheduled, so anything that claims to be blocked on it is
> blocked on nothing — the workover case above is the only caller still waiting,
> and it should be given a phase when a workover is built rather than inheriting
> a number that already means something else.

## 5b. Operations that move mass

> **R12b.18 amendment (finding 245) — "a well test" named two different
> activities and only one of them is built.** This section's opening line
> described a FLOWING test — barrels produced and flared outside the routed
> network — and `WellTestActivity`'s own file header described a SHUT-IN
> pressure build-up: the well closed, no flow at all, the reservoir watched
> answer for itself as pressure recovers. These are mutually exclusive, and
> only the shut-in one is shipped: `Complete` delivers pressure and
> permeability observations and nothing else, `OperationMass.None(width)` is
> the correct answer for it, and no activity in this composition flows and
> flares outside the network. The line below is corrected to describe what is
> actually built; a flowing test (a DST, or frac-fluid recovery) remains a
> real, DIFFERENT, unbuilt activity that would genuinely need `OperationMass`
> the day it exists.
>
> **The shut-in itself was also pure prose until this amendment.** The file's
> own claim — "the well is shut in for the build-up, so the test costs the
> month's oil" — was true of nothing: no code touched a well's choke. It is
> shut in now, for real, the instant the test is booked, and stays shut after
> it completes rather than reopening automatically — `ActivityStage.Execute`
> calls `Complete` from inside stage 3 (Operations), and SolveFlow is stage 5
> of the SAME tick, so a one-tick test that reopened its well in `Complete`
> would never have been absent from a single segment the solve actually ran.
> The player reopens it through `SetWellChokeCommand`, the same door every
> other shut-in reason already uses.

A build-up test shuts in every well on the compartment it targets and
delivers pressure and permeability observations; it moves no mass and reports
`OperationMass.None(width)`. Any operation may report per-tick `Sourced` /
`Disposed` masses (a flowing test, frac-fluid recovery — neither shipped);
these post **directly into the tick's 04 §7 terms** with the operation as the
audited element — small volumes, same conservation, no special ledger. If the
site has a routed test separator, the network path wins and the operation
reports nothing (no double count, checked).

> **R20d review amendment (finding 147) — the shape, declared.** This section
> pinned the rule and declared no type, so the tick's conservation check had no
> member to read. The prose above is now this:
>
> ```csharp
> public sealed record OperationMass(Composition Sourced, DisposedMass Disposed)
> {
>     // Takes the catalogue's width: a zero movement is still a composition of a
>     // particular width, and a shared empty singleton would be the one value in
>     // the engine that does not know how many materials exist.
>     public static OperationMass None(int materialCount);
> }
>
> // on IOperation:
> OperationMass MassThisTick { get; }
> ```
>
> Every operation answers — `None(width)` is the true answer for drilling,
> completion and construction, not a placeholder — which is what lets INV1 cover
> operations as a term rather than skip them. The network-path-wins rule is the
> implementer's obligation on this member, restated there verbatim.

## 6. Abandonment obligations

```csharp
public interface IObligationRegistry     // OGSim.Operations owns it
{
    void Register(EntityRef asset, ContentId abandonmentTemplate);   // at asset creation, ALWAYS
    Money EstimatedCost(EntityRef asset);                            // feeds R13's provision accrual
    void Discharge(EntityRef asset, EntityId<IOperation> completedAbandonment);
    // pass 10: was `OperationId`, an identity scheme declared nowhere —
    // identity is EntityId<T>, SDD-001 §2 (the CompartmentId/PerforationId
    // pattern of SDD-003, third occurrence)
}
```

Registration is unconditional at creation (02 §3.4 — no path skips it); only a
completed abandonment operation discharges. The registry is the single source
the provision (R13.8) and the licence rules (R16) both read.

## 7. Error surface

| Situation | Response |
|---|---|
| Gating/prerequisite/reservation failure | Command rejection, all reasons, next-free date where relevant |
| Target vanishes mid-operation (should be impossible — entities are never deleted) | Invariant fault INV3 |
| Outcome table rows not summing to 1.0 | Content consistency failure at load (SDD-004 stage 5) |
| Disaster hook with no bow-tie composed (pre-R23) | Composition rule: the hook targets a declared threat id; under pre-R23 composition the threat resolves to the hazard model directly — both real compositions, neither a stub |

## 8. Test mapping

R12-V1 (duration) · V2 (accrual arithmetic = §3 exactly) · V3 (reservation +
next-free) · V4 (prereqs) · V5 (grade rates over a large sample) · V6/V7
(draw-at-start determinism + audit) · V8 (cancellation) · V9 (crew) · V10
(obligations) · V11 (gating at scheduling) · TM5/TM6 (fractional progress,
standby cost) land here.

## 9. Open items

| # | Item | Trigger |
|---|---|---|
| S007-1 | Worst-case reservation wastes rig calendar on lucky operations — a release-early rule on OnTime completion is trivial; measure whether contention makes it matter | R12 integration |
| S007-2 | Multi-resource operations (rig + frac spread simultaneously) — reservation is per-resource already; confirm the rejection message composes readably | R12.4 |
