// R5.9 — the material balance (SDD-003 §3.1, design 05 §3.1).
//
// R5's whole claim is G1: pressure falls because of physics, not because a
// decline formula said so. These tests are what make that claim checkable —
// each asserts a property of the BALANCE, not a number the balance happened to
// produce, so they would survive a re-tune of the correlations behind it.

using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Subsurface;

namespace OGSim.Subsurface.Tests;

public class MaterialBalanceTests
{
    private static readonly Temperature ReservoirT = Temperature.FromCelsius(93.3);   // 200 °F

    private static IFluidPropertyModel Fluid() => new BlackOilModel(
        new BlackOilInputs(
            OilGravity: new ApiGravity(35.0),
            GasSpecificGravity: 0.75,
            ReservoirTemperature: ReservoirT,
            SolutionGorAtBubblePoint: 100.0,      // sm³/sm³
            Form: FluidForm.BlackOil),
        // Wide enough to cover the bracket the balance searches. The correlations'
        // own published ranges are narrower; R5's job is the balance, and clipping
        // the search to a range the solver must traverse would test the clip.
        new ValidityRange(
            MinP: new Pressure(500.0), MaxP: new Pressure(60e6),
            MinT: Temperature.FromCelsius(10.0), MaxT: Temperature.FromCelsius(180.0)));

    /// <summary>An undersaturated oil compartment with no gas cap and no aquifer.</summary>
    private static MaterialBalanceInput SolutionGas(
        double oilProducedM3 = 0.0,
        double gasProducedM3 = 0.0,
        double waterProducedM3 = 0.0,
        double startPressurePa = 30e6) =>
        new(InitialPressure: new Pressure(30e6),
            OriginalOilInPlace: new SurfaceVolume(1.0e6),
            GasCapRatio: 0.0,
            ConnateWaterSaturation: 0.2,
            WaterCompressibility: 4.4e-10,
            RockCompressibility: 5.8e-10,
            CumulativeOilProduced: new SurfaceVolume(oilProducedM3),
            CumulativeGasProduced: new StandardGasVolume(gasProducedM3),
            CumulativeWaterProduced: new SurfaceVolume(waterProducedM3),
            CumulativeWaterInflux: new ReservoirVolume(0.0),
            CumulativeInjected: new ReservoirVolume(0.0),
            StartPressure: new Pressure(startPressurePa),
            WithdrawnThisTick: new ReservoirVolume(0.0),
            GasInPlace: new StandardGasVolume(0.0),
            ReservoirTemperature: ReservoirT);

    // ------------------------------------------------------------- the balance

    [Fact] // An untouched compartment is still at its initial pressure
    public void R5V9_nothing_produced_leaves_the_pressure_at_initial()
    {
        MaterialBalanceInput input = SolutionGas();

        Assert.Equal(0.0, MaterialBalance.Residual(input, Fluid(), input.InitialPressure), 9);
        Assert.Equal(input.InitialPressure.Pascals,
                     MaterialBalance.Solve(input, Fluid(), 0.25).Pascals,
                     precision: 0);
    }

    [Fact] // G1: pressure falls BY MATERIAL BALANCE — more out, lower pressure
    public void R5V4_producing_more_lowers_the_pressure_monotonically()
    {
        IFluidPropertyModel fluid = Fluid();

        double previous = double.MaxValue;
        for (int step = 1; step <= 6; step++)
        {
            double np = 2_000.0 * step;
            Pressure p = MaterialBalance.Solve(
                SolutionGas(oilProducedM3: np, gasProducedM3: np * 100.0), fluid, 0.25);

            Assert.True(p.Pascals < previous,
                $"step {step}: pressure {p.Pascals} did not fall below {previous}");
            previous = p.Pascals;
        }
    }

    [Fact] // The solved pressure is the ROOT — verified against Φ, not against itself
    public void R5V9_the_solved_pressure_is_a_root_of_the_balance()
    {
        IFluidPropertyModel fluid = Fluid();
        MaterialBalanceInput input =
            SolutionGas(oilProducedM3: 8_000.0, gasProducedM3: 800_000.0);

        Pressure solved = MaterialBalance.Solve(input, fluid, 0.25);

        // Φ is in reservoir m³ against a compartment holding a million of them,
        // so "zero" is judged relative to the expansion the solve is balancing.
        double residual = MaterialBalance.Residual(input, fluid, solved);
        double scale = MaterialBalance.Withdrawal(input, fluid, solved);

        Assert.True(Math.Abs(residual) < 1e-3 * scale,
            $"Φ = {residual} reservoir m³ at the solved pressure, against a withdrawal of {scale}");
    }

