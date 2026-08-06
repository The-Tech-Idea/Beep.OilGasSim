# SDD-002 — Streams and the Flow Solver

**Status:** drafted · **Serves:** R2 (streams), R4 (solver) · **Design docs:** [02](../design/02_DOMAIN_MODEL.md) §1.4–1.5, [04](../design/04_MATERIAL_AND_FLOW.md), [21](../design/21_INTEGRATION.md) §5

The highest-hallucination-risk area of the engine: an implementer without this
document would have to invent the iteration scheme, the throttle rule, the
apportionment policy and a dozen numeric tolerances. Every one is pinned here.

---

## 1. Scope

Stream types live in `OGSim.Kernel` (they are the currency every module trades
in). The network, solver and conservation checker live in `OGSim.Flow`.
`OGSim.Flow` references Kernel and Contracts only — it must not know any
concrete element.

## 2. Materials and composition

```csharp
public readonly record struct MaterialId(int Ordinal);   // catalogue order, save-stable

public readonly struct Composition                        // immutable; mass flow per material
{
    // Backing: double[] indexed by MaterialId.Ordinal, length = catalogue size.
    // All values kg/s. Absent material == 0.0. NEVER negative (asserted on construction).
    public MassRate this[MaterialId m] { get; }
    public MassRate Total { get; }
    public Composition Plus(in Composition other);
    public Composition Scaled(double factor);             // factor in [0,1] for splits; >1 is a fault
    public (Composition a, Composition b) Split(double fractionToA);
    // Iteration: ALWAYS ascending Ordinal (determinism rule D-5).
}
```

**Decision — dense array, not dictionary.** The catalogue is small (~12 shipped
materials), streams are created constantly, and ordinal indexing gives
deterministic iteration for free. A modded 40-material game is still tiny.

## 3. Provenance

```csharp
public readonly struct Allocation
{
    // Sorted array of (CompartmentId, double fraction), ascending id.
    // Invariant: fractions > 0, sum == 1 within 1e-12; renormalised on construction,
    // and a renormalisation larger than 1e-9 is an invariant fault (INV-class), not a fix.
    public static Allocation Blend(ReadOnlySpan<(Allocation a, Mass weight)> parts);
}
```

Blending is mass-weighted merge of the sorted arrays. Tank inventory stores an
`Allocation` updated on every receipt ([04](../design/04_MATERIAL_AND_FLOW.md)
§2.2); lifting reads the inventory's current allocation.

## 4. Stream

```csharp
// F-4 event #1 (first compiler contact): `Stream` collides with System.IO.Stream
// under implicit usings — the C# type is `MaterialStream`; the design term stays
// "stream". Logged as finding 61.
public readonly struct MaterialStream
{
    public Composition MassRates { get; }    // kg/s per material
    public Pressure P { get; }
    public Temperature T { get; }
    public Allocation Provenance { get; }
    // NO cached phase split. Phase behaviour is a function of (composition, P, T)
    // via IFluidPropertyModel — caching it here invites staleness bugs. Elements
    // that need the split ask the model.
}
```

## 5. Elements

```csharp
public readonly record struct PortId(int Index);          // element-local, 0-based

public sealed record PortSpec(
    PortId Id, PortDirection Direction,
    PortRole Role);                                       // Main | Gas | Liquid | Water | Reject
    // Reject: where spec-failing mass is routed (e.g. to a flare element).
    // An element with a spec gate MUST declare a Reject port — checked at network build.

public interface IFlowElement
{
    EntityId<IFlowElement> Id { get; }
    IReadOnlyList<PortSpec> Ports { get; }

    // Called with proposed inlet streams for a segment. Must be PURE:
    // no state mutation — commit happens at stage 6 via ICommitTarget.
    TransformResult Transform(in TransformInput input);

    // Constraints evaluated against the same proposal.
    void EvaluateConstraints(in TransformInput input, ref ConstraintWriter constraints);
}

public readonly record struct TransformInput(
    ReadOnlySpan<Stream> Inlets,          // by inlet PortId.Index
    SegmentContext Segment);              // duration fraction, ambient conditions (from R22)

public readonly struct TransformResult
{
    public ReadOnlySpan<Stream> Outlets { get; }          // by outlet PortId.Index
    public Composition Sourced { get; }                   // SOURCE elements only: mass entering the
                                                          // network (completion withdrawal, purchased CO₂)
    public Composition FuelConsumed { get; }              // the fuel term of 04 §7
    public DisposedMass Disposed { get; }                 // kind-tagged: Flared | Vented | Discharged —
                                                          // the flare/VRU/water-outfall terms of 04 §7
    public Power PowerDraw { get; }                       // recorded for stage-4 duty next tick
    // ELEMENT-LEVEL CONSERVATION, the complete form (an earlier draft omitted
    // Sourced and Disposed, so a flare or a completion could not pass its own
    // check):  Σ inlets + Sourced == Σ outlets + FuelConsumed + Disposed
    // per material, |error| <= max(1e-12 · massTotal, 1e-9 kg/s). Checked after
    // EVERY transform — what makes an INV1 breakdown attributable to one
    // element. Each Disposed kind rolls up into its own 04 §7 term.
}

public readonly struct DisposedMass        // referenced by TransformResult; defined:
{
    public Composition Flared { get; }      // combusted per the flare's efficiency
    public Composition Vented { get; }      // uncombusted release (fugitives, boil-off without VRU)
    public Composition Discharged { get; }  // permitted outfall (treated water)
    // Each maps 1:1 onto its 04 §7 conservation term; Vented and Flared also
    // post to the emissions ledger at stage 9 (Flared via combustion products).
}

public enum ConstraintKind
{
    GasCapacity, LiquidCapacity, TotalCapacity, Ullage, PressureRating,
    Power, ErosionalVelocity, SpecGate, BerthOccupancy, Injectivity
}
```

