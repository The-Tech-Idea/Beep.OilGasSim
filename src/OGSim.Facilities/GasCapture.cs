// R20d.17 — somewhere for the gas to go (SDD-006 §3b, finding 172).
//
// A PENALTY WITH NO ANSWER IS A TAX. R20d.16 priced flaring into the cost of
// debt, and this composition shipped exactly one gas destination — so a company
// could be charged for flaring and could do nothing about it but produce less
// oil. That is the failure the ESG record's own floor argues against, and this
// is the answer to it: a plant a company buys, which takes what it can and sends
// the rest to the flare.
//
// CAPACITY IS THE WHOLE MECHANIC. A field's gas comes up with its oil whether
// anybody wants it or not, so the question is never "shall I flare?" but "how
// much can I handle?" — and the answer is a capital item with a size. A company
// that grows its oil production past its gas plant starts flaring again, which
// is the same bottleneck shape as the separator and the export line and the one
// this game is played on.
//
// A TERMINAL SINK, like the flare. What it takes leaves the network as sales
// gas; what it cannot take goes out the reject leg to be burned. Metering it
// here rather than through a custody point of its own is the honest simple
// model: the plant IS the sales point, and gas leaves the field the moment it
// is processed.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Facilities;

/// <summary>What a gas plant of this size can take (SDD-006 §3b).</summary>
public sealed record GasPlantTier(ContentId Id, MassRate Capacity);

/// <summary>
/// The gas plant: a socket with a tier fitted (SDD-006 §0c), like the separator
/// and the export line.
/// </summary>
public sealed class GasCapture : IFlowElement
{
    private readonly int _materialCount;

    public GasCapture(EntityId<IFlowElement> id, GasPlantTier fitted, int materialCount)
    {
        ArgumentNullException.ThrowIfNull(fitted);

        if (fitted.Capacity.KgPerSecond < 0.0)
            throw new ContentFault("SDD-006 §3b", null,
                $"gas plant tier '{fitted.Id.Value}' has negative capacity");

        Id = id;
        Tier = fitted;
        _materialCount = materialCount;

        // Material-count wide from the first read, not width zero: stage 8's
        // gas sale reads this before the plant has ever transformed, and a
        // width-zero composition reports no ordinals to anything that walks
        // its length (finding 284).
        Captured = Composition.Zero(materialCount);
    }

    public EntityId<IFlowElement> Id { get; }

    /// <summary>What is fitted now — the one fact about gas-handling capacity
    /// there is (law L5).</summary>
    public GasPlantTier Tier { get; private set; }

    /// <summary>Fit a bigger plant. Called only by a COMPLETED install.</summary>
    public void Fit(GasPlantTier tier)
    {
        ArgumentNullException.ThrowIfNull(tier);
        Tier = tier;
    }

    public static PortId Inlet { get; } = new(0);

    /// <summary>Where the gas it cannot take goes. A plant at capacity does not
    /// stop the field; it overflows to the flare, which is what a flare is
    /// for.</summary>
    public static PortId RejectOutlet { get; } = new(1);

    public IReadOnlyList<PortSpec> Ports { get; } =
    [
        new PortSpec(new PortId(0), PortDirection.Inlet, PortRole.Main),
        new PortSpec(new PortId(1), PortDirection.Outlet, PortRole.Reject),
    ];

    /// <summary>What the plant took this tick, as sales gas. Read by stage 8,
    /// which prices it.</summary>
    public Composition Captured { get; private set; }

    /// <summary>
    /// NONE, and that is the design rather than an omission.
    ///
    /// <para>A gas plant does not throttle the field. Its capacity decides how
    /// much gas is SOLD rather than how much oil is produced — the excess goes
    /// out the reject leg to the flare, which is what a flare is for. Reporting
    /// a capacity here made the solver enforce it, and a field that shipped with
    /// no gas handling produced nothing at all: the plant's zero became the
    /// whole chain's zero.</para>
    ///
    /// <para>That is the physically interesting case too. Gas handling costs a
    /// company its RECORD rather than its production, which is exactly why
    /// fields flare for years instead of shutting in — and what the flaring
    /// costs is now the price of its debt (SDD-012 §4).</para>
    /// </summary>
    public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return [];
    }

    public TransformResult Transform(TransformInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        MaterialStream inlet = input.Inlets.Count > 0
            ? input.Inlets[0]
            : new MaterialStream(
                Composition.Zero(_materialCount),
                new Pressure(0.0),
                input.Segment.Ambient,
                Allocation.FromSingle(new EntityRef(EntityKind.Compartment, 1)));

        Composition arriving = inlet.MassRates;

        double total = arriving.Total.KgPerSecond;

        // NOTHING ARRIVING, NOTHING TAKEN. Said explicitly because the fraction
        // below divides by it.
        if (total <= 0.0)
        {
            Captured = Composition.Zero(_materialCount);

            return Terminal(inlet, Composition.Zero(_materialCount));
        }

        double taken = Math.Min(total, Tier.Capacity.KgPerSecond);

        // Split by FRACTION rather than by material: a plant at capacity does
        // not choose which molecules to keep, it takes the stream it is given
        // until it is full. Preferring one component would be a separation this
        // element does not do.
        (Composition captured, Composition rejected) = arriving.Split(taken / total);

        Captured = captured;

        return Terminal(inlet, rejected);
    }

    /// <summary>
    /// What it took leaves the network here — it has been sold. What it could
    /// not take goes out the reject leg to the flare, which is a connection and
    /// not a disposal: the flare is what accounts for burning it.
    /// </summary>
    private TransformResult Terminal(MaterialStream inlet, Composition rejected) =>
        new(
            // ONE OUTLET, and its index here is 0 while its PORT is 1 — outlets
            // are positional in declaration order, which is the trap that once
            // metered a custody point's reject leg as its sales leg.
            [new MaterialStream(rejected, inlet.P, inlet.T, inlet.Provenance)],
            Sourced: Composition.Zero(_materialCount),
            FuelConsumed: Composition.Zero(_materialCount),
            Disposed: new DisposedMass(
                Flared: Composition.Zero(_materialCount),
                Vented: Composition.Zero(_materialCount),

                // SOLD, and Discharged is the conservation term that says mass
                // left the network here. It is not a loss: stage 8 prices it,
                // which is the difference between selling gas and dumping it.
                Discharged: Captured),
            PowerDraw: new Power(0.0));
}
