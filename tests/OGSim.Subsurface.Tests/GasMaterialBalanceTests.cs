// R5.9 — MX3 and the p/Z line (SDD-003 §3.1b, design 05 §3.2).
//
// The player is never told how big their gas reservoir is. They produce, plot
// p/Z against cumulative gas, extrapolate the line, and read G off the
// x-intercept — and every point on that plot cost them a shut-in survey.
//
// So EXACTNESS is the requirement here, not approximation. The player is invited
// to trust their own arithmetic on this line; a percent of drift would make a
// correct deduction give a wrong answer, and the mechanic would quietly become a
// lie. MX3 is what stops that.

using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Subsurface;

namespace OGSim.Subsurface.Tests;

public class GasMaterialBalanceTests
{
    private static readonly Pressure Initial = new(30e6);
    private static readonly StandardGasVolume InPlace = new(1.0e9);      // G, sm³
    private static readonly GasFormationVolumeFactor InitialBg = new(0.005);
    private const double InitialZ = 0.9;

    private static double LineAt(double producedFraction, double influxM3 = 0.0) =>
        GasMaterialBalance.PressureOverZ(
            Initial, InitialZ, InPlace,
            new StandardGasVolume(InPlace.CubicMetres * producedFraction),
            new ReservoirVolume(influxM3),
            InitialBg);

    // --------------------------------------------------------------- MX3

    [Fact] // MX3: p/Z versus Gp is EXACTLY linear for a volumetric reservoir
    public void MX3_the_p_over_z_line_is_exactly_linear()
    {
        // Equal steps in Gp must give equal steps in p/Z. Not "close to" equal:
        // the player extrapolates this line by hand.
        double first = LineAt(0.0) - LineAt(0.1);

        for (int step = 1; step <= 8; step++)
        {
            double difference = LineAt(step * 0.1) - LineAt((step + 1) * 0.1);
            Assert.Equal(first, difference, precision: 6);
        }
    }

    [Fact] // MX3: the x-intercept IS the gas initially in place
    public void MX3_the_x_intercept_equals_gas_in_place()
    {
        // Extrapolate from two points exactly as a player would: fit the line
        // through them, and solve for where it reaches zero.
        double gp1 = InPlace.CubicMetres * 0.10;
        double gp2 = InPlace.CubicMetres * 0.25;

        double y1 = LineAt(0.10);
        double y2 = LineAt(0.25);

        double slope = (y2 - y1) / (gp2 - gp1);
        double intercept = gp1 - y1 / slope;

        Assert.Equal(InPlace.CubicMetres, intercept, precision: 0);
    }

    [Fact] // p/Z at zero production is pi/Zi, by definition
    public void MX3_the_line_starts_at_initial_conditions()
    {
        Assert.Equal(Initial.Pascals / InitialZ, LineAt(0.0), precision: 6);
    }

    [Fact] // Produce it all and p/Z reaches zero
    public void MX3_the_line_reaches_zero_at_full_depletion()
    {
        Assert.Equal(0.0, LineAt(1.0), precision: 6);
    }

    // ------------------------------------------------- the water-drive bend

    /// <summary>A real aquifer's influx GROWS as the reservoir is drawn down.
    /// Holding it constant would not be a water drive; it would be a one-off
    /// slug, and the plot would show an offset rather than a bend.</summary>
    private static double WithAquifer(double producedFraction)
    {
        double poreVolume = InPlace.CubicMetres * InitialBg.Rm3PerSm3;
        return LineAt(producedFraction, influxM3: 0.35 * producedFraction * poreVolume);
    }

    [Fact] // 05 §3.2: with water drive the points sit ABOVE the volumetric line
    public void R5V3_water_influx_holds_the_line_above_the_volumetric_case()
    {
        // Above, at every point: the aquifer is replacing produced volume, so
        // the reservoir holds pressure better than depletion alone explains.
        // And the departure GROWS with production, so it reads as a bend rather
        // than as a shifted line the player would misread as a bigger reservoir
        // of the same kind.
        double previous = 0.0;
        foreach (double fraction in new[] { 0.10, 0.20, 0.30, 0.40, 0.50 })
        {
            double relative = WithAquifer(fraction) / LineAt(fraction) - 1.0;

            Assert.True(relative > previous,
                $"at {fraction:P0} produced the departure {relative} did not exceed {previous}");
            previous = relative;
        }
    }

    [Fact] // The volumetric line is straight; the water-driven one is not
    public void R5V3_only_the_volumetric_line_has_a_constant_slope()
    {
        static double Slope(Func<double, double> line, double from, double to) =>
            (line(to) - line(from)) / ((to - from) * InPlace.CubicMetres);

        // MX3 again, from the other side: equal steps, equal slope.
        Assert.Equal(Slope(f => LineAt(f), 0.1, 0.2),
                     Slope(f => LineAt(f), 0.6, 0.7), precision: 6);

        // With an aquifer the slope steepens as the water encroaches — the
        // late-life fall-off that follows the early flattening.
        Assert.True(Slope(WithAquifer, 0.6, 0.7) < Slope(WithAquifer, 0.1, 0.2));
    }

    [Fact] // The classic misdiagnosis: a water drive reads as a BIGGER reservoir
    public void R5V3_extrapolating_a_bent_line_overstates_gas_in_place()
    {
        double gp1 = InPlace.CubicMetres * 0.05;
        double gp2 = InPlace.CubicMetres * 0.10;

        double y1 = WithAquifer(0.05);
        double slope = (WithAquifer(0.10) - y1) / (gp2 - gp1);
        double deduced = gp1 - y1 / slope;

        // A player who extrapolates early points on a water-driven reservoir
        // books more gas than exists. The engine never says so — the bend is the
        // only evidence, and reading it is the skill (05 §3.2).
        Assert.True(deduced > InPlace.CubicMetres,
            $"deduced {deduced} sm³ did not exceed the true {InPlace.CubicMetres} sm³");
    }

    // ------------------------------------------------------------- refusals

    [Fact] // Producing more gas than existed is a fault, never a clamp
    public void R5V9_producing_beyond_gas_in_place_is_a_model_fault()
    {
        var fault = Assert.Throws<ModelFault>(() => LineAt(1.5));
        Assert.Contains("exceeds gas in place", fault.Fault.Detail);
    }

    [Fact] // A fully watered-out compartment has no p/Z relation left to state
    public void R5V8_water_taking_the_whole_pore_volume_is_a_model_fault()
    {
        double wholePoreVolume = InPlace.CubicMetres * InitialBg.Rm3PerSm3;

        var fault = Assert.Throws<ModelFault>(() => LineAt(0.1, wholePoreVolume));
        Assert.Contains("watered out", fault.Fault.Detail);
    }

    [Fact] // Zero gas in place is a content error, named as one
    public void R5V9_zero_gas_in_place_is_a_model_fault()
    {
        var fault = Assert.Throws<ModelFault>(() => GasMaterialBalance.PressureOverZ(
            Initial, InitialZ, new StandardGasVolume(0.0),
            new StandardGasVolume(0.0), new ReservoirVolume(0.0), InitialBg));

        Assert.Contains("gas in place", fault.Fault.Detail);
    }
}
