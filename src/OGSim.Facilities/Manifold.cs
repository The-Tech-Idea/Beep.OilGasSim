// R20d.1 — the manifold (SDD-006 §1b, design 04 §2.2 and §5 stage 3).
//
// WHERE WELLS MEET. `FlowNetwork` refuses two edges into one inlet (FD4) because
// two streams arriving at one port would be an undeclared commingle — mixing is
// an element's job, never an emergent property of the wiring, or provenance
// would blend with nothing recording it. This is that element: the second well
// on a field has somewhere to go, and what it contributed is still answerable
// when the oil is sold.
//
// A HEADER STORES NOTHING AND DROPS NOTHING. It sums what arrives and passes its
// downstream demand back to every well equally — which is the whole of design
// 04's commingling trap: a new high-rate well raises the throughput through
// whatever is downstream, raises the drop across it, raises the header pressure,
// and the weakest well on the line goes DEAD. Nobody codes that. It is
// backpressure arithmetic, and this element is why the arithmetic reaches more
// than one well.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Facilities;

/// <summary>
/// A header's datasheet (SDD-006 §8). One field, and it is a real limit: a fixed
/// header has a declared number of slots, so a field that has filled its
/// manifold buys a bigger one before the next well can be tied in
/// (catalogue C06's tier ladder).
/// </summary>
public sealed record ManifoldTier(ContentId Id, int Slots);

/// <summary>
/// The commingling point (SDD-006 §1b).
///
/// <para>Declared as an ordinary <see cref="IFlowElement"/> rather than as
/// design 01 §C5's <c>IFlowNode</c>: that name predates <c>IFlowElement</c>, and
/// a second element interface would be the facility-type hierarchy design 02
/// §4.1 forbids. The solver knows one kind of thing.</para>
/// </summary>
public sealed class Manifold : IFlowElement
{
    private readonly ManifoldTier _tier;
    private readonly int _materialCount;
    private readonly List<PortSpec> _ports = [];

    public Manifold(EntityId<IFlowElement> id, ManifoldTier tier, int materialCount)
    {
        ArgumentNullException.ThrowIfNull(tier);

        if (tier.Slots <= 0)
            throw new ModelFault("SDD-006 §1b", null,
                $"manifold tier {tier.Id.Value} declares {tier.Slots} slots; a header with " +
                "nowhere to tie a well in is not a header");

        Id = id;
        _tier = tier;
        _materialCount = materialCount;

        for (int slot = 0; slot < tier.Slots; slot++)
            _ports.Add(new PortSpec(new PortId(slot), PortDirection.Inlet, PortRole.Main));

        _ports.Add(new PortSpec(new PortId(tier.Slots), PortDirection.Outlet, PortRole.Main));
    }

    public EntityId<IFlowElement> Id { get; }

    /// <summary>How many wells this header can take. What a tie-in checks
    /// against, and what its refusal quotes.</summary>
    public int Slots => _tier.Slots;

    /// <summary>The slot ports, then the outlet. A caller wiring a well asks for
    /// <see cref="SlotAt"/> rather than counting.</summary>
    public IReadOnlyList<PortSpec> Ports => _ports;

    public PortId SlotAt(int slot)
    {
        if (slot < 0 || slot >= _tier.Slots)
            throw new InvariantFault("SDD-006 §1b", null,
                $"manifold {Id.Value} has {_tier.Slots} slots and was asked for slot {slot}");

        return new PortId(slot);
    }

    public PortId Outlet => new(_tier.Slots);

    /// <summary>
    /// Sum the inlets, blend the provenance (SDD-006 §1b).
    ///
    /// <para>The blend is what makes commingling honest: when 10,000 barrels are
    /// sold downstream the engine can still say how much came from which
    /// compartment, and royalties and reserves depletion depend on that being
    /// true rather than approximated (design 04 §2.2).</para>
    /// </summary>
    public TransformResult Transform(TransformInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        double[] byOrdinal = new double[_materialCount];
        var parts = new List<(Allocation Part, Mass Weight)>(input.Inlets.Count);

        var totalKgPerSecond = 0.0;
        var weightedKelvin = 0.0;

        for (int i = 0; i < input.Inlets.Count; i++)
        {
            MaterialStream inlet = input.Inlets[i];

            for (int m = 0; m < _materialCount; m++)
                byOrdinal[m] += inlet.MassRates[new MaterialId(m)].KgPerSecond;

            double mass = inlet.MassRates.Total.KgPerSecond;
            if (mass <= 0.0) continue;

            // A ZERO-RATE inlet contributes no provenance. A shut-in well is
            // still tied in, and letting it into the blend would allocate a
            // share of the sale to a compartment that produced nothing this
            // month — which is the allocation error design 04 §2.2 exists to
            // make impossible.
            parts.Add((inlet.Provenance, new Mass(mass)));

            totalKgPerSecond += mass;
            weightedKelvin += mass * inlet.T.Kelvin;
        }

        return new TransformResult(
            [new MaterialStream(
                Composition.Validated([.. byOrdinal]),
                HeaderPressure(input),
                totalKgPerSecond > 0.0
                    ? new Temperature(weightedKelvin / totalKgPerSecond)
                    : input.Segment.Ambient,
                parts.Count > 0
                    ? Allocation.Blend(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(parts))
                    : EmptyProvenance(input))],
            Sourced: Composition.Zero(_materialCount),
            FuelConsumed: Composition.Zero(_materialCount),
            Disposed: new DisposedMass(
                Composition.Zero(_materialCount),
                Composition.Zero(_materialCount),
                Composition.Zero(_materialCount)),
            PowerDraw: new Power(0.0));
    }

    /// <summary>
    /// A header has NO pressure drop of its own (SDD-006 §1b), and the outlet
    /// takes the first inlet's pressure so the solver reads exactly zero.
    ///
    /// <para>S4 hands every element feeding one manifold the same demanded inlet
    /// pressure — the demand is stored per element and they all feed the same one
    /// — so in the converged state every inlet is at the header pressure and the
    /// choice of which to take does not matter. Taking a minimum instead would
    /// report a fictitious drop during iteration and slow convergence for no
    /// physical reason.</para>
    /// </summary>
    private Pressure HeaderPressure(TransformInput input) =>
        input.Inlets.Count > 0 ? input.Inlets[0].P : new Pressure(0.0);

    /// <summary>
    /// A header with nothing flowing still has to name a provenance, and there is
    /// no honest one — nothing contributed. It carries the blend of what was
    /// tied in rather than a compartment nobody produced from, and the stream it
    /// describes has zero mass, so no allocation is ever computed from it.
    /// </summary>
    private static Allocation EmptyProvenance(TransformInput input) =>
        input.Inlets.Count > 0
            ? input.Inlets[0].Provenance
            : Allocation.FromSingle(new EntityRef(EntityKind.FlowElement, 0));

    /// <summary>
    /// None. A header has no capacity — the flowline downstream of it does, and
    /// reporting one here would have S3 throttling wells for being connected.
    /// </summary>
    public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input) => [];
}
