// R6.11 — MX1, MX2, FV3 and the phase's verification suite (SDD-003 §6).

using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Wells;

namespace OGSim.Wells.Tests;

public static class Fixtures
{
    public const double WellboreRadiusM = 0.108;      // SDD-003 §6.1's content default

    public static InflowConditions Conditions(
        double permeabilityM2 = 1.0e-14,              // ~10 mD
        double netPayM = 20.0,
        double drainageAreaM2 = 2.0e5,                // ~50 acres
        double viscosityPaS = 2.0e-3,
        double bubblePointPa = 10.0e6) =>
        new(new Permeability(permeabilityM2),
            new Length(netPayM),
            new Area(drainageAreaM2),
            new Length(WellboreRadiusM),
            new Viscosity(viscosityPaS),
            new Pressure(bubblePointPa));

    public static Perforation Perf(double skin = 0.0, double lengthM = 20.0, bool isolated = false) =>
        new(new EntityId<IReservoirCompartmentEntity>(1),
            new Length(2000.0), new Length(2000.0 + lengthM), skin, isolated);

    public static TubingGeometry Tubing(
        double diameterM = 0.0889,                    // 3½" tubing
        double tvdM = 2000.0,
        double mdM = 2000.0) =>
        new(new Length(mdM), new Length(tvdM), new Length(diameterM), RoughnessMetres: 4.6e-5);
}

public class InflowTests
{
    // ------------------------------------------------------------- MX1

    [Fact] // MX1: the SI Darcy form matches an independently computed rate
    public void MX1_darcy_inflow_matches_the_analytic_rate()
    {
        var model = new DarcyInflowModel(Fixtures.Conditions());
        Perforation perf = Fixtures.Perf();

        ReservoirRate q = model.InflowAt(
            new Pressure(25.0e6), new Pressure(20.0e6), perf);

        // Recomputed here from the form SDD-003 §6.1 states, with every term
        // written out — not by calling the model a second way.
        const double k = 1.0e-14, h = 20.0, mu = 2.0e-3, drawdown = 5.0e6;
        double re = Math.Sqrt(2.0e5 / Math.PI);
        double expected = 2.0 * Math.PI * k * h * drawdown
                        / (mu * (Math.Log(re / Fixtures.WellboreRadiusM) - 0.75));

        Assert.Equal(expected, q.CubicMetresPerSecond, precision: 12);
    }

    [Theory] // Across a sweep, not at one point
    [InlineData(1.0e-15, 5.0)]
    [InlineData(5.0e-14, 40.0)]
    [InlineData(2.0e-13, 100.0)]
    public void MX1_darcy_holds_across_a_parameter_sweep(double permeability, double netPay)
    {
        var model = new DarcyInflowModel(
            Fixtures.Conditions(permeabilityM2: permeability, netPayM: netPay));

        ReservoirRate q = model.InflowAt(
            new Pressure(25.0e6), new Pressure(20.0e6),
            Fixtures.Perf(lengthM: netPay));

        double re = Math.Sqrt(2.0e5 / Math.PI);
        double expected = 2.0 * Math.PI * permeability * netPay * 5.0e6
                        / (2.0e-3 * (Math.Log(re / Fixtures.WellboreRadiusM) - 0.75));

        Assert.Equal(expected, q.CubicMetresPerSecond, precision: 12);
    }

    [Fact] // Inflow is linear in drawdown above the bubble point — the straight-line IPR
    public void MX1_darcy_inflow_is_linear_in_drawdown()
    {
        var model = new DarcyInflowModel(Fixtures.Conditions());
        Perforation perf = Fixtures.Perf();

        double one = model.InflowAt(new Pressure(25e6), new Pressure(24e6), perf).CubicMetresPerSecond;
        double three = model.InflowAt(new Pressure(25e6), new Pressure(22e6), perf).CubicMetresPerSecond;

        Assert.Equal(3.0, three / one, precision: 9);
    }

    [Fact] // No drawdown, no flow. And backwards drawdown is not injection.
    public void MX1_no_drawdown_gives_no_flow()
    {
        var model = new DarcyInflowModel(Fixtures.Conditions());

        Assert.Equal(0.0,
            model.InflowAt(new Pressure(25e6), new Pressure(25e6), Fixtures.Perf())
                 .CubicMetresPerSecond, precision: 12);

        Assert.Equal(0.0,
            model.InflowAt(new Pressure(25e6), new Pressure(30e6), Fixtures.Perf())
                 .CubicMetresPerSecond, precision: 12);
    }

    // ------------------------------------------------------------- MX2

    [Fact] // MX2: +10 skin costs the analytically predicted fraction of productivity
    public void MX2_skin_costs_the_analytic_fraction_of_productivity()
    {
        var model = new DarcyInflowModel(Fixtures.Conditions());

        double clean = model.ProductivityIndex(Fixtures.Perf(skin: 0.0));
        double damaged = model.ProductivityIndex(Fixtures.Perf(skin: 10.0));

        // J ∝ 1/(ln(re/rw) − 0.75 + s), so the ratio is a pure function of the
        // geometry — no rate, no pressure, nothing else.
        double re = Math.Sqrt(2.0e5 / Math.PI);
        double baseTerm = Math.Log(re / Fixtures.WellboreRadiusM) - 0.75;

        Assert.Equal(baseTerm / (baseTerm + 10.0), damaged / clean, precision: 12);

        // And it is a big number: skin 10 on this geometry costs over half the
        // well's productivity, which is why acidising pays for itself.
        Assert.True(damaged / clean < 0.5);
    }

