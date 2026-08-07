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

## 2b. Properties, distributions and the material catalogue — R2.1–R2.4

> **Ninth contract pass (finding 82), R2.0.** The eight R1-C passes declared every
> [03](../design/03_ARCHITECTURE.md) §3.2 replaceable slot but never the property
> and material surface R2 is built from: `IPropertyKind`, `IProperty`,
> `IMaterial`, `IMaterialCatalog` and the distribution types appeared in the R2
> phase document's deliverables and in **no SDD and no code**. The decisions below
> are not new — they are [R2](../phases/R2_MATERIALS.md) §2.1–2.4, already
> approved — this section is the signature level they were missing.

```csharp
// ---- distributions (R2 §2.1: a property holds a distribution, never a value)
public abstract record Distribution
{
    public abstract double Mean { get; }
    public abstract double P90 { get; }   // LOW  — see the convention note
    public abstract double P50 { get; }
    public abstract double P10 { get; }   // HIGH
}

public sealed record PointValue(double Value) : Distribution;
public sealed record NormalDistribution(double Mean, double StandardDeviation) : Distribution;
public sealed record LogNormalDistribution(double LogMean, double LogStandardDeviation) : Distribution
{
    // R2-V5: a product of log-normals is log-normal, analytically.
    public static LogNormalDistribution Product(LogNormalDistribution a, LogNormalDistribution b);
}
public sealed record TriangularDistribution(double Minimum, double Mode, double Maximum) : Distribution;
public sealed record UniformDistribution(double Minimum, double Maximum) : Distribution;

// ---- property kinds (R2.1: dimension binding and validity range)
public interface IPropertyKind
{
    ContentId Id { get; }
    Dimension Dimension { get; }          // binds content's "3200 psi" to a quantity type
    double MinimumValid { get; }          // canonical SI, inclusive
    double MaximumValid { get; }
    BeliefSpace Space { get; }            // Log for volumes and permeability
}

// ---- properties (R2.2: value, provenance, uncertainty, as-of — all required)
public interface IProperty
{
    ContentId Kind { get; }
    Distribution Value { get; }
    Provenance Source { get; }
    GameDate AsOf { get; }
}

// ---- materials (R2.4)
public enum PhaseAtStandardConditions { Liquid, Gas, Aqueous, Solid }

public interface IMaterial
{
    ContentId Id { get; }
    MaterialId Ordinal { get; }           // catalogue position; NEVER persisted (SDD-004 §6)
    PhaseAtStandardConditions Phase { get; }
    IReadOnlyList<IProperty> Properties { get; }
}

public interface IMaterialCatalog
{
    int Count { get; }
    IMaterial this[MaterialId ordinal] { get; }
    IMaterial Resolve(ContentId id);      // unknown id → content fault, never null
    bool TryResolve(ContentId id, out IMaterial material);
}
```

