// R5.9 — drive mechanisms and the aquifer (SDD-003 §3.3, §4.2b).

using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Subsurface;

namespace OGSim.Subsurface.Tests;

public class DriveMechanismTests
{
    private static IFluidPropertyModel Fluid() => new BlackOilModel(
        new BlackOilInputs(
            OilGravity: new ApiGravity(35.0),
            GasSpecificGravity: 0.75,
            ReservoirTemperature: Temperature.FromCelsius(93.3),
            SolutionGorAtBubblePoint: 100.0,
            Form: FluidForm.BlackOil),
        new ValidityRange(
            new Pressure(500.0), new Pressure(60e6),
            Temperature.FromCelsius(10.0), Temperature.FromCelsius(180.0)));

    private static MaterialBalanceInput Input(
        double gasCapRatio = 0.0, double influxM3 = 0.0, double startPa = 30e6) =>
        new(InitialPressure: new Pressure(30e6),
            OriginalOilInPlace: new SurfaceVolume(1.0e6),
            GasCapRatio: gasCapRatio,
            ConnateWaterSaturation: 0.2,
            WaterCompressibility: 4.4e-10,
            RockCompressibility: 5.8e-10,
            CumulativeOilProduced: new SurfaceVolume(4_000.0),
            CumulativeGasProduced: new StandardGasVolume(1_200_000.0),
            CumulativeWaterProduced: new SurfaceVolume(200.0),
            CumulativeWaterInflux: new ReservoirVolume(influxM3),
            CumulativeInjected: new ReservoirVolume(0.0),
            StartPressure: new Pressure(startPa),
            WithdrawnThisTick: new ReservoirVolume(0.0),
            GasInPlace: new StandardGasVolume(0.0),
            ReservoirTemperature: Temperature.FromCelsius(93.3));

    [Fact] // §4.2b: the six are distinguished by which terms they admit
    public void R5V4_each_mechanism_declares_the_terms_it_admits()
    {
        Assert.Equal(new AdmittedTerms(false, false), new SolutionGasDrive().Admits);
        Assert.Equal(new AdmittedTerms(true, false), new GasCapExpansionDrive().Admits);
        Assert.Equal(new AdmittedTerms(false, true), new WaterDrive().Admits);
        Assert.Equal(new AdmittedTerms(false, false), new CompactionDrive().Admits);
        Assert.Equal(new AdmittedTerms(false, false), new GravityDrainageDrive().Admits);
        Assert.Equal(new AdmittedTerms(true, true), new CombinationDrive().Admits);
    }

    [Fact] // Each ships under its own content id — the player names their drive
    public void R5V4_each_mechanism_has_a_distinct_content_id()
    {
        string[] ids =
        [
            new SolutionGasDrive().Id.Value, new GasCapExpansionDrive().Id.Value,
            new WaterDrive().Id.Value, new CompactionDrive().Id.Value,
            new GravityDrainageDrive().Id.Value, new CombinationDrive().Id.Value,
        ];

        Assert.Equal(6, ids.Distinct().Count());
    }

    [Fact] // A compartment that contradicts its drive is caught, not quietly zeroed
    public void R5V4_a_gas_cap_under_a_solution_gas_drive_is_a_model_fault()
    {
        var fault = Assert.Throws<ModelFault>(() =>
            new SolutionGasDrive().SolveEndPressure(Input(gasCapRatio: 0.3), Fluid()));

        Assert.Contains("does not admit a gas cap", fault.Fault.Detail);
        Assert.Contains("solution-gas-drive", fault.Fault.Detail);
    }

    [Fact] // ...and so is influx under a drive that does not admit it
    public void R5V4_influx_under_a_solution_gas_drive_is_a_model_fault()
    {
        var fault = Assert.Throws<ModelFault>(() =>
            new SolutionGasDrive().SolveEndPressure(Input(influxM3: 5_000.0), Fluid()));

        Assert.Contains("does not admit aquifer influx", fault.Fault.Detail);
    }

    [Fact] // A gas-cap drive with no gas cap is a name promising what it has not got
    public void R5V4_a_gas_cap_drive_without_a_gas_cap_is_a_model_fault()
    {
        var fault = Assert.Throws<ModelFault>(() =>
            new GasCapExpansionDrive().SolveEndPressure(Input(gasCapRatio: 0.0), Fluid()));

        Assert.Contains("declares no gas cap", fault.Fault.Detail);
    }

    [Fact] // The combination drive admits everything, so it refuses nothing coherent
    public void R5V4_the_combination_drive_accepts_both_terms()
    {
        Pressure p = new CombinationDrive()
            .SolveEndPressure(Input(gasCapRatio: 0.2, influxM3: 3_000.0), Fluid());

        Assert.True(p.Pascals > 0.0);
    }

    [Fact] // Natural drives take no injectant; R9/R10 add mechanisms that do
    public void R5V4_natural_drives_accept_no_injectants()
    {
        Assert.Empty(new SolutionGasDrive().AcceptedInjectants);
        Assert.Empty(new WaterDrive().AcceptedInjectants);
    }

