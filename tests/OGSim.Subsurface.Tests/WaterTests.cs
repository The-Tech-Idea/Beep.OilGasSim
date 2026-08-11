// R10's subsurface half (SDD-003 §3.1c, R10 §4).

using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Subsurface;

namespace OGSim.Subsurface.Tests;

public class WaterCutTests
{
    private static RelativePermeabilityCurve Curve(
        double swc = 0.20, double sor = 0.25,
        double krwMax = 0.30, double kroMax = 0.90,
        double nw = 3.0, double no = 2.0) =>
        RelativePermeabilityCurve.Validated(swc, sor, krwMax, kroMax, nw, no);

    private static readonly Viscosity Water = new(0.5e-3);
    private static readonly Viscosity Oil = new(2.0e-3);

    private static double Cut(double sw, Viscosity? oil = null) =>
        FractionalFlow.WaterCut(Curve(), sw, Water, oil ?? Oil);

    // ------------------------------------------------------------ SC4 / R10-V1

    [Fact] // Before breakthrough NO water flows — and that is what breakthrough IS
    public void SC4_below_connate_saturation_the_water_cut_is_exactly_zero()
    {
        Assert.Equal(0.0, Cut(0.20), precision: 12);
        Assert.Equal(0.0, Cut(0.10), precision: 12);

        // Not a guard bolted on: krw is a power of a normalised saturation that
        // is zero here, so the fractional-flow expression is 1/∞ on its own.
        Assert.Equal(0.0, Curve().WaterPermeability(0.20), precision: 12);
    }

    [Fact] // R10-V1 / SC4: an S-CURVE — rising, and steepest in the middle
    public void SC4_the_water_cut_follows_an_s_curve_after_breakthrough()
    {
        var saturations = new List<double>();
        var cuts = new List<double>();

        for (double sw = 0.20; sw <= 0.75; sw += 0.01)
        {
            saturations.Add(sw);
            cuts.Add(Cut(sw));
        }

        // Monotone.
        for (int i = 1; i < cuts.Count; i++)
            Assert.True(cuts[i] >= cuts[i - 1], $"water cut fell at Sw {saturations[i]}");

        // Bounded by the ends: dry at breakthrough, effectively pure water at
        // residual oil.
        Assert.Equal(0.0, cuts[0], precision: 12);
        Assert.True(cuts[^1] > 0.95, $"expected near-total water at Sw 0.75, got {cuts[^1]}");

        // AND IT IS AN S, not a ramp: the steepest rise is in the interior,
        // with gentler slopes at both ends. A straight line would pass every
        // check above and be the wrong shape.
        int steepest = 0;
        double best = 0.0;
        for (int i = 1; i < cuts.Count; i++)
        {
            double slope = cuts[i] - cuts[i - 1];
            if (slope > best) { best = slope; steepest = i; }
        }

        Assert.True(steepest > cuts.Count / 8, "the curve is steepest right at the start");
        Assert.True(steepest < cuts.Count * 7 / 8, "the curve is steepest right at the end");
    }

    [Fact] // CAL3's shape responds to VISCOSITY — the reason a sigmoid would not do
    public void SC4_a_more_viscous_oil_waters_out_earlier()
    {
        // At the same saturation, a heavier oil gives a higher water cut: the
        // water is relatively more mobile, so it takes a larger share of the
        // flow. This is the mobility ratio in the denominator, and no fitted
        // sigmoid would have produced it.
        double light = Cut(0.40, new Viscosity(1.0e-3));
        double heavy = Cut(0.40, new Viscosity(50.0e-3));

        Assert.True(heavy > light,
            $"the viscous oil did not water out earlier: {heavy} not above {light}");
    }

