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

## 3. Evaluation

Stage 12, pure over the sealed snapshot + sealed event list (SDD-001 §6):
evaluate every active objective's AST; emit `objective.*` events on state
change. Deterministic iteration: objectives by content id. No command bus
reference exists in the assembly (R24-V15).

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
