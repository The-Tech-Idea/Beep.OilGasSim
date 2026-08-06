// R1.1 — the quantity set (SDD-001 §1). R1 §1 goal G1: "a physical value cannot
// be misused". The half of that goal which is a COMPILE error has no runtime
// test and cannot have one — Pressure + Length simply does not exist, and the
// R1-V2 compile-failure corpus (a Roslyn-driven negative test) is R1.12's job.
// What is tested here is the half that runs: conversions round-trip, the volume
// families stay apart, and the nonlinear scales convert rather than average.

using OGSim.Kernel;

namespace OGSim.Kernel.Tests;

public class QuantityTests
{
    private const int RoundTripPrecision = 12;   // decimal places; conversions are 2-3 ulp at worst

    // ------------------------------------------------------------- R1-V1 / MX7

    [Fact] // R1-V1: every unit → canonical → unit returns the original
    public void R1V1_pressure_conversions_round_trip()
    {
        Assert.Equal(3200.0, Pressure.FromPsi(3200.0).ToPsi(), RoundTripPrecision);
        Assert.Equal(250.0, Pressure.FromBar(250.0).ToBar(), RoundTripPrecision);
        Assert.Equal(1.0e5, Pressure.FromBar(1.0).Pascals, RoundTripPrecision);
        Assert.Equal(1.0e5, Pressure.FromKPa(100.0).Pascals, RoundTripPrecision);
    }

    [Fact] // R1-V1 across the remaining dimensions that carry field units
    public void R1V1_length_area_and_material_property_conversions_round_trip()
    {
        Assert.Equal(9000.0, Length.FromFeet(9000.0).ToFeet(), RoundTripPrecision);
        Assert.Equal(0.3048, Length.FromFeet(1.0).Metres, RoundTripPrecision);

        Assert.Equal(640.0, Area.FromAcres(640.0).ToAcres(), RoundTripPrecision);
        Assert.Equal(25.0, Area.FromSquareKilometres(25.0).ToSquareKilometres(), RoundTripPrecision);
        Assert.Equal(1.0e4, Area.FromHectares(1.0).SquareMetres, RoundTripPrecision);

        Assert.Equal(1.0e-3, Viscosity.FromCentipoise(1.0).PascalSeconds, RoundTripPrecision);

        // Permeability's canonical magnitude is ~1e-16, so an absolute decimal
        // tolerance would pass on any value at all — compare the ratio instead.
        double millidarcy = Permeability.FromMillidarcy(1.0).SquareMetres;
        Assert.Equal(1.0, millidarcy / 9.869233e-16, RoundTripPrecision);
        Assert.Equal(1.0, Permeability.FromMillidarcy(250.0).SquareMetres / (250.0 * 9.869233e-16),
                     RoundTripPrecision);

        Assert.Equal(25.0, Temperature.FromCelsius(25.0).ToCelsius(), RoundTripPrecision);
        Assert.Equal(273.15, Temperature.FromCelsius(0.0).Kelvin, RoundTripPrecision);
    }

    [Fact] // A temperature difference is its own type: 20 °C + 20 °C is meaningless
    public void R1V1_temperature_delta_is_distinct_from_temperature()
    {
        var reservoir = Temperature.FromCelsius(90.0);
        var ambient = Temperature.FromCelsius(15.0);
        TemperatureDelta drop = reservoir - ambient;
        Assert.Equal(75.0, drop.Kelvin, RoundTripPrecision);
        Assert.Equal(reservoir.Kelvin, (ambient + drop).Kelvin, RoundTripPrecision);
    }

    // ------------------------------------------------------------- R1-V3

    [Fact] // R1-V3: the two volume families convert only through an FVF, and it round-trips
    public void R1V3_oil_volume_conversion_requires_the_fvf()
    {
        var bo = new FormationVolumeFactor(1.25);
        var reservoir = new ReservoirVolume(1000.0);

        SurfaceVolume surface = bo.Shrink(reservoir);
        Assert.Equal(800.0, surface.CubicMetres, RoundTripPrecision);
        Assert.Equal(reservoir.CubicMetres, bo.Swell(surface).CubicMetres, RoundTripPrecision);

        // ReservoirVolume + SurfaceVolume does not compile. That is the point of
        // the family split and it is asserted by the R1-V2 corpus, not here.
    }

