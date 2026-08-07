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

public sealed record Trajectory(IReadOnlyList<TrajectoryStation> Stations)
{
    // Finding 131: a trajectory is the survey, and two wellbores drilled to the
    // same survey have the same trajectory. Reference equality would make a
    // restored wellbore differ from the one that was saved.
    public bool Equals(Trajectory? other) =>
        other is not null && Structural.Equal(Stations, other.Stations);

    public override int GetHashCode() => Structural.HashOf(Stations);
}

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

/// <summary>
/// SDD-003 §6.2's three lift hooks, plus the power draw R7 §2.4 needs, as ONE
/// value.
///
/// <para>One value rather than four members, deliberately: the outflow model
/// applies every field uniformly, a gas-lift method leaves
/// <see cref="PressureBoost"/> at zero because it genuinely adds no pressure,
/// and no code anywhere asks which method is installed. Four separate members
/// would invite <c>if (lift is Esp)</c> at the first call site needing only
/// one of them — an equipment hierarchy by the back door (design 02 §4.1).</para>
/// </summary>
public sealed record LiftEffect(
    Pressure PressureBoost,           // ESP / jet pump
    double DensityFactor,             // gas lift: lightens the column, ≤ 1
    ReservoirRate? DisplacementCap,   // rod pump / PCP: a hard ceiling
    Power PowerDraw)
{
    /// <summary>No lift. Not a fallback — a naturally flowing well genuinely has
    /// no lift effect, and the VLP applies this exactly as it applies any other.</summary>
    public static LiftEffect None { get; } =
        new(new Pressure(0.0), DensityFactor: 1.0, DisplacementCap: null, new Power(0.0));
}

/// <summary>R7 §2.2 — each method declares where it works.</summary>
public sealed record LiftEnvelope(
    ReservoirRate MinRate,
    ReservoirRate MaxRate,
    Length MaxDepth,
    double MaxDeviationDegrees,
    double MaxGasFraction,
    Temperature MaxTemperature,
    double MaxSolidsFraction);

/// <summary>The well as the lift method finds it.</summary>
public sealed record LiftConditions(
    ReservoirRate Rate,
    Length Depth,
    double DeviationDegrees,
    double GasFraction,
    Temperature Temperature,
    double SolidsFraction);

/// <summary>
/// R7 §2.2's consequence. Operating outside the envelope DEGRADES performance
/// and raises the failure hazard; it does not refuse installation.
///
/// <para>The interesting failure is the player who puts an ESP in a gassy well,
/// sees good rates for eight months and then loses the pump. A hard block
/// teaches nothing; a consequence teaches the envelope — and the envelope is
/// readable before installing, so it is a trap that can be avoided.</para>
/// </summary>
public sealed record EnvelopeAssessment(
    bool Within,
    double PerformanceFactor,         // ≤ 1
    double HazardMultiplier,          // ≥ 1
    IReadOnlyList<ContentId> Exceeded)
{
    // Finding 131.
    public bool Equals(EnvelopeAssessment? other) =>
        other is not null && Within == other.Within
        && PerformanceFactor.Equals(other.PerformanceFactor)
        && HazardMultiplier.Equals(other.HazardMultiplier)
        && Structural.Equal(Exceeded, other.Exceeded);

    public override int GetHashCode() =>
        HashCode.Combine(Within, PerformanceFactor, HazardMultiplier,
                         Structural.HashOf(Exceeded));
}

/// <summary>A lift method modifies the VLP — its tier datasheet IS the effect (07 §4b).</summary>
public interface ILiftMethod : IWellComponent
{
    ContentId InstalledTier { get; }
    LiftEnvelope Envelope { get; }
    EnvelopeAssessment Assess(LiftConditions conditions);
    LiftEffect EffectAt(ReservoirRate rate, Density mixtureDensity);
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

    /// <summary>
    /// SDD-002 §7 S4: true while the choke reports critical flow (SDD-003 §6.3).
    /// The completion keeps its rate and ignores backpressure until sub-critical,
    /// which is why a choked well survives backpressure swings on a shared line.
    ///
    /// <para>Owned here because the choke that decides it is the completion's own
    /// component. The solver only asks.</para>
    /// </summary>
    bool IsPressureDecoupled { get; }
}

/// <summary>
/// Design 02 §3.2 — an equipment instance. It carries only what is per-asset;
/// everything specifiable lives in the catalogue <see cref="Tier"/> it
/// references (design 07 §4b): size, rating, material, performance curves,
/// capital cost, install duration, degradation profile, requiresTech.
///
/// <para>There is no component-TYPE hierarchy — no <c>ITubing</c>, no
/// <c>IPacker</c>, no <c>IChoke</c>. Design 02 §4.1 and non-negotiable 11 apply
/// to well equipment exactly as to facility units: adding a kind of component is
/// a JSON entry, never a type.</para>
/// </summary>
public interface IWellComponent
{
    EntityId<IWellComponent> Id { get; }
    ContentId Tier { get; }
    double Condition { get; }        // 0..1, per-asset
    GameDate Installed { get; }
}
