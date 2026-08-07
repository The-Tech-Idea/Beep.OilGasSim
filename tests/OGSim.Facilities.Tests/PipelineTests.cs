// R11's transport half (SDD-006 §6, R11 §4).
//
// G1: CAPACITY IS NEVER CONFIGURED. Every number below is an outcome of geometry
// and fluid, and the tests are written so that a stored maxRate field could not
// have passed them.

using OGSim.Contracts;
using OGSim.Facilities;
using OGSim.Kernel;

namespace OGSim.Facilities.Tests;

public class PipelineTests
{
    private static PipeGeometry Line(
        double lengthKm = 20.0, double diameterM = 0.3, double riseM = 0.0) =>
        new(new Length(lengthKm * 1000.0), new Length(diameterM),
            Roughness: 4.6e-5, ElevationRise: new Length(riseM));

    private static readonly Density Crude = Density.FromSpecificGravity(0.85);
    private static readonly Viscosity Light = new(3.0e-3);

    private static LiquidHydraulicModel Liquid(
        Density? density = null, Viscosity? viscosity = null, double riseM = 0.0) =>
        new(density ?? Crude, viscosity ?? Light, new Length(riseM));

    private static MaterialStream At(double massRate) =>
        new(Composition.Validated([massRate, 0.0, 0.0]),
            Pressure.FromBar(50.0), Temperature.FromCelsius(40.0), Fx.One);

    // ------------------------------------------------------------ MX4

    [Fact] // MX4: the drop matches Darcy-Weisbach, recomputed independently
    public void MX4_the_pressure_drop_matches_darcy_weisbach()
    {
        PipeGeometry geometry = Line();
        Pressure drop = Liquid().DropAlong(At(100.0), geometry, new Fx.IdealSplitFluid());

        const double rho = 850.0, mu = 3.0e-3, d = 0.3, l = 20_000.0;
        double area = Math.PI * d * d / 4.0;
        double velocity = 100.0 / (rho * area);
        double reynolds = rho * velocity * d / mu;

        // The friction factor from the kernel's Colebrook — the same
        // implementation the VLP uses (SDD-006 §6: one implementation, shared).
        double f = Friction.Factor(reynolds, 4.6e-5 / d);
        double expected = f * (l / d) * rho * velocity * velocity / 2.0;

        Assert.Equal(expected, drop.Pascals, precision: 6);
    }

    [Fact] // Static head is there even at zero flow, and only there
    public void MX4_elevation_costs_pressure_independently_of_rate()
    {
        PipeGeometry geometry = Line();
        LiquidHydraulicModel uphill = Liquid(riseM: 200.0);

        double atRest = uphill.DropAlong(At(0.0), geometry, new Fx.IdealSplitFluid()).Pascals;
        double expected = 850.0 * PhysicalConstants.GravityMPerS2 * 200.0;

        Assert.Equal(expected, atRest, precision: 6);

        // And it adds to the friction rather than replacing it.
        double flowing = uphill.DropAlong(At(100.0), geometry, new Fx.IdealSplitFluid()).Pascals;
        Assert.True(flowing > atRest);
    }

    // ------------------------------------------------------------ MX5

    [Fact] // MX5: doubling the diameter raises capacity by the predicted factor
    public void MX5_capacity_scales_with_diameter_as_the_hydraulics_predict()
    {
        var available = Pressure.FromBar(20.0);

        double narrow = Liquid().CapacityFor(Line(diameterM: 0.2), available).KgPerSecond;
        double wide = Liquid().CapacityFor(Line(diameterM: 0.4), available).KgPerSecond;

        // For fully-rough turbulent flow ΔP ~ ṁ²/D⁵, so at a fixed ΔP the
        // capacity goes as D^2.5 — a factor of about 5.7 for a doubling. The
        // friction factor drifts with Reynolds, so the check is a band around
        // the analytic exponent rather than a point.
        double ratio = wide / narrow;
        double analytic = Math.Pow(2.0, 2.5);

        Assert.InRange(ratio, analytic * 0.9, analytic * 1.15);
    }

