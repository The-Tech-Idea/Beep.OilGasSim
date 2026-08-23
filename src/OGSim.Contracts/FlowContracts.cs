// SDD-002 §5–8 — elements, transforms, the solver surface. The solver knows
// only IFlowElement (design 04 §1): adding equipment never touches it.

using OGSim.Kernel;

namespace OGSim.Contracts;

public readonly record struct PortId(int Index);

public enum PortDirection { Inlet, Outlet }

/// <summary>Reject: where spec-failing mass routes (e.g. a flare). An element
/// with a spec gate MUST declare one — checked at network build (SDD-002 §5).</summary>
public enum PortRole { Main, Gas, Liquid, Water, Reject }

public sealed record PortSpec(PortId Id, PortDirection Direction, PortRole Role);

/// <summary>SDD-002 §5 constraint kinds — either capacity of a separator can bind (R8-V2).</summary>
public enum ConstraintKind
{
    GasCapacity, LiquidCapacity, TotalCapacity, Ullage, PressureRating,
    Power, ErosionalVelocity, SpecGate, BerthOccupancy, Injectivity
}

public sealed record ConstraintEvaluation(
    ConstraintKind Kind,
    double Capacity,
    double Load);

/// <summary>Ambient inputs per segment (SDD-016: one climate region per location).</summary>
public sealed record SegmentContext(
    int DurationDays,
    Temperature Ambient,
    double WeatherSeverity);

/// <summary>
/// What an element is given for a segment (SDD-002 §5).
///
/// <para><see cref="SolvedRate"/> is how §7 S2's "build streams from q_w"
/// reaches the element: the solver hands a completion the rate S1 damped and S3
/// capped, and the element turns it into a stream through its own PVT. Since
/// <c>Transform</c> is pure, this is the ONLY channel — without it S3's cap
/// would adjust a number the forward pass never reads.</para>
///
/// <para>Null means the solver holds no rate for this element, which is not the
/// same as zero: a shut-in or DEAD completion is asked for 0.0 and must produce
/// nothing, while a non-completion is asked for nothing at all.</para>
/// </summary>
public sealed record TransformInput(
    IReadOnlyList<MaterialStream> Inlets,
    SegmentContext Segment,
    ReservoirRate? SolvedRate)
{
    // Finding 131. Comparing two transform inputs is how a test asks "did the
    // damped iteration actually move?", which by reference is always yes.
    public bool Equals(TransformInput? other) =>
        other is not null && Segment == other.Segment && SolvedRate.Equals(other.SolvedRate)
        && Structural.Equal(Inlets, other.Inlets);

    public override int GetHashCode() =>
        HashCode.Combine(Segment, SolvedRate, Structural.HashOf(Inlets));
}

/// <summary>Kind-tagged mass leaving the network — each maps 1:1 onto its
/// 04 §7 conservation term (SDD-002 §5, third pass).</summary>
public sealed record DisposedMass(
    Composition Flared,
    Composition Vented,
    Composition Discharged);

/// <summary>
/// Element-level conservation, the complete form (SDD-002 §5):
/// Σ inlets + Sourced == Σ outlets + FuelConsumed + Disposed, per material —
/// checked after every transform, which is what makes an INV1 breakdown
/// attributable to a single element.
/// </summary>
public sealed record TransformResult(
    IReadOnlyList<MaterialStream> Outlets,
    Composition Sourced,
    Composition FuelConsumed,
    DisposedMass Disposed,
    Power PowerDraw)
{
    // Finding 131 — and this one is load-bearing: S5 convergence compares one
    // iteration's result against the last, and reference equality would report
    // a converged network as still moving.
    public bool Equals(TransformResult? other) =>
        other is not null && Sourced == other.Sourced && FuelConsumed == other.FuelConsumed
        && Disposed == other.Disposed && PowerDraw == other.PowerDraw
        && Structural.Equal(Outlets, other.Outlets);

    public override int GetHashCode() =>
        HashCode.Combine(Sourced, FuelConsumed, Disposed, PowerDraw,
                         Structural.HashOf(Outlets));
}

/// <summary>
/// Anything the stream passes through (design 02 §1.5). Transform is PURE —
/// commit happens at stage 6. Availability is NOT here: the segment plan lists
/// available elements; an unavailable element is absent from the network.
/// </summary>
public interface IFlowElement
{
    EntityId<IFlowElement> Id { get; }
    IReadOnlyList<PortSpec> Ports { get; }
    TransformResult Transform(TransformInput input);
    IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input);
}

/// <summary>
/// An element that HOLDS its inlet at a pressure instead of dropping the one it
/// is handed (SDD-002 §7 S4).
///
/// <para>S4's <c>P_downstream + ΔP</c> describes a FRICTION element — a
/// flowline, a choke — and the solver infers ΔP the only way a pure transform
/// allows, from what the stream lost crossing it. For a vessel held at a set
/// point by a controller that inference yields "whatever arrived minus my set
/// point", which grows with the pressure upstream: a separator fed from a
/// completion at reservoir pressure would demand an inlet high enough to shut
/// the well it exists to receive from.</para>
///
/// <para>What the arithmetic could not say is that <b>a controller decouples
/// upstream from downstream</b> — the entire purpose of the device, and a
/// concept this design already has one instance of in the critical-choke
/// completion S4 flags pressure-decoupled.</para>
/// </summary>
public interface IPressureController : IFlowElement
{
    /// <summary>
    /// What the controller holds its inlet at. S4 propagates this as a FLOOR,
    /// not a fixed value: a controller can hold pressure up — it is a
    /// restriction, not a pump — so a downstream demand above the set point
    /// passes straight through. Pinning it outright would make a facility a
    /// wall, and a full tank could never back up through the separator ahead of
    /// it (R8-V5).
    /// </summary>
    Pressure SetPoint { get; }
}

