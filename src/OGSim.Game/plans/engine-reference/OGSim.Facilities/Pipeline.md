# Pipeline

Source: `src\OGSim.Facilities\Pipeline.cs` · Lines: 367

## File intent

> R11.1 / R11.3 — pipelines (SDD-006 §6, R11 §2.1–2.3).
> 
> CAPACITY IS NEVER CONFIGURED. A pipeline declares geometry and a rating; its
> throughput is whatever the hydraulics permit for the fluid actually flowing at
> the actual inlet pressure. A configured maxRate would make pipelines inert
> numbers and would have made the emergent behaviour below impossible to express
> — which is why SDD-006 §6 says the field is deliberately absent.
> 

## Namespaces

- `OGSim.Facilities`

## Type declarations

- `L29` `public sealed class LiquidHydraulicModel : IHydraulicModel`
- `L152` `public sealed class GasHydraulicModel`
- `L206` `public sealed class Pipeline : IPipeline`

## Accessible members

- `L31` `public ContentId Id { get; } = new("liquid-darcy-weisbach");`
- `L33` `private readonly Density _density;`
- `L34` `private readonly Viscosity _viscosity;`
- `L35` `private readonly Length _elevationRise;`
- `L37` `public LiquidHydraulicModel(Density density, Viscosity viscosity, Length elevationRise)`
- `L49` `public Pressure DropAlong(`
- `L82` `public MassRate CapacityFor(PipeGeometry geometry, Pressure available)`
- `L111` `private double DropFor(PipeGeometry geometry, double massRate)`
- `L126` `private const int BisectionIterations = 80;`
- `L127` `private const int BracketGrowths = 20;`
- `L129` `internal static void Validate(PipeGeometry geometry)`
- `L154` `private readonly double _molarMassKgPerMol;`
- `L155` `private readonly double _compressibility;    // Z̄`
- `L156` `private readonly Temperature _average;       // T̄`
- `L157` `private readonly double _frictionFactor;     // f, from Colebrook at line conditions`
- `L159` `public GasHydraulicModel(`
- `L178` `public MassRate CapacityBetween(PipeGeometry geometry, Pressure inlet, Pressure outlet)`
- `L208` `private readonly IHydraulicModel _hydraulics;`
- `L209` `private readonly IFluidPropertyModel _fluid;`
- `L210` `private readonly Density _density;`
- `L211` `private readonly int _materialCount;`
- `L213` `private MaterialInventory _linefill;`
- `L215` `public Pipeline(`
- `L243` `public EntityId<IFlowElement> Id { get; }`
- `L244` `public PipeGeometry Geometry { get; private set; }`
- `L245` `public Length PipeLength { get; private set; }`
- `L246` `public Length InnerDiameter { get; private set; }`
- `L261` `public void Route(PipeGeometry geometry)`
- `L274` `public Pressure Rating { get; }`
- `L275` `public ContentId PipeSpec { get; }`
- `L278` `public MaterialInventory Linefill => _linefill;`
- `L285` `public Mass FullLinefill => new(`
- `L292` `public static PortId Inlet { get; } = new(0);`
- `L294` `public static PortId Outlet { get; } = new(1);`
- `L296` `public IReadOnlyList<PortSpec> Ports { get; } =`
- `L302` `public TransformResult Transform(TransformInput input)`
- `L333` `public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input)`
- `L349` `public void CommitLinefill(MaterialInventory contents)`
- `L361` `private const double ErosionalConstant = 122.0;`
- `L363` `private const double LinefillTolerance = 1e-6;`
- `L365` `private static string Format(double value) =>`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