    [Fact] // The mobility ratio explains the curve, and is exposed for that reason
    public void SC4_the_mobility_ratio_marks_an_unfavourable_flood()
    {
        double favourable = FractionalFlow.MobilityRatio(Curve(), Water, new Viscosity(0.4e-3));
        double unfavourable = FractionalFlow.MobilityRatio(Curve(), Water, new Viscosity(50.0e-3));

        Assert.True(favourable < 1.0, "a light oil should give a favourable ratio");
        Assert.True(unfavourable > 1.0, "a viscous oil should give an unfavourable one");
    }

    [Fact] // Past residual oil there is nothing left to produce but water
    public void SC4_at_residual_oil_the_cut_is_total()
    {
        Assert.Equal(1.0, Cut(0.75), precision: 12);
        Assert.Equal(1.0, Cut(0.90), precision: 12);
    }

    [Theory] // Rock-type content is refused at the door
    [InlineData(0.6, 0.5, "no movable saturation")]
    [InlineData(0.2, 0.2, "")]
    public void SC4_incoherent_endpoints_are_a_model_fault(double swc, double sor, string expected)
    {
        if (expected.Length == 0)
        {
            // A legitimate pair — the guard must not be over-eager.
            RelativePermeabilityCurve.Validated(swc, sor, 0.3, 0.9, 3.0, 2.0);
            return;
        }

        var fault = Assert.Throws<ModelFault>(
            () => RelativePermeabilityCurve.Validated(swc, sor, 0.3, 0.9, 3.0, 2.0));
        Assert.Contains(expected, fault.Fault.Detail);
    }
}

public class WaterfloodTests
{
    private static IFluidPropertyModel Fluid() => new BlackOilModel(
        new BlackOilInputs(
            new ApiGravity(35.0), 0.75, Temperature.FromCelsius(93.3), 100.0, FluidForm.BlackOil),
        new ValidityRange(new Pressure(500.0), new Pressure(60e6),
                          Temperature.FromCelsius(10.0), Temperature.FromCelsius(180.0)));

    private static MaterialBalanceInput Input(double influxM3 = 0.0, double injectedM3 = 0.0) =>
        new(InitialPressure: new Pressure(30e6),
            OriginalOilInPlace: new SurfaceVolume(1.0e6),
            GasCapRatio: 0.0,
            ConnateWaterSaturation: 0.2,
            WaterCompressibility: 4.4e-10,
            RockCompressibility: 5.8e-10,
            CumulativeOilProduced: new SurfaceVolume(8_000.0),
            CumulativeGasProduced: new StandardGasVolume(2_400_000.0),
            CumulativeWaterProduced: new SurfaceVolume(500.0),
            CumulativeWaterInflux: new ReservoirVolume(influxM3),
            CumulativeInjected: new ReservoirVolume(injectedM3),
            StartPressure: new Pressure(30e6),
            WithdrawnThisTick: new ReservoirVolume(0.0));

    [Fact] // R10 §2.3: waterflood is an ADDITION — a distinct, named mechanism
    public void R10V6_the_waterflood_is_its_own_mechanism()
    {
        var flood = new WaterfloodDrive();

        Assert.Equal("waterflood-drive", flood.Id.Value);
        Assert.True(flood.Admits.AquiferInflux);
        Assert.False(flood.Admits.GasCap);
    }

    [Fact] // It accepts water, named by CONTENT ID and not branched on
    public void R10V6_the_waterflood_declares_water_as_its_injectant()
    {
        // Through the INTERFACE, which is how every caller holds it — a derived
        // class hiding the base property with `new` would pass a test written
        // against the concrete type and fail in the engine.
        IDriveMechanism flood = new WaterfloodDrive();

        ContentId injectant = Assert.Single(flood.AcceptedInjectants);
        Assert.Equal("water", injectant.Value);

        // And the natural drives still accept none.
        Assert.Empty(((IDriveMechanism)new SolutionGasDrive()).AcceptedInjectants);
    }

