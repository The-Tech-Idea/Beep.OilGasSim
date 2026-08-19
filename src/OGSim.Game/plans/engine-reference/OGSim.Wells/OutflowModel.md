# OutflowModel

Source: `src\OGSim.Wells\OutflowModel.cs` · Lines: 177

## File intent

> R6.6 — vertical lift performance (SDD-003 §6.2, design 05 §4.3).
> 
> Inverted relative to the physics, deliberately. The VLP naturally answers
> "what wellhead pressure results from this rate"; §6.3's operating-point
> bisection searches on Pwf, so the useful direction is the one that answers
> "what bottomhole pressure does this rate DEMAND".
> 
> The hydrostatic term is why every well eventually dies: the column has to be

## Namespaces

- `OGSim.Wells`

## Type declarations

- `L19` `public sealed record TubingGeometry(`
- `L34` `public sealed class HydrostaticFrictionOutflowModel : IOutflowModel`

## Accessible members

- `L36` `private readonly TubingGeometry _tubing;`
- `L37` `private readonly Density _mixtureDensity;`
- `L38` `private readonly ILiftMethod? _lift;`
- `L45` `public HydrostaticFrictionOutflowModel(`
- `L56` `public ContentId Id { get; } = new("hydrostatic-friction-outflow");`
- `L72` `public Pressure RequiredBottomhole(ReservoirRate rate, Pressure wellheadPressure)`
- `L93` `public LiftEffect EffectAt(ReservoirRate rate) =>`
- `L101` `public double HydrostaticPa => HydrostaticAt(EffectAt(new ReservoirRate(0.0)).DensityFactor);`
- `L103` `private double HydrostaticAt(double densityFactor) =>`
- `L109` `public double FrictionPa(double rateM3PerS) => FrictionPa(rateM3PerS, densityFactor: 1.0);`
- `L111` `private double FrictionPa(double rateM3PerS, double densityFactor)`
- `L134` `private const double DensityFactorFloor = 1e-3;`
- `L143` `private double FrictionFactor(double velocity, double diameter)`
- `L153` `private const double ViscosityPaS = 1e-3;`
- `L155` `private static void Validate(TubingGeometry tubing, Density density)`
- `L175` `private static string Format(double value) =>`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

