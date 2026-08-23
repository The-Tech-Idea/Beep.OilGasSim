// R6.11 — the operating point, the well that dies, and the network coupling
// (SDD-003 §6.3, R6 §2.2/§2.3).

using OGSim.Contracts;
using OGSim.Flow;
using OGSim.Kernel;
using OGSim.Wells;

namespace OGSim.Wells.Tests;

public class OperatingPointTests
{
    private static Completion Well(
        ulong id = 1,
        double reservoirBarA = 250.0,
        double skin = 0.0,
        double tubingDiameterM = 0.0889,
        double permeabilityM2 = 1.0e-13,
        ChokeSetting? choke = null)
    {
        InflowConditions conditions = Fixtures.Conditions(
            permeabilityM2: permeabilityM2, bubblePointPa: 5.0e6);

        var outflow = new HydrostaticFrictionOutflowModel(
            Fixtures.Tubing(diameterM: tubingDiameterM),
            Density.FromSpecificGravity(0.85), lift: null);

        return new Completion(
            new EntityId<ICompletion>(id),
            new EntityId<IWellbore>(id),
            [Fixtures.Perf(skin: skin)],
            new CompositeInflowModel(conditions),
            outflow,
            new CompletionFluid(
                Density.FromSpecificGravity(0.85),
                new FormationVolumeFactor(1.2),
                Allocation.FromSingle(new EntityRef(EntityKind.Compartment, 1)),
                new Pressure(reservoirBarA * 1e5),
                Temperature.FromCelsius(80.0),
                Fx.GasDensity,
                Fx.NoSolutionGas,
                Fx.WaterDensity,
                Fx.Dry),
            choke ?? ChokeSetting.Open,
            oilOrdinal: 0,
            gasOrdinal: 1,
            waterOrdinal: 2,
            materialCount: 3,
            lift: null);
    }

    // -------------------------------------------------------------- FV3

    [Fact] // FV3 / R6-V5: the point is the IPR ∩ VLP intersection
    public void FV3_the_operating_point_is_where_the_two_curves_cross()
    {
        Completion well = Well();
        var wellhead = new Pressure(20.0e5);

        var flowing = Assert.IsType<Flowing>(well.SolveOperatingPoint(wellhead));

        // Independently: at the solved Pwf, what does the tubing DEMAND, and what
        // does the reservoir SUPPLY? At a true intersection they agree.
        var outflow = new HydrostaticFrictionOutflowModel(
            Fixtures.Tubing(), Density.FromSpecificGravity(0.85), lift: null);

        double demanded = outflow.RequiredBottomhole(flowing.Rate, wellhead).Pascals;

        // To the solver's own tolerance (500 Pa on a bracket spanning ~20 MPa).
        Assert.Equal(flowing.Bottomhole.Pascals, demanded, tolerance: 5_000.0);
    }

    [Theory] // Across a sweep, as R6-V5 asks
    // All above the ~167 bar hydrostatic column of 2000 m of 0.85 SG oil. A
    // first draft of this sweep started at 150 and the case came back DEAD —
    // correctly, which is R6-V6's whole subject. An intersection sweep has to
    // run where an intersection exists; the dying case has its own tests below.
    [InlineData(200.0)]
    [InlineData(250.0)]
    [InlineData(300.0)]
    [InlineData(400.0)]
    public void FV3_the_intersection_holds_across_a_pressure_sweep(double reservoirBarA)
    {
        Completion well = Well(reservoirBarA: reservoirBarA);
        var wellhead = new Pressure(15.0e5);

        var flowing = Assert.IsType<Flowing>(well.SolveOperatingPoint(wellhead));

        var outflow = new HydrostaticFrictionOutflowModel(
            Fixtures.Tubing(), Density.FromSpecificGravity(0.85), lift: null);

        Assert.Equal(flowing.Bottomhole.Pascals,
                     outflow.RequiredBottomhole(flowing.Rate, wellhead).Pascals,
                     tolerance: 5_000.0);
    }

    // ------------------------------------------------------------ R6-V6