    [Fact] // R10-V5: injected water reaches the compartment and slows the decline
    public void R10V5_injection_supports_the_pressure()
    {
        IFluidPropertyModel fluid = Fluid();
        IDriveMechanism flood = new WaterfloodDrive();

        double unsupported = flood.SolveEndPressure(Input(), fluid).Pascals;
        double flooded = flood.SolveEndPressure(Input(injectedM3: 9_000.0), fluid).Pascals;

        Assert.True(flooded > unsupported,
            $"injection did not support pressure: {flooded} not above {unsupported}");
    }

    [Fact] // R10-V6: the supported case recovers materially more
    public void R10V6_a_flood_recovers_more_than_the_unsupported_case()
    {
        IFluidPropertyModel fluid = Fluid();

        // How much oil can be produced before the compartment falls to an
        // abandonment pressure — with and without voidage replacement.
        double natural = RecoveryAt(fluid, injectedFraction: 0.0);
        double flooded = RecoveryAt(fluid, injectedFraction: 1.0);

        Assert.True(flooded > natural,
            $"the flood recovered {flooded}, not more than {natural}");

        // Materially more, not marginally: R10-V6 asks for 10–25 points, and
        // the band itself is calibration (CAL) rather than a unit test's business.
        Assert.True(flooded > natural * 1.5,
            $"the flood's gain was not material: {flooded} against {natural}");
    }

    /// <summary>Bisection on cumulative oil for the production that lands the
    /// compartment at an abandonment pressure, with injection replacing a
    /// declared fraction of the voidage.</summary>
    private static double RecoveryAt(IFluidPropertyModel fluid, double injectedFraction)
    {
        var at = new Pressure(12.0e6);
        double low = 0.0, high = 400_000.0;

        for (int i = 0; i < 200; i++)
        {
            double mid = low + (high - low) * 0.5;

            MaterialBalanceInput input = Input() with
            {
                CumulativeOilProduced = new SurfaceVolume(mid),
                CumulativeGasProduced = new StandardGasVolume(mid * 300.0),
                CumulativeInjected = new ReservoirVolume(mid * 1.2 * injectedFraction),
            };

            if (MaterialBalance.Residual(input, fluid, at) > 0.0) low = mid;
            else high = mid;
        }

        return low / 1.0e6;
    }
}

/// <summary>
/// R20d.4 — the compartment waters out (SDD-003 §3.1c, §3.3).
///
/// <para>The truth half of the late-life game. Fractional flow and the aquifer
/// were both built, tested and called by nothing; these assert them working
/// together on a real compartment, which is where breakthrough actually
/// happens.</para>
/// </summary>
public class BreakthroughTests
{
    private static readonly Viscosity Water = new(0.5e-3);
    private static readonly Viscosity Oil = new(2.0e-3);

    private static SubsurfaceState WaterDriven()
    {
        var state = new SubsurfaceState(
            new BlackOilModel(
                new BlackOilInputs(
                    new ApiGravity(35.0), 0.75, Temperature.FromCelsius(93.3), 100.0,
                    FluidForm.BlackOil),
                new ValidityRange(
                    new Pressure(500.0), new Pressure(60e6),
                    Temperature.FromCelsius(10.0), Temperature.FromCelsius(180.0))),
            new WaterDrive(),
            maxTickPressureDropFraction: 0.4);

        return state;
    }

    private static EntityId<IReservoirCompartmentEntity> Add(SubsurfaceState state) =>
        state.Create(
            new GeneratedCompartment(
                PoreVolume: new ReservoirVolume(1.0e6),
                Porosity: 0.22,
                OilSaturation: 0.7,
                InitialPressure: new Pressure(30.0e6),
                Temperature: Temperature.FromCelsius(93.3),
                Depth: new Length(2000.0)),
            permeability: new Permeability(1.0e-13),
            netThickness: new Length(20.0),
            drainageArea: new Area(2.0e5),
            rockCompressibility: 4.5e-10,
            gasOilContact: new Length(1900.0),
            oilWaterContact: new Length(2100.0),
            RelativePermeabilityCurve.Validated(
                swc: 0.30, sor: 0.25, krwMax: 0.35, kroMax: 0.90, nw: 3.0, no: 2.0),
            new ContentId("water-drive"),

            // NO AQUIFER. These tests supply influx directly as a withdrawal
            // term, which is what stage 6 does — a compartment that also made
            // its own would be counted twice (SDD-003 §3.3a).
            aquiferStrength: 0.0,
            Duration.FromTicks(1.0));

