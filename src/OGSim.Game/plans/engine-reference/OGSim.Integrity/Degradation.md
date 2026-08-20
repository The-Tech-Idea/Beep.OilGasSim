# Degradation

Source: `src\OGSim.Integrity\Degradation.cs` · Lines: 146

## File intent

> R18.1 / R18.2 — degradation and hazard (SDD-012 §0–2).
> 
> NEITHER MODEL DRAWS. The hazard model returns a PROBABILITY and the engine
> performs the draw at stage 4, consuming only the `Hazard` stream — the same
> separation SDD-008 applies to observation, and for the same reason: a plugin
> drawing its own numbers could consume a different count and shift every later
> draw in that stream.
> 

## Namespaces

- `OGSim.Integrity`

## Type declarations

- `L21` `public sealed record DegradationCoefficients(`
- `L38` `public sealed class SeverityWeightedDegradation : IDegradationModel`
- `L103` `public sealed class ExponentialHazardModel : IHazardModel`

## Accessible members

- `L40` `private readonly DegradationCoefficients _coefficients;`
- `L42` `public SeverityWeightedDegradation(ContentId id, DegradationCoefficients coefficients)`
- `L55` `public ContentId Id { get; }`
- `L57` `public double NextCondition(double condition, ServiceSeverity severity, Duration dt)`
- `L88` `private const double DaysPerYear = 360.0;`
- `L89` `private const double TicksPerYear = 12.0;`
- `L91` `private static string Format(double value) =>`
- `L105` `private readonly double _baseRatePerYear;   // λ_base`
- `L106` `private readonly double _conditionExponent; // k_h`
- `L108` `public ExponentialHazardModel(ContentId id, double baseRatePerYear, double conditionExponent)`
- `L124` `public ContentId Id { get; }`
- `L128` `public double RateAt(double condition) =>`
- `L131` `public double FailureProbability(double condition, Duration dt)`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

