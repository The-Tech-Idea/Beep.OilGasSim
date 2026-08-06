// R2.7 — the black-oil model (SDD-003 §4.1, design 05 §2).
//
// WHAT THESE TESTS ARE. Rule F-3 asks for pinning against reference values from
// the published papers. A value recomputed from the same formula the code
// implements is NOT independent — it would only prove arithmetic is repeatable.
// So the assertions below are physical invariants, continuity and round-trips,
// every one of which holds independently of whether a coefficient was
// transcribed correctly... EXCEPT that a transcription error preserving
// monotonicity would survive them. That gap is open item S003-4 and is closed at
// R5 by someone transcribing a worked example with the paper in hand.
//
// R2-V6 (bubble-point behaviour) and R2-V7 (Bo round-trip) are the named suites.

using OGSim.Contracts;
using OGSim.Kernel;
using Xunit;

namespace OGSim.Tests;

public class BlackOilModelTests
{
    private static readonly ValidityRange WideOpen = new(
        Pressure.FromPsi(14.7), Pressure.FromPsi(10_000.0),
        Temperature.FromCelsius(0.0), Temperature.FromCelsius(200.0));

    /// <summary>A 35 °API oil at 200 °F with 100 sm³/sm³ solution gas.</summary>
    private static BlackOilModel Fluid(double api = 35.0, double rsb = 100.0) =>
        new(new BlackOilInputs(
                new ApiGravity(api),
                GasSpecificGravity: 0.7,
                ReservoirTemperature: Temperature.FromCelsius(93.33),   // 200 °F
                SolutionGorAtBubblePoint: rsb,
                Form: FluidForm.BlackOil),
            WideOpen);

    // ------------------------------------------------------------- R2-V6

    [Fact] // Rs is CONSTANT above Pb and falls below — the undersaturated plateau
    public void R2V6_solution_gas_is_flat_above_the_bubble_point_and_falls_below()
    {
        BlackOilModel fluid = Fluid();
        Pressure pb = fluid.Pb;

        // FLAT is the claim, not "equal to the declared Rsb" — the plateau is
        // anchored to Rs(Pb) by the forward form so the property is continuous
        // (see the round-trip test for why those differ by ~1e-4).
        double plateau = fluid.Rs(pb);
        Assert.Equal(plateau, fluid.Rs(new Pressure(pb.Pascals * 1.5)), 12);
        Assert.Equal(plateau, fluid.Rs(new Pressure(pb.Pascals * 3.0)), 12);
        Assert.InRange(plateau, 99.9, 100.0);

        double justBelow = fluid.Rs(new Pressure(pb.Pascals * 0.9));
        double wellBelow = fluid.Rs(new Pressure(pb.Pascals * 0.4));
        Assert.True(justBelow < plateau, "Rs must fall as gas comes out of solution");
        Assert.True(wellBelow < justBelow, "and keep falling");
    }

    [Fact] // R2-V6: Bo PEAKS at Pb — rising with Rs below, compressed above
    public void R2V6_oil_formation_volume_factor_peaks_at_the_bubble_point()
    {
        BlackOilModel fluid = Fluid();
        double pb = fluid.Pb.Pascals;

        double atBubblePoint = fluid.Bo(new Pressure(pb)).RbPerStb;
        double below = fluid.Bo(new Pressure(pb * 0.5)).RbPerStb;
        double above = fluid.Bo(new Pressure(pb * 2.0)).RbPerStb;

        Assert.True(atBubblePoint > below, "Bo rises with Rs up to Pb");
        Assert.True(atBubblePoint > above, "and falls above Pb as the oil is compressed");

        // A reservoir barrel is always at least a stock-tank barrel.
        Assert.True(below >= 1.0 && atBubblePoint >= 1.0 && above >= 1.0);
    }

    [Fact] // R2-V6: oil viscosity RISES below Pb as the light ends leave
    public void R2V6_oil_viscosity_rises_below_the_bubble_point()
    {
        BlackOilModel fluid = Fluid();
        double pb = fluid.Pb.Pascals;

        double atBubblePoint = fluid.MuOil(new Pressure(pb)).PascalSeconds;
        double wellBelow = fluid.MuOil(new Pressure(pb * 0.35)).PascalSeconds;
        double above = fluid.MuOil(new Pressure(pb * 2.0)).PascalSeconds;

        Assert.True(wellBelow > atBubblePoint,
            "losing dissolved gas leaves heavier, more viscous oil behind");
        Assert.True(above > atBubblePoint,
            "and compression above Pb raises it again — Pb is the minimum");
    }