    [Fact] // Water drive: influx supports pressure — the same offtake costs less
    public void R5V3_aquifer_influx_supports_the_pressure()
    {
        IFluidPropertyModel fluid = Fluid();

        MaterialBalanceInput dry =
            SolutionGas(oilProducedM3: 8_000.0, gasProducedM3: 800_000.0);
        MaterialBalanceInput supported = dry with
        {
            CumulativeWaterInflux = new ReservoirVolume(6_000.0),
        };

        double dryPa = MaterialBalance.Solve(dry, fluid, 0.25).Pascals;
        double supportedPa = MaterialBalance.Solve(supported, fluid, 0.4).Pascals;

        Assert.True(supportedPa > dryPa,
            $"influx did not support pressure: {supportedPa} not above {dryPa}");
    }

    [Fact] // A gas cap does the same work, through m rather than through We
    public void R5V3_a_gas_cap_supports_the_pressure()
    {
        IFluidPropertyModel fluid = Fluid();

        MaterialBalanceInput none =
            SolutionGas(oilProducedM3: 8_000.0, gasProducedM3: 800_000.0);
        MaterialBalanceInput capped = none with { GasCapRatio = 0.3 };

        Assert.True(MaterialBalance.Solve(capped, fluid, 0.25).Pascals
                  > MaterialBalance.Solve(none, fluid, 0.25).Pascals);
    }

    [Fact] // Cumulative, not incremental: the answer depends only on the totals
    public void R5V9_the_solve_depends_only_on_cumulative_totals()
    {
        IFluidPropertyModel fluid = Fluid();
        MaterialBalanceInput input =
            SolutionGas(oilProducedM3: 6_000.0, gasProducedM3: 600_000.0);

        // The same cumulative position reached from two different start
        // pressures. §3.1's amendment is exactly this property: each tick
        // re-solves from Pi, so a rounding error in one tick's pressure cannot
        // propagate into the next.
        double fromHigh = MaterialBalance.Solve(
            input with { StartPressure = new Pressure(30e6) }, fluid, 0.25).Pascals;
        double fromLow = MaterialBalance.Solve(
            input with { StartPressure = new Pressure(26e6) }, fluid, 0.25).Pascals;

        Assert.Equal(fromHigh, fromLow, precision: 0);
    }

    // ------------------------------------------------------- the validity limit

    [Fact] // 05 §3.1: too large a step is refused, never fudged
    public void R5V11_a_step_beyond_the_pressure_drop_limit_is_a_model_fault()
    {
        IFluidPropertyModel fluid = Fluid();

        // A month that takes the compartment from initial pressure to deep
        // depletion in one go. The ANSWER is not wrong so much as unbounded:
        // explicit first-order integration says nothing useful across a step
        // that large, and 05 §3.1 refuses rather than returning it.
        MaterialBalanceInput input =
            SolutionGas(oilProducedM3: 60_000.0, gasProducedM3: 6_000_000.0);

        var fault = Assert.Throws<ModelFault>(() => MaterialBalance.Solve(input, fluid, 0.25));

        Assert.Contains("drops the pressure", fault.Fault.Detail);
        Assert.Contains("0.25", fault.Fault.Detail);
    }

    [Fact] // A step inside the limit passes without comment
    public void R5V11_a_step_inside_the_pressure_drop_limit_is_accepted()
    {
        IFluidPropertyModel fluid = Fluid();
        MaterialBalanceInput input =
            SolutionGas(oilProducedM3: 2_000.0, gasProducedM3: 200_000.0);

        Pressure end = MaterialBalance.Solve(input, fluid, 0.25);

        Assert.True(end.Pascals < input.StartPressure.Pascals);
        Assert.True((input.StartPressure.Pascals - end.Pascals) / input.StartPressure.Pascals <= 0.25);
    }

