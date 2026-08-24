# SDD-014 — Objectives and Scenarios

**Status:** drafted · **Serves:** R24 · **Design docs:** [18](../design/18_GAME_MODES.md), [R24](../phases/R24_OBJECTIVES.md)

The predicate machinery, the eight score formulas, and campaign persistence —
pinned so "add a mission" never becomes engine work.

---

## 1. The predicate AST

```csharp
// A dotted key resolved against the read-model schema registry (§2). A struct
// over a string rather than a bare string, so a path cannot be confused with a
// display id or a localisation key at a call site.
public readonly record struct ReadModelPath(string Path);

public enum CompareOp { Lt, Le, Eq, Ne, Ge, Gt }
public enum AggOp { Max, Min, Sum, Count, Any, All }

public abstract record Predicate;
public sealed record Metric(ReadModelPath Path) : Predicate;         // §2
public sealed record Const(double Value) : Predicate;
public sealed record Compare(Predicate L, CompareOp Op, Predicate R) : Predicate;
public sealed record All(IReadOnlyList<Predicate> Items) : Predicate;
public sealed record Any(IReadOnlyList<Predicate> Items) : Predicate;
public sealed record CountOf(int N, IReadOnlyList<Predicate> Items) : Predicate;
public sealed record SustainedFor(Predicate Inner, int Ticks) : Predicate;   // stateful: consecutive-true counter
public sealed record InSequence(IReadOnlyList<Predicate> Steps) : Predicate; // stateful: current-step index
public sealed record Never(Predicate Inner) : Predicate;                      // failure condition
public sealed record OnEvent(EventCategory Cat, EventFilter Filter) : Predicate; // true for the tick the event fired
public sealed record Aggregate(ReadModelPath Collection, AggOp Op,               // Max | Min | Sum | Count | Any | All
    ReadModelPath ItemField) : Predicate;
// The quantifier an earlier draft lacked: "any well's water cut > 0.6" is
// Compare(Aggregate(wells, Max, waterCut), Gt, Const(0.6)). Path grammar:
// `[id]` selects one item by display id; a bare collection feeds Aggregate.
// Without this node, per-item objectives were only expressible one id at a
// time — unusable for fleet-level missions.
```

Closed hierarchy (like `Effect` — SDD-005 §4); content expresses these as a
small JSON tree, validated at load. The stateful nodes' counters/indices are
**objective state, persisted** (SDD-013 module block `objectives`).

> **Contract pass 10, member-level diff.** Four types this AST is built from
> were used in the declarations above and declared nowhere: `ReadModelPath`,
> `CompareOp`, `AggOp` and `EventFilter`. The same defect as SDD-002's
> `ConstraintWriter` and SDD-005's `EnvelopeContext` — a signature that cannot
> be implemented because a type it names does not exist.
>
All four are determined by this document's own text: `AggOp`'s members came from
the trailing comment on `Aggregate`, which listed them without declaring them,
and **`EventFilter`'s shape is settled by open item S014-1 below** — "subject /
category / severity now; payload fields later?". Category is already `OnEvent`'s
first argument, so the filter narrows the remaining two:

```csharp
// Both optional: an unset field does not narrow. S014-1 tracks whether payload
// fields are ever needed — until then this is the whole vocabulary, which is
// what keeps objectives unable to see anything the event does not carry.
public sealed record EventFilter(EntityRef? Subject, Severity? MinimumSeverity);
```

## 2. `ReadModelPath` — the no-invention rule

A path is a dotted key (`company.rrr`, `field[thunder-horse].waterCut`)
resolved against a **read-model schema registry** generated from R21's
projection contracts. **Load-time validation**: a path that does not exist in
the registry is a content fault — so an objective can never reference data the
player cannot see (GM4/R24-V14 mechanised), and a read-model rename breaks
content loudly at load, not silently at runtime.