    [Fact] // R6-V6: the well that dies, reported DISTINCTLY from a zero rate
    public void R6V6_a_well_that_cannot_lift_its_column_reports_dead()
    {
        // The hydrostatic column of 2000 m of 0.85 SG oil is ~167 bar. A
        // reservoir at 100 bar cannot push it, at any rate, ever.
        Completion dying = Well(reservoirBarA: 100.0);

        OperatingPoint point = dying.SolveOperatingPoint(new Pressure(10.0e5));

        // DEAD, not Flowing(0). "Produced nothing this tick" and "cannot flow at
        // any rate" have different remedies, and R7 exists to answer the second.
        Assert.IsType<Dead>(point);
    }

    [Fact] // R6-V6: and it happens by DECLINE, not by a threshold
    public void R6V6_the_well_dies_as_reservoir_pressure_declines()
    {
        // Walk the reservoir down and find where it stops flowing. Nothing in
        // the code names this pressure — it falls out of the two curves.
        bool everFlowed = false, died = false;

        for (double bar = 300.0; bar >= 80.0; bar -= 5.0)
        {
            OperatingPoint point = Well(reservoirBarA: bar).SolveOperatingPoint(new Pressure(10.0e5));

            if (point is Flowing) { everFlowed = true; Assert.False(died, "revived after dying"); }
            else died = true;
        }

        Assert.True(everFlowed, "the well never flowed at any pressure");
        Assert.True(died, "the well never died, so the test proved nothing");
    }

    // ------------------------------------------------------------ R6-V9

    [Fact] // R6-V9: raising wellhead pressure reduces rate — the coupling, by hand
    public void R6V9_raising_wellhead_pressure_reduces_the_rate()
    {
        Completion well = Well();

        double low = Rate(well.SolveOperatingPoint(new Pressure(10.0e5)));
        double high = Rate(well.SolveOperatingPoint(new Pressure(60.0e5)));

        Assert.True(high < low, $"backpressure did not bite: {high} not below {low}");
    }

    // ------------------------------------------------------------ R6-V7

    [Fact] // R6-V7: the tubing-size trade — narrow is friction-limited
    public void R6V7_too_narrow_tubing_is_friction_limited()
    {
        double narrow = Rate(Well(tubingDiameterM: 0.0254).SolveOperatingPoint(new Pressure(10.0e5)));
        double normal = Rate(Well(tubingDiameterM: 0.0889).SolveOperatingPoint(new Pressure(10.0e5)));

        // ΔP_friction ~ 1/D⁵, so a 1" string strangles a well a 3½" string flows.
        Assert.True(narrow < normal, $"narrow tubing flowed {narrow}, not less than {normal}");
    }

    // ------------------------------------------------------------ R6-V4

    [Fact] // R6-V4: skin costs production, through the operating point
    public void R6V4_skin_reduces_the_operating_rate()
    {
        double clean = Rate(Well(skin: 0.0).SolveOperatingPoint(new Pressure(10.0e5)));
        double damaged = Rate(Well(skin: 10.0).SolveOperatingPoint(new Pressure(10.0e5)));

        Assert.True(damaged < clean);
    }

    // ------------------------------------------------------------ R6-V8

    [Fact] // R6-V8: critical flow makes rate independent of downstream pressure
    public void R6V8_a_critical_choke_decouples_the_well_from_backpressure()
    {
        // Critical ratio 0.9: any wellhead pressure below 90% of Pwf is critical,
        // which on this well is every case below.
        var choke = new ChokeSetting(
            CriticalPressureRatio: 0.9, CriticalRate: new ReservoirRate(1.0e-3));

        Completion well = Well(choke: choke);

        double low = Rate(well.SolveOperatingPoint(new Pressure(10.0e5)));
        Assert.True(well.IsPressureDecoupled, "the choke should report critical");

        double higher = Rate(well.SolveOperatingPoint(new Pressure(40.0e5)));

        // Clamped to the critical rate at both, so the swing changes nothing.
        Assert.Equal(1.0e-3, low, precision: 12);
        Assert.Equal(1.0e-3, higher, precision: 12);
    }

