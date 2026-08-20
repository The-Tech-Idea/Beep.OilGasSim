# Separation

Source: `src\OGSim.Facilities\Separation.cs` · Lines: 385

## File intent

> R8.3 / R8.4 — separation (SDD-006 §2, design 02 §4.2, R8 §2.6).
> 
> SEPARATION EFFICIENCY IS NEVER 100%. Carry-over and carry-under are modelled,
> and an undersized vessel at high rate separates worse through the
> residence-time term. That is what makes vessel sizing a decision rather than a
> threshold, and it produces the authentic late-life problem: a separator sized
> for early oil rates handles late-life liquid volumes badly.
> 

## Namespaces

- `OGSim.Facilities`

## Type declarations

- `L28` `public sealed class FixedEfficiencySeparationModel : ISeparationModel`
- `L96` `public sealed record SeparatorTier(`
- `L125` `public sealed class Separator : IPressureController`

## Accessible members

- `L30` `public ContentId Id { get; } = new("fixed-efficiency-separation");`
- `L32` `public PhaseSplit SeparateAt(`
- `L73` `private static void Validate(SeparationEfficiency efficiency)`
- `L127` `private SeparatorTier _tier;`
- `L128` `private readonly ISeparationModel _model;`
- `L129` `private readonly IFluidPropertyModel _fluid;`
- `L130` `private readonly int _materialCount;`
- `L132` `public Separator(`
- `L163` `public EntityId<IFlowElement> Id { get; }`
- `L170` `public Pressure SetPoint => _tier.OperatingPressure;`
- `L173` `public SeparatorTier Tier => _tier;`
- `L189` `public void Fit(SeparatorTier tier)`
- `L206` `public static PortId Inlet { get; } = new(0);`
- `L208` `public static PortId GasOutlet { get; } = new(1);`
- `L210` `public static PortId LiquidOutlet { get; } = new(2);`
- `L212` `public static PortId WaterOutlet { get; } = new(3);`
- `L215` `public IReadOnlyList<PortSpec> Ports { get; } =`
- `L223` `public TransformResult Transform(TransformInput input)`
- `L286` `public SeparationEfficiency EfficiencyAt(MaterialStream inlet)`
- `L306` `public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input)`
- `L346` `private MaterialStream AtVesselPressure(MaterialStream inlet) =>`
- `L355` `private MaterialStream Leg(MaterialStream inlet, double[] byOrdinal) =>`
- `L359` `private TransformResult Empty(TransformInput input)`
- `L374` `private static double VolumetricRateOf(MaterialStream stream) =>`
- `L379` `private const double ApproximateDensityKgPerM3 = 800.0;`
- `L381` `private DisposedMass NoDisposal => new(`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