**The P10/P90 convention is pinned here because it silently inverts reserves.**
Petroleum practice (SPE-PRMS, and [08](../design/08_ECONOMICS.md)'s 1P/2P/3P) is
the *reverse* of the statistical reading: **`P90` is the LOW, conservative
estimate** — 90% probability of being exceeded, the proved case — and **`P10` is
the HIGH** one. Numerically `P90 < P50 < P10`. A contributor who reads `P10` as
"the 10th percentile" books possible reserves as proved and nothing in the type
system objects, which is exactly why the ordering is stated on the contract
rather than left to the reader.

**`Distribution` is closed to these five** (R2 §2.1). A point value is a
distribution with zero spread, not a special case — that is what stops consumers
reading a scalar and letting the uncertainty go decorative.

**Validity ranges are on the KIND, not the correlation.** R2-V10 requires an
out-of-range input to raise a model fault rather than extrapolate silently; a
range attached to the property kind is checked once at the boundary every value
crosses, instead of being restated by each correlation that consumes it.

**Ordinals are catalogue positions and never persist** — SDD-004 §6 already says
so for content generally, restated because `Composition` is a dense array indexed
by them, which makes the temptation to save one strong.

> **R2.1 layering correction.** [R2](../phases/R2_MATERIALS.md) §3's deliverables
> table places these contracts in `OGSim.Contracts` and their implementations in
> `OGSim.Kernel`. That is impossible: Contracts references Kernel and not the
> reverse, so no kernel type can implement a contract declared in Contracts. The
> table specified a build that could not compile.
>
> **The whole material layer lives in `OGSim.Kernel`**, on §1's own stated
> principle — stream types are there "because they are the currency every module
> trades in", and a material catalogue is that same currency: `Composition` is a
> dense array indexed by `MaterialId`, which was already a kernel type. The
> alternative, an `OGSim.Materials` module, would add a project
> [03](../design/03_ARCHITECTURE.md) §8 does not list, to hold types every module
> needs — which is the "shared Common project" that SDD-000 §2 forbids, wearing a
> domain name.
>
> `BeliefSpace` and `Provenance` moved with them. `IProperty` needs both, and R2
> runs eleven phases before R14 — they are vocabulary for *how a value is known*,
> not belief state, and nothing in them knows what a belief is.

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

// Reject: where spec-failing mass is routed (e.g. to a flare element). An
// element with a spec gate MUST declare a Reject port — checked at network
// build (§6), because a spec that can refuse mass with nowhere to send it is a
// network that cannot conserve.
public enum PortRole { Main, Gas, Liquid, Water, Reject }

public sealed record PortSpec(PortId Id, PortDirection Direction, PortRole Role);

public enum PortDirection { Inlet, Outlet }

public interface IFlowElement
{
    EntityId<IFlowElement> Id { get; }
    IReadOnlyList<PortSpec> Ports { get; }

    // Called with proposed inlet streams for a segment. Must be PURE:
    // no state mutation — commit happens at stage 6 via ICommitTarget.
    TransformResult Transform(TransformInput input);

    // Constraints evaluated against the same proposal, returned rather than
    // written into a caller's buffer.
    IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input);
}

public sealed record TransformInput(
    IReadOnlyList<MaterialStream> Inlets,  // by inlet PortId.Index
    SegmentContext Segment,
    ReservoirRate? SolvedRate);            // §7 S2's q_w, for completion elements
    // SolvedRate is how "build streams from q_w" (§7 S2) reaches the element:
    // the solver hands a completion the rate S1 damped and S3 capped, and the
    // element turns that rate into a stream through its own PVT. NULL means the
    // solver holds no rate for this element (it is not a completion) — which is
    // NOT the same as zero: a shut-in or DEAD completion is asked for 0.0 and
    // must produce nothing, while a non-completion source is asked for nothing
    // and produces whatever it produces.
    //
    // Without this the cap S3 computes could never reduce the mass in the
    // network: Transform is pure, so an element cannot be told its rate by
    // mutation, and the solver must therefore pass it in.

public sealed record SegmentContext(
    int DurationDays,                      // /30ths grid, SDD-001 §9
    Temperature Ambient,
    double WeatherSeverity);               // from R22 (SDD-016)

public sealed record ConstraintEvaluation(ConstraintKind Kind, double Capacity, double Load);

public sealed record TransformResult(
    IReadOnlyList<MaterialStream> Outlets, // by outlet PortId.Index
    Composition Sourced,                   // SOURCE elements only: mass entering the network
    Composition FuelConsumed,              // the fuel term of 04 §7
    DisposedMass Disposed,                 // Flared | Vented | Discharged — 04 §7's disposal terms
    Power PowerDraw);                      // recorded for stage-4 duty next tick
    // ELEMENT-LEVEL CONSERVATION, the complete form (an earlier draft omitted
    // Sourced and Disposed, so a flare or a completion could not pass its own
    // check):  Σ inlets + Sourced == Σ outlets + FuelConsumed + Disposed
    // on TOTAL MASS, |error| <= max(1e-12 · massTotal, 1e-9 kg/s). Checked after
    // EVERY transform — what makes an INV1 breakdown attributable to one element.

