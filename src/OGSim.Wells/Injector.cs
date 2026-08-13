// R10.2 — injection and disposal (SDD-003 §3.1d, R10 §2.2).
//
// An injector is §6.1's Darcy form with the pressure difference reversed and
// water's viscosity in place of oil's. It is not a producer with a minus sign
// bolted on: the fluid is different, the skin grows instead of staying put, and
// the constraint it reports is Injectivity rather than a capacity.
//
// INJECTIVITY DECLINES. The formation plugs with solids and fines, so skin grows
// with cumulative volume, so the rate at a given pressure falls. That is what
// makes water disposal an ongoing operational problem rather than a one-time
// build, and it produces the authentic case where a field is throttled by
// disposal capacity and by nothing upstream at all (R10-V3).

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Wells;

/// <summary>What an injector needs to state a rate (SDD-003 §3.1d).</summary>
public sealed record InjectionConditions(
    Permeability Permeability,
    Length InjectionInterval,
    Area DrainageArea,
    Length WellboreRadius,
    Viscosity WaterViscosity,
    double InitialSkin,                 // s0
    double PluggingPerReferenceVolume,  // α
    ReservoirVolume ReferenceVolume);   // V_reference

/// <summary>
/// SDD-003 §3.1d. A disposal or water-injection well.
/// </summary>
public sealed class Injector : IFlowElement
{
    private readonly InjectionConditions _conditions;
    private readonly int _materialCount;
    private readonly int _waterOrdinal;
    private double _cumulativeInjectedM3;

    // Pushed in before the solve, exactly as a completion is refreshed with its
    // compartment's pressure (finding 137): an injector's acceptance depends on
    // what it is injecting against, and Transform is pure.
    private Pressure _reservoirPressure;
    private Pressure _injectionPressure;

    public Injector(
        EntityId<ICompletion> id,
        InjectionConditions conditions,
        int waterOrdinal,
        int materialCount)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        Validate(conditions);