> **Amendment (finding 267) — `company.value` joins the registry.**
> SDD-017 §2's finding-262 amendment gave `FieldReadModel` a `CompanyValue`
> (`cash + PV(1P) − debt − provisions`, the same figure SDD-014 §4 already
> scored Capital efficiency against) and said plainly what it did not do:
> "it does not by itself close R24.6 … or rebuild R11.6's reverted berth/cargo
> mechanic." What it also did not do, unnamed at the time, is reach an
> OBJECTIVE — `Defaults.ProjectedPaths` registered `company.cash` and
> `company.insolvent` the same task and never added `company.value` beside
> them, so a scenario asking for "the company is worth $X" was and is refused
> at composition as an unknown path, the exact GM4 mechanism this section
> exists to guarantee working against the one figure R11.6's own row names as
> its prerequisite.
>
> **Computed once, at stage 12, not twice.** `FieldPosition` — the position an
> objective sees (§5a) — gains `Money CompanyValue`, computed in
> `FieldProjection.Take` from the same three facts `Publish` already read it
> from (`Bank.Terms.ReserveValue`, `Bank.Drawn`, the ledger's cash and
> abandonment-provision balances). `Publish` now reads `position.CompanyValue`
> instead of recomputing it — one owner (law L5), where before the value
> existed only late enough for a host to see it and too late for an objective
> to ask about it.
>
> **Still does not rebuild R11.6.** This closes the one prerequisite
> `FieldReadModel.CompanyValue`'s own doc comment names for "a mechanic that
> defers revenue reading as a decision rather than a loss" — a scenario CAN
> now be authored against `company.value` — not the berth/cargo mechanic
> itself, which stays its own task exactly as SDD-017 said.

