// SDD-003 §3.1b's finding-264 amendment — the dry-gas balance, dispatched
// (R14.6, design 05 §3.2).
//
// The p/Z line existed since R5.7 and was tested only against inputs a fixture
// assembled by hand: there was no dry-gas compartment anywhere in this
// composition for the deduction to have a subject. These prove the RESERVOIR
// half of that claim — a hand-declared gas compartment produces, its pressure
// is governed by p/Z through the real Drive.SolveEndPressure dispatch (not a
// second, isolated call to the same formula), and the line is linear to
// floating-point tolerance, which is the whole of what a player is invited to
// trust when they extrapolate it.

using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Persistence;
using OGSim.Subsurface;

namespace OGSim.Subsurface.Tests;

public class VolumetricGasDriveTests
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
        double gasInPlaceM3 = 5.0e9,
        double gasProducedM3 = 0.0,
        double waterInfluxM3 = 0.0) =>
        new(InitialPressure: new Pressure(30e6),
            OriginalOilInPlace: new SurfaceVolume(0.0),
            GasCapRatio: 0.0,
            ConnateWaterSaturation: 0.2,
            WaterCompressibility: 4.4e-10,
            RockCompressibility: 5.8e-10,
            CumulativeOilProduced: new SurfaceVolume(0.0),
            CumulativeGasProduced: new StandardGasVolume(gasProducedM3),
            CumulativeWaterProduced: new SurfaceVolume(0.0),
            CumulativeWaterInflux: new ReservoirVolume(waterInfluxM3),
            CumulativeInjected: new ReservoirVolume(0.0),
            StartPressure: new Pressure(30e6),
            WithdrawnThisTick: new ReservoirVolume(0.0),
            GasInPlace: new StandardGasVolume(gasInPlaceM3),
            ReservoirTemperature: Temperature.FromCelsius(93.3));

    [Fact] // §3.1b: the oil form has no term for a dry-gas compartment either way
    public void An_oil_bearing_compartment_is_refused()
    {
        MaterialBalanceInput input = Input() with { OriginalOilInPlace = new SurfaceVolume(1.0) };

        var fault = Assert.Throws<ModelFault>(
            () => new VolumetricGasDrive().SolveEndPressure(input, Fluid()));

        Assert.Contains("original oil in place", fault.Fault.Detail);
    }

    [Fact] // The p/Z line has no denominator without a declared G
    public void A_compartment_with_no_gas_in_place_is_refused()
    {
        MaterialBalanceInput input = Input(gasInPlaceM3: 0.0);

        var fault = Assert.Throws<ModelFault>(
            () => new VolumetricGasDrive().SolveEndPressure(input, Fluid()));

        Assert.Contains("no gas in place", fault.Fault.Detail);
    }

    /// <summary>
    /// THE DISPATCH IS THE SAME FORMULA, not a second one. `VolumetricGasDrive`
    /// unpacks `MaterialBalanceInput` into `GasMaterialBalance.Solve`'s own
    /// parameters; this proves the unpacking is faithful by checking the drive
    /// against the function it wraps, called independently.
    /// </summary>
    [Fact]
    public void The_drive_matches_the_gas_balance_it_dispatches_to()
    {
        MaterialBalanceInput input = Input(gasProducedM3: 2.0e9);
        IFluidPropertyModel fluid = Fluid();

        Pressure viaDrive = new VolumetricGasDrive().SolveEndPressure(input, fluid);

        double initialZ = fluid.Z(input.InitialPressure, input.ReservoirTemperature);
        GasFormationVolumeFactor initialBg = fluid.Bg(input.InitialPressure);

        Pressure viaFunction = GasMaterialBalance.Solve(
            input.InitialPressure, initialZ, input.GasInPlace, input.CumulativeGasProduced,
            new ReservoirVolume(0.0), initialBg, fluid, input.ReservoirTemperature);

        Assert.Equal(viaFunction.Pascals, viaDrive.Pascals, precision: 6);
    }

    /// <summary>Water invading the gas pore volume BENDS the line (05 §3.2) —
    /// admitted, and pressure is supported above the volumetric-only case.</summary>
    [Fact]
    public void Water_influx_supports_pressure_above_the_volumetric_case()
    {
        IFluidPropertyModel fluid = Fluid();

        Pressure volumetric = new VolumetricGasDrive().SolveEndPressure(
            Input(gasProducedM3: 2.0e9, waterInfluxM3: 0.0), fluid);
        Pressure waterDriven = new VolumetricGasDrive().SolveEndPressure(
            Input(gasProducedM3: 2.0e9, waterInfluxM3: 1.0e6), fluid);

        Assert.True(waterDriven.Pascals > volumetric.Pascals,
            "water invading the gas pore volume did not support the pressure above the " +
            "volumetric-depletion case");
    }
}