**Availability is not on the element.** The segment plan lists available
elements; an unavailable element is absent from the network
([04](../design/04_MATERIAL_AND_FLOW.md) §4). `Transform` being pure is what
makes per-segment solving and whole-tick abandonment cheap.

## 6. Network

```csharp
public sealed class FlowNetwork
{
    // Built per segment from (all elements) ∩ (segment availability set).
    // Topology: TREE toward each sink (FD4). Validated at build:
    //   - every non-source element has exactly one downstream edge per outlet role path
    //   - no cycles; parallel loops modelled as one combined element (R11 §6 risk note)
    //   - RECYCLE STREAMS (gas lift; any future recycle) are closed with a
    //     ONE-TICK LAG, never as in-tick cycles: tick t's lift-gas rate is
    //     tick t-1's committed value — a fixed SINK at the compression side
    //     and an equal fixed SOURCE at the completion in t. Same shape as the
    //     genset fuel sink (SDD-006 §3b). FD4's tree stays a tree, the lag is
    //     one month of lift-gas ramp (physically fine), and conservation holds
    //     per tick with the lagged pair audited as a matched entry.
    //     Lift-gas provenance: the compression point's blend at t-1.
    // Element order: topological, ties broken by ascending EntityId — the single
    // deterministic order every pass uses.
}
```

## 7. The solve — the algorithm, pinned

> **Pass-2 amendment (finding 62):** the solver signature is
> `Solve(SegmentContext, FlowTopology)` — `FlowTopology(Elements, Connections)`
> with `FlowConnection(From, FromPort, To, ToPort)`. The original signature
> passed elements only; the wiring between them was declared nowhere.


Per segment. All symbols per completion `w`: rate `q_w` (reservoir-condition
volumetric, m³/s), wellhead backpressure `Pwh_w`.

```text
S0  INIT       first segment of tick: q_w from previous tick's committed rates
               (0 for new wells); later segments: carry forward. Pwh_w likewise.
S1  WELLS      for each completion (ascending id):
                 q*_w = OperatingPoint(Pwh_w)          // SDD-003 §6; may be DEAD → q*=0
                 q_w  = q_w + λ·(q*_w − q_w)           // damping λ = 0.5, fixed
S2  FORWARD    topo order: build streams from q_w, apply Transform at every
               element, run element-level conservation check, evaluate constraints.
S3  THROTTLE   for each violated capacity constraint (deterministic element order):
                 excess = Load − Capacity
                 reduce each contributing completion's target PRO-RATA to its
                 provenance share through that element; record
                 deferred[(element, kind)] += reduction · segmentDuration.
               Throttled targets re-enter S1 as upper bounds on q*.
S4  BACKWARD   with rates fixed, from each sink upward:
                 P_upstream = P_downstream + ΔP_element(q)   // element's hydraulic transform
               yielding new Pwh_w for every completion — EXCEPT completions whose
               choke reports critical flow (SDD-003 §6.3): they are flagged
               PRESSURE-DECOUPLED and keep their rate until the ratio goes
               sub-critical. This is why a choked well survives backpressure
               swings, and it is what damps oscillation on shared lines.
S5  CONVERGED? max_w |q_w − q_prev_w| / max(q_w, q_floor) < 1e-4
               AND no capacity violated AND max_w |ΔPwh_w| < 1 kPa
               → ATTRIBUTE, done.  q_floor = 1e-8 m³/s.
S6  BUDGET     outer iterations S1–S5 capped at maxOuterIterations (content,
               default 200). Exhausted → SHUT-IN LADDER:
                 pick completion with largest relative residual (ties: lowest id),
                 force it shut for this segment (cause: solver-stability, audited,
                 event flow.forcedShutIn), restart S1 with fresh budget.
               Ladder steps ≤ completion count → termination by construction.
```

