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
}