    [Fact] // R1-V3: gas bridges to its OWN family — the finding-77 bug, pinned
    public void R1V3_gas_volume_conversion_uses_the_gas_bridge()
    {
        var bg = new GasFormationVolumeFactor(0.005);
        var reservoir = new ReservoirVolume(1000.0);

        StandardGasVolume standard = bg.Shrink(reservoir);   // typed: NOT SurfaceVolume
        Assert.Equal(200_000.0, standard.CubicMetres, RoundTripPrecision);
        Assert.Equal(reservoir.CubicMetres, bg.Swell(standard).CubicMetres, RoundTripPrecision);
    }

    [Fact] // R1.1: each volume condition has its own rate, and rate × time is its own volume
    public void R1V3_each_volume_condition_has_its_own_rate()
    {
        var oneDay = new Duration(1.0);
        Assert.Equal(86_400.0, oneDay.Seconds, RoundTripPrecision);

        ReservoirVolume reservoir = new ReservoirRate(2.0) * oneDay;
        SurfaceVolume surface = new SurfaceRate(2.0) * oneDay;
        StandardGasVolume gas = new StandardGasRate(2.0) * oneDay;

        Assert.Equal(172_800.0, reservoir.CubicMetres, RoundTripPrecision);
        Assert.Equal(172_800.0, surface.CubicMetres, RoundTripPrecision);
        Assert.Equal(172_800.0, gas.CubicMetres, RoundTripPrecision);
        // The three results are numerically equal and mutually unassignable.
    }

    [Fact] // The 30/360 tick: SDD-001 §3 makes this exactly 30 days, always
    public void R1V3_mass_rate_times_a_tick_is_a_mass()
    {
        Mass produced = new MassRate(1.5) * Duration.FromTicks(1.0);
        Assert.Equal(30.0, Duration.DaysPerTick, RoundTripPrecision);
        Assert.Equal(1.5 * 30.0 * 86_400.0, produced.Kilograms, RoundTripPrecision);
    }

    // ------------------------------------------------------------- R1-V4

    [Fact] // R1-V4: API gravity is a conversion of density and round-trips
    public void R1V4_api_gravity_round_trips_through_density()
    {
        foreach (double degrees in new[] { 10.0, 22.3, 35.0, 45.5 })
        {
            var api = new ApiGravity(degrees);
            Assert.Equal(degrees, ApiGravity.FromDensity(api.ToDensity()).Degrees, RoundTripPrecision);
        }

        // The reference point the scale is defined by: 10 °API is water.
        Assert.Equal(1000.0, new ApiGravity(10.0).ToDensity().KgPerCubicMetre, 9);
    }

    [Fact] // R1-V4: averaging API is not offered, because it is not meaningful
    public void R1V4_api_gravity_offers_no_arithmetic()
    {
        var light = new ApiGravity(45.0);
        var heavy = new ApiGravity(15.0);

        // ApiGravity + ApiGravity does not compile: you average the DENSITIES,
        // and the two answers genuinely differ.
        Density averagedDensity = new((light.ToDensity().KgPerCubicMetre
                                     + heavy.ToDensity().KgPerCubicMetre) * 0.5);
        double throughDensity = ApiGravity.FromDensity(averagedDensity).Degrees;
        double naiveMean = (light.Degrees + heavy.Degrees) * 0.5;

        Assert.NotEqual(naiveMean, throughDensity, 6);
    }

    // ------------------------------------------------------------- declared operators

    [Fact] // SDD-001 §1: legal products exist, illegal ones are absent
    public void R1V1_declared_products_and_quotients_carry_the_right_dimension()
    {
        Area block = new Length(2000.0) * new Length(3000.0);
        Assert.Equal(6.0e6, block.SquareMetres, RoundTripPrecision);

        Length side = block / new Length(2000.0);
        Assert.Equal(3000.0, side.Metres, RoundTripPrecision);

        // Ratios of like quantities are dimensionless doubles.
        double drawdownRatio = Pressure.FromPsi(500.0) / Pressure.FromPsi(2000.0);
        Assert.Equal(0.25, drawdownRatio, RoundTripPrecision);
    }

    [Fact] // Density ↔ specific gravity is the other nonlinear-adjacent conversion
    public void R1V1_density_specific_gravity_round_trips()
    {
        var density = Density.FromSpecificGravity(0.85);
        Assert.Equal(850.0, density.KgPerCubicMetre, RoundTripPrecision);
        Assert.Equal(0.85, density.SpecificGravity(), RoundTripPrecision);
    }
}
