# SDD-014 — Objectives and Scenarios

**Status:** drafted · **Serves:** R24 · **Design docs:** [18](../design/18_GAME_MODES.md), [R24](../phases/R24_OBJECTIVES.md)

The predicate machinery, the eight score formulas, and campaign persistence —
pinned so "add a mission" never becomes engine work.

---

## 1. The predicate AST

```csharp
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