public sealed record DisposedMass(
    Composition Flared,                    // combusted per the flare's efficiency
    Composition Vented,                    // uncombusted release (fugitives, boil-off without VRU)
    Composition Discharged);               // permitted outfall (treated water)
    // Each maps 1:1 onto its 04 §7 conservation term; Vented and Flared also
    // post to the emissions ledger at stage 9 (Flared via combustion products).

public enum ConstraintKind
{
    GasCapacity, LiquidCapacity, TotalCapacity, Ullage, PressureRating,
    Power, ErosionalVelocity, SpecGate, BerthOccupancy, Injectivity
}
```

> **Contract pass 10 — §5 brought to the committed shape.** Four divergences,
> three of them forced by the language rather than by design:
>
> - **`Stream` → `MaterialStream`** throughout. §4 above records the rename
>   (finding 61) and this section was never updated with it — the same
>   back-annotation miss that left §6 and §7 disagreeing below.
> - **`ReadOnlySpan<Stream>` → `IReadOnlyList<MaterialStream>`**, and
>   `TransformResult`/`DisposedMass` from `readonly struct` to `sealed record`.
>   A `ref struct` field cannot live in a record, and the transform results are
>   held across the solver's iterations rather than consumed within one stack
>   frame, so the span form was not implementable. The allocation is per element
>   per segment — four segments of a few hundred elements — not per solver
>   iteration, so D-4's per-tick-path concern does not bite here.
> - **`void EvaluateConstraints(in TransformInput, ref ConstraintWriter)` →
>   `IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput)`**.
>   `ConstraintWriter` was named here and declared in no SDD and no code, so the
>   signature could not be implemented as written. Returning the evaluations also
>   keeps `EvaluateConstraints` as pure as `Transform` — a caller-supplied
>   writer is a mutable output parameter, which is the one shape that could have
>   let an element smuggle state out of a pure call.
> - **`in` dropped from both parameters.** `TransformInput` is a record — a
>   reference type — so `in` bought nothing and read as though it were a struct.

> **R9.0 amendment (finding 118): INV1 is on TOTAL MASS, not per material.**
> This section said "per material", which was true of every element that existed
> when it was written — a separator splits phases of the same materials and a
> pipeline moves them. R9's NGL plant is the first element that CONVERTS: it
> takes propane dissolved in a gas stream and produces liquid propane, so gas
> mass falls and liquid mass rises by the same amount. Per-material closure is
> false for it and always will be.
>
> §4's own text already said "mass closure across the split", and the solver has
> always checked totals — `AssertElementConservation` sums across materials.
> So the code and SDD-006 agreed and only this line did not; it is corrected
> rather than the behaviour changed.
>
> **What is lost by relaxing it, and why that is acceptable:** a per-material
> check would catch an element that silently turned water into oil. Nothing else
> does — but nothing else needs to, because material identity is only ever
> changed by elements whose whole purpose is changing it, and each of those is
> element-checked on totals and tested on its own conversion (R9-V5, R9-V4).
> A stricter rule that every converting element had to be exempted from would
> have taught nobody anything.

> **R4 amendment (finding 96): `TransformInput.SolvedRate`.** §7 S2 says "build
> streams from `q_w`", but nothing in §5 gave the solver a way to hand `q_w` to
> an element. `Transform` is pure by §5's own rule, so the rate cannot arrive by
> mutation; with no channel for it, S3's pro-rata cap adjusted a number the
> forward pass never read, and a bound constraint could be violated for every one
> of S6's 200 iterations without the load ever falling. Found by FV7, which
> looped to budget exhaustion and then reported a ladder failure — blaming
> convergence for a wiring omission. The field closes the S1→S2 loop.

**Availability is not on the element.** The segment plan lists available
elements; an unavailable element is absent from the network
([04](../design/04_MATERIAL_AND_FLOW.md) §4). `Transform` being pure is what
makes per-segment solving and whole-tick abandonment cheap.

## 6. Network

```csharp
// An edge: who feeds whom, port to port. The state behind it is owned by the
// modules that create it (a flowline laid, a tie-in made) — the topology is a
// per-segment VIEW, never the owner of the connection (law L5).
public sealed record FlowConnection(
    EntityId<IFlowElement> From, PortId FromPort,
    EntityId<IFlowElement> To,   PortId ToPort);