    [Fact] // Every property is continuous across the Pb boundary
    public void R2V6_properties_are_continuous_at_the_bubble_point()
    {
        BlackOilModel fluid = Fluid();
        double pb = fluid.Pb.Pascals;

        var justUnder = new Pressure(pb * (1.0 - 1e-9));
        var justOver = new Pressure(pb * (1.0 + 1e-9));

        Assert.Equal(fluid.Rs(justUnder), fluid.Rs(justOver), 6);
        Assert.Equal(fluid.Bo(justUnder).RbPerStb, fluid.Bo(justOver).RbPerStb, 9);
        Assert.Equal(fluid.MuOil(justUnder).PascalSeconds,
                     fluid.MuOil(justOver).PascalSeconds, 12);
    }

    /// <summary>
    /// Standing's two published forms are only APPROXIMATE inverses: the Pb
    /// form's exponent 0.83 is a rounding of 1/1.2048 = 0.8299468. Round-tripping
    /// Rsb → Pb → Rs therefore lands about 1e-4 low, and that is the paper's
    /// rounding rather than a transcription error.
    ///
    /// The engine anchors its plateau to Rs(Pb) by the forward form, so the
    /// property is CONTINUOUS at Pb even though it does not return the declared
    /// Rsb exactly. This test pins both halves of that: close, and not equal.
    /// </summary>
    [Fact]
    public void R2V6_the_bubble_point_round_trips_to_within_standings_own_rounding()
    {
        foreach (double rsb in new[] { 40.0, 100.0, 180.0 })
        {
            BlackOilModel fluid = Fluid(rsb: rsb);
            double recovered = fluid.Rs(fluid.Pb);

            double relative = Math.Abs(recovered - rsb) / rsb;
            Assert.True(relative < 1e-3, $"Rsb {rsb} recovered as {recovered}");
            Assert.True(relative > 0.0,
                "the two forms are not exact inverses; if this ever passes exactly, " +
                "the plateau has been re-anchored to the declared Rsb and Rs is " +
                "discontinuous at Pb again");
        }
    }

    [Fact] // The correlation's own API discontinuity is at 30, and both sides work
    public void R2V6_both_vazquez_beggs_coefficient_sets_are_reachable()
    {
        BlackOilModel heavy = Fluid(api: 22.0);     // API <= 30 set
        BlackOilModel light = Fluid(api: 42.0);     // API > 30 set

        Assert.True(heavy.Bo(heavy.Pb).RbPerStb >= 1.0);
        Assert.True(light.Bo(light.Pb).RbPerStb >= 1.0);

        // Lighter oil holds more gas in solution at the same Rsb, so it swells more.
        Assert.True(light.Bo(light.Pb).RbPerStb > heavy.Bo(heavy.Pb).RbPerStb);
    }

    // ------------------------------------------------------------- R2-V7

    [Fact] // R2-V7: rb <-> stb through Bo round-trips exactly
    public void R2V7_reservoir_and_surface_volumes_round_trip_through_Bo()
    {
        BlackOilModel fluid = Fluid();
        FormationVolumeFactor bo = fluid.Bo(fluid.Pb);

        var reservoir = new ReservoirVolume(1000.0);
        SurfaceVolume surface = bo.Shrink(reservoir);

        Assert.True(surface.CubicMetres < reservoir.CubicMetres, "oil shrinks at surface");
        Assert.Equal(reservoir.CubicMetres, bo.Swell(surface).CubicMetres, 9);
    }

    // ------------------------------------------------------------- gas

    [Fact] // Z -> 1 as pressure -> 0: the ideal-gas limit, independent of DAK's constants
    public void R2V6_z_factor_approaches_unity_at_low_pressure()
    {
        BlackOilModel fluid = Fluid();
        Temperature t = Temperature.FromCelsius(93.33);

        double nearVacuum = fluid.Z(Pressure.FromPsi(1.0), t);
        Assert.Equal(1.0, nearVacuum, 2);

        // And it dips below 1 in the mid-pressure range, which is the whole
        // reason a Z factor exists.
        double mid = fluid.Z(Pressure.FromPsi(2000.0), t);
        Assert.True(mid < 1.0, $"Z at 2000 psia was {mid}; real gas should compress more than ideal");
        Assert.InRange(mid, 0.5, 1.0);
    }

