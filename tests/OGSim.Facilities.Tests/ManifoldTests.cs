// R20d.1 — the manifold (SDD-006 §1b, design 04 §2.2).
//
// Two things are asserted here and the second is the one that matters. A header
// sums what arrives — easy, and a test would catch it breaking. It also BLENDS
// PROVENANCE mass-weighted, and nothing downstream would notice that being
// wrong: the oil still sells, the cash still lands, and the royalty is paid to
// the wrong compartment quietly for forty years.

using OGSim.Contracts;
using OGSim.Facilities;
using OGSim.Kernel;

namespace OGSim.Facilities.Tests;

public sealed class ManifoldTests
{
    private static readonly EntityRef North = new(EntityKind.Compartment, 1);
    private static readonly EntityRef South = new(EntityKind.Compartment, 2);

    private static Manifold Header(int slots = 4) =>
        new(new EntityId<IFlowElement>(1), new ManifoldTier(new ContentId("header-4"), slots),
            Fx.MaterialCount);

    private static MaterialStream From(EntityRef compartment, double oil, double bar = 15.0) =>
        new(Fx.Comp(oil, 0.0, 0.0), Pressure.FromBar(bar), Temperature.FromCelsius(60.0),
            Allocation.FromSingle(compartment));

    private static TransformInput In(params MaterialStream[] inlets) =>
        new(inlets, Fx.WholeTick, SolvedRate: null);

    private static double ShareOf(Allocation allocation, EntityRef compartment)
    {
        foreach ((EntityRef where, double fraction) in allocation.Shares)
            if (where == compartment) return fraction;

        return 0.0;
    }

    // ------------------------------------------------------------- commingling

    [Fact] // A header sums what arrives; it stores nothing
    public void R20dV1_a_header_sums_its_inlets()
    {
        TransformResult result = Header().Transform(In(From(North, 6.0), From(South, 2.0)));

        Assert.Equal(8.0, result.Outlets[0].MassRates[new MaterialId(0)].KgPerSecond, precision: 12);
        Assert.Equal(0.0, result.Sourced.Total.KgPerSecond, precision: 12);
    }

    /// <summary>
    /// Design 04 §2.2, and the reason a manifold is an element rather than a
    /// wiring rule: the combined stream carries the proportions each well
    /// contributed, so when the oil is sold the engine can still say where it
    /// came from.
    ///
    /// <para>Royalties differ per licence and reserves deplete against a named
    /// compartment. A blend that was merely plausible would be wrong in a way
    /// nothing downstream could detect.</para>
    /// </summary>
    [Fact]
    public void R20dV1_provenance_blends_by_mass_not_by_well_count()
    {
        TransformResult result = Header().Transform(In(From(North, 6.0), From(South, 2.0)));

        Allocation blended = result.Outlets[0].Provenance;

        Assert.Equal(0.75, ShareOf(blended, North), precision: 12);
        Assert.Equal(0.25, ShareOf(blended, South), precision: 12);
    }

    /// <summary>
    /// A shut-in well is still tied in. Letting it into the blend would allocate
    /// a share of the month's sale to a compartment that produced nothing —
    /// exactly the allocation error §2.2 exists to make impossible.
    /// </summary>
    [Fact]
    public void R20dV1_a_well_producing_nothing_takes_no_share_of_the_sale()
    {
        TransformResult result = Header().Transform(In(From(North, 5.0), From(South, 0.0)));

        Allocation blended = result.Outlets[0].Provenance;

        Assert.Equal(1.0, ShareOf(blended, North), precision: 12);
        Assert.Equal(0.0, ShareOf(blended, South), precision: 12);
    }

    // ------------------------------------------------------------- pressure

    /// <summary>
    /// SDD-006 §1b. A header drops nothing, so the solver reads exactly zero
    /// across it and its downstream demand reaches every well unchanged — which
    /// is what makes the commingling trap arithmetic rather than a rule.
    /// </summary>
    [Fact]
    public void R20dV1_a_header_has_no_pressure_drop()
    {
        TransformResult result = Header().Transform(In(From(North, 6.0, bar: 15.0)));

        Assert.Equal(15.0e5, result.Outlets[0].P.Pascals, precision: 6);
    }

    [Fact] // A header has no capacity; the flowline downstream of it does
    public void R20dV1_a_header_reports_no_constraint()
    {
        Assert.Empty(Header().EvaluateConstraints(In(From(North, 6.0))));
    }

    // ------------------------------------------------------------- slots

    /// <summary>Slots are a real limit (catalogue C06): a field that has filled
    /// its header buys a bigger one before the next well can be tied in.</summary>
    [Fact]
    public void R20dV1_a_header_declares_its_slots_as_inlet_ports()
    {
        Manifold header = Header(slots: 4);

        var inlets = 0;
        for (int i = 0; i < header.Ports.Count; i++)
            if (header.Ports[i].Direction == PortDirection.Inlet) inlets++;

        Assert.Equal(4, inlets);
        Assert.Equal(4, header.Slots);
        Assert.Equal(new PortId(4), header.Outlet);
    }

    [Fact] // Asking for a slot that does not exist names the count, not "out of range"
    public void R20dV1_a_slot_beyond_the_header_is_an_invariant_fault()
    {
        var fault = Assert.Throws<InvariantFault>(() => Header(slots: 2).SlotAt(2));

        Assert.Contains("2 slots", fault.Fault.Detail);
    }

    [Fact] // A header with nowhere to tie a well in is not a header
    public void R20dV1_a_header_with_no_slots_is_a_model_fault()
    {
        Assert.Throws<ModelFault>(() =>
            new Manifold(new EntityId<IFlowElement>(1),
                         new ManifoldTier(new ContentId("header-0"), Slots: 0),
                         Fx.MaterialCount));
    }

    // ------------------------------------------------------------- conservation

    [Fact] // SDD-002 §5: what arrives leaves, per material
    public void R8V10_a_header_conserves_mass_exactly()
    {
        MaterialStream north = From(North, 6.0);
        MaterialStream south = From(South, 2.5);

        TransformResult result = Header().Transform(In(north, south));

        double inbound = north.MassRates.Total.KgPerSecond + south.MassRates.Total.KgPerSecond;

        Assert.Equal(inbound, result.Outlets[0].MassRates.Total.KgPerSecond, precision: 12);
        Assert.Equal(0.0, result.FuelConsumed.Total.KgPerSecond, precision: 12);
        Assert.Equal(0.0, result.Disposed.Flared.Total.KgPerSecond, precision: 12);
    }
}
