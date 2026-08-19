// SDD-001 §1 — Quantities and units.
// One readonly record struct per dimension, canonical SI magnitude inside,
// factory-per-unit. Cross-dimension arithmetic does not exist; the legal
// products/quotients are declared operators. Field-unit constants live here
// as conversion factors only and never appear in simulation formulas (SDD-003 §2).

namespace OGSim.Kernel;

/// <summary>
/// Runtime tag for the dimensions of SDD-001 §1. The typed structs below are the
/// compile-time story; this is the runtime one, needed wherever a dimension is
/// data rather than code — content binding `"3200 psi"` to a quantity, and a
/// property kind declaring what it measures (SDD-002 §2b).
/// </summary>
public enum Dimension
{
    Dimensionless,
    Length, Area, Mass, Duration, Pressure, Temperature, TemperatureDelta,
    MassRate, Power, Energy, Permeability, Viscosity, Density, HeatingValue,
    ReservoirVolume, SurfaceVolume, StandardGasVolume,
    ReservoirRate, SurfaceRate, StandardGasRate,
    Money,
}

/// <summary>Pressure, canonical pascals. SDD-001 §1 exemplar — the full pinned surface.</summary>
public readonly record struct Pressure(double Pascals) : IComparable<Pressure>
{
    public static Pressure FromPsi(double psi) => new(psi * 6894.757293168);
    public static Pressure FromBar(double bar) => new(bar * 1e5);
    public static Pressure FromKPa(double kPa) => new(kPa * 1e3);
    public double ToPsi() => Pascals / 6894.757293168;
    public double ToBar() => Pascals / 1e5;

    public static Pressure operator +(Pressure a, Pressure b) => new(a.Pascals + b.Pascals);
    public static Pressure operator -(Pressure a, Pressure b) => new(a.Pascals - b.Pascals);
    /// <summary>Ratio of pressures is dimensionless — the only legal division.</summary>
    public static double operator /(Pressure a, Pressure b) => a.Pascals / b.Pascals;
    public static bool operator >(Pressure a, Pressure b) => a.Pascals > b.Pascals;
    public static bool operator <(Pressure a, Pressure b) => a.Pascals < b.Pascals;
    // no operator *(Pressure, Pressure) — inexpressible by omission (SDD-001 §1)

    public int CompareTo(Pressure other) => Pascals.CompareTo(other.Pascals);
}

/// <summary>Temperature, canonical kelvin. Nonlinear display scales (°C, °F) are conversions.</summary>
public readonly record struct Temperature(double Kelvin) : IComparable<Temperature>
{
    public static Temperature FromCelsius(double c) => new(c + 273.15);
    public double ToCelsius() => Kelvin - 273.15;
    public static Temperature operator -(Temperature a, TemperatureDelta d) => new(a.Kelvin - d.Kelvin);
    public static Temperature operator +(Temperature a, TemperatureDelta d) => new(a.Kelvin + d.Kelvin);
    public static TemperatureDelta operator -(Temperature a, Temperature b) => new(a.Kelvin - b.Kelvin);
    public static bool operator >(Temperature a, Temperature b) => a.Kelvin > b.Kelvin;
    public static bool operator <(Temperature a, Temperature b) => a.Kelvin < b.Kelvin;
    public int CompareTo(Temperature other) => Kelvin.CompareTo(other.Kelvin);
}

/// <summary>A temperature difference — distinct from an absolute temperature.</summary>
public readonly record struct TemperatureDelta(double Kelvin);

/// <summary>Length, canonical metres.</summary>
public readonly record struct Length(double Metres) : IComparable<Length>
{
    public static Length FromFeet(double ft) => new(ft * 0.3048);
    public double ToFeet() => Metres / 0.3048;
    public static Length operator +(Length a, Length b) => new(a.Metres + b.Metres);
    public static Length operator -(Length a, Length b) => new(a.Metres - b.Metres);
    public static double operator /(Length a, Length b) => a.Metres / b.Metres;
    /// <summary>The declared product (SDD-001 §1, R1.0 amendment): length × length is area.</summary>
    public static Area operator *(Length a, Length b) => new(a.Metres * b.Metres);
    public static bool operator >(Length a, Length b) => a.Metres > b.Metres;
    public static bool operator <(Length a, Length b) => a.Metres < b.Metres;
    public int CompareTo(Length other) => Metres.CompareTo(other.Metres);
}

