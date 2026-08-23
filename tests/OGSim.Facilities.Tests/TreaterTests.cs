// Finding 284 — the treater's empty-inlet path and the pre-transform state.
//
// The treater had no unit tests at all: its behaviour was pinned only through
// composition's chain fixtures, which always feed it — so the one path those
// could never reach was the empty inlet, and that is exactly where it built a
// stream from `default` while every sibling constructs an explicit empty one.

using OGSim.Contracts;
using OGSim.Facilities;
using OGSim.Kernel;

namespace OGSim.Facilities.Tests;

public sealed class TreaterTests
{
    private static Treater NewTreater() => new(
        new EntityId<IFlowElement>(1),
        new TreaterTier(new ContentId("treater-test"), WaterRemoved: 0.9),
        new MaterialId(2),
        Fx.MaterialCount);

    /// <summary>
    /// An unfed treater still emits a WELL-FORMED stream (finding 284). The
    /// empty-inlet path passed <c>default(MaterialStream)</c> through: a
    /// provenance whose backing array was uninitialized — reading it throws —
    /// and 0 Pa / 0 K stamped on a stream that then flows downstream. Every
    /// sibling (separator, gas plant, custody point) builds an explicit empty
    /// stream at ambient instead, and this pins the treater to the same rule.
    /// </summary>
    [Fact]
    public void An_empty_inlet_transform_produces_a_well_formed_stream()
    {
        TransformResult result = NewTreater().Transform(
            new TransformInput([], Fx.WholeTick, SolvedRate: null));

        MaterialStream outlet = Assert.Single(result.Outlets);

        Assert.Equal(0.0, outlet.MassRates.Total.KgPerSecond);

        // The provenance is READABLE — a default ImmutableArray throws here,
        // which is the defect this test exists to catch.
        Assert.Single(outlet.Provenance.Shares);

        // Ambient rather than absolute zero: the stream is empty, not
        // unphysical.
        Assert.Equal(Fx.WholeTick.Ambient.Kelvin, outlet.T.Kelvin);
    }

    /// <summary>
    /// <see cref="Treater.Removed"/> opens at the MATERIAL COUNT, not at width
    /// zero (finding 284) — it is read before the first transform ever runs
    /// (`ProductionLoop` reads sibling state the same way), and a width-zero
    /// composition reports no ordinals to anything that walks its length.
    /// </summary>
    [Fact]
    public void Removed_is_material_count_wide_before_the_first_transform()
    {
        Assert.Equal(Fx.MaterialCount, NewTreater().Removed.Length);
    }

    /// <summary>As above, for the gas plant's <see cref="GasCapture.Captured"/>
    /// — read by stage 8's gas sale before the plant has ever transformed.</summary>
    [Fact]
    public void A_gas_plants_captured_is_material_count_wide_before_the_first_transform()
    {
        var plant = new GasCapture(
            new EntityId<IFlowElement>(9),
            new GasPlantTier(new ContentId("plant-test"), new MassRate(0.0)),
            Fx.MaterialCount);

        Assert.Equal(Fx.MaterialCount, plant.Captured.Length);
    }
}
