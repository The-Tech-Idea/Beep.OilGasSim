# LiftMethods

Source: `src\OGSim.Wells\LiftMethods.cs` · Lines: 376

## File intent

> R7.1–R7.5 — artificial lift (SDD-003 §6.2, design 02 §3.3, R7).
> 
> Every well starts flowing naturally and every well stops. R6-V6 is that
> moment; this is the answer to it. Each method has a different cost, a
> different capability envelope and a different failure mode, and choosing
> between them is one of the game's best decisions precisely because the
> envelopes overlap only partly.
> 

## Namespaces

- `OGSim.Wells`

## Type declarations

- `L24` `public abstract class LiftMethod : ILiftMethod`
- `L139` `public sealed class ElectricSubmersiblePump : LiftMethod`
- `L235` `public sealed class GasLift : LiftMethod`
- `L309` `public sealed class RodPump : LiftMethod`
- `L349` `public sealed class ProgressingCavityPump : LiftMethod`

## Accessible members

- `L28` `private const double DegradationPerExceedance = 0.35;`
- `L32` `private const double HazardPerExceedance = 3.0;`
- `L34` `protected LiftMethod(`
- `L47` `public EntityId<IWellComponent> Id { get; }`
- `L48` `public ContentId Tier { get; }`
- `L49` `public ContentId InstalledTier { get; }`
- `L50` `public LiftEnvelope Envelope { get; }`
- `L51` `public GameDate Installed { get; }`
- `L52` `public double Condition { get; }`
- `L60` `public EnvelopeAssessment Assess(LiftConditions conditions)`
- `L120` `public abstract LiftEffect EffectAt(ReservoirRate rate, Density mixtureDensity);`
- `L124` `private const double RelativeFloor = 1e-9;`
- `L142` `private const double ReferenceDensityKgPerM3 = 1000.0;`
- `L144` `private readonly IReadOnlyList<(double RateM3PerS, double HeadM)> _curve;`
- `L145` `private readonly double _efficiency;`
- `L147` `public ElectricSubmersiblePump(`
- `L177` `public override LiftEffect EffectAt(ReservoirRate rate, Density mixtureDensity)`
- `L202` `public double HeadAt(double rateM3PerS)`
- `L218` `private static string Format(double value) =>`
- `L237` `private readonly double _injectionRateM3PerS;`
- `L238` `private readonly double _gasDensityKgPerM3;`
- `L240` `public GasLift(`
- `L263` `public ReservoirRate InjectionRate => new(_injectionRateM3PerS);`
- `L265` `public override LiftEffect EffectAt(ReservoirRate rate, Density mixtureDensity)`
- `L293` `private static string Format(double value) =>`
- `L311` `private readonly double _displacementM3PerS;`
- `L313` `public RodPump(`
- `L328` `public override LiftEffect EffectAt(ReservoirRate rate, Density mixtureDensity) =>`
- `L334` `private static string Format(double value) =>`
- `L351` `private readonly double _displacementM3PerS;`
- `L353` `public ProgressingCavityPump(`
- `L368` `public override LiftEffect EffectAt(ReservoirRate rate, Density mixtureDensity) =>`
- `L374` `private static string Format(double value) =>`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

