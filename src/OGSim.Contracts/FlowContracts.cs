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

public sealed record TransformInput(
    IReadOnlyList<MaterialStream> Inlets,
    SegmentContext Segment);

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
    Power PowerDraw);

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
    IReadOnlyList<ForcedShutIn> ForcedShutIns,
    int OuterIterations);

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
    IReadOnlyList<FlowConnection> Connections);

/// <summary>
/// The one flow engine (concept G12), replaceable per FD1. The algorithm —
/// damped fixed-point, pro-rata throttling, the shut-in ladder — is pinned in
/// SDD-002 §7; per-segment invocation in SDD-002 §9.
/// </summary>
public interface IFlowSolver
{
    SolveReport Solve(SegmentContext segment, FlowTopology topology);
}