// What the solver actually receives. Built per segment from
// (all elements) ∩ (segment availability set), with the connections among them.
public sealed record FlowTopology(
    IReadOnlyList<IFlowElement> Elements,
    IReadOnlyList<FlowConnection> Connections);

public interface IFlowSolver
{
    SolveReport Solve(SegmentContext segment, FlowTopology topology);   // §7, §8
}
```

**Validated at build** — a network that fails any of these is a composition
fault and the engine refuses to start (§10):

- every non-source element has exactly one downstream edge per outlet role path;
- no cycles — topology is a **tree toward each sink** (FD4); parallel loops are
  modelled as one combined element (R11 §6 risk note);
- an element with a spec gate declares a `Reject` port (§5).

**Recycle streams — gas lift, and any future recycle — are closed with a
one-tick lag and never as in-tick cycles.** Tick *t*'s lift-gas rate is tick
*t−1*'s committed value: a fixed sink at the compression side and an equal fixed
source at the completion in *t*, the same shape as the genset fuel sink
(SDD-006 §3b). FD4's tree stays a tree, the lag is one month of lift-gas ramp
(physically fine), and conservation holds per tick with the lagged pair audited
as a matched entry. Lift-gas provenance is the compression point's blend at
*t−1*.

**Element order is topological, ties broken by ascending `EntityId`** — the
single deterministic order every pass uses.

> **R20c review correction (finding 130).** This section says the topology is
> "built per segment from (all elements) ∩ (segment availability set)" and never
> said **by whom, from what**. `IFlowSolver.Solve` takes a `FlowTopology`;
> nothing produced one. Elements are created by four different modules — Wells
> makes completions, Facilities makes separators and tanks, Transport makes
> pipelines — and no contract let a stage see across them, so stage 5 could not
> be written at all: the solver was reachable only by a test that hand-built its
> input.
>
> ```csharp
> public interface IFlowElementRegistry
> {
>     void Add(IFlowElement element);              // the module that made it registers it
>     void Connect(FlowConnection connection);     // and the tie-ins it made
>     FlowTopology ViewFor(IReadOnlyCollection<EntityRef> available);   // per segment
> }
> ```
>
> **The registry holds edges; the modules keep their equipment.** L5 is not
> strained by this: "the state behind [an edge]" is the *flowline* — its length,
> diameter and condition — and that stays in the module that owns the pipeline.
> A `FlowConnection` is an immutable statement about which port feeds which,
> registered once, held in one place. What the law forbids is the topology
> owning a second copy of the equipment, and it does not.
>
> **`ViewFor` filters rather than mutates**, which is what makes it a view: an
> unavailable element is absent from the returned topology, and every connection
> touching it goes with it (design 04 §4 — "an unavailable element is absent
> from the network", not an element at zero rate). The registry is untouched, so
> the four segments of a tick each get their own view of one unchanging field
> and the tick can be abandoned whole without anything to undo.
>
> The registry is **provided by the flow module**, because it is the solver's
> input and the solver is what gives it meaning; the modules that create
> elements require it. That is a contract dependency, never an assembly one —
> Wells still references only Kernel and Contracts.

> **Contract pass 10 — §6 was the SDD contradicting itself.** This section
> declared `public sealed class FlowNetwork` while §7's pass-2 amendment and the
> committed code both say `FlowTopology(Elements, Connections)`. Two names for
> one concept inside one document is the exact failure glossary rule N1 exists to
> prevent, and it survived because §6's declaration was a class whose body was
> entirely comments — nothing to implement against, so nothing to notice. The
> rules those comments carried are real and are kept above as prose, where they
> belong: they are constraints on *building* a topology, not members of it.

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
                 reduce each contributing completion's target PRO-RATA to its
                 provenance share through that element.
               Throttled targets re-enter S1 as upper bounds on q*.
               The DEFERRED VOLUME is not recorded here — it is a property of the
               converged state, computed once by §8's attribution pass.
               A violated constraint with NO live completion upstream cannot be
               relieved by throttling at all: model fault naming the element and
               the constraint, rather than iterating to the S6 budget and then
               blaming the ladder for a composition problem.
S4  BACKWARD   with rates fixed, from each sink upward, starting at the network
               boundary pressure (content, default 101 325 Pa — a terminal sink
               discharges to somewhere, and zero absolute would let a completion
               flow against a vacuum):
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

// Pass 10: SolveReport carried these and nothing declared them. A completion the
// ladder shut in is not a fault by itself (§10) — it is a physical action the
// solver took — so the residual it was carrying when it went is recorded, and
// that number is what tells a second consecutive shut-in from a first (04 §4.0b).
public sealed record ForcedShutIn(EntityId<IFlowElement> Completion, double RelativeResidual);

public sealed record SolveReport(
    IReadOnlyList<ElementSolution> Solutions,
    IReadOnlyList<CompletionState> CompletionStates,
    IReadOnlyList<(EntityId<IFlowElement> Element, ConstraintKind Kind, Mass Deferred)> Deferrals,
    IReadOnlyList<ForcedShutIn> ForcedShutIns,
    int OuterIterations);
```