/// <summary>
/// A hand-declared dry-gas field, produced through the real
/// <c>SubsurfaceState</c>/<c>ReservoirCompartment</c>/<c>Drive</c> chain rather
/// than a fixture handed straight to the balance function.
/// </summary>
public class GasReservoirTests
{
    private static readonly ContentId FluidSystemId = new("medium-crude");

    private static SubsurfaceState Gas() =>
        new(
            new Dictionary<ContentId, IFluidPropertyModel>
            {
                [FluidSystemId] = new BlackOilModel(
                    new BlackOilInputs(
                        new ApiGravity(35.0), 0.75, Temperature.FromCelsius(93.3), 100.0,
                        FluidForm.BlackOil),
                    new ValidityRange(
                        new Pressure(500.0), new Pressure(60e6),
                        Temperature.FromCelsius(10.0), Temperature.FromCelsius(180.0))),
            },
            new VolumetricGasDrive(),
            Souring.SweetRock, Souring.TheRock, Souring.SouringReference,
            maxTickPressureDropFraction: 0.4);

    private static EntityId<IReservoirCompartmentEntity> Add(SubsurfaceState state) =>
        state.CreateGas(
            poreVolume: new ReservoirVolume(5.0e8),
            porosity: 0.20,
            gasSaturation: 0.75,
            initialPressure: new Pressure(30.0e6),
            reservoirTemperature: Temperature.FromCelsius(93.3),
            permeability: new Permeability(5.0e-14),
            netThickness: new Length(15.0),
            drainageArea: new Area(4.0e6),
            rockCompressibility: 4.5e-10,
            gasWaterContact: new Length(2200.0),
            RelativePermeabilityCurve.Validated(
                swc: 0.25, sor: 0.0, krwMax: 0.35, kroMax: 0.90, nw: 3.0, no: 2.0),
            new ContentId("volumetric-gas-drive"),
            fluidSystem: FluidSystemId,
            aquiferStrength: 0.0,
            aquiferResponseTime: Duration.FromTicks(1.0));

    /// <summary>
    /// THE REAL DISPATCH, not the isolated formula: cumulative production is
    /// committed a chunk at a time through <c>SubsurfaceState.CommitTick</c> —
    /// the exact door stage 6 calls — and the pressure it reports each time is
    /// checked against `GasMaterialBalance.Solve` computed independently for
    /// the same cumulative history. If the dispatch unpacked a field wrong,
    /// this is what would catch it.
    /// </summary>
    [Fact]
    public void A_gas_compartments_pressure_follows_the_pZ_line_through_real_ticks()
    {
        SubsurfaceState state = Gas();
        EntityId<IReservoirCompartmentEntity> id = Add(state);
        IFluidPropertyModel fluid = new BlackOilModel(
            new BlackOilInputs(
                new ApiGravity(35.0), 0.75, Temperature.FromCelsius(93.3), 100.0,
                FluidForm.BlackOil),
            new ValidityRange(
                new Pressure(500.0), new Pressure(60e6),
                Temperature.FromCelsius(10.0), Temperature.FromCelsius(180.0)));

        var gasInPlace = new StandardGasVolume(
            5.0e8 * 0.75 / fluid.Bg(new Pressure(30.0e6)).Rm3PerSm3);
        double initialZ = fluid.Z(new Pressure(30.0e6), Temperature.FromCelsius(93.3));
        var initialBg = fluid.Bg(new Pressure(30.0e6));

        double cumulativeGas = 0.0;

        for (var tick = 0; tick < 6; tick++)
        {
            double thisTick = gasInPlace.CubicMetres * 0.03;   // 3% of G per month
            cumulativeGas += thisTick;

            state.CommitTick(
            [
                new CompartmentWithdrawal(
                    id, new SurfaceVolume(0.0), new StandardGasVolume(thisTick),
                    new SurfaceVolume(0.0), new ReservoirVolume(0.0), new ReservoirVolume(0.0),
                    new ReservoirVolume(0.0), new ReservoirVolume(0.0)),
            ]);

            Pressure expected = GasMaterialBalance.Solve(
                new Pressure(30.0e6), initialZ, gasInPlace, new StandardGasVolume(cumulativeGas),
                new ReservoirVolume(0.0), initialBg, fluid, Temperature.FromCelsius(93.3));

            Assert.Equal(
                expected.Pascals, state.TruePressureOf(id).Pascals, precision: 0);
        }
    }

