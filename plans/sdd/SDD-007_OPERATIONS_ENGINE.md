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

## 4. Outcomes — drawn at start, applied across execution (pinned)

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

## 5b. Operations that move mass

A well test produces and flares real barrels outside the routed network. Any
operation may report per-tick `Sourced` / `Disposed` masses (well tests,
frac-fluid recovery); these post **directly into the tick's 04 §7 terms** with
the operation as the audited element — small volumes, same conservation, no
special ledger. If the site has a routed test separator, the network path wins
and the operation reports nothing (no double count, checked).

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