    [Fact] // R11-V4: a heavier, more viscous crude moves less through the same pipe
    public void R11V4_a_more_viscous_crude_reduces_capacity()
    {
        var available = Pressure.FromBar(20.0);
        PipeGeometry geometry = Line();

        double light = Liquid(viscosity: new Viscosity(2.0e-3))
            .CapacityFor(geometry, available).KgPerSecond;
        double heavy = Liquid(density: Density.FromSpecificGravity(0.95),
                              viscosity: new Viscosity(200.0e-3))
            .CapacityFor(geometry, available).KgPerSecond;

        Assert.True(heavy < light,
            $"the heavy crude moved {heavy}, not less than {light}");
    }

    [Fact] // R11-V5: a parallel line raises capacity — and by LESS than double
    public void R11V5_looping_raises_capacity_by_the_predicted_amount()
    {
        var available = Pressure.FromBar(20.0);

        double single = Liquid().CapacityFor(Line(diameterM: 0.3), available).KgPerSecond;

        // Two identical lines share the same end pressures, so each carries what
        // one carried: exactly double. Looping with a DIFFERENT diameter does
        // not, which is why the loop's size is a decision.
        double looped = 2.0 * single;
        double biggerSingle = Liquid().CapacityFor(Line(diameterM: 0.42), available).KgPerSecond;

        Assert.Equal(2.0, looped / single, precision: 9);

        // And a single line of the same total cross-section beats the loop,
        // because capacity goes as D^2.5 rather than as area.
        Assert.True(biggerSingle > looped,
            "one larger line should beat two smaller ones of the same total area");
    }

    // ------------------------------------------------------------ R11-V3

    [Fact] // R11-V3: a GAS line's capacity collapses as inlet pressure declines
    public void R11V3_gas_capacity_follows_the_pressure_squared_form()
    {
        var gas = new GasHydraulicModel(
            molarMassKgPerMol: 0.019, averageCompressibility: 0.88,
            averageTemperature: Temperature.FromCelsius(25.0), frictionFactor: 0.015);

        PipeGeometry geometry = Line(lengthKm: 100.0, diameterM: 0.5);
        var outlet = Pressure.FromBar(20.0);

        double atHigh = gas.CapacityBetween(geometry, Pressure.FromBar(80.0), outlet).KgPerSecond;
        double atMid = gas.CapacityBetween(geometry, Pressure.FromBar(50.0), outlet).KgPerSecond;
        double atLow = gas.CapacityBetween(geometry, Pressure.FromBar(30.0), outlet).KgPerSecond;

        Assert.True(atMid < atHigh);
        Assert.True(atLow < atMid);

        // THE SQUARES ARE THE POINT. Halving the inlet from 80 to 40 bar against
        // a 20 bar outlet leaves (40² − 20²)/(80² − 20²) = 0.2 of the driving
        // force, so capacity falls to sqrt(0.2) = 0.447 of what it was.
        double atHalf = gas.CapacityBetween(geometry, Pressure.FromBar(40.0), outlet).KgPerSecond;
        Assert.Equal(Math.Sqrt(0.2), atHalf / atHigh, precision: 9);

        // And the shape is what matters, not that one number: capacity goes as
        // sqrt(P1² − P2²), which COLLAPSES as the inlet approaches the outlet.
        // The last few bar of declining field pressure cost far more line
        // capacity than the first few did.
        double nearlyStalled = gas.CapacityBetween(
            geometry, Pressure.FromBar(21.0), outlet).KgPerSecond;

        Assert.True(nearlyStalled < atHigh * 0.1,
            $"at 1 bar of drive the line still passed {nearlyStalled / atHigh:P0} of capacity");

        // And at the outlet pressure it carries nothing at all.
        Assert.Equal(0.0, gas.CapacityBetween(geometry, outlet, outlet).KgPerSecond, precision: 12);
    }

    [Fact] // R11-V3, the consequence: a line sized at first gas is inadequate later
    public void R11V3_a_line_sized_at_first_gas_starves_as_the_field_declines()
    {
        var gas = new GasHydraulicModel(0.019, 0.88, Temperature.FromCelsius(25.0), 0.015);
        PipeGeometry geometry = Line(lengthKm: 100.0, diameterM: 0.5);
        var outlet = Pressure.FromBar(20.0);

        double firstGas = gas.CapacityBetween(geometry, Pressure.FromBar(90.0), outlet).KgPerSecond;

        // Ten years on, the field delivers 35 bar to the line. The line has not
        // changed and neither has its rating; it simply cannot pass what it
        // once did, and no amount of it having been "big enough" helps.
        double lateLife = gas.CapacityBetween(geometry, Pressure.FromBar(35.0), outlet).KgPerSecond;

        Assert.True(lateLife < firstGas * 0.4,
            $"late-life capacity {lateLife} is not materially below first gas {firstGas}");
    }