    [Fact] // Gravity drainage tolerates a smaller step than solution gas does
    public void R5V11_gravity_drainage_carries_a_tighter_step_limit()
    {
        IFluidPropertyModel fluid = Fluid();

        // A step that lands between the two limits: accepted under solution gas,
        // refused under gravity drainage, whose segregation the monthly step
        // must not step over. Found by search rather than by a hand-picked
        // number, so a change to the correlations cannot silently make this test
        // stop testing anything.
        MaterialBalanceInput? separating = null;

        for (int np = 1_000; np <= 40_000 && separating is null; np += 500)
        {
            MaterialBalanceInput candidate = Input() with
            {
                CumulativeOilProduced = new SurfaceVolume(np),
                CumulativeGasProduced = new StandardGasVolume(np * 300.0),
            };

            if (Accepts(new SolutionGasDrive(), candidate, fluid)
                && !Accepts(new GravityDrainageDrive(), candidate, fluid))
                separating = candidate;
        }

        Assert.NotNull(separating);
        Assert.True(new SolutionGasDrive().SolveEndPressure(separating, fluid).Pascals > 0.0);
        Assert.Throws<ModelFault>(() => new GravityDrainageDrive().SolveEndPressure(separating, fluid));
    }

    private static bool Accepts(
        IDriveMechanism drive, MaterialBalanceInput input, IFluidPropertyModel fluid)
    {
        try
        {
            drive.SolveEndPressure(input, fluid);
            return true;
        }
        catch (ModelFault)
        {
            // The question this helper asks IS "does it fault?", so the fault is
            // the answer rather than an error being swallowed (law L4).
            return false;
        }
    }
}

public class AquiferTests
{
    private static FetkovichAquifer Aquifer(
        double productivityIndex = 1e-8, double maximumInfluxM3 = 1.0e6) =>
        new(productivityIndex, new Pressure(30e6), new ReservoirVolume(maximumInfluxM3));

    private static readonly Duration OneMonth = Duration.FromTicks(1.0);

    [Fact] // R5-V8: influx responds to pressure drop
    public void R5V8_influx_grows_with_the_pressure_difference()
    {
        double small = Aquifer().InfluxDuring(new Pressure(29e6), OneMonth).CubicMetres;
        double large = Aquifer().InfluxDuring(new Pressure(20e6), OneMonth).CubicMetres;

        Assert.True(large > small, $"{large} was not more than {small}");
    }

    [Fact] // ...and to elapsed time
    public void R5V8_influx_grows_with_elapsed_time()
    {
        // Large enough that the cap does not bind: this test is about the time
        // term, and a capped answer would be testing the cap instead.
        var at = new Pressure(25e6);
        double month = Aquifer(maximumInfluxM3: 1.0e8).InfluxDuring(at, OneMonth).CubicMetres;
        double year = Aquifer(maximumInfluxM3: 1.0e8)
            .InfluxDuring(at, Duration.FromTicks(12.0)).CubicMetres;

        Assert.Equal(12.0, year / month, precision: 9);
    }

    [Fact] // An aquifer WEAKENS as it is drawn down — water drive is not free
    public void R5V8_the_aquifer_weakens_as_it_delivers()
    {
        // Sized so that a year of production draws it down measurably without
        // exhausting it: the weakening under test is the pressure term, not the
        // cap, and an aquifer that emptied on the first call would prove neither.
        FetkovichAquifer aquifer = Aquifer(productivityIndex: 1e-9, maximumInfluxM3: 1.0e6);
        var reservoir = new Pressure(20e6);

        double initialAquiferPa = aquifer.AquiferPressure.Pascals;
        double first = aquifer.InfluxDuring(reservoir, OneMonth).CubicMetres;

        for (int i = 0; i < 10; i++) aquifer.InfluxDuring(reservoir, OneMonth);

        double later = aquifer.InfluxDuring(reservoir, OneMonth).CubicMetres;

        Assert.True(later < first, $"a drawn-down aquifer delivered {later}, not less than {first}");
        Assert.True(aquifer.AquiferPressure.Pascals < initialAquiferPa);
        Assert.True(aquifer.CumulativeInflux.CubicMetres < 1.0e6, "the cap should not have bound");
    }

    [Fact] // It cannot deliver more than it has
    public void R5V8_cumulative_influx_never_exceeds_the_maximum()
    {
        FetkovichAquifer aquifer = Aquifer(productivityIndex: 1e-3, maximumInfluxM3: 1_000.0);

        for (int i = 0; i < 50; i++) aquifer.InfluxDuring(new Pressure(1e6), OneMonth);

        Assert.True(aquifer.CumulativeInflux.CubicMetres <= 1_000.0);
        Assert.Equal(1_000.0, aquifer.CumulativeInflux.CubicMetres, precision: 9);
    }

    [Fact] // Water does not flow back: a shut-in compartment cannot refill itself
    public void R5V8_a_reservoir_above_the_aquifer_takes_no_influx()
    {
        Assert.Equal(0.0,
            Aquifer().InfluxDuring(new Pressure(40e6), OneMonth).CubicMetres, precision: 12);
    }

    [Fact] // An aquifer that cannot deliver is expressed by not attaching one
    public void R5V8_a_non_positive_productivity_index_is_a_model_fault()
    {
        var fault = Assert.Throws<ModelFault>(() => Aquifer(productivityIndex: 0.0));
        Assert.Contains("productivity index", fault.Fault.Detail);
    }
}