public sealed record ForcedShutIn(
    EntityId<IFlowElement> Completion,
    double RelativeResidual);

/// <summary>One element's converged transform — what stage 6 duration-weights and commits.</summary>
public sealed record ElementSolution(
    EntityId<IFlowElement> Element,
    TransformResult Converged);

/// <summary>A completion's converged state — S0 of the NEXT segment/tick initialises from these.</summary>
public sealed record CompletionState(
    EntityId<IFlowElement> Completion,
    ReservoirRate Rate,
    Pressure WellheadBackpressure);

/// <summary>
/// The complete solve output (SDD-002 §8; pass 4, finding 73): attribution AND
/// the converged state. Without Solutions the §9 commit step had no input;
/// without CompletionStates the next solve's S0 had no seed.
/// </summary>
public sealed record SolveReport(
    IReadOnlyList<ElementSolution> Solutions,
    IReadOnlyList<CompletionState> CompletionStates,
    IReadOnlyList<(EntityId<IFlowElement> Element, ConstraintKind Kind, Mass Deferred)> Deferrals,

    /// <summary>SDD-002 §8's finding-283 amendment — every constraint the
    /// attribution pass evaluated, not only the ones that bound. The same
    /// walk that builds <see cref="Deferrals"/>; empty for an element with
    /// no capacity constraint of its own (a custody transfer point), the
    /// same way it is absent from Deferrals.</summary>
    IReadOnlyList<(EntityId<IFlowElement> Element, ConstraintKind Kind, double Capacity, double Load)> Utilisations,

    IReadOnlyList<ForcedShutIn> ForcedShutIns,
    int OuterIterations)
{
    // Finding 131.
    public bool Equals(SolveReport? other) =>
        other is not null && OuterIterations == other.OuterIterations
        && Structural.Equal(Solutions, other.Solutions)
        && Structural.Equal(CompletionStates, other.CompletionStates)
        && Structural.Equal(Deferrals, other.Deferrals)
        && Structural.Equal(Utilisations, other.Utilisations)
        && Structural.Equal(ForcedShutIns, other.ForcedShutIns);

    public override int GetHashCode() =>
        HashCode.Combine(OuterIterations, Structural.HashOf(Solutions),
            Structural.HashOf(CompletionStates), Structural.HashOf(Deferrals),
            Structural.HashOf(Utilisations), Structural.HashOf(ForcedShutIns));
}

/// <summary>
/// SDD-002 §9 — the ONLY mutation path out of a solve. Transform is pure; at
/// stage 6, after ALL segments solve, duration-weighted masses commit through
/// these and nothing else (third pass, finding 67: the SDD named this family
/// but no type declared it).
/// </summary>
public interface ICommitTarget
{
    EntityRef Target { get; }
}

/// <summary>Compartment withdrawal (SDD-003 §3).</summary>
public interface IWithdrawalTarget : ICommitTarget
{
    void CommitWithdrawal(Composition mass);
}

/// <summary>Inventory receipt — tanks, line fill.</summary>
public interface IReceiptTarget : ICommitTarget
{
    void CommitReceipt(Composition mass, Allocation provenance);
}

/// <summary>Custody record — the delivery stage 8 prices; never priced here.</summary>
public interface ICustodyRecorder : ICommitTarget
{
    void RecordDelivery(Composition mass, Allocation provenance);
}

/// <summary>An edge: who feeds whom, port to port. State owned by the modules
/// that create it (flowline laid, tie-in made); validated tree-ward at network
/// build (SDD-002 §6).</summary>
public sealed record FlowConnection(
    EntityId<IFlowElement> From, PortId FromPort,
    EntityId<IFlowElement> To, PortId ToPort);

/// <summary>What the solver actually receives — elements AND their wiring
/// (second contract pass: the solver had elements but no topology).</summary>
public sealed record FlowTopology(
    IReadOnlyList<IFlowElement> Elements,
    IReadOnlyList<FlowConnection> Connections)
{
    // Finding 131 — two segments whose availability sets coincide produce the
    // same topology, and that is worth being able to notice.
    public bool Equals(FlowTopology? other) =>
        other is not null
        && Structural.Equal(Elements, other.Elements)
        && Structural.Equal(Connections, other.Connections);

    public override int GetHashCode() =>
        HashCode.Combine(Structural.HashOf(Elements), Structural.HashOf(Connections));
}

/// <summary>
/// The one flow engine (concept G12), replaceable per FD1. The algorithm —
/// damped fixed-point, pro-rata throttling, the shut-in ladder — is pinned in
/// SDD-002 §7; per-segment invocation in SDD-002 §9.
/// </summary>
public interface IFlowSolver
{
    SolveReport Solve(SegmentContext segment, FlowTopology topology);
}