Deferrals accumulate across segments and feed the bottleneck report,
`flow.constraintBound` events and the read model's deferred-by-element
projection verbatim — the UI never re-derives them.

**The attribution pass.** Within one segment a deferral is computed ONCE, on the
converged state: the same network re-evaluated with every completion at the
UNCAPPED target S1 last produced, and each violated constraint's
`(Load − Capacity) × segmentDuration` recorded against its element.

> **R4 amendment (finding 97): deferrals cannot be recorded in S3.** The original
> text said S3 had already duration-weighted them, and that cannot work in either
> direction. Recording per iteration and summing makes the reported volume a
> function of how many iterations convergence happened to take — the same
> bottleneck reports a different number under different damping, which is a
> numerics artefact presented to the player as a business fact. Recording only
> the final iteration's excess reports zero, because §7 S5 refuses to declare
> convergence while any capacity is still violated: by the time the solve ends,
> the cap has removed the very excess that was to be measured.
>
> Measured against the uncapped targets instead, the figure is what the
> bottleneck actually refused, and it does not depend on the solver's path to the
> answer — which is what this section promises when it says the projection is
> used verbatim. Pinned by `FV4_the_deferred_volume_is_independent_of_the_iteration_count`.
>
> S3 keeps the pro-rata cap and nothing else. **Accumulation across segments is
> unaffected**: each segment contributes its own converged figure.

## 9. Commit and conservation

> **Pass-3 amendment (finding 67):** the commit family is pinned. **Pass 10**
> promotes it from this note into a declaration — it is the only mutation path
> out of a solve, which is too load-bearing to live in prose.

```csharp
// The ONLY way a solve changes anything. Transform is pure; at stage 6, after
// ALL segments have solved, duration-weighted masses commit through these and
// nothing else. Injection commits as a RECEIPT targeting the compartment —
// there is deliberately no separate injection target, because injection and
// tank receipt are the same act against different inventories.
public interface ICommitTarget          { EntityRef Target { get; } }

public interface IWithdrawalTarget : ICommitTarget
{
    void CommitWithdrawal(Composition mass);                        // SDD-003 §3
}

public interface IReceiptTarget : ICommitTarget
{
    void CommitReceipt(Composition mass, Allocation provenance);    // tanks, line fill, injection
}

public interface ICustodyRecorder : ICommitTarget
{
    void RecordDelivery(Composition mass, Allocation provenance);   // stage 8 prices it; never here
}
```

Provenance travels with a receipt and not with a withdrawal, and that asymmetry
is the point: a compartment is where provenance is *created*, so there is nothing
to carry in; an inventory is where provenance is *blended* (§3), so it must be
carried and merged. `RecordDelivery` takes it because royalty and working
interest are settled per source compartment.

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