**Numeric guards (every step):** any `NaN`/`Infinity` in any stream, pressure or
rate → immediate **model fault naming the element** — never propagated, never
clamped.

**Why pro-rata apportionment (S3) and not priority:** it is the only rule that
is simultaneously deterministic, explainable in the bottleneck report
("SEP-01 throttled all three wells 18%"), and free of hidden favouritism.
Priority throttling (protect the best well) is a *player policy*, expressible
through chokes — not a solver default. Open item S002-1 keeps the door open.

## 8. Attribution

```csharp
// Pass-4 amendment (finding 73): the report carries the CONVERGED STATE, not
// just attribution — §9's commit step consumes Solutions; the next solve's S0
// seeds from CompletionStates. Diagnostics-only output made both impossible.
public sealed record ElementSolution(EntityId<IFlowElement> Element, TransformResult Converged);
public sealed record CompletionState(EntityId<IFlowElement> Completion, ReservoirRate Rate, Pressure WellheadBackpressure);

public sealed record SolveReport(
    IReadOnlyList<ElementSolution> Solutions,
    IReadOnlyList<CompletionState> CompletionStates,
    IReadOnlyList<(EntityId<IFlowElement> Element, ConstraintKind Kind, Mass Deferred)> Deferrals,
    IReadOnlyList<ForcedShutIn> ForcedShutIns,
    int OuterIterations);
```

Deferrals accumulate across segments (duration-weighted in S3 already) and feed
the bottleneck report, `flow.constraintBound` events and the read model's
deferred-by-element projection verbatim — the UI never re-derives them.

## 9. Commit and conservation

> **Pass-3 amendment (finding 67):** the commit family is pinned:
> `ICommitTarget { EntityRef Target }` with `IWithdrawalTarget.CommitWithdrawal(Composition)`,
> `IReceiptTarget.CommitReceipt(Composition, Allocation)` and
> `ICustodyRecorder.RecordDelivery(Composition, Allocation)`. Injection commits
> as a receipt targeting the compartment. Nothing else mutates from a solve.

Stage 6, after **all** segments have solved (FV13 atomicity):

1. Duration-weight each segment's committed masses (durations are /30ths-of-tick
   rationals — SDD-001 §9 — so the weights are exact).
2. Apply withdrawals to compartments (SDD-003 §3), receipts to inventories,
   deliveries to custody records — through `ICommitTarget` interfaces, the only
   mutation path.
3. Assert the tick-level balance of [04](../design/04_MATERIAL_AND_FLOW.md) §7
   per material. Summation: plain doubles in the single deterministic element
   order (no parallelism, so no reassociation). Tolerance:
   `|imbalance| ≤ max(1e-9 · extractedTotal, 1 kg)`. Violation → INV1 halt with
   the per-element breakdown from the element-level checks.

## 10. Error surface

| Situation | Response |
|---|---|
| NaN/Inf anywhere in a solve | Model fault, element named |
| Element-level conservation breach in `Transform` | Model fault, element named — caught before it can become INV1 |
| Network build finds a cycle or a spec gate without a Reject port | Composition fault — refuses to start |
| Budget exhausted | Shut-in ladder (§7 S6) — never a fault by itself |
| Same completion forced shut in consecutive ticks | `flow.solverFault` at `C` (04 §4.0b) |
| Tick-level conservation breach at commit | INV1 — halt |

## 11. Test mapping

FV1–FV13 map directly; the ones this document newly pins:
**FV3** asserts §7's damped fixed-point against independently computed operating
points; **FV4/FV11** assert the S3 bookkeeping numerically; **FV9** asserts the
ladder including the ≤-completion-count termination bound; **FV13** asserts
nothing commits until all segments solve. R4-V12/V13 assert pro-rata
apportionment exactly.

## 12. Open items

| # | Item | Trigger |
|---|---|---|
| S002-1 | Player-policy throttling (priority orders) as choke-level content, layered on the pro-rata default | R8, if the bottleneck report shows players wanting it |
| S002-2 | λ adaptation if 200 iterations proves tight on gas-lift-heavy networks (gas lift couples rate→gas→lift→rate) | R7 integration tests |
| S002-3 | `Composition` pooling strategy if allocation profiling demands it | R4 benchmarks (R4-V18) |