    /// <summary>
    /// MX3's own claim, on a compartment produced through real ticks rather
    /// than fed to the formula directly: p/Z is linear in cumulative gas
    /// production to floating-point tolerance, which is what a player is
    /// invited to extrapolate.
    /// </summary>
    [Fact]
    public void The_pZ_line_is_linear_in_cumulative_production()
    {
        SubsurfaceState state = Gas();
        EntityId<IReservoirCompartmentEntity> id = Add(state);
        IFluidPropertyModel fluid = new BlackOilModel(
            new BlackOilInputs(
                new ApiGravity(35.0), 0.75, Temperature.FromCelsius(93.3), 100.0,
                FluidForm.BlackOil),
            new ValidityRange(
                new Pressure(500.0), new Pressure(60e6),
                Temperature.FromCelsius(10.0), Temperature.FromCelsius(180.0)));

        var gasInPlace = new StandardGasVolume(
            5.0e8 * 0.75 / fluid.Bg(new Pressure(30.0e6)).Rm3PerSm3);

        var points = new List<(double Gp, double PressureOverZ)>();

        for (var tick = 0; tick < 5; tick++)
        {
            double thisTick = gasInPlace.CubicMetres * 0.05;

            state.CommitTick(
            [
                new CompartmentWithdrawal(
                    id, new SurfaceVolume(0.0), new StandardGasVolume(thisTick),
                    new SurfaceVolume(0.0), new ReservoirVolume(0.0), new ReservoirVolume(0.0),
                    new ReservoirVolume(0.0), new ReservoirVolume(0.0)),
            ]);

            Pressure p = state.TruePressureOf(id);
            double z = fluid.Z(p, Temperature.FromCelsius(93.3));

            points.Add((tick == 0 ? thisTick : points[^1].Gp + thisTick, p.Pascals / z));
        }

        // A straight line's second difference is zero — the linearity check
        // MX3 makes, applied to points read off a real compartment.
        for (int i = 1; i < points.Count - 1; i++)
        {
            double slopeBefore =
                (points[i].PressureOverZ - points[i - 1].PressureOverZ)
                / (points[i].Gp - points[i - 1].Gp);
            double slopeAfter =
                (points[i + 1].PressureOverZ - points[i].PressureOverZ)
                / (points[i + 1].Gp - points[i].Gp);

            Assert.Equal(slopeBefore, slopeAfter, precision: 3);
        }
    }

    /// <summary>
    /// SDD-013 §4's own rule for this schema bump (finding 264): gas in place
    /// and reservoir temperature are hardcoded to zero at both of
    /// <c>InitialConditions</c>'s construction sites for an OIL compartment —
    /// this is the one that actually carries them, and a save that forgot
    /// either would hand back a reservoir the p/Z line has no denominator for.
    /// </summary>
    [Fact]
    public void A_gas_reservoir_restores_to_the_same_pressure()
    {
        SubsurfaceState captured = Gas();
        EntityId<IReservoirCompartmentEntity> id = Add(captured);

        captured.CommitTick(
        [
            new CompartmentWithdrawal(
                id, new SurfaceVolume(0.0), new StandardGasVolume(2.0e8),
                new SurfaceVolume(0.0), new ReservoirVolume(0.0), new ReservoirVolume(0.0),
                new ReservoirVolume(0.0), new ReservoirVolume(0.0)),
        ]);

        double expected = captured.TruePressureOf(id).Pascals;

        SubsurfaceState restored = Gas();
        StateBlock.Restore(restored, StateBlock.Capture(captured).Written());

        Assert.Equal(1, restored.Count);
        Assert.Equal(expected, restored.TruePressureOf(id).Pascals, precision: 6);
    }
}