/// <summary>
/// Area, canonical square metres. Added by the R1.0 review: §1.4's Polygon.Area
/// consumed it while no dimension list or file declared it. Licence blocks,
/// facility footprints and drainage areas all speak it.
/// </summary>
public readonly record struct Area(double SquareMetres) : IComparable<Area>
{
    public static Area FromSquareKilometres(double km2) => new(km2 * 1e6);
    public static Area FromHectares(double ha) => new(ha * 1e4);
    public static Area FromAcres(double acres) => new(acres * 4046.8564224);
    public double ToSquareKilometres() => SquareMetres / 1e6;
    public double ToAcres() => SquareMetres / 4046.8564224;

    public static Area operator +(Area a, Area b) => new(a.SquareMetres + b.SquareMetres);
    public static Area operator -(Area a, Area b) => new(a.SquareMetres - b.SquareMetres);
    public static double operator /(Area a, Area b) => a.SquareMetres / b.SquareMetres;
    /// <summary>The declared quotient: area over a length is a length.</summary>
    public static Length operator /(Area a, Length l) => new(a.SquareMetres / l.Metres);
    public static bool operator >(Area a, Area b) => a.SquareMetres > b.SquareMetres;
    public static bool operator <(Area a, Area b) => a.SquareMetres < b.SquareMetres;
    public int CompareTo(Area other) => SquareMetres.CompareTo(other.SquareMetres);
}

/// <summary>Mass, canonical kilograms. What the engine conserves (04 §2.1).</summary>
public readonly record struct Mass(double Kilograms) : IComparable<Mass>
{
    public static Mass operator +(Mass a, Mass b) => new(a.Kilograms + b.Kilograms);
    public static Mass operator -(Mass a, Mass b) => new(a.Kilograms - b.Kilograms);
    public static double operator /(Mass a, Mass b) => a.Kilograms / b.Kilograms;
    public static bool operator >(Mass a, Mass b) => a.Kilograms > b.Kilograms;
    public static bool operator <(Mass a, Mass b) => a.Kilograms < b.Kilograms;
    public int CompareTo(Mass other) => Kilograms.CompareTo(other.Kilograms);
}

/// <summary>Mass flow rate, canonical kg/s.</summary>
public readonly record struct MassRate(double KgPerSecond) : IComparable<MassRate>
{
    public static MassRate operator +(MassRate a, MassRate b) => new(a.KgPerSecond + b.KgPerSecond);
    public static MassRate operator -(MassRate a, MassRate b) => new(a.KgPerSecond - b.KgPerSecond);
    /// <summary>Rate × time = mass — one of the declared legal products.</summary>
    public static Mass operator *(MassRate r, Duration d) => new(r.KgPerSecond * d.Seconds);
    public static bool operator >(MassRate a, MassRate b) => a.KgPerSecond > b.KgPerSecond;
    public static bool operator <(MassRate a, MassRate b) => a.KgPerSecond < b.KgPerSecond;
    public int CompareTo(MassRate other) => KgPerSecond.CompareTo(other.KgPerSecond);
}

/// <summary>
/// Simulation duration. SDD-001 §3: the 30/360 convention — every month is 30
/// days, so a tick is always exactly 30 days and the /30ths grid is uniform.
/// </summary>
public readonly record struct Duration(double Days)
{
    public const double DaysPerTick = 30.0; // 30/360, pinned (SDD-001 §3)
    public double Seconds => Days * 86_400.0;
    public static Duration FromTicks(double ticks) => new(ticks * DaysPerTick);
    public static Duration operator +(Duration a, Duration b) => new(a.Days + b.Days);
}

/// <summary>Power, canonical watts.</summary>
public readonly record struct Power(double Watts) : IComparable<Power>
{
    public static Power operator +(Power a, Power b) => new(a.Watts + b.Watts);
    public static Power operator -(Power a, Power b) => new(a.Watts - b.Watts);
    public static bool operator >(Power a, Power b) => a.Watts > b.Watts;
    public static bool operator <(Power a, Power b) => a.Watts < b.Watts;
    public int CompareTo(Power other) => Watts.CompareTo(other.Watts);
}

/// <summary>Energy, canonical joules.</summary>
public readonly record struct Energy(double Joules);

/// <summary>Dynamic viscosity, canonical Pa·s. Display cP = mPa·s.</summary>
public readonly record struct Viscosity(double PascalSeconds)
{
    public static Viscosity FromCentipoise(double cP) => new(cP * 1e-3);
}

/// <summary>Permeability, canonical m². Display millidarcy.</summary>
public readonly record struct Permeability(double SquareMetres)
{
    public static Permeability FromMillidarcy(double mD) => new(mD * 9.869233e-16);
}

