# Tank

Source: `src\OGSim.Facilities\Tank.cs` · Lines: 219

## File intent

> R8.6 — the tank (SDD-006 §5, R8 §2.3).
> 
> THE SINGLE MOST IMPORTANT COUPLING IN THE EXPORT CHAIN, and the reason a
> buffer was one of R4's five synthetic elements: the shape was proven before
> the real thing was written.
> 
> A full tank accepts nothing. That constraint throttles the network through
> SDD-002 S3, the throttling raises pressure back up the line, and the pressure

## Namespaces

- `OGSim.Facilities`

## Type declarations

- `L22` `public sealed record TankTier(`
- `L30` `public sealed class Tank : IFlowElement`

## Accessible members

- `L32` `private readonly TankTier _tier;`
- `L33` `private readonly int _materialCount;`
- `L35` `private MaterialInventory _held;`
- `L36` `private Allocation _provenance;`
- `L38` `public Tank(`
- `L62` `public EntityId<IFlowElement> Id { get; }`
- `L65` `public MaterialInventory Held => _held;`
- `L69` `public Allocation Provenance => _provenance;`
- `L72` `public Mass Ullage => new(Math.Max(0.0, _tier.Capacity.Kilograms - _held.Total.Kilograms));`
- `L76` `public static PortId Inlet { get; } = new(0);`
- `L78` `public static PortId Outlet { get; } = new(1);`
- `L80` `public IReadOnlyList<PortSpec> Ports { get; } =`
- `L96` `public TransformResult Transform(TransformInput input)`
- `L131` `public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input)`
- `L154` `public void Receive(Composition massRate, Allocation provenance, Duration duration)`
- `L183` `public MaterialInventory Draw(Mass wanted)`
- `L200` `public MaterialInventory VapourLossOver(Duration duration)`
- `L213` `private const double OverfillTolerance = 1e-6;`
- `L215` `private const double SecondsPerDay = 86_400.0;`
- `L217` `private static string Format(double value) =>`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

