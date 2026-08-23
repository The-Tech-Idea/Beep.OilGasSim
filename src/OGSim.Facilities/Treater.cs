// R20d.21 — taking the water back out (SDD-006 §2, finding 173).
//
// A FIELD SELLS WET OIL WHEN IT WATERS OUT. A separator leaves some water in the
// liquid leg at its design rate and more when it is pushed past it, and a
// developed field late in life makes a fifth of a barrel of water for every
// barrel of oil — so what reaches the meter fails the sales spec, routes to the
// reject leg, and stops being revenue.
//
// This is what a company buys to fix it. A heater-treater breaks the emulsion
// and drops the water out, and it is the answer to what would otherwise be a tax
// on getting old: the oil is there, it is worth money, and it cannot be sold
// with the water still in it.
//
// A SOCKET WITH A TIER FITTED (SDD-006 §0c), like every other capacity here. The
// first rung takes NOTHING out — a field ships without one, sells dry oil for
// years, and only needs it when the water arrives.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Facilities;

/// <summary>How much of the water a treater of this size takes out.</summary>
public sealed record TreaterTier(ContentId Id, double WaterRemoved);

/// <summary>SDD-006 §2's oil treating.</summary>
public sealed class Treater : IFlowElement
{
    private readonly int _materialCount;
    private readonly MaterialId _water;

    public Treater(
        EntityId<IFlowElement> id, TreaterTier fitted, MaterialId water, int materialCount)
    {
        ArgumentNullException.ThrowIfNull(fitted);

        if (fitted.WaterRemoved is < 0.0 or > 1.0)
            throw new ContentFault("SDD-006 §2", null,
                $"treater tier '{fitted.Id.Value}' removes a fraction outside [0, 1]");

        Id = id;
        Tier = fitted;
        _water = water;
        _materialCount = materialCount;

        // Material-count wide from the first read, not width zero: this is
        // read before the first transform ever runs, and a width-zero
        // composition reports no ordinals to anything that walks its length
        // (finding 284).
        Removed = Composition.Zero(materialCount);
    }

    public EntityId<IFlowElement> Id { get; }

    /// <summary>What is fitted now.</summary>
    public TreaterTier Tier { get; private set; }

    public void Fit(TreaterTier tier)
    {
        ArgumentNullException.ThrowIfNull(tier);
        Tier = tier;
    }

    public static PortId Inlet { get; } = new(0);

    public static PortId Outlet { get; } = new(1);

    public IReadOnlyList<PortSpec> Ports { get; } =
    [
        new PortSpec(new PortId(0), PortDirection.Inlet, PortRole.Main),
        new PortSpec(new PortId(1), PortDirection.Outlet, PortRole.Liquid),
    ];

    /// <summary>The water it dropped out this tick.</summary>
    public Composition Removed { get; private set; }

    /// <summary>
    /// NONE. A treater does not limit what a field produces; it changes what
    /// reaches the meter on spec. Reporting a capacity here would let the
    /// shipped tier — which removes nothing — throttle the whole chain, which is
    /// the mistake the gas plant made first and which stopped the field dead
    /// (R20d.17).
    /// </summary>
    public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return [];
    }

    public TransformResult Transform(TransformInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        // AN EXPLICIT EMPTY STREAM, never `default` (finding 284): a default
        // MaterialStream carries a provenance whose backing array is
        // uninitialized — reading it throws — and 0 Pa / 0 K stamped on a
        // stream that flows downstream. The same shape every sibling already
        // builds (the custody point, the gas plant), and the main path below
        // handles it with no special case: zero in, zero kept, zero pulled.
        MaterialStream inlet = input.Inlets.Count > 0
            ? input.Inlets[0]
            : new MaterialStream(
                Composition.Zero(_materialCount),
                new Pressure(0.0),
                input.Segment.Ambient,
                Allocation.FromSingle(new EntityRef(EntityKind.Compartment, 1)));

        double wet = inlet.MassRates[_water].KgPerSecond;
        double dropped = wet * Tier.WaterRemoved;

        // Rebuilt by ordinal: a Composition is an immutable array with no
        // single-material setter, which is the right shape — a stream is what it
        // is, and changing one component means stating the whole of it.
        var kept = new double[_materialCount];
        var pulled = new double[_materialCount];

        for (int i = 0; i < _materialCount; i++)
        {
            double rate = inlet.MassRates[new MaterialId(i)].KgPerSecond;

            // Only the WATER moves. A treater that took oil out with it would be
            // a separator, and this element exists precisely because the
            // separator has already done what it can.
            kept[i] = i == _water.Ordinal ? wet - dropped : rate;
            pulled[i] = i == _water.Ordinal ? dropped : 0.0;
        }

        Removed = new Composition([.. pulled]);

        return Pass(inlet, new Composition([.. kept]));
    }

    private TransformResult Pass(MaterialStream inlet, Composition dry) =>
        new(
            [new MaterialStream(dry, inlet.P, inlet.T, inlet.Provenance)],
            Sourced: Composition.Zero(_materialCount),
            FuelConsumed: Composition.Zero(_materialCount),
            Disposed: new DisposedMass(
                Flared: Composition.Zero(_materialCount),
                Vented: Composition.Zero(_materialCount),

                // The water leaves the network here and goes back into the
                // ground with everything else the field produced.
                Discharged: Removed),
            PowerDraw: new Power(0.0));
}
