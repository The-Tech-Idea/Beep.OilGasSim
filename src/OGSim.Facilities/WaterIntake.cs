// R20d.24 — where flood water comes from (SDD-003 §3.1d, finding 182).
//
// A SOURCE, and deliberately the same shape as a completion: no inlets, one
// outlet, mass appearing at a commanded rate. Lifting water from the sea is not
// a special case of anything — it is an element that makes mass, exactly as a
// well is, and the network cannot tell them apart.
//
// CONSERVATION IS WHY THIS EXISTS AT ALL. Imported water enters the reservoir
// without leaving the surface network, so adding it straight to the injected
// volume at stage 6 would create mass and INV1 would halt the tick — correctly.
// Sourced here and discharged at the injector, it crosses the network the way
// produced oil does and the balance closes for the same reason. The alternative
// was a number, and a number would have been a leak.
//
// IT REPORTS NO CONSTRAINT, and that is not an omission. An intake's limit is
// what it was told to lift: S3 throttles COMPLETIONS and nothing else
// (SDD-002 §7), so a constraint reported here could never be relieved — the
// solver would shut in every well in the field and then report ladder
// exhaustion, blaming the reservoir for the water plant. The clamp belongs
// where the rate is commanded, and SDD-003 §3.1d's R20d.24b amendment puts it
// there.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Facilities;

/// <summary>
/// The water-injection plant's intake: lift, filter, deaerate, pump
/// (SDD-003 §3.1d).
/// </summary>
public sealed class WaterIntake : IFlowElement
{
    private readonly int _materialCount;
    private readonly MaterialId _water;
    private readonly Allocation _provenance;
    private readonly Pressure _dischargePressure;
    private readonly Temperature _temperature;

    private ReservoirRate _commanded = new(0.0);

    public WaterIntake(
        EntityId<IFlowElement> id,
        MaterialId water,
        int materialCount,

        /// <summary>
        /// Whose water this counts as. Sea water is nobody's production, but an
        /// allocation must name at least one owner (SDD-002 §3) — and it never
        /// reaches a custody meter, because the injector discharges every
        /// kilogram of it. Named by composition rather than invented here, for
        /// the same reason the tank's opening provenance is.
        /// </summary>
        Allocation provenance,
        Pressure dischargePressure,
        Temperature temperature)
    {
        Id = id;
        _water = water;
        _materialCount = materialCount;
        _provenance = provenance;
        _dischargePressure = dischargePressure;
        _temperature = temperature;
    }

    public EntityId<IFlowElement> Id { get; }

    /// <summary>The one port, named so a caller wiring the chain never writes a
    /// bare index.</summary>
    public static PortId Outlet { get; } = new(0);

    public IReadOnlyList<PortSpec> Ports { get; } =
        [new PortSpec(new PortId(0), PortDirection.Outlet, PortRole.Water)];

    /// <summary>
    /// What it has been told to lift, in reservoir m³/s.
    ///
    /// <para>The player's lever, arrived at from the voidage replacement ratio
    /// and clamped by the injector's headroom before it gets here — this element
    /// holds the answer, not the arithmetic.</para>
    /// </summary>
    public ReservoirRate Commanded => _commanded;

    /// <summary>
    /// Set the rate, before the solve.
    ///
    /// <para>Pushed rather than pulled for the same reason a completion's
    /// conditions are (finding 137): <c>Transform</c> is pure, and the layer that
    /// can see both a reservoir's voidage and an injector's acceptance is the one
    /// above this module.</para>
    /// </summary>
    public void Command(ReservoirRate rate)
    {
        if (rate.CubicMetresPerSecond < 0.0 || !double.IsFinite(rate.CubicMetresPerSecond))
            throw new ModelFault("SDD-003 §3.1d", null,
                "an intake cannot be commanded a negative or non-finite rate; " +
                "water running backwards out of a reservoir is a producer, not an intake");

        _commanded = rate;
    }

    /// <summary>
    /// Mass out of nothing, which is what a source IS: 0 in + Sourced = out, so
    /// SDD-002 §5's element balance holds exactly.
    ///
    /// <para>Water's density both ways. The injector turns the mass it receives
    /// back into a volume through the same constant, so the round trip through
    /// the network is exact rather than nearly — and the volume the reservoir
    /// receives at stage 6 is the volume that was commanded.</para>
    /// </summary>
    public TransformResult Transform(TransformInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        double[] byOrdinal = new double[_materialCount];
        byOrdinal[_water.Ordinal] =
            _commanded.CubicMetresPerSecond * PhysicalConstants.WaterDensityKgPerM3;

        Composition lifted = Composition.Validated([.. byOrdinal]);

        return new TransformResult(
            [new MaterialStream(lifted, _dischargePressure, _temperature, _provenance)],
            Sourced: lifted,
            FuelConsumed: Composition.Zero(_materialCount),
            Disposed: new DisposedMass(
                Composition.Zero(_materialCount),
                Composition.Zero(_materialCount),
                Composition.Zero(_materialCount)),
            PowerDraw: new Power(0.0));
    }

    /// <summary>None — see the note at the head of this file.</summary>
    public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input) => [];
}
