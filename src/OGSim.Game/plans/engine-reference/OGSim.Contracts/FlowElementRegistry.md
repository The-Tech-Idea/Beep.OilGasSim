# FlowElementRegistry

Source: `src\OGSim.Contracts\FlowElementRegistry.cs` · Lines: 136

## File intent

> R20c — where the field's elements and wiring are collected (SDD-002 §6).
> 
> Elements are created by four different modules — Wells makes completions,
> Facilities makes separators and tanks, Transport makes pipelines — and the
> solver needs all of them at once. Without this there is no way for stage 5 to
> see across modules, and the solver is reachable only by a test that hand-builds
> its input.
> 

## Namespaces

- `OGSim.Contracts`

## Type declarations

- `L26` `public interface IFlowElementRegistry`
- `L55` `public sealed class FlowElementRegistry : IFlowElementRegistry`

## Accessible members

- `L59` `private readonly List<IFlowElement> _elements = [];`
- `L60` `private readonly HashSet<EntityId<IFlowElement>> _registered = [];`
- `L61` `private readonly List<FlowConnection> _connections = [];`
- `L63` `public void Add(IFlowElement element)`
- `L77` `public void Connect(FlowConnection connection)`
- `L99` `public FlowTopology ViewFor(IReadOnlyCollection<EntityRef> available)`
- `L127` `public IReadOnlyList<IFlowElement> Registered => _elements;`
- `L131` `public static EntityRef ReferenceTo(IFlowElement element)`

## Imports

- `using OGSim.Kernel;`