    [Fact] // R6-V8: sub-critical does NOT decouple
    public void R6V8_a_sub_critical_choke_leaves_the_well_coupled()
    {
        // Critical ratio 0.01: the wellhead never falls that far below Pwf here.
        var choke = new ChokeSetting(
            CriticalPressureRatio: 0.01, CriticalRate: new ReservoirRate(1.0e-3));

        Completion well = Well(choke: choke);

        double low = Rate(well.SolveOperatingPoint(new Pressure(10.0e5)));
        Assert.False(well.IsPressureDecoupled);

        double high = Rate(well.SolveOperatingPoint(new Pressure(60.0e5)));
        Assert.True(high < low, "a sub-critical well must still feel backpressure");
    }

    private static double Rate(OperatingPoint point) =>
        point is Flowing flowing ? flowing.Rate.CubicMetresPerSecond : 0.0;

    // ---------------------------------------------------------- R12b.7 / 253

    [Fact] // A stimulated well produces MORE, at the same conditions — the physical proof
    public void R12b7V1_a_stimulated_well_flows_more_at_the_same_conditions()
    {
        Completion well = Well(skin: 3.0);
        var wellhead = new Pressure(20.0e5);

        double before = Rate(well.SolveOperatingPoint(wellhead));

        well.Stimulate(3.0);

        double after = Rate(well.SolveOperatingPoint(wellhead));

        Assert.True(after > before,
            $"a well acidised from skin 3.0 to 0.0 flowed {after} against {before} before it");
    }

    [Fact] // The mechanism itself: skin moves by exactly the amount ordered
    public void R12b7V2_stimulate_reduces_every_perforations_skin_by_the_same_amount()
    {
        Completion well = Well(skin: 5.0);

        well.Stimulate(2.0);

        Assert.Equal(3.0, well.Perforations[0].Skin, precision: 9);
    }

    [Fact] // A well drilled clean, stimulated, goes NEGATIVE — a real physical outcome
    public void R12b7V3_stimulating_a_clean_well_drives_skin_negative()
    {
        Completion well = Well(skin: 0.0);

        well.Stimulate(3.0);

        Assert.Equal(-3.0, well.Perforations[0].Skin, precision: 9);
    }

    [Fact] // A job that reduces nothing is not a job
    public void R12b7V4_a_non_positive_reduction_is_refused()
    {
        Completion well = Well(skin: 2.0);

        Assert.Throws<ModelFault>(() => well.Stimulate(0.0));
        Assert.Throws<ModelFault>(() => well.Stimulate(-1.0));
    }

    [Fact] // Repeated jobs compound — nothing here caps how many times a well can be acidised
    public void R12b7V5_repeated_jobs_compound()
    {
        Completion well = Well(skin: 6.0);

        well.Stimulate(2.0);
        well.Stimulate(2.0);

        Assert.Equal(2.0, well.Perforations[0].Skin, precision: 9);
    }

    // ---------------------------------------------------------- R12b.2 / 255

    /// <summary>The tubing <see cref="Well"/> builds its own outflow model
    /// from — installing a lift method has to match it, or the mutation
    /// would be comparing a well against a different string than the one it
    /// is actually completed in.</summary>
    private static readonly TubingGeometry InstallTubing = Fixtures.Tubing();

    [Fact] // The mutation itself: a strong well is capped once a pump is fitted
    public void R12b2V1_installing_a_rod_pump_caps_a_strong_wells_rate()
    {
        Completion well = Well(reservoirBarA: 350.0, skin: 0.0, permeabilityM2: 1.0e-12);
        var wellhead = new Pressure(10.0e5);

        double before = Rate(well.SolveOperatingPoint(wellhead));

        const double displacement = 1.0e-4;
        Assert.True(before > displacement, "the well was not strong enough to prove a cap");

        var lift = new RodPump(
            new EntityId<IWellComponent>(99), new ContentId("rod-pump-a"),
            Wide, new GameDate(1970, 6), displacement);

        well.InstallLift(
            lift, new HydrostaticFrictionOutflowModel(
                InstallTubing, Density.FromSpecificGravity(0.85), lift));

        double after = Rate(well.SolveOperatingPoint(wellhead));

        Assert.Equal(displacement, after, precision: 12);
        Assert.Same(lift, well.Lift);
    }

