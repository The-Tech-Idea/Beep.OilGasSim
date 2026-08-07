// R10.2 — injection and disposal (SDD-003 §3.1d, R10 §4).

using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Wells;

namespace OGSim.Wells.Tests;

public class InjectorTests
{
    private static Injector Well(
        double permeabilityM2 = 1.0e-13,
        double initialSkin = 0.0,
        double plugging = 2.0,
        double referenceVolumeM3 = 1.0e5) =>
        new(new EntityId<ICompletion>(1),
            new InjectionConditions(
                new Permeability(permeabilityM2),
                new Length(30.0),
                new Area(2.0e5),
                new Length(0.108),
                new Viscosity(0.5e-3),
                initialSkin, plugging, new ReservoirVolume(referenceVolumeM3)));

    private static readonly Pressure Reservoir = new(20.0e6);

    // ------------------------------------------------------------ acceptance

    [Fact] // Injection needs a pressure difference in the right direction
    public void R10V3_acceptance_grows_with_the_injection_pressure()
    {
        Injector well = Well();

        double gentle = well.AcceptanceAt(new Pressure(22.0e6), Reservoir).CubicMetresPerSecond;
        double hard = well.AcceptanceAt(new Pressure(30.0e6), Reservoir).CubicMetresPerSecond;

        Assert.True(gentle > 0.0);
        Assert.True(hard > gentle);

        // Linear in the difference — it is Darcy's law with the sign reversed.
        Assert.Equal(5.0, hard / gentle, precision: 9);
    }

    [Fact] // Below reservoir pressure it accepts nothing — it does not produce
    public void R10V3_an_injector_below_reservoir_pressure_accepts_nothing()
    {
        Injector well = Well();

        Assert.Equal(0.0,
            well.AcceptanceAt(new Pressure(19.0e6), Reservoir).CubicMetresPerSecond, precision: 12);

        // A disposal well run backwards is a different well with a different
        // completion. Treating it as one here would let disposal quietly become
        // a producer of formation water.
        Assert.Equal(0.0,
            well.AcceptanceAt(new Pressure(20.0e6), Reservoir).CubicMetresPerSecond, precision: 12);
    }

    // ------------------------------------------------------------ R10-V4

    [Fact] // R10-V4: injectivity DECLINES as the formation plugs
    public void R10V4_injectivity_falls_as_volume_accumulates()
    {
        Injector well = Well();
        var pressure = new Pressure(30.0e6);

        double fresh = well.AcceptanceAt(pressure, Reservoir).CubicMetresPerSecond;

        double previous = fresh;
        for (int year = 0; year < 5; year++)
        {
            well.Commit(new ReservoirVolume(1.0e5));

            double now = well.AcceptanceAt(pressure, Reservoir).CubicMetresPerSecond;
            Assert.True(now < previous, $"year {year}: injectivity {now} did not fall below {previous}");
            previous = now;
        }

        // Materially, not marginally: this is meant to be an operational problem.
        Assert.True(previous < fresh * 0.6,
            $"after five reference volumes the well still takes {previous / fresh:P0} of new");
    }

    [Fact] // R10-V4: remediation RESTORES it — and does not improve on new
    public void R10V4_remediation_restores_the_original_injectivity()
    {
        Injector well = Well(initialSkin: 2.0);
        var pressure = new Pressure(30.0e6);

        double fresh = well.AcceptanceAt(pressure, Reservoir).CubicMetresPerSecond;

        well.Commit(new ReservoirVolume(4.0e5));
        Assert.True(well.AcceptanceAt(pressure, Reservoir).CubicMetresPerSecond < fresh);

        well.Remediate();

        // Exactly back to new. Remediation undoes damage; it does not stimulate,
        // and a well that came back BETTER than new would make the decline free
        // to ignore.
        Assert.Equal(fresh, well.AcceptanceAt(pressure, Reservoir).CubicMetresPerSecond, precision: 9);
        Assert.Equal(2.0, well.CurrentSkin, precision: 12);
    }

    [Fact] // Skin follows the declared plugging law, recomputed independently
    public void R10V4_skin_grows_linearly_with_cumulative_volume()
    {
        Injector well = Well(initialSkin: 1.0, plugging: 2.0, referenceVolumeM3: 1.0e5);

        Assert.Equal(1.0, well.CurrentSkin, precision: 12);

        well.Commit(new ReservoirVolume(5.0e4));      // half a reference volume
        Assert.Equal(1.0 + 2.0 * 0.5, well.CurrentSkin, precision: 12);

        well.Commit(new ReservoirVolume(5.0e4));      // one in total
        Assert.Equal(1.0 + 2.0 * 1.0, well.CurrentSkin, precision: 12);
    }

    // ------------------------------------------------------------ R10-V3

    [Fact] // R10-V3: disposal reports an Injectivity constraint, so the solver throttles
    public void R10V3_the_injector_reports_its_limit_as_a_constraint()
    {
        Injector well = Well();

        ConstraintEvaluation constraint =
            well.ConstraintAt(new Pressure(24.0e6), Reservoir, offeredM3PerS: 1.0);

        // The same shape a separator or a flare reports, so S3 throttles against
        // disposal exactly as it throttles against anything else — and the
        // bottleneck report names water handling.
        Assert.Equal(ConstraintKind.Injectivity, constraint.Kind);
        Assert.Equal(well.AcceptanceAt(new Pressure(24.0e6), Reservoir).CubicMetresPerSecond,
                     constraint.Capacity, precision: 12);
        Assert.Equal(1.0, constraint.Load, precision: 12);
        Assert.True(constraint.Load > constraint.Capacity, "the offered rate should exceed it");
    }

    [Fact] // A plugged well eventually becomes the field's constraint
    public void R10V3_a_plugged_injector_binds_where_a_fresh_one_did_not()
    {
        Injector well = Well();
        var pressure = new Pressure(24.0e6);

        double offered = well.AcceptanceAt(pressure, Reservoir).CubicMetresPerSecond * 0.8;

        // Fresh: comfortable.
        Assert.True(well.ConstraintAt(pressure, Reservoir, offered).Load
                  < well.ConstraintAt(pressure, Reservoir, offered).Capacity);

        well.Commit(new ReservoirVolume(3.0e5));

        // Plugged: the same offered rate now exceeds what the rock will take,
        // and nothing upstream changed at all.
        ConstraintEvaluation now = well.ConstraintAt(pressure, Reservoir, offered);
        Assert.True(now.Load > now.Capacity,
            "a plugged injector should refuse a rate it once accepted");
    }

    // ------------------------------------------------------------ refusals

    [Fact] // Content errors are refused where the datasheet is in hand
    public void R10V4_a_negative_plugging_rate_is_a_model_fault()
    {
        var fault = Assert.Throws<ModelFault>(() => Well(plugging: -1.0));
        Assert.Contains("does not clean itself", fault.Fault.Detail);
    }

    [Fact] // A negative commit is an invariant failure, not a free remediation
    public void R10V4_a_negative_committed_volume_is_an_invariant_fault()
    {
        Assert.Throws<InvariantFault>(
            () => Well().Commit(new ReservoirVolume(-1.0)));
    }
}
