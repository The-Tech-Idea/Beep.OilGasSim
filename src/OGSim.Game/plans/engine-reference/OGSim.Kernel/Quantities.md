# Quantities

Source: `src\OGSim.Kernel\Quantities.cs` · Lines: 238

## File intent

> SDD-001 §1 — Quantities and units.
> One readonly record struct per dimension, canonical SI magnitude inside,
> factory-per-unit. Cross-dimension arithmetic does not exist; the legal
> products/quotients are declared operators. Field-unit constants live here
> as conversion factors only and never appear in simulation formulas (SDD-003 §2).
> <summary>
> Runtime tag for the dimensions of SDD-001 §1. The typed structs below are the
> compile-time story; this is the runtime one, needed wherever a dimension is

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L15` `public enum Dimension`
- `L26` `public readonly record struct Pressure(double Pascals) : IComparable<Pressure>`
- `L46` `public readonly record struct Temperature(double Kelvin) : IComparable<Temperature>`
- `L59` `public readonly record struct TemperatureDelta(double Kelvin);`
- `L62` `public readonly record struct Length(double Metres) : IComparable<Length>`
- `L81` `public readonly record struct Area(double SquareMetres) : IComparable<Area>`
- `L100` `public readonly record struct Mass(double Kilograms) : IComparable<Mass>`
- `L111` `public readonly record struct MassRate(double KgPerSecond) : IComparable<MassRate>`
- `L126` `public readonly record struct Duration(double Days)`
- `L135` `public readonly record struct Power(double Watts) : IComparable<Power>`
- `L145` `public readonly record struct Energy(double Joules);`
- `L148` `public readonly record struct Viscosity(double PascalSeconds)`
- `L154` `public readonly record struct Permeability(double SquareMetres)`
- `L160` `public readonly record struct Density(double KgPerCubicMetre)`
- `L168` `public readonly record struct HeatingValue(double JoulesPerKg);`
- `L174` `public readonly record struct ApiGravity(double Degrees)`
- `L185` `public static class PhysicalConstants`

## Accessible members

- `L28` `public static Pressure FromPsi(double psi) => new(psi * 6894.757293168);`
- `L29` `public static Pressure FromBar(double bar) => new(bar * 1e5);`
- `L30` `public static Pressure FromKPa(double kPa) => new(kPa * 1e3);`
- `L31` `public double ToPsi() => Pascals / 6894.757293168;`
- `L32` `public double ToBar() => Pascals / 1e5;`
- `L34` `public static Pressure operator +(Pressure a, Pressure b) => new(a.Pascals + b.Pascals);`
- `L35` `public static Pressure operator -(Pressure a, Pressure b) => new(a.Pascals - b.Pascals);`
- `L37` `public static double operator /(Pressure a, Pressure b) => a.Pascals / b.Pascals;`
- `L38` `public static bool operator >(Pressure a, Pressure b) => a.Pascals > b.Pascals;`
- `L39` `public static bool operator <(Pressure a, Pressure b) => a.Pascals < b.Pascals;`
- `L42` `public int CompareTo(Pressure other) => Pascals.CompareTo(other.Pascals);`
- `L48` `public static Temperature FromCelsius(double c) => new(c + 273.15);`
- `L49` `public double ToCelsius() => Kelvin - 273.15;`
- `L50` `public static Temperature operator -(Temperature a, TemperatureDelta d) => new(a.Kelvin - d.Kelvin);`
- `L51` `public static Temperature operator +(Temperature a, TemperatureDelta d) => new(a.Kelvin + d.Kelvin);`
- `L52` `public static TemperatureDelta operator -(Temperature a, Temperature b) => new(a.Kelvin - b.Kelvin);`
- `L53` `public static bool operator >(Temperature a, Temperature b) => a.Kelvin > b.Kelvin;`
- `L54` `public static bool operator <(Temperature a, Temperature b) => a.Kelvin < b.Kelvin;`
- `L55` `public int CompareTo(Temperature other) => Kelvin.CompareTo(other.Kelvin);`
- `L64` `public static Length FromFeet(double ft) => new(ft * 0.3048);`
- `L65` `public double ToFeet() => Metres / 0.3048;`
- `L66` `public static Length operator +(Length a, Length b) => new(a.Metres + b.Metres);`
- `L67` `public static Length operator -(Length a, Length b) => new(a.Metres - b.Metres);`
- `L68` `public static double operator /(Length a, Length b) => a.Metres / b.Metres;`
- `L70` `public static Area operator *(Length a, Length b) => new(a.Metres * b.Metres);`
- `L71` `public static bool operator >(Length a, Length b) => a.Metres > b.Metres;`
- `L72` `public static bool operator <(Length a, Length b) => a.Metres < b.Metres;`
- `L73` `public int CompareTo(Length other) => Metres.CompareTo(other.Metres);`
- `L83` `public static Area FromSquareKilometres(double km2) => new(km2 * 1e6);`
- `L84` `public static Area FromHectares(double ha) => new(ha * 1e4);`
- `L85` `public static Area FromAcres(double acres) => new(acres * 4046.8564224);`
- `L86` `public double ToSquareKilometres() => SquareMetres / 1e6;`
- `L87` `public double ToAcres() => SquareMetres / 4046.8564224;`
- `L89` `public static Area operator +(Area a, Area b) => new(a.SquareMetres + b.SquareMetres);`
- `L90` `public static Area operator -(Area a, Area b) => new(a.SquareMetres - b.SquareMetres);`
- `L91` `public static double operator /(Area a, Area b) => a.SquareMetres / b.SquareMetres;`
- `L93` `public static Length operator /(Area a, Length l) => new(a.SquareMetres / l.Metres);`
- `L94` `public static bool operator >(Area a, Area b) => a.SquareMetres > b.SquareMetres;`
- `L95` `public static bool operator <(Area a, Area b) => a.SquareMetres < b.SquareMetres;`
- `L96` `public int CompareTo(Area other) => SquareMetres.CompareTo(other.SquareMetres);`
- `L102` `public static Mass operator +(Mass a, Mass b) => new(a.Kilograms + b.Kilograms);`
- `L103` `public static Mass operator -(Mass a, Mass b) => new(a.Kilograms - b.Kilograms);`
- `L104` `public static double operator /(Mass a, Mass b) => a.Kilograms / b.Kilograms;`
- `L105` `public static bool operator >(Mass a, Mass b) => a.Kilograms > b.Kilograms;`
- `L106` `public static bool operator <(Mass a, Mass b) => a.Kilograms < b.Kilograms;`
- `L107` `public int CompareTo(Mass other) => Kilograms.CompareTo(other.Kilograms);`
- `L113` `public static MassRate operator +(MassRate a, MassRate b) => new(a.KgPerSecond + b.KgPerSecond);`
- `L114` `public static MassRate operator -(MassRate a, MassRate b) => new(a.KgPerSecond - b.KgPerSecond);`
- `L116` `public static Mass operator *(MassRate r, Duration d) => new(r.KgPerSecond * d.Seconds);`
- `L117` `public static bool operator >(MassRate a, MassRate b) => a.KgPerSecond > b.KgPerSecond;`
- `L118` `public static bool operator <(MassRate a, MassRate b) => a.KgPerSecond < b.KgPerSecond;`
- `L119` `public int CompareTo(MassRate other) => KgPerSecond.CompareTo(other.KgPerSecond);`
- `L128` `public const double DaysPerTick = 30.0; // 30/360, pinned (SDD-001 §3)`
- `L129` `public double Seconds => Days * 86_400.0;`
- `L130` `public static Duration FromTicks(double ticks) => new(ticks * DaysPerTick);`
- `L131` `public static Duration operator +(Duration a, Duration b) => new(a.Days + b.Days);`
- `L137` `public static Power operator +(Power a, Power b) => new(a.Watts + b.Watts);`
- `L138` `public static Power operator -(Power a, Power b) => new(a.Watts - b.Watts);`
- `L139` `public static bool operator >(Power a, Power b) => a.Watts > b.Watts;`
- `L140` `public static bool operator <(Power a, Power b) => a.Watts < b.Watts;`
- `L141` `public int CompareTo(Power other) => Watts.CompareTo(other.Watts);`
- `L150` `public static Viscosity FromCentipoise(double cP) => new(cP * 1e-3);`
- `L156` `public static Permeability FromMillidarcy(double mD) => new(mD * 9.869233e-16);`
- `L163` `public double SpecificGravity() => KgPerCubicMetre / PhysicalConstants.WaterDensityKgPerM3;`
- `L164` `public static Density FromSpecificGravity(double sg) => new(sg * PhysicalConstants.WaterDensityKgPerM3);`
- `L176` `public static ApiGravity FromDensity(Density d) => new(141.5 / d.SpecificGravity() - 131.5);`
- `L177` `public Density ToDensity() => Density.FromSpecificGravity(141.5 / (Degrees + 131.5));`
- `L188` `public const double WaterDensityKgPerM3 = 1000.0;`
- `L191` `public const double GravityMPerS2 = 9.80665;`
- `L194` `public const double GasConstantJPerMolK = 8.31446261815324;`
- `L197` `public const double NormalZ10 = 1.281552;`
- `L200` `public const double DefaultChokeCriticalRatio = 0.55;`
- `L205` `public const double Pi = Math.PI;`
- `L208` `public const double TwoPi = 2.0 * Math.PI;`
- `L219` `public const double StandardPressurePa = 101_325.0;`
- `L222` `public const double StandardTemperatureK = 288.706;`
- `L225` `public const double AirMolarMassKgPerMol = 0.0289647;`
- `L235` `public const double AirDensityAtStandardKgPerM3 =`