    [Fact] // A weak well is unaffected — the cap is a ceiling, not a floor (R7-V6's claim, through the mutation)
    public void R12b2V2_installing_a_generous_pump_on_a_weak_well_changes_nothing()
    {
        Completion well = Well(reservoirBarA: 250.0, skin: 0.0, permeabilityM2: 1.0e-15);
        var wellhead = new Pressure(10.0e5);

        double before = Rate(well.SolveOperatingPoint(wellhead));

        const double generousDisplacement = 1.0;

        var lift = new RodPump(
            new EntityId<IWellComponent>(99), new ContentId("rod-pump-a"),
            Wide, new GameDate(1970, 6), generousDisplacement);

        well.InstallLift(
            lift, new HydrostaticFrictionOutflowModel(
                InstallTubing, Density.FromSpecificGravity(0.85), lift));

        double after = Rate(well.SolveOperatingPoint(wellhead));

        Assert.Equal(before, after, precision: 9);
    }

    /// <summary>The mutation generalises: an ESP's pressure boost, not a
    /// rod pump's displacement cap, still installs through the same
    /// <see cref="Completion.InstallLift"/> and still revives a well the
    /// same way R7-V2 already proved for a well built WITH one from the
    /// start.</summary>
    [Fact]
    public void R12b2V3_installing_an_esp_revives_a_well_that_cannot_flow_naturally()
    {
        Completion well = Well(reservoirBarA: 120.0, skin: 0.0);
        var wellhead = new Pressure(10.0e5);

        Assert.IsType<Dead>(well.SolveOperatingPoint(wellhead));

        var lift = new ElectricSubmersiblePump(
            new EntityId<IWellComponent>(99), new ContentId("esp-a"), Wide, new GameDate(1970, 6),
            headCurve: [(0.0, 1400.0), (0.005, 1100.0), (0.010, 700.0), (0.020, 0.0)],
            efficiency: 0.55);

        well.InstallLift(
            lift, new HydrostaticFrictionOutflowModel(
                InstallTubing, Density.FromSpecificGravity(0.85), lift));

        var flowing = Assert.IsType<Flowing>(well.SolveOperatingPoint(wellhead));
        Assert.True(flowing.Rate.CubicMetresPerSecond > 0.0);
    }

    /// <summary>And gas lift's density reduction — a third, different effect
    /// — installs and revives through the same mutation too.</summary>
    [Fact]
    public void R12b2V4_installing_gas_lift_revives_a_well_that_cannot_flow_naturally()
    {
        Completion well = Well(reservoirBarA: 140.0, skin: 0.0);
        var wellhead = new Pressure(10.0e5);

        Assert.IsType<Dead>(well.SolveOperatingPoint(wellhead));

        var lift = new GasLift(
            new EntityId<IWellComponent>(99), new ContentId("gas-lift-a"), Wide,
            new GameDate(1970, 6), injectionRateM3PerS: 0.02, gasDensityKgPerM3: 80.0);

        well.InstallLift(
            lift, new HydrostaticFrictionOutflowModel(
                InstallTubing, Density.FromSpecificGravity(0.85), lift));

        var flowing = Assert.IsType<Flowing>(well.SolveOperatingPoint(wellhead));
        Assert.True(flowing.Rate.CubicMetresPerSecond > 0.0);
    }

    private static readonly LiftEnvelope Wide = new(
        MinRate: new ReservoirRate(0.0),
        MaxRate: new ReservoirRate(1.0),
        MaxDepth: new Length(10_000.0),
        MaxDeviationDegrees: 90.0,
        MaxGasFraction: 1.0,
        MaxTemperature: Temperature.FromCelsius(250.0),
        MaxSolidsFraction: 1.0);
}