    [Fact] // Negative skin — a fracture — helps, and its help is bounded by geometry
    public void MX2_negative_skin_improves_productivity()
    {
        var model = new DarcyInflowModel(Fixtures.Conditions());

        Assert.True(model.ProductivityIndex(Fixtures.Perf(skin: -3.0))
                  > model.ProductivityIndex(Fixtures.Perf(skin: 0.0)));

        // Skin so negative it inverts the denominator would give a perforation
        // producing against its own drawdown. Refused, not clamped.
        var fault = Assert.Throws<ModelFault>(
            () => model.ProductivityIndex(Fixtures.Perf(skin: -20.0)));
        Assert.Contains("against its own drawdown", fault.Fault.Detail);
    }

    [Fact] // R6 §2.4: isolating a zone is a real intervention with a computable effect
    public void R6V10_an_isolated_perforation_contributes_nothing()
    {
        var model = new DarcyInflowModel(Fixtures.Conditions());

        Assert.Equal(0.0,
            model.InflowAt(new Pressure(25e6), new Pressure(20e6),
                           Fixtures.Perf(isolated: true)).CubicMetresPerSecond,
            precision: 12);
    }

    // ------------------------------------------------------- composite IPR

    [Fact] // R6-V3: the composite IPR is CONTINUOUS across the bubble point
    public void R6V3_the_composite_ipr_is_continuous_at_the_bubble_point()
    {
        var model = new CompositeInflowModel(Fixtures.Conditions(bubblePointPa: 15.0e6));
        Perforation perf = Fixtures.Perf();
        var pr = new Pressure(25.0e6);

        // Approached from both sides. A step here would mean crossing Pb created
        // or destroyed rate — in the one place the design most wants trust.
        double above = model.InflowAt(pr, new Pressure(15.0e6 + 1.0), perf).CubicMetresPerSecond;
        double below = model.InflowAt(pr, new Pressure(15.0e6 - 1.0), perf).CubicMetresPerSecond;

        Assert.Equal(above, below, precision: 9);
    }

    [Fact] // R6-V2: below Pb the curve BENDS — that is Vogel's whole content
    public void R6V2_below_the_bubble_point_the_ipr_bends_away_from_the_straight_line()
    {
        var model = new CompositeInflowModel(Fixtures.Conditions(bubblePointPa: 15.0e6));
        var darcy = new DarcyInflowModel(Fixtures.Conditions(bubblePointPa: 15.0e6));

        Perforation perf = Fixtures.Perf();
        var pr = new Pressure(25.0e6);
        var deep = new Pressure(2.0e6);

        double composite = model.InflowAt(pr, deep, perf).CubicMetresPerSecond;
        double straightLine = darcy.InflowAt(pr, deep, perf).CubicMetresPerSecond;

        // Below the straight line: gas coming out of solution takes mobility
        // from the oil, so pulling harder buys less than Darcy would promise.
        Assert.True(composite < straightLine,
            $"Vogel {composite} was not below the Darcy line {straightLine}");
        Assert.True(composite > 0.0);
    }

    [Fact] // Vogel is monotone: pulling harder always buys SOMETHING more
    public void R6V2_the_composite_ipr_is_monotone_in_drawdown()
    {
        var model = new CompositeInflowModel(Fixtures.Conditions(bubblePointPa: 15.0e6));
        Perforation perf = Fixtures.Perf();
        var pr = new Pressure(25.0e6);

        double previous = -1.0;
        for (double pwf = 24.0e6; pwf >= 1.0e6; pwf -= 1.0e6)
        {
            double q = model.InflowAt(pr, new Pressure(pwf), perf).CubicMetresPerSecond;
            Assert.True(q > previous, $"at Pwf {pwf} the rate {q} did not exceed {previous}");
            previous = q;
        }
    }

    [Fact] // A wholly saturated reservoir is Vogel over its whole range
    public void R6V2_a_saturated_reservoir_uses_vogel_throughout()
    {
        // Pr below Pb: the reservoir is already saturated everywhere.
        var model = new CompositeInflowModel(Fixtures.Conditions(bubblePointPa: 30.0e6));
        Perforation perf = Fixtures.Perf();
        var pr = new Pressure(20.0e6);

        double qMax = model.InflowAt(pr, new Pressure(0.0), perf).CubicMetresPerSecond;
        double half = model.InflowAt(pr, new Pressure(10.0e6), perf).CubicMetresPerSecond;

        // Vogel at Pwf/Pr = 0.5: 1 − 0.2(0.5) − 0.8(0.25) = 0.7
        Assert.Equal(0.7, half / qMax, precision: 9);
    }
}
