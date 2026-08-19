# SpecificationGate

Source: `src\OGSim.Facilities\SpecificationGate.cs` · Lines: 192

## File intent

> R8.7 — the spec gate (SDD-006 §7, design 02 §4.3, R8 §2.4).
> 
> A stream failing a spec DOES NOT PASS. It routes to the Reject port, and the
> failing parameter and its margin are reported.
> 
> This is the mechanism that makes the player build a dehydrator. Not a
> tech-tree prompt, not a hint: a rejection with a reason. The stream arrives at
> the custody point, fails on water content, and the flare volume equals the

## Namespaces

- `OGSim.Facilities`

## Type declarations

- `L21` `public sealed record SpecBreach(SpecProperty Property, double Limit, double Measured)`
- `L34` `public sealed record StreamProperties(`
- `L61` `public static class SpecificationCheck`
- `L100` `public sealed class CustodyTransferPoint : ICustodyTransferPoint`

## Accessible members

- `L24` `public double Margin => Measured - Limit;`
- `L42` `public double ValueOf(SpecProperty property) => property switch`
- `L68` `public static IReadOnlyList<SpecBreach> Evaluate(`
- `L102` `private readonly int _materialCount;`
- `L103` `private readonly Func<MaterialStream, StreamProperties> _measure;`
- `L105` `public CustodyTransferPoint(`
- `L120` `public EntityId<IFlowElement> Id { get; }`
- `L122` `public Specification Spec { get; }`
- `L126` `public IReadOnlyList<SpecBreach> LastBreaches { get; private set; } = [];`
- `L129` `public static PortId Inlet { get; } = new(0);`
- `L133` `public static PortId OnSpecOutlet { get; } = new(1);`
- `L135` `public static PortId RejectOutlet { get; } = new(2);`
- `L138` `public IReadOnlyList<PortSpec> Ports { get; } =`
- `L145` `public TransformResult Transform(TransformInput input)`
- `L169` `public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input) => [];`
- `L171` `private TransformResult Both(Composition onSpec, Composition rejected, TransformInput input)`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

