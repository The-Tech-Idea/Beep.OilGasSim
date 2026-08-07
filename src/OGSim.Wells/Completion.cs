// R6.3 / R6.9 / R6.10 — the completion: the network's source element
// (SDD-003 §6, design 02 §3.1).
//
// This is where the "one engine" claim is genuinely tested (R6 §2.3). The
// completion takes wellhead pressure as a BOUNDARY CONDITION from the flow
// solve, so a full tank raises manifold pressure, raises wellhead pressure,
// raises Pwf, reduces drawdown, and reduces withdrawal — with no rule anywhere
// saying tanks affect reservoirs. If R4 needed changing to accommodate this, the
// claim was false; it did not.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Wells;

/// <summary>
/// The choke's settings, from its `well-component` catalogue tier (SDD-003 §5.1).
/// NOT an <c>IChoke</c>: design 02 §4.1 and non-negotiable 11 keep equipment
/// types out of code, so a choke is an instance plus a content tier, and this is
/// the part of the tier §6.3 reads.
/// </summary>
public sealed record ChokeSetting(double CriticalPressureRatio, ReservoirRate CriticalRate)
{
    /// <summary>Wide open: no restriction, never critical. The natural-flow case,
    /// and not a fallback — a well with no choke installed genuinely has none.</summary>
    public static ChokeSetting Open { get; } =
        new(CriticalPressureRatio: 0.0, CriticalRate: new ReservoirRate(double.PositiveInfinity));
}

/// <summary>What a completion needs to turn a solved rate into a stream.</summary>
public sealed record CompletionFluid(
    Density SurfaceDensity,                 // ρ at standard conditions, kg/m³
    FormationVolumeFactor OilFormationVolumeFactor,
    Allocation Provenance,                  // which compartments the mass is credited to
    Pressure ReservoirPressure,             // Pr — from the compartment, through a contract
    Temperature ReservoirTemperature);

/// <summary>
/// SDD-003 §6. A source element: no inlets, and its withdrawal reported as
/// <c>TransformResult.Sourced</c>, which is how SDD-002 §5's element-level
/// conservation check comes to cover wells at all.
/// </summary>
public sealed class Completion : ICompletion
{
    private readonly IInflowModel _inflow;
    private readonly IOutflowModel _outflow;
    private readonly CompletionFluid _fluid;
    private readonly ChokeSetting _choke;
    private readonly int _materialOrdinal;
    private readonly int _materialCount;

    private bool _pressureDecoupled;

    public Completion(
        EntityId<ICompletion> completionId,
        EntityId<IWellbore> wellbore,
        IReadOnlyList<Perforation> perforations,
        IInflowModel inflow,
        IOutflowModel outflow,
        CompletionFluid fluid,
        ChokeSetting choke,
        int materialOrdinal,
        int materialCount,
        ILiftMethod? lift)
    {
        ArgumentNullException.ThrowIfNull(perforations);
        ArgumentNullException.ThrowIfNull(inflow);
        ArgumentNullException.ThrowIfNull(outflow);
        ArgumentNullException.ThrowIfNull(fluid);
        ArgumentNullException.ThrowIfNull(choke);

        if (perforations.Count == 0)
            throw new ModelFault("SDD-003 §6", null,
                $"completion {completionId.Value} has no perforations; a completion that " +
                "connects to no compartment is not a completion");

        CompletionId = completionId;
        Id = new EntityId<IFlowElement>(completionId.Value);
        Wellbore = wellbore;
        Perforations = perforations;
        Lift = lift;

        _inflow = inflow;
        _outflow = outflow;
        _fluid = fluid;
        _choke = choke;
        _materialOrdinal = materialOrdinal;
        _materialCount = materialCount;
    }

    public EntityId<ICompletion> CompletionId { get; }

    public EntityId<IFlowElement> Id { get; }

    public EntityId<IWellbore> Wellbore { get; }

    public IReadOnlyList<Perforation> Perforations { get; }

    public ILiftMethod? Lift { get; }

    public bool IsPressureDecoupled => _pressureDecoupled;

    /// <summary>One outlet: the wellhead. No inlets — a source element.</summary>
    public IReadOnlyList<PortSpec> Ports { get; } =
        [new PortSpec(new PortId(0), PortDirection.Outlet, PortRole.Main)];

