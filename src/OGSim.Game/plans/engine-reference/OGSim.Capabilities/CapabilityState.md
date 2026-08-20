# CapabilityState

Source: `src\OGSim.Capabilities\CapabilityState.cs` · Lines: 82

## File intent

> R20c.6 — acquired technology as an owned, saved fact (SDD-001 §10, SDD-005 §2).
> 
> ACQUISITION IS REPLAYED IN ORDER, not restored as a set. Acquire checks
> prerequisites, so replaying in the order they were granted rebuilds the same
> holdings through the same graph that authorised them. A save listing a
> technology whose prerequisite is missing is refused rather than loaded, which
> is the point: the graph is what makes the tree a sequence rather than a menu,
> and a load that bypassed it would be a route around the whole system.

## Namespaces

- `OGSim.Capabilities`

## Type declarations

- `L22` `public sealed class CapabilityState : IStateOwner`

## Accessible members

- `L24` `private readonly IReadOnlyList<TechnologyNode> _graph;`
- `L26` `public CapabilityState(IReadOnlyList<TechnologyNode> graph, Era era)`
- `L35` `public TechnologyState Technology { get; private set; }`
- `L37` `public Era Era { get; private set; }`
- `L39` `public StateKey Key { get; } = new("capabilities.technology");`
- `L41` `public int SchemaVersion => 1;`
- `L43` `public void Capture(IStateWriter writer)`
- `L59` `public void Restore(IStateReader reader)`
- `L80` `private static string Prefix(long index) =>`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