> **Amendment (finding 276) — `company.taken-over` joins the registry,
> R13.10's third and last restructuring finding.** Findings 274 and 275 gave
> a distressed company two levers — a forced cash sweep, a working-interest
> sale — and both run out of road: a sweep needs cash the company may not
> have, and a sale is capped at 50% before the company has given up
> operatorship in substance. This is what happens when both are exhausted.
>
> **The plan this amendment implements originally described the mechanism as
> "the scenario's `Overall` verdict becomes `Failed`", and that turned out
> not to be how `Overall` works** (F-4 — implementation showing a design
> wrong is corrected here, not worked around in code). `ScenarioRunner.Overall`
> (§5a) is PURELY a function of the scenario's own DECLARED `Objectives`/
> `Failures` predicates, evaluated against read-model paths — the same
> "content, not code" shape §3.3 pins for the win condition itself. Nothing
> in the engine may reach in and set a verdict directly; `company.insolvent`
> does not either, which is why an idle company today can run cash-negative
> forever without the shipped scenario ending — `Defaults.ProjectedPaths`
> registers the path and no content currently declares a Failure objective
> against it. **`company.taken-over` is built the identical way, for
> consistency with that precedent rather than in spite of it**: a computed,
> latched, audited fact a scenario's content CAN reference, not a verdict
> forced from outside the predicate system. Wiring either fact into the
> SHIPPED scenario's own Failure objectives is unchanged, pre-existing,
> content-authoring work this amendment does not newly create or attempt to
> close.
>
> **The trigger: the covenant has read `Amortising` for 12 straight ticks —
> 18 months of covenant distress once the 6-tick cure window is counted —
> with `WorkingInterest.PartnerShare` already at finding 275's own 50% cap.**
> 12 ticks, confirmed with Fahad before landing the same gate every other
> invented number this session has gone through. The clock (`ObjectiveStage`'s
> own `_ticksAmortising`) resets to zero on anything OTHER than `Amortising`
> — a company that cures its covenant genuinely clears the risk rather than
> merely pausing a clock that resumes where it left off, the same shape
> `Curing`'s own cure window already has in `Lending.cs`. `PartnerShare` only
> grows (finding 275), so once the cap is reached it never becomes false
> again — the only thing that can still change is the clock.
>
> **Once true, always true — the SAME latch `Insolvent` already is, on the
> SAME owner.** `ObjectiveStage.TakenOver`, computed and audited
> (`AuditCategory.StateTransition`, `["kind"] = "company.taken-over"` — the
> SAME "one transition, a `kind`-keyed reason" shape `licence.expired`/
> `licence.commitment-unmet` already established for a licence's own loss,
> SDD-011 §1's R20d.9 amendment) right beside where `Insolvent` is computed,
> since both are company-level terminal facts and `ObjectiveStage` is
> already their one owner (law L5). `ObjectiveStage` gains two new read-only
> dependencies to make the check — `Bank` (the covenant) and
> `WorkingInterest` (the share) — neither of which it may mutate.
>
> **Persisted, the same reason `Insolvent` itself needed to be (finding
> 266)**: `objectives.reporting`'s schema moves 1 → 2, carrying `TakenOver`
> and the ticks-Amortising clock alongside it, so a reload cannot silently
> reset either — a save mid-clock that came back at zero would let a player
> launder ten of the twelve ticks by saving and loading, the same exploit
> class the covenant clock's own carve-out closed at finding 210 and laytime/
> demurrage's own persistence closed at finding 269.
>
> **Published on `FieldPosition`/`FieldReadModel` as `bool TakenOver`,
> appended after `CompanyValue`/`World` respectively rather than inserted —
> every existing positional construction of either record keeps its
> meaning.** A host can show it the same way it already shows `Insolvent`.

## 3. Evaluation

Stage 12, pure over the sealed snapshot + sealed event list (SDD-001 §6):
evaluate every active objective's AST; emit `objective.*` events on state
change. Deterministic iteration: objectives by content id. No command bus
reference exists in the assembly (R24-V15).

> **R24.5 amendment (finding 247): emitted by `ObjectiveStage`, not by
> `ScenarioRunner`.** R24-V15's own test forbids the runner an `IAuditTrail`
> field at all — "observes, never influences" reaches auditing too, since a
> runner that could write the trail is a runner touching something outside
> its own return value. `ObjectiveStage` already holds the trail for the
> scenario's combined verdict, so it is also the one owner of "did this
> particular objective just settle": a `Dictionary<ContentId, ObjectiveState>`
> cache, looked up per objective and never enumerated, tells a transition
> from a tick that merely re-confirms an already-latched state. Recorded as
> `AuditCategory.StateTransition` with `kind ∈ {objective.met, objective.failed,
> objective.expired}` — the same category every other transition in this
> engine shares, told apart by the `kind` key rather than a dedicated enum
> member per fact.

## 4. The eight score dimensions — formulas pinned

| Dimension | Formula (period = scenario span) |
|---|---|
| Reserves | 2P added (integer volume) and RRR = added/produced |
| Recovery | Σ produced / Σ EUR-truth? **No — truth is unreachable.** Σ produced / Σ 2P-at-sanction, the honest bookable proxy |
| Capital efficiency | (Δ company value + distributions) / Σ capex, value = cash + PV(1P) − debt − provisions (SDD-009 terms) |
| Finding cost | Σ exploration+appraisal spend / 2P added by discovery |
| Operating cost | Σ opex / Σ produced (per-barrel, integer cents) |
| Uptime | Σ produced / Σ (produced + attributed deferrals) — straight from the solver's ledger (SDD-002 §8) |
| HSE | Composite of incident tier counts (weights content), emissions & flaring intensity, spill volume |
| Legacy | Obligations discharged / obligations incurred; restoration completion fraction |

Composite = content-weighted sum of normalised dimensions; **dimensions always
reported individually** (18 §4). Every input is an existing ledger/registry
value — scoring reads, never computes new simulation facts.

> **Amendment (finding 290, R24.6 built).** The eight dimensions are computed
> by `ScoreLedger` (`OGSim.Composition`), a saved span accumulator fed each
> tick by `ObjectiveStage` with the same sealed position the objectives
> evaluate against plus the sources this section names — the cash ledger's
> period effect by cause, the reserves book's 2P (the same
> `Remaining(CumulativeProduced)` call the projection publishes, law L5), and
> the solver's own custody and deferral masses (SDD-002 §8). The stage
> attaches `Scores` to the runner's `ScenarioProgress`; the runner itself
> stays blind to ledgers and registries, the same division that put R24.5's
> event cache on the consumer. Decisions recorded:
>
> - **A dimension whose denominator has not happened is OMITTED**, never
>   reported as zero — "the finding cost of nothing found" has no answer, and
>   zero would flatter it (the same refusal §1 makes for `Max` over an empty
>   collection). Reserves reports RRR; the 2P addition is recoverable from the
>   same two terms.
> - **HSE reads `EsgStanding`** — the engine already composes this row's exact
>   terms (tier-weighted incident points via `ConsequencePoints`, decayed, and
>   `EsgRecord`-normalised flaring intensity) into one published standing, and
>   a second composite of the same events would be a second owner of one fact
>   (law L5). Spill volume joins when incidents distinguish one.
> - **Distributions are zero by construction**: no distribution mechanic
>   exists; when one lands it gets its own `MovementCategory` and the term
>   reads it.
> - **Uptime is measured in the solver's own mass basis** — custody throughput
>   over custody throughput plus attributed deferrals, both from
>   `ProductionLoop`'s per-tick accessors — so made and lost are the same kind
>   of number.
> - **Legacy's discharged/incurred is also the restoration fraction** in an
>   engine where discharging an obligation is the restoration (SDD-007 §6);
>   `IObligationRegistry` gained the `Discharged` count, persisted (v3).
> - **The span survives a reload** (`scenario.scores` state block), and the
>   post-restore evaluation `SaveGame.Load` runs (finding 266) does not
>   double-integrate its tick — the ledger latches the last observed tick.
> - Pinned by GM9 (formulas over a played period, omit rule) and R24-V16
>   (the span identical across a reload; capital efficiency's load-instant
>   `CompanyValue` variance is the S013-9 family, asserted finite there).