/// <summary>Density, canonical kg/m³.</summary>
public readonly record struct Density(double KgPerCubicMetre)
{
    /// <summary>Specific gravity relative to water at standard conditions.</summary>
    public double SpecificGravity() => KgPerCubicMetre / PhysicalConstants.WaterDensityKgPerM3;
    public static Density FromSpecificGravity(double sg) => new(sg * PhysicalConstants.WaterDensityKgPerM3);
}

/// <summary>Heating value, canonical J/kg.</summary>
public readonly record struct HeatingValue(double JoulesPerKg);

/// <summary>
/// API gravity — a NONLINEAR transform of density (SDD-001 §1.2).
/// Deliberately: no operator +, no Average(). You average densities.
/// </summary>
public readonly record struct ApiGravity(double Degrees)
{
    public static ApiGravity FromDensity(Density d) => new(141.5 / d.SpecificGravity() - 131.5);
    public Density ToDensity() => Density.FromSpecificGravity(141.5 / (Degrees + 131.5));
}

/// <summary>
/// SDD-000 §8 rule F-2: every numeric constant in simulation code lives here,
/// with its SDD citation and unit. Simulation assemblies may use no other
/// numeric literals except 0 and 1 (analyzer-enforced from R1.12).
/// </summary>
public static class PhysicalConstants
{
    /// <summary>Reference water density for SG, kg/m³. SDD-001 §1.</summary>
    public const double WaterDensityKgPerM3 = 1000.0;

    /// <summary>
    /// Days in a year on the 30/360 calendar — twelve months of exactly
    /// <see cref="Duration.DaysPerTick"/> (SDD-001 §3).
    ///
    /// <para>Here rather than at a call site because a per-year rate applied over
    /// a tick needs the ratio, and a rate divided by a hand-written 360 is the
    /// same constant written twice: the day the calendar changes, one of them
    /// moves. Real years have 365 days and this engine's do not, deliberately —
    /// the /30ths segment grid is exact only because every month is the same
    /// length.</para>
    /// </summary>
    public const double DaysPerYear = 360.0;

    /// <summary>Standard gravity, m/s². SDD-003 §6.2 (hydrostatic term).</summary>
    public const double GravityMPerS2 = 9.80665;

    /// <summary>Universal gas constant, J/(mol·K). SDD-006 §3, §6.</summary>
    public const double GasConstantJPerMolK = 8.31446261815324;

    /// <summary>z for P10/P90 of a normal distribution. SDD-008 §2.</summary>
    public const double NormalZ10 = 1.281552;

    /// <summary>Default choke critical pressure ratio, dimensionless. SDD-003 §6.3 (content-overridable).</summary>
    public const double DefaultChokeCriticalRatio = 0.55;

    /// <summary>π. Named here rather than written as a literal so F-2's scan
    /// stays absolute: SDD-003 §6.1's radial Darcy form is stated as 2πkh, and
    /// a formula reading <c>3.14159...</c> would be unreviewable against it.</summary>
    public const double Pi = Math.PI;

    /// <summary>2π — the radial-flow coefficient of SDD-003 §6.1, as the form states it.</summary>
    public const double TwoPi = 2.0 * Math.PI;

    // ------------------------------------------------- standard conditions
    //
    // SDD-003 §3.1 states p_sc and T_sc in its prose and nothing carried them. A
    // gas specific gravity is relative to AIR (γg, air = 1), so SDD-003 §6.1b's
    // ρ_sc,gas = γg · ρ_air,sc needs an air density — and without one it could
    // only be written as a literal, which is the rule this class exists to
    // enforce.

    /// <summary>p_sc, Pa. SDD-003 §3.1.</summary>
    public const double StandardPressurePa = 101_325.0;

    /// <summary>T_sc, K — 60 °F, the industry's standard. SDD-003 §3.1.</summary>
    public const double StandardTemperatureK = 288.706;

    /// <summary>Dry air, kg/mol. SDD-001 §1.</summary>
    public const double AirMolarMassKgPerMol = 0.0289647;

    /// <summary>
    /// ρ_air at standard conditions, kg/m³ — what a gas specific gravity is
    /// measured against (SDD-001 §1).
    ///
    /// <para>DERIVED as <c>pM/RT</c> rather than stated as ≈1.2255, so the number
    /// is checkable against the three constants above. A constant nobody can
    /// re-derive is a literal with a comment on it.</para>
    /// </summary>
    public const double AirDensityAtStandardKgPerM3 =
        StandardPressurePa * AirMolarMassKgPerMol
        / (GasConstantJPerMolK * StandardTemperatureK);
}