    [Fact] // Bg falls monotonically: gas is enormously compressible (05 §2)
    public void R2V6_gas_formation_volume_factor_falls_monotonically_with_pressure()
    {
        BlackOilModel fluid = Fluid();

        double previous = double.MaxValue;
        for (double psi = 200.0; psi <= 6000.0; psi += 200.0)
        {
            double bg = fluid.Bg(Pressure.FromPsi(psi)).Rm3PerSm3;
            Assert.True(bg < previous, $"Bg rose at {psi} psia");
            Assert.True(bg > 0.0);
            previous = bg;
        }

        // Orders of magnitude across field life, not a few percent.
        double shallow = fluid.Bg(Pressure.FromPsi(200.0)).Rm3PerSm3;
        double deep = fluid.Bg(Pressure.FromPsi(6000.0)).Rm3PerSm3;
        Assert.True(shallow / deep > 20.0);
    }

    [Fact] // Gas viscosity is positive, small, and rises with pressure
    public void R2V6_gas_viscosity_is_physical_and_rises_with_pressure()
    {
        BlackOilModel fluid = Fluid();

        double low = fluid.MuGas(Pressure.FromPsi(500.0)).PascalSeconds;
        double high = fluid.MuGas(Pressure.FromPsi(4000.0)).PascalSeconds;

        Assert.True(low > 0.0 && high > low);
        // Natural gas sits around 0.01-0.03 cp: 1e-5 to 3e-5 Pa.s.
        Assert.InRange(low, 5e-6, 5e-5);
        Assert.InRange(high, 5e-6, 1e-4);
    }

    // ------------------------------------------------------------- faults

    [Fact] // 05 §2: running a condensate under the plain form is a MODEL FAULT
    public void R2V10_a_condensate_fluid_refuses_the_plain_black_oil_form()
    {
        var condensate = new BlackOilModel(
            new BlackOilInputs(new ApiGravity(50.0), 0.75,
                Temperature.FromCelsius(120.0), 250.0, FluidForm.ModifiedBlackOil),
            WideOpen);

        var fault = Assert.Throws<ModelFault>(() => condensate.Rv(Pressure.FromPsi(3000.0)));
        Assert.Equal(FaultClass.Model, fault.Fault.Class);

        // Plain black oil answers 0 without complaint.
        Assert.Equal(0.0, Fluid().Rv(Pressure.FromPsi(3000.0)));
    }

    [Fact] // A fluid whose inputs give no physical bubble point fails at construction
    public void R2V10_a_non_physical_bubble_point_is_a_model_fault()
    {
        Assert.Throws<ModelFault>(() => new BlackOilModel(
            new BlackOilInputs(new ApiGravity(35.0), 0.7,
                Temperature.FromCelsius(93.33),
                SolutionGorAtBubblePoint: 0.0,          // no dissolved gas at all
                Form: FluidForm.BlackOil),
            WideOpen));
    }

    [Fact] // The split reads the catalogue, and refuses to guess without one
    public void R2V1_the_phase_split_is_by_declared_standard_phase()
    {
        BlackOilModel fluid = Fluid();

        // Unbound: faults rather than defaulting (law L2).
        Assert.Throws<ArgumentNullException>(
            () => fluid.SplitAt(Composition.Validated([1.0]), fluid.Pb, Temperature.FromCelsius(90.0)));

        var catalogue = new MaterialCatalogue(
        [
            (new ContentId("gas"), PhaseAtStandardConditions.Gas, (IReadOnlyList<IProperty>)[]),
            (new ContentId("oil"), PhaseAtStandardConditions.Liquid, []),
            (new ContentId("water"), PhaseAtStandardConditions.Aqueous, []),
        ]);
        fluid.BindMaterials(catalogue);

        PhaseSplit split = fluid.SplitAt(
            catalogue.ZeroComposition(), fluid.Pb, Temperature.FromCelsius(90.0));

        Assert.Equal(3, split.Fractions.Count);
        Assert.Equal(1.0, split.Fractions[0].GasFraction);       // gas, ordinal 0
        Assert.Equal(1.0, split.Fractions[1].LiquidFraction);    // oil, ordinal 1
        Assert.Equal(1.0, split.Fractions[2].AqueousFraction);   // water, ordinal 2

        // Binding twice would be a second owner of one fact (L5).
        Assert.Throws<InvariantFault>(() => fluid.BindMaterials(catalogue));
    }
}
