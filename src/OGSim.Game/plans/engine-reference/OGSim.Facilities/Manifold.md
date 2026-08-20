# Manifold

Source: `src\OGSim.Facilities\Manifold.cs` · Lines: 173

## File intent

> R20d.1 — the manifold (SDD-006 §1b, design 04 §2.2 and §5 stage 3).
> 
> WHERE WELLS MEET. `FlowNetwork` refuses two edges into one inlet (FD4) because
> two streams arriving at one port would be an undeclared commingle — mixing is
> an element's job, never an emergent property of the wiring, or provenance
> would blend with nothing recording it. This is that element: the second well
> on a field has somewhere to go, and what it contributed is still answerable
> when the oil is sold.

## Namespaces

- `OGSim.Facilities`

## Type declarations

- `L29` `public sealed record ManifoldTier(ContentId Id, int Slots);`
- `L39` `public sealed class Manifold : IFlowElement`

## Accessible members

- `L41` `private readonly ManifoldTier _tier;`
- `L42` `private readonly int _materialCount;`
- `L43` `private readonly List<PortSpec> _ports = [];`
- `L45` `public Manifold(EntityId<IFlowElement> id, ManifoldTier tier, int materialCount)`
- `L64` `public EntityId<IFlowElement> Id { get; }`
- `L68` `public int Slots => _tier.Slots;`
- `L72` `public IReadOnlyList<PortSpec> Ports => _ports;`
- `L74` `public PortId SlotAt(int slot)`
- `L83` `public PortId Outlet => new(_tier.Slots);`
- `L93` `public TransformResult Transform(TransformInput input)`
- `L154` `private Pressure HeaderPressure(TransformInput input) =>`
- `L163` `private static Allocation EmptyProvenance(TransformInput input) =>`
- `L172` `public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input) => [];`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