    /// <summary>
    /// SDD-003 §6.3. Solves the operating point, then applies the choke.
    ///
    /// <para>The choke is applied AFTER the point, not inside it: it restricts
    /// what leaves, and a completion whose choke reports critical flow becomes
    /// PRESSURE-DECOUPLED — it keeps its rate and stops responding to
    /// backpressure until sub-critical again. That is what lets a choked well
    /// survive S4's backpressure swings on a shared line.</para>
    /// </summary>
    public OperatingPoint SolveOperatingPoint(Pressure wellheadBackpressure)
    {
        OperatingPoint point = OperatingPointSolver.Solve(
            _fluid.ReservoirPressure, wellheadBackpressure,
            _inflow, _outflow, Perforations);

        if (point is not Flowing flowing)
        {
            _pressureDecoupled = false;
            return point;
        }

        // R7-V6 / SDD-003 §6.2. A positive-displacement pump moves a fixed
        // volume per stroke, so the rate is the PUMP's and not the reservoir's:
        // a well capable of ten times the displacement produces the
        // displacement. Applied here rather than in the VLP because it is a
        // bound on rate, and the VLP speaks in pressures.
        if (Lift?.EffectAt(flowing.Rate, _fluid.SurfaceDensity).DisplacementCap
            is ReservoirRate cap
            && flowing.Rate.CubicMetresPerSecond > cap.CubicMetresPerSecond)
            flowing = new Flowing(cap, flowing.Bottomhole);

        // Critical when the pressure ratio across the choke falls below its
        // critical value: downstream can no longer signal upstream at all.
        double ratio = wellheadBackpressure.Pascals / flowing.Bottomhole.Pascals;
        bool critical = ratio < _choke.CriticalPressureRatio;

        if (!critical)
        {
            _pressureDecoupled = false;
            return flowing;
        }

        _pressureDecoupled = true;

        double capped = Math.Min(
            flowing.Rate.CubicMetresPerSecond, _choke.CriticalRate.CubicMetresPerSecond);

        return new Flowing(new ReservoirRate(capped), flowing.Bottomhole);
    }

    /// <summary>
    /// SDD-002 §5. Turns the rate the solver handed us into a stream.
    ///
    /// <para><c>SolvedRate</c> is the rate S1 damped and S3 capped — NOT a rate
    /// this element recomputes. Recomputing here would silently discard the
    /// solver's cap, and the network's mass would stop matching the rate the
    /// solver believes it allowed.</para>
    /// </summary>
    public TransformResult Transform(TransformInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        // Null means the solver holds no rate for this element, which for a
        // completion cannot happen: S1 solves one every iteration.
        if (input.SolvedRate is not ReservoirRate rate)
            throw new InvariantFault("SDD-002 §5", null,
                $"completion {CompletionId.Value} was not given a solved rate; " +
                "the solver must hand every completion the rate it solved");

        // Reservoir volume to surface volume through the FVF, then to mass. The
        // conversion needs the factor in hand — the type system will not let a
        // ReservoirVolume be added to a SurfaceVolume without one (kernel
        // Volumes.cs), which is exactly the error this crossing invites.
        double surfaceRate =
            rate.CubicMetresPerSecond / _fluid.OilFormationVolumeFactor.RbPerStb;

        double[] byOrdinal = new double[_materialCount];
        byOrdinal[_materialOrdinal] = surfaceRate * _fluid.SurfaceDensity.KgPerCubicMetre;

        Composition produced = Composition.Validated([.. byOrdinal]);

        return new TransformResult(
            [new MaterialStream(produced, _fluid.ReservoirPressure,
                                _fluid.ReservoirTemperature, _fluid.Provenance)],
            Sourced: produced,          // 0 in + Sourced = out: the element conserves
            FuelConsumed: Composition.Zero(_materialCount),
            Disposed: new DisposedMass(
                Composition.Zero(_materialCount),
                Composition.Zero(_materialCount),
                Composition.Zero(_materialCount)),
            PowerDraw: new Power(0.0));
    }

    /// <summary>
    /// A completion reports no capacity constraint. Its limit is the IPR, which
    /// is already in the rate — reporting it again as a constraint would let S3
    /// throttle a well for being at its own operating point.
    /// </summary>
    public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input) => [];
}