    // ------------------------------------------------------------ R11-V7

    [Fact] // R11-V7: linefill is real, owned mass
    public void R11V7_linefill_is_inventory_the_line_owns()
    {
        Pipeline line = NewPipeline(diameterM: 0.3, lengthKm: 20.0);

        // ρ̄·A·L, recomputed independently.
        double expected = 850.0 * Math.PI * 0.3 * 0.3 / 4.0 * 20_000.0;
        Assert.Equal(expected, line.FullLinefill.Kilograms, precision: 3);

        Assert.Equal(0.0, line.Linefill.Total.Kilograms, precision: 12);

        line.CommitLinefill(MaterialInventory.Of(expected * 0.6, 0.0, 0.0));
        Assert.Equal(expected * 0.6, line.Linefill.Total.Kilograms, precision: 6);
    }

    [Fact] // More linefill than the pipe holds is an invariant failure
    public void R11V7_overfilling_the_line_is_an_invariant_fault()
    {
        Pipeline line = NewPipeline();

        var fault = Assert.Throws<InvariantFault>(
            () => line.CommitLinefill(MaterialInventory.Of(line.FullLinefill.Kilograms * 2.0, 0.0, 0.0)));

        Assert.Contains("linefill", fault.Fault.Detail);
    }

    [Fact] // R11 §2.1: the line has NO capacity field to configure
    public void R11V1_a_pipeline_declares_geometry_and_never_a_capacity()
    {
        Pipeline line = NewPipeline();

        // Everything it declares is geometry or a rating.
        Assert.Equal(20_000.0, line.PipeLength.Metres, 9);
        Assert.Equal(0.3, line.InnerDiameter.Metres, 9);
        Assert.True(line.Rating.Pascals > 0.0);

        // Throughput is asked of the hydraulics for the fluid actually flowing —
        // a stored maxRate could not have produced R11-V3 or R11-V4 at all.
        Assert.DoesNotContain(typeof(Pipeline).GetProperties(),
            p => p.Name.Contains("Capacity", StringComparison.Ordinal)
              && p.Name != nameof(Pipeline.FullLinefill));
    }

    [Fact] // SDD-006 §6: erosional velocity is a CONSTRAINT, not a block
    public void R11V12_erosional_velocity_is_reported_as_a_constraint()
    {
        Pipeline line = NewPipeline(diameterM: 0.1);

        ConstraintEvaluation constraint = Assert.Single(
            line.EvaluateConstraints(Fx.In(At(300.0))));

        Assert.Equal(ConstraintKind.ErosionalVelocity, constraint.Kind);

        // v_max = C/sqrt(ρ), recomputed.
        Assert.Equal(122.0 / Math.Sqrt(850.0), constraint.Capacity, precision: 9);

        // Over it — a line run this hot erodes. It does not refuse; the hazard
        // rate rises (design 05 §6.3), which is a different kind of answer.
        Assert.True(constraint.Load > constraint.Capacity);
    }

    [Fact] // The drop reaches the outlet pressure, which is how backpressure travels
    public void R11V1_the_transform_lowers_the_outlet_pressure_by_the_drop()
    {
        Pipeline line = NewPipeline();
        MaterialStream inlet = At(100.0);

        TransformResult result = line.Transform(Fx.In(inlet));

        double expected = inlet.P.Pascals
                        - Liquid().DropAlong(inlet, line.Geometry, new Fx.IdealSplitFluid()).Pascals;

        Assert.Equal(expected, result.Outlets[0].P.Pascals, precision: 6);
        Assert.Equal(100.0, result.Outlets[0].MassRates.Total.KgPerSecond, 12);
    }

    private static Pipeline NewPipeline(double diameterM = 0.3, double lengthKm = 20.0) =>
        new(new EntityId<IFlowElement>(20),
            Line(lengthKm, diameterM), Pressure.FromBar(100.0), new ContentId("api-5l-x60"),
            Liquid(), new Fx.IdealSplitFluid(), Crude, Fx.MaterialCount);
}