        InjectorId = id;
        Id = new EntityId<IFlowElement>(id.Value);
        _conditions = conditions;
        _waterOrdinal = waterOrdinal;
        _materialCount = materialCount;
    }

    public EntityId<ICompletion> InjectorId { get; }

    /// <summary>SDD-003 §3.1d's R20d.4 amendment. An injector is a flow element:
    /// the `Injectivity` constraint it reports is read by the SOLVER, from
    /// `IFlowElement.EvaluateConstraints`, and nowhere else.</summary>
    public EntityId<IFlowElement> Id { get; }

    /// <summary>
    /// Two inlets, no outlets — water in, nothing out. A sink from the network's
    /// point of view.
    ///
    /// <para>The second is R20d.24's: produced water arrives on one port and
    /// imported flood water on the other, and they are DECLARED ports rather
    /// than an emergent commingle because §6's tree-toward-sink rule allows one
    /// edge per port. Both go down the same hole against the same injectivity,
    /// which is the trade — a flood competes with disposal for what the well
    /// will take, and plugs it twice as fast.</para>
    /// </summary>
    public IReadOnlyList<PortSpec> Ports { get; } =
    [
        new PortSpec(new PortId(0), PortDirection.Inlet, PortRole.Water),
        new PortSpec(new PortId(1), PortDirection.Inlet, PortRole.Water),
    ];

    /// <summary>Where produced water arrives, named so a caller wiring the chain
    /// never writes a bare index.</summary>
    public static PortId Inlet { get; } = new(0);

    /// <summary>Where imported flood water arrives (SDD-003 §3.1d's R20d.24
    /// amendment).</summary>
    public static PortId ImportInlet { get; } = new(1);

    /// <summary>
    /// The conditions it injects against, pushed in before the solve.
    ///
    /// <para>Pushed rather than pulled for the same reason a completion's are
    /// (finding 137): <c>Transform</c> is pure and <c>OGSim.Wells</c> cannot see
    /// a compartment. The composition that owns both sides passes the
    /// numbers.</para>
    /// </summary>
    public void SetInjectionConditions(Pressure reservoir, Pressure injection)
    {
        _reservoirPressure = reservoir;
        _injectionPressure = injection;
    }

    /// <summary>
    /// Everything offered leaves the network here (SDD-003 §3.1d's R20d.4
    /// amendment).
    ///
    /// <para>Reported as DISPOSED and never as a receipt against the
    /// compartment: SDD-002 §9 makes injection-as-pressure-support a commit-stage
    /// act, and an injector doing both would put the same mass on both sides of
    /// the tick balance. This well disposes; it claims nothing about
    /// pressure.</para>
    /// </summary>
    public TransformResult Transform(TransformInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        // SUMMED, never indexed. The solver's inlet list is COMPACTED — a port
        // whose upstream element is unavailable this segment is simply absent
        // (SolveState.InletsFrom), so index 0 is the lowest CONNECTED port and
        // not port zero. Reading a fixed index would silently drop the flood
        // water on a segment where the separator was down, and drop the produced
        // water on one where the intake was.
        Composition arriving = Composition.Zero(_materialCount);
        for (int i = 0; i < input.Inlets.Count; i++)
            arriving = arriving.Plus(input.Inlets[i].MassRates);

        return new TransformResult(
            [],
            Sourced: Composition.Zero(_materialCount),
            FuelConsumed: Composition.Zero(_materialCount),
            Disposed: new DisposedMass(
                Composition.Zero(_materialCount),
                Composition.Zero(_materialCount),
                Discharged: arriving),
            PowerDraw: new Power(0.0));
    }

    /// <summary>
    /// SDD-002 §5's constraint, so the solver throttles the FIELD against
    /// disposal exactly as it throttles it against a separator (R10-V3).
    ///
    /// <para>Volumetric, because injectivity is: the Darcy form answers in
    /// reservoir m³/s, so the offered mass is turned into a rate through water's
    /// density rather than compared against a mass rating that does not
    /// exist.</para>
    /// </summary>
    public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Inlets.Count == 0) return [];

        // EVERY INLET, for the reason Transform sums them — and because the
        // injectivity is the WELL's rather than a port's: produced water and
        // flood water are the same water once they are past the wellhead, and a
        // constraint evaluated on one of them would let the other in for free.
        var offered = 0.0;
        for (int i = 0; i < input.Inlets.Count; i++)
            offered += input.Inlets[i].MassRates[new MaterialId(_waterOrdinal)].KgPerSecond;

        return
        [
            ConstraintAt(
                _injectionPressure, _reservoirPressure,
                offered / PhysicalConstants.WaterDensityKgPerM3),
        ];
    }

    /// <summary>
    /// What it will take right now, at the conditions it was last given
    /// (SDD-003 §3.1d's R20d.24b amendment).
    ///
    /// <para>Asked by the layer that commands the intake, which has to know the
    /// headroom BEFORE the solve — the solver cannot clamp a source that is not
    /// a completion, so an intake commanded past this would report a violation
    /// nothing could relieve.</para>
    /// </summary>
    public ReservoirRate Acceptance => AcceptanceAt(_injectionPressure, _reservoirPressure);

    public ReservoirVolume CumulativeInjected => new(_cumulativeInjectedM3);

    /// <summary>
    /// <c>s(V) = s0 + α·V/V_ref</c>. Grows without bound in principle; in
    /// practice the rate it produces falls far enough first that the player
    /// either remediates or stops.
    /// </summary>
    public double CurrentSkin =>
        _conditions.InitialSkin
        + _conditions.PluggingPerReferenceVolume
          * (_cumulativeInjectedM3 / _conditions.ReferenceVolume.CubicMetres);

    /// <summary>
    /// The rate this well accepts at a given injection pressure — §6.1's form
    /// with the difference reversed.
    /// </summary>
    public ReservoirRate AcceptanceAt(Pressure injectionPressure, Pressure reservoirPressure)
    {
        double drive = injectionPressure.Pascals - reservoirPressure.Pascals;

        // Injection pressure below reservoir pressure is not production. A
        // disposal well run backwards is a different well with a different
        // completion, and treating it as one here would let a disposal well
        // quietly become a producer of formation water.
        if (drive <= 0.0) return new ReservoirRate(0.0);

        double re = DetMath.Sqrt(_conditions.DrainageArea.SquareMetres / PhysicalConstants.Pi);
        double denominator = _conditions.WaterViscosity.PascalSeconds
                           * (DetMath.Ln(re / _conditions.WellboreRadius.Metres)
                              - SteadyStateOffset + CurrentSkin);

        if (denominator <= 0.0)
            throw new ModelFault("SDD-003 §3.1d", null,
                $"injector {InjectorId.Value}: skin {Format(CurrentSkin)} makes the Darcy " +
                "denominator non-positive");

        return new ReservoirRate(
            PhysicalConstants.TwoPi * _conditions.Permeability.SquareMetres
            * _conditions.InjectionInterval.Metres * drive / denominator);
    }

    /// <summary>
    /// SDD-002 §5's constraint, so the solver throttles against disposal exactly
    /// as it throttles against a separator (R10-V3).
    /// </summary>
    public ConstraintEvaluation ConstraintAt(
        Pressure injectionPressure, Pressure reservoirPressure, double offeredM3PerS) =>
        new(ConstraintKind.Injectivity,
            AcceptanceAt(injectionPressure, reservoirPressure).CubicMetresPerSecond,
            offeredM3PerS);

    /// <summary>Stage 6. What actually went in, which is what plugs the rock.</summary>
    public void Commit(ReservoirVolume injected)
    {
        if (injected.CubicMetres < 0.0)
            throw new InvariantFault("SDD-003 §3.1d", null,
                $"injector {InjectorId.Value} was committed a negative volume");

        _cumulativeInjectedM3 += injected.CubicMetres;
    }

    /// <summary>
    /// R10-V4. An acid job or a filter upgrade clears the accumulated plugging.
    ///
    /// <para>It restores <c>s0</c> and never improves on it: remediation undoes
    /// damage, it does not stimulate. A well that came back better than new
    /// would make the decline free to ignore.</para>
    /// </summary>
    public void Remediate() => _cumulativeInjectedM3 = 0.0;

    /// <summary>
    /// Puts a restored injector back to what it has already taken (R20d.12).
    ///
    /// <para>THE PLUGGING IS THIS NUMBER. Impairment scales with the volume put
    /// away against the reference, so an injector restored at zero comes back
    /// with a clean formation however many years it has been used — which showed
    /// up as a reloaded field disposing water at a different rate and, through
    /// the severity terms, ageing its equipment differently.</para>
    ///
    /// <para>Separate from <see cref="Commit"/> for the reason every restore in
    /// this engine is separate from its advance: restoring is allowed to jump,
    /// and only before the engine ticks.</para>
    /// </summary>
    public void RestoreTo(ReservoirVolume injected)
    {
        if (injected.CubicMetres < 0.0 || !double.IsFinite(injected.CubicMetres))
            throw new SaveDataFault("SDD-003 §6c", null,
                "a save says this injector has taken a negative or non-finite volume; " +
                "an injector cannot un-inject what it has put away");

        _cumulativeInjectedM3 = injected.CubicMetres;
    }

    private const double SteadyStateOffset = 0.75;

    private static void Validate(InjectionConditions c)
    {
        if (c.Permeability.SquareMetres <= 0.0 || c.InjectionInterval.Metres <= 0.0)
            throw new ModelFault("SDD-003 §3.1d", null,
                "permeability and injection interval must be positive");

        if (c.WaterViscosity.PascalSeconds <= 0.0)
            throw new ModelFault("SDD-003 §3.1d", null, "water viscosity must be positive");

        if (c.ReferenceVolume.CubicMetres <= 0.0)
            throw new ModelFault("SDD-003 §3.1d", null,
                "the plugging reference volume must be positive; the decline divides by it");

        if (c.PluggingPerReferenceVolume < 0.0)
            throw new ModelFault("SDD-003 §3.1d", null,
                "plugging cannot be negative; an injector does not clean itself by running");
    }

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}