    /// <summary>
    /// BREAKTHROUGH IS NOT SCHEDULED. It is the first tick the saturation
    /// exceeds connate — and before that the water cut is exactly zero because
    /// krw is, not because anything checked a date.
    /// </summary>
    [Fact]
    public void R10V1_a_compartment_at_connate_saturation_produces_no_water()
    {
        SubsurfaceState state = WaterDriven();
        EntityId<IReservoirCompartmentEntity> id = Add(state);

        Assert.Equal(0.0, state.TrueWaterCutOf(id, Water, Oil), precision: 12);
    }

    /// <summary>
    /// Aquifer influx raises the saturation, the saturation raises the water
    /// cut, and the field waters out. Three mechanisms that existed separately
    /// and had never met.
    ///
    /// <para>Influx is paired with production, because that is what a water
    /// drive IS: the aquifer replaces much of the voidage, so the compartment
    /// falls in pressure slowly and fills with water while it does. Influx that
    /// MATCHED the voidage would hold the pressure at its initial value and any
    /// excess would need a pressure above it — which the material balance
    /// refuses outright rather than solving for a pressure it cannot reach, and
    /// which is the honest answer: a field cannot be repressurised above
    /// discovery by its own aquifer.</para>
    /// </summary>
    [Fact]
    public void R10V1_aquifer_influx_waters_the_compartment_out()
    {
        SubsurfaceState state = WaterDriven();
        EntityId<IReservoirCompartmentEntity> id = Add(state);

        double before = state.TrueWaterCutOf(id, Water, Oil);

        // Twenty years of a well-supported field: most of what is taken is
        // replaced, and what replaces it is water.
        for (var month = 0; month < 240; month++)
            state.CommitTick(
            [
                new CompartmentWithdrawal(
                    id,
                    new SurfaceVolume(1_000.0),
                    new StandardGasVolume(0.0),
                    new SurfaceVolume(0.0),
                    Influx: new ReservoirVolume(600.0),
                    Injected: new ReservoirVolume(0.0),
                    ReservoirVolume: new ReservoirVolume(1_200.0)),
            ]);

        double after = state.TrueWaterCutOf(id, Water, Oil);

        Assert.Equal(0.0, before, precision: 12);
        Assert.True(after > 0.0,
            $"water that arrived from the aquifer must reach the producer ({after}), or a " +
            "water drive is pressure support with no consequence");
    }

    /// <summary>
    /// A field with NO aquifer never waters out, however long it produces. The
    /// water has to come from somewhere, and a volumetric depletion reservoir
    /// with connate water only has nowhere for it to come from.
    /// </summary>
    [Fact]
    public void R10V1_without_influx_a_field_never_waters_out()
    {
        SubsurfaceState state = WaterDriven();
        EntityId<IReservoirCompartmentEntity> id = Add(state);

        for (var month = 0; month < 240; month++)
            state.CommitTick(
            [
                new CompartmentWithdrawal(
                    id,
                    new SurfaceVolume(100.0),
                    new StandardGasVolume(0.0),
                    new SurfaceVolume(0.0),
                    Influx: new ReservoirVolume(0.0),
                    Injected: new ReservoirVolume(0.0),
                    ReservoirVolume: new ReservoirVolume(120.0)),
            ]);

        Assert.Equal(0.0, state.TrueWaterCutOf(id, Water, Oil), precision: 12);
    }

}
