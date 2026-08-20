# FlowNetwork

Source: `src\OGSim.Flow\FlowNetwork.cs` · Lines: 266

## File intent

> R4.2 — network construction and validation (SDD-002 §6).
> 
> The solver knows only IFlowElement (design 04 §1): nothing here names a
> separator, a pipeline or a well, which is what makes adding equipment a
> content edit rather than a solver change.
> 
> Validation happens ONCE, at build, and every failure is reported together —
> a network that fails is a COMPOSITION fault and the engine refuses to start

## Namespaces

- `OGSim.Flow`

## Type declarations

- `L16` `public sealed record NetworkProblem(EntityId<IFlowElement>? Element, string Detail);`
- `L18` `public abstract record NetworkBuildResult;`
- `L19` `public sealed record NetworkBuilt(FlowNetwork Network) : NetworkBuildResult;`
- `L20` `public sealed record NetworkRefused(IReadOnlyList<NetworkProblem> Problems) : NetworkBuildResult`
- `L35` `public sealed class FlowNetwork`

## Accessible members

- `L23` `public bool Equals(NetworkRefused? other) =>`
- `L26` `public override int GetHashCode() => Structural.HashOf(Problems);`
- `L37` `private readonly Dictionary<EntityId<IFlowElement>, IFlowElement> _byId;`
- `L38` `private readonly Dictionary<EntityId<IFlowElement>, List<FlowConnection>> _outgoing;`
- `L39` `private readonly Dictionary<EntityId<IFlowElement>, List<FlowConnection>> _incoming;`
- `L41` `private FlowNetwork(`
- `L59` `public IReadOnlyList<IFlowElement> Ordered { get; }`
- `L61` `public IReadOnlyList<FlowConnection> Connections { get; }`
- `L63` `public IFlowElement this[EntityId<IFlowElement> id] => _byId[id];`
- `L65` `public IReadOnlyList<FlowConnection> Downstream(EntityId<IFlowElement> id) =>`
- `L68` `public IReadOnlyList<FlowConnection> Upstream(EntityId<IFlowElement> id) =>`
- `L72` `public bool IsSource(IFlowElement element)`
- `L80` `public static NetworkBuildResult Build(FlowTopology topology)`
- `L186` `private static bool HasPort(IFlowElement element, PortId port, PortDirection direction)`
- `L194` `private static void Index(`
- `L213` `private static IReadOnlyList<IFlowElement>? TopologicalOrder(`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

