// R8.7 — the spec gate (SDD-006 §7, design 02 §4.3, R8 §2.4).
//
// A stream failing a spec DOES NOT PASS. It routes to the Reject port, and the
// failing parameter and its margin are reported.
//
// This is the mechanism that makes the player build a dehydrator. Not a
// tech-tree prompt, not a hint: a rejection with a reason. The stream arrives at
// the custody point, fails on water content, and the flare volume equals the
// rejected volume exactly (FV6 / R8-V6).
//
// The Reject port is MANDATORY on a spec gate and is checked at network build
// (SDD-002 §6): a spec that can refuse mass with nowhere to send it is a network
// that cannot conserve.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Facilities;

/// <summary>Which limit a stream failed, and by how much.</summary>
public sealed record SpecBreach(SpecProperty Property, double Limit, double Measured)
{
    /// <summary>How far over — the number the player needs to size the fix.</summary>
    public double Margin => Measured - Limit;
}

/// <summary>
/// How a stream's properties are measured against a specification.
///
/// <para>Separate from the gate because the PROPERTIES are a question about the
/// stream and the LIMITS are a question about the contract. R9's gas treating
/// changes the first and never the second.</para>
/// </summary>
public sealed record StreamProperties(
    double BasicSedimentAndWater,
    double H2SFraction,
    double Co2Fraction,
    double WaterInGasFraction,
    double LightEndsFraction,
    HeatingValue Heating)
{
    public double ValueOf(SpecProperty property) => property switch
    {
        SpecProperty.BasicSedimentAndWater => BasicSedimentAndWater,
        SpecProperty.H2SFraction => H2SFraction,
        SpecProperty.Co2Fraction => Co2Fraction,
        SpecProperty.WaterInGasFraction => WaterInGasFraction,
        SpecProperty.LightEndsFraction => LightEndsFraction,
        SpecProperty.HeatingValueMin => Heating.JoulesPerKg,
        SpecProperty.HeatingValueMax => Heating.JoulesPerKg,
        _ => throw new ModelFault("SDD-006 §7", null,
            $"unknown spec property {property}; a property with no measurement " +
            "cannot be gated on"),
    };
}

/// <summary>
/// SDD-006 §7's evaluation. Static because it is a question about two values and
/// owns nothing.
/// </summary>
public static class SpecificationCheck
{
    /// <summary>
    /// Every breach, not the first. A stream that is wet AND sour needs both
    /// fixing, and reporting one at a time would make the player buy equipment
    /// in two rounds to learn what one report could have told them.
    /// </summary>
    public static IReadOnlyList<SpecBreach> Evaluate(
        Specification specification, StreamProperties properties)
    {
        ArgumentNullException.ThrowIfNull(specification);
        ArgumentNullException.ThrowIfNull(properties);

        var breaches = new List<SpecBreach>();

        IReadOnlyList<SpecLimit> limits = specification.Limits;
        for (int i = 0; i < limits.Count; i++)
        {
            SpecLimit limit = limits[i];
            double measured = properties.ValueOf(limit.Property);

            // HeatingValueMin is the one limit that binds from BELOW — a lean
            // gas fails a minimum-calorific contract. Treating every limit as a
            // ceiling would silently let it pass.
            bool failed = limit.Property == SpecProperty.HeatingValueMin
                ? measured < limit.Limit
                : measured > limit.Limit;

            if (failed) breaches.Add(new SpecBreach(limit.Property, limit.Limit, measured));
        }

        return breaches;
    }
}

/// <summary>
/// A custody transfer point with a spec gate: the metered, contractual point
/// where revenue originates, and the one place off-spec material is stopped.
/// </summary>
public sealed class CustodyTransferPoint : ICustodyTransferPoint
{
    private readonly int _materialCount;
    private readonly Func<MaterialStream, StreamProperties> _measure;

    public CustodyTransferPoint(
        EntityId<IFlowElement> id,
        Specification spec,
        int materialCount,
        Func<MaterialStream, StreamProperties> measure)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(measure);

        Id = id;
        Spec = spec;
        _materialCount = materialCount;
        _measure = measure;
    }

    public EntityId<IFlowElement> Id { get; }

    public Specification Spec { get; }

    /// <summary>The last evaluation, for the audit and the event. Reset each
    /// transform, so it always describes the pass just made.</summary>
    public IReadOnlyList<SpecBreach> LastBreaches { get; private set; } = [];

    /// <summary>The ports by name (see <see cref="Separator.Inlet"/>).</summary>
    public static PortId Inlet { get; } = new(0);

    /// <summary>What passed the gate — the only leg custody records, and the only
    /// mass stage 8 may price (SDD-009 §1).</summary>
    public static PortId OnSpecOutlet { get; } = new(1);

    public static PortId RejectOutlet { get; } = new(2);

    /// <summary>Inlet, the on-spec outlet, and the MANDATORY Reject.</summary>
    public IReadOnlyList<PortSpec> Ports { get; } =
    [
        new PortSpec(new PortId(0), PortDirection.Inlet, PortRole.Main),
        new PortSpec(new PortId(1), PortDirection.Outlet, PortRole.Main),
        new PortSpec(new PortId(2), PortDirection.Outlet, PortRole.Reject),
    ];

    public TransformResult Transform(TransformInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Inlets.Count == 0)
        {
            LastBreaches = [];
            return Both(Composition.Zero(_materialCount), Composition.Zero(_materialCount), input);
        }

        MaterialStream inlet = input.Inlets[0];
        LastBreaches = SpecificationCheck.Evaluate(Spec, _measure(inlet));

        // ALL or NOTHING, deliberately. A spec is a contractual property of the
        // stream, so a stream that fails does not partly pass — passing the
        // fraction that would have been in spec would be selling a blend nobody
        // metered.
        return LastBreaches.Count == 0
            ? Both(inlet.MassRates, Composition.Zero(_materialCount), input)
            : Both(Composition.Zero(_materialCount), inlet.MassRates, input);
    }

    /// <summary>A custody point declares no capacity of its own — it meters and
    /// gates, and the pipe on either side is what has a throughput.</summary>
    public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input) => [];

    private TransformResult Both(Composition onSpec, Composition rejected, TransformInput input)
    {
        MaterialStream reference = input.Inlets.Count > 0
            ? input.Inlets[0]
            : new MaterialStream(
                Composition.Zero(_materialCount), new Pressure(0.0), input.Segment.Ambient,
                Allocation.FromSingle(new EntityRef(EntityKind.Compartment, 1)));

        return new TransformResult(
            [
                reference with { MassRates = onSpec },
                reference with { MassRates = rejected },
            ],
            Sourced: Composition.Zero(_materialCount),
            FuelConsumed: Composition.Zero(_materialCount),
            Disposed: new DisposedMass(
                Composition.Zero(_materialCount),
                Composition.Zero(_materialCount),
                Composition.Zero(_materialCount)),
            PowerDraw: new Power(0.0));
    }
}