    [Fact] // The limit is on THE STEP, so the same cumulative state is fine from nearer
    public void R5V11_the_limit_measures_the_step_not_the_depletion()
    {
        IFluidPropertyModel fluid = Fluid();
        MaterialBalanceInput deep =
            SolutionGas(oilProducedM3: 60_000.0, gasProducedM3: 6_000_000.0);

        // Refused as one leap from initial pressure...
        Assert.Throws<ModelFault>(() => MaterialBalance.Solve(deep, fluid, 0.25));

        // ...and accepted when the compartment is already most of the way there,
        // which is what happens when the ticks in between were each taken.
        Pressure end = MaterialBalance.Solve(
            deep with { StartPressure = new Pressure(9.0e6) }, fluid, 0.25);

        Assert.True(end.Pascals > 0.0);
    }

    [Theory] // Content errors are caught where the cause is still in hand
    [InlineData(0.0, 0.25, "original oil in place")]
    [InlineData(1.0, 0.25, "connate water saturation")]
    [InlineData(-1.0, 0.25, "connate water saturation")]
    public void R5V11_incoherent_input_is_named(double swcOrOoip, double limit, string expected)
    {
        MaterialBalanceInput input = expected.StartsWith("original", StringComparison.Ordinal)
            ? SolutionGas() with { OriginalOilInPlace = new SurfaceVolume(swcOrOoip) }
            : SolutionGas() with { ConnateWaterSaturation = swcOrOoip };

        var fault = Assert.Throws<ModelFault>(() => MaterialBalance.Solve(input, Fluid(), limit));
        Assert.Contains(expected, fault.Fault.Detail);
    }

    // ------------------------------------------------------- recovery emerges

    [Theory] // MB2: solution gas recovers little — once its gas is PRODUCED
    [InlineData(300.0)]    // 3 × Rsi
    [InlineData(600.0)]    // 6 × Rsi
    [InlineData(1200.0)]   // 12 × Rsi
    public void R5V4_solution_gas_recovery_lands_in_band_MB2(double producingGor)
    {
        // Abandonment at a tenth of initial pressure. Recovery is whatever
        // cumulative production puts the balance there; it is stored nowhere
        // and computed by nothing but this equation (R5 G2).
        double factor = RecoveryAtAbandonment(Fluid(), 3.0e6, producingGor) / 1.0e6;

        Assert.InRange(factor, 0.05, 0.30);
    }

    [Fact] // The bubble-point cliff, in the balance rather than in a script
    public void R5V5_recovery_falls_as_producing_gas_oil_ratio_rises()
    {
        IFluidPropertyModel fluid = Fluid();

        // Below Pb, gas comes out of solution and is produced; GOR climbs. Each
        // sm³ of free gas produced is reservoir volume the compartment does not
        // get to keep as expansion, so recovery falls — steeply.
        double previous = double.MaxValue;
        foreach (double gor in new[] { 100.0, 300.0, 600.0, 1200.0, 2400.0 })
        {
            double factor = RecoveryAtAbandonment(fluid, 3.0e6, gor) / 1.0e6;
            Assert.True(factor < previous,
                $"GOR {gor}: recovery {factor} did not fall below {previous}");
            previous = factor;
        }

        // And the magnitude is the drama design 02 §2.3 asks for, not a nudge:
        // retaining the liberated gas gives ~0.72, producing it at 12 × Rsi
        // gives ~0.07. An order of magnitude, from one physical mechanism.
        Assert.True(RecoveryAtAbandonment(fluid, 3.0e6, 100.0)
                  > 10.0 * RecoveryAtAbandonment(fluid, 3.0e6, 1200.0));
    }

    /// <summary>Bisection on cumulative oil for the production that lands the
    /// compartment at a given abandonment pressure. Independent of the solver
    /// under test — it only calls Residual.</summary>
    private static double RecoveryAtAbandonment(
        IFluidPropertyModel fluid, double abandonmentPa, double gasOilRatio)
    {
        double low = 0.0, high = 900_000.0;
        var at = new Pressure(abandonmentPa);

        for (int i = 0; i < 200; i++)
        {
            double mid = low + (high - low) * 0.5;
            MaterialBalanceInput input =
                SolutionGas(oilProducedM3: mid, gasProducedM3: mid * gasOilRatio);

            // Φ > 0: expansion exceeds withdrawal, so the compartment is above
            // the abandonment pressure and can give more.
            if (MaterialBalance.Residual(input, fluid, at) > 0.0) low = mid;
            else high = mid;
        }

        return low;
    }
}
