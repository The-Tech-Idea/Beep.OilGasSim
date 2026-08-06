// SDD-003 §5–6 — the PPDM four-level hierarchy (design 02 §3): a well is not a
// hole. IWell owns identity and status; IWellbore geometry; ICompletion the
// physics; Perforation the reservoir connection.

using OGSim.Kernel;

namespace OGSim.Contracts;

/// <summary>Design 02 §3.4 — every transition is a command; none skips abandonment.</summary>
public enum WellStatus
{
    Proposed, Permitted, Drilling, DryHole, Logged, SuspendedNonCommercial,
    Completing, Producing, ShutIn, Workover, Injecting, Abandoned
}

public enum WellClassification { Exploration, Appraisal, Development, Injector, Observation }

/// <summary>Identity + status machine; NO physics (SDD-003 §5).</summary>
public interface IWell
{
    EntityId<IWell> Id { get; }
    WellStatus Status { get; }
    WellClassification Classification { get; }
    EntityId<ILicence> Licence { get; }
    Coordinate Surface { get; }
    IReadOnlyList<EntityId<IWellbore>> Wellbores { get; }
}

/// <summary>A trajectory station: measured depth, true vertical depth, plan position.</summary>
public readonly record struct TrajectoryStation(Length Md, Length Tvd, Coordinate Position);

public sealed record Trajectory(IReadOnlyList<TrajectoryStation> Stations);

/// <summary>A physical hole — the original plus each sidetrack (design 02 §3.1).</summary>
public interface IWellbore
{
    EntityId<IWellbore> Id { get; }
    EntityId<IWell> Well { get; }
    Trajectory Path { get; }
    Length ContactLengthIn(EntityId<IReservoirCompartmentEntity> compartment);
    IReadOnlyList<EntityId<ICompletion>> Completions { get; }
}

/// <summary>
/// The connection to one compartment (design 02 §3.1). Standoff to the nearest
/// contact is DERIVED each tick from trajectory + contacts, never stored
/// (law L5; SDD-003 §5) — it feeds the coning model (05 §3.3b).
/// </summary>
public sealed record Perforation(
    EntityId<IReservoirCompartmentEntity> Drains,
    Length TopMd,
    Length BottomMd,
    double Skin,
    bool Isolated);

/// <summary>Inflow: SI Darcy/Vogel composite per perforation (SDD-003 §6.1).</summary>
public interface IInflowModel
{
    ContentId Id { get; }
    ReservoirRate InflowAt(Pressure reservoirPressure, Pressure bottomholePressure, Perforation perforation);
}

/// <summary>Outflow: required bottomhole pressure to deliver q to the wellhead (SDD-003 §6.2).</summary>
public interface IOutflowModel
{
    ContentId Id { get; }
    Pressure RequiredBottomhole(ReservoirRate rate, Pressure wellheadPressure);
}

/// <summary>A lift method modifies the VLP — its tier datasheet IS the effect (07 §4b).</summary>
public interface ILiftMethod
{
    ContentId InstalledTier { get; }
}

/// <summary>
/// The operating point outcome: DEAD is a distinct result, not a zero rate —
/// "cannot flow at any rate" and "produced nothing" have different remedies
/// (SDD-003 §6.3, R6-V6).
/// </summary>
public abstract record OperatingPoint;
public sealed record Flowing(ReservoirRate Rate, Pressure Bottomhole) : OperatingPoint;
public sealed record Dead : OperatingPoint;

/// <summary>
/// The source element (design 02 §3.1: where the well physics lives). As an
/// IFlowElement it has no inlets and reports withdrawal as TransformResult.Sourced.
/// </summary>
public interface ICompletion : IFlowElement
{
    EntityId<ICompletion> CompletionId { get; }
    EntityId<IWellbore> Wellbore { get; }
    IReadOnlyList<Perforation> Perforations { get; }
    ILiftMethod? Lift { get; }
    OperatingPoint SolveOperatingPoint(Pressure wellheadBackpressure);
}