> **Amendment (finding 291, R24.8 built): the script executes, and the
> modifier is a requirement.** `IScenarioRunner.EntriesFor` was consumed by
> nothing — a scenario could script a beat and the engine ran as if the script
> were blank. `ScenarioScriptStage` (stage 2, the Commands slot's first
> contributor) now executes each tick's entries: a `ScriptedCommand` through
> the player's own bus (bound in the builder's last step, the same late
> binding the handlers get), a refused scripted order recorded on the trail as
> `scenario.script-refused` — a mission visibly skips a beat, never crashes
> and never swallows (L4). A `ScriptedParameter` has its executable arm (the
> same effect door technology applies through) but the runner's
> composition-time refusal STANDS: `IEffectState.Parameter` has no reader in
> any composed model yet, so an override would land in a dictionary nothing
> consumes and the mission would look scripted while changing nothing; the
> refusal lifts with R20d.10. `Scenario.RealityProfile` — the §5 modifier —
> was accepted and read by nothing, and the shipped scenario named a profile
> this build never shipped ("standard"): the field is now NULLABLE, null
> meaning the scenario genuinely runs at whatever fidelity composed (the
> shared default's truth), and a scenario that NAMES a profile refuses a
> build composed at any other — its balance was calibrated against those
> physics, and running it laxer would award its scores for a different game.
> Pinned by GM12 ×4 (a beat fires at its tick and no other, through the real
> bus; a refused beat lands on the trail; a scripted parameter is refused
> until a model consumes one; the composed engine's stage is bound
> write-once) and the fidelity-mismatch refusal test.

## 5. Scenarios and campaigns

```text
scenario content: world source (seed | authored), starting state block,
  objectives[], failure conditions (Never), scoring weights, modifiers
  (reality profile — SDD-005/18 §5b), scripted entries [(tick, entry)] where an
  entry is a COMMAND or a model-parameter override executed by the scenario
  runner at stages 1–2 — never a raw published event: a notification without
  its occurrence would violate 16 §1 (you script the price shock's parameter,
  and the market model publishes the event honestly)
campaign content: chapters[], persistence WHITELIST (explicit paths into
  company state: cash, tech nodes, reputation, named assets…), branching:
  outcomeKey → next chapter id, where outcomeKey = a declared objective's
  terminal state — branching on a small enum, never on arbitrary state (R24 risk)
Chapter load = scenario load + whitelist application over the carried state.
Anything not whitelisted resets — R24-V17's isolation follows.
```

> **R21c review correction (finding 141).** This section described scenario and
> campaign content in PROSE and declared no types, so R24.7's `IScenario` /
> `ICampaign` had nothing to implement and the first playable goal was written
> as an ad-hoc `ScenarioGoal` record inside composition — a scenario that could
> not be authored, loaded, or varied without editing the engine, which is
> exactly what design 03 §3.3 says a mode must never be. The shapes:
>
> ```csharp
> // Where a scenario's world comes from. Authored is a content id rather than a
> // blob: an authored world is still content, loaded through the same pipeline.
> public abstract record WorldSource;
> public sealed record GeneratedWorld(ulong Seed) : WorldSource;
> public sealed record AuthoredWorld(ContentId World) : WorldSource;
>
> // One scripted intervention. A COMMAND or a model-parameter override, executed
> // at stages 1-2 — never a raw published event, because a notification without
> // its occurrence would violate 16 §1: you script the price shock's PARAMETER
> // and let the market model publish the event honestly.
> public abstract record ScriptedEntry(Tick At);
> public sealed record ScriptedCommand(Tick At, Command Command) : ScriptedEntry(At);
> public sealed record ScriptedParameter(
>     Tick At, ModelSlot Slot, ParameterKey Key, double Value) : ScriptedEntry(At);
>
> // What a run is scored on (§4). A challenge names the dimensions that count,
> // so "maximise recovery" and "minimise finding cost" reward different play
> // (18 §3.3) — an empty set means the run is not scored, which is what a
> // sandbox is.
> public sealed record ScoreWeight(ScoreDimension Dimension, double Weight);
>
> public enum ScoreDimension
> {
>     Reserves, Recovery, CapitalEfficiency, FindingCost,
>     OperatingCost, Uptime, Hse, Legacy,
> }
>
> public sealed record Scenario(
>     ContentId Id,
>     WorldSource World,
>     ContentId StartingState,              // the state block a run opens from
>     IReadOnlyList<Objective> Objectives,  // success conditions
>     IReadOnlyList<Objective> Failures,    // `Never` predicates — 18 §3.3's hard limits
>     IReadOnlyList<ScoreWeight> Scoring,
>     ContentId RealityProfile,             // modifiers (SDD-005, 18 §5b)
>     IReadOnlyList<ScriptedEntry> Script,
>     Tick Deadline);
>
> // outcomeKey is a declared objective's TERMINAL STATE — branching on a small
> // enum, never on arbitrary state (R24 risk).
> public sealed record ChapterLink(ObjectiveState Outcome, ContentId NextChapter);
>
> public sealed record Campaign(
>     ContentId Id,
>     IReadOnlyList<ContentId> Chapters,        // scenario ids, in order
>     IReadOnlyList<ReadModelPath> Persisted,   // the WHITELIST; anything else resets
>     IReadOnlyList<ChapterLink> Branches);
>
> // What an objective has come to, and the only thing a campaign may branch on.
> public enum ObjectiveState { Pending, Met, Failed, Expired }
> ```
>
> **`Failures` is a separate list from `Objectives`, not an objective with a
> flag.** A failure condition is a `Never` that ends the run the moment it
> breaks, and a success condition is something the player is working toward;
> merging them would mean every consumer testing which kind it was holding.
>
> **`Deadline` sits on the scenario rather than on each objective.** An objective
> may carry its own (§3's `Tick? Deadline`), but the RUN has to end whatever the
> objectives say — a scenario whose goals were all open-ended would never
> resolve, and "did they manage it in time" is the question a challenge is
> asking.

## 5a. Running one — the report, the runner, and what it evaluates against

> **R21e review (finding 154).** The note above lists eight shapes as written
> and declares six. `ScenarioProgress` and `IScenarioRunner` — the report and
> the interface, which is to say the two a runner must implement — went into
> `OGSim.Contracts` and into no document, inside the very change that was
> fixing prose-only specification. `ObjectiveSnapshot` did the same at R24.
> Declared here, and the signature corrected while declaring it.

```csharp
// The sealed position an objective sees: read-model values BY PATH (§2), the
// collections an Aggregate quantifies over, and the tick's events.
//
// It lives in OGSim.Contracts because it crosses a contract boundary; the
// evaluator that consumes it stays in OGSim.Objectives. The same split as
// Observation / ObservationSampler in SDD-008 §3 — the shape is vocabulary, the
// thing that acts on it is a module.
public sealed record ObjectiveSnapshot(
    IReadOnlyDictionary<string, double> Values,
    IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, double>>> Collections,
    IReadOnlyList<EngineEvent> Events);

// How a run stands. A REPORT: the runner observes and never acts (R24-V15), so
// nothing here can change what the engine does next.
public sealed record ScenarioProgress(
    IReadOnlyList<(ContentId Objective, ObjectiveState State, double Progress)> Objectives,
    IReadOnlyList<(ScoreDimension Dimension, double Score)> Scores,
    ObjectiveState Overall);

public interface IScenarioRunner
{
    ContentId Id { get; }

    /// Everything the scenario scripts for this tick, for the engine to execute
    /// at stages 1-2. RETURNED rather than applied, so the runner still never
    /// acts — the engine does, through the same command path a player uses.
    IReadOnlyList<ScriptedEntry> EntriesFor(Tick tick);

    /// Stage 12.
    ScenarioProgress Evaluate(ObjectiveSnapshot position, Tick tick);
}

// The stateful nodes' counters (§1), persisted as part of `ScenarioRunner`'s
// own state below (§5a's finding-266 amendment) — NOT a standalone `objectives`
// block; no such block exists. NOT `ObjectiveState` either: that enum is what an
// objective has COME TO, and this is what its SustainedFor / InSequence / Never
// nodes have accumulated on the way. One name for two concepts is what glossary
// rule N1 forbids, and the two meet on one object the moment a runner is written.
public sealed class PredicateState { … }
```

**`Evaluate` takes the snapshot, not the `ReadModel` record.** The earlier
signature took [SDD-017](SDD-017_HOST_SURFACE.md) §2's fifteen-view root, and
that is wrong on three counts. §1 and §2 are explicit that an objective sees the
read model *through paths validated against the registry* — the snapshot is that
view and the record is not. A runner handed the record would have to flatten
fifteen nested views into `path → double` itself, and SDD-017 §3 generates the
registry from those same records: one algorithm with one correct answer, so a
per-runner flattening is law L5 broken, and a plugin runner that flattened
differently would evaluate content against paths it was never validated against.
And `IScenarioRunner` is a replaceable slot — handing a plugin the whole read
model gives it strictly more than this document says an objective may see.

**`Overall`, pinned.** In this order, and the first match wins:

| # | Condition | `Overall` |
|---|---|---|
| 1 | any failure objective's `Never` has broken | `Failed` |
| 2 | every success objective is `Met` | `Met` |
| 3 | `tick ≥ scenario.Deadline` | `Expired` |
| 4 | otherwise | `Pending` |

Failure before success, because a company that cannot pay has lost even in the
month it would otherwise have hit the target: the money is gone before the goal
is measured, and that is the order it happens in. **A terminal overall is
final** — a run does not un-fail, and a player who hit the target in month 90
does not lose it in month 91.

Per objective: a success objective is `Met` once its predicate holds and stays
met; `Expired` if its own `Deadline` passes unmet. A failure objective's
condition is a `Never`, which reads TRUE while it still holds, so the objective
is `Failed` the tick that condition evaluates false — the one place in this
document where a false predicate is the bad news.

**`Progress` is 0.0 or 1.0 and nothing between.** A fraction would need a
per-predicate distance metric — how near is `SustainedFor(12)` at month seven,
how near is a `Never` to breaking — and no such metric is specified. Inventing
one at the call site is what F-4 forbids; open item S014-4 carries it.

### Amendment (finding 266) — a reload silently rewound every objective

**Nothing this section describes was persisted.** `ScenarioRunner._tracked`'s
`PredicateState` per objective, its `_states` latch and its `_overall` latch,
and `ObjectiveStage`'s "already reported" cache all lived only in memory. A
reload composed a fresh `ScenarioRunner` from the same content and got fresh,
EMPTY counters — so `SustainedFor(12)` resumed from zero instead of wherever it
stood, a `Never` that had already broken (Failed, latched) could read
un-broken again if the condition happened to hold true at the reload instant,
and stage 13's read model stayed `null` until a further tick ran one full
`Execute`, because nothing had rebuilt `Position` (found chasing the client's
GC-2: "a restored engine has no read model until a tick runs" — the fifteen
projections behind it are no longer the blocker R20d.12.0 recorded, F-4). None
of that is "a game that has not started has nothing to show" (`Engine.ReadModel`'s
own doc comment) — a reload HAS a month behind it, and the month vanishing is
data loss with an audit trail that then re-announces already-settled verdicts
on top of it.

**Two owners, because two different facts (L5).** `ScenarioRunner` decides
where a run stands; `ObjectiveStage` decides what has already been told to the
trail about it (R24-V15 keeps the runner unable to reach `IAuditTrail` at all,
so that cache was never the runner's fact to own).

- `ScenarioRunner` now implements `IStateOwner`, key `objectives.evaluation`.
  Captures `_overall`, then per tracked objective (in `_tracked`'s own
  construction order, which is the scenario's declared order and does not
  change without a content edit): the objective's id (checked on restore
  against what `_tracked[i]` actually is, refused by name on a mismatch — a
  scenario's objective SET is content, not something a save is allowed to
  reshape), its latched `ObjectiveState`, and its `PredicateState`'s three
  counters (`SustainedTicks`/`SequenceStep`/`IsBroken` per node, each keyed by
  the node's own path string and written in the order each was first touched —
  a `List` alongside each `Dictionary`/`HashSet`, never an enumeration of
  either, per SDD-000 §3).
- `ObjectiveStage` now implements `IStateOwner`, key `objectives.reporting`.
  Captures `Insolvent` (SDD-009 §7's latch, unpersisted since it was written
  and folded in here rather than left a second, smaller gap beside this one)
  and the "already reported" cache — `_reported` and `_reportedByObjective`,
  the latter written in first-touch order the same way.

**Why restoring the evaluation state first is what makes an immediate
re-`Execute` safe.** `SaveGame.Load` (SDD-013 §6's amendment, this same
finding) now calls `objectives.Execute` once, at the restored tick and date,
immediately after `Restore` and before returning. That call re-derives
`Position` and `Progress` against the STATE the restore just put back —
mostly, though not entirely, the identical inputs `Execute` last saw before
the save (the exception is named below). With `ScenarioRunner`'s latches
restored, `Progress.Objectives` comes back holding the SAME states
`ObjectiveStage`'s reporting cache already has on file for every objective
whose reading has not changed, so the per-objective diff at
`ObjectiveStage.Execute`'s loop finds `state == before` and reports nothing
new. Doing this with the evaluation state still zeroed would have re-diffed
every settled objective against a fabricated `Pending` and re-announced it —
the exact defect this amendment exists to close, reintroduced one layer up.

**The named exception — S014-5.** `Position` is built by the SAME
`FieldProjection.Take` a real tick uses, and five of the projections it draws
on are deliberately never saved because they are recomputed from stages 1–11
every tick rather than stored (SDD-013 §4's own table, and this amendment's
own SDD-013 §4 entry): this tick's production, the chain's throughput, every
wellbore's rate, the borrowing terms, and the flood controller's figures. A
reload runs NONE of those stages — running them would mean consuming random
draws beyond the header's recorded stream positions, which loading a save must
never do — so the immediate post-restore `Position` reads those five at their
freshly-composed default rather than the save's last real value, until the
next actual tick recomputes them on both counts.

For every objective the shipped content authors today — `company.cash`,
`company.insolvent`, anything over reserves or wells drilled — this is inert:
those paths ARE persisted state and read correctly. **It stops being inert the
day a `SustainedFor`, `InSequence` or `Never` node names one of the five**
(`field.producedThisTick` already sits in the registry, SDD-017 §3, reachable
and un-refused today): the immediate post-restore call could read a spurious
zero for one evaluation and mutate the SAME `PredicateState` a real tick would
have used — resetting a sustained count that should not have reset, or worse,
latching a `Never` that should never have broken. Not guarded here, because no
shipped or authored scenario reaches it and a guard invented against a
predicate combination nothing uses would be F-4's "inventing a number" in
different clothing. **What would close it**: either a "dry" partial tick that
recomputes flow honestly without touching content or consuming a stream
position — materially larger than this amendment — or refusing at load a
stateful node that names one of the five paths, trading the future capability
away instead of building the machinery for it. Neither is this amendment's to
choose.

## 6. Test mapping

GM2/GM3 (AST nodes incl. stateful counters) · GM4 + R24-V14 (§2 registry) ·
GM5 (observer purity — digest identity) · GM6/GM7 (deadlines, `Never`) · GM9
(§4 formulas vs hand-computed period) · GM10/GM11 + R24-V17 (whitelist,
branching) · GM12 (mission completability scripts) · R24-V18 (stage placement).

## 7. Open items

| # | Item | Trigger |
|---|---|---|
| S014-1 | Event filters' expressiveness (subject/category/severity now; payload fields later?) | First mission authoring (R20.5) |
| S014-2 | Whether the Recovery proxy (2P-at-sanction) needs a truth-side CAL check to stay honest | R20 calibration |
| S014-3 | **An objective cannot see events at stage 12.** §3 says evaluation is pure over "the sealed snapshot + sealed event list", and the pipeline seals at the CLOSE — after stage 12, because stages 12 and 13 may still publish. So `ObjectiveSnapshot.Events` is empty at the only point it is read, and an `OnEvent` predicate is silently false rather than wrong-and-loud. Three candidate answers, none of them free: seal before stage 12 and forbid publication after it; expose the pending list to stage 12 and stop calling it sealed; or evaluate `OnEvent` against the PREVIOUS tick's sealed set and accept a one-tick lag like stage 4's. **Until it is decided a runner REFUSES a scenario containing an `OnEvent`**, naming this item — a load-time refusal rather than a predicate that quietly never fires | R21e, deferred there rather than guessed |
| S014-4 | Fractional objective progress — a per-predicate distance metric (how near is `SustainedFor(12)` at month seven?). `ScenarioProgress.Progress` is 0.0/1.0 until one is specified | First mission UI (R21f) |
| S014-5 | A stateful node (`SustainedFor`/`InSequence`/`Never`) naming a per-tick FLOW path (production, chain throughput, a wellbore rate, borrowing terms, flood figures) can read a spurious default on the one evaluation `SaveGame.Load` runs immediately after a restore, before the next real tick recomputes those five (finding 266's §5a amendment, above) | First scenario that names one of the five inside a stateful node |
