# WellsState

Source: `src\OGSim.Wells\WellsState.cs` · Lines: 193

## File intent

> R20c.7 — the wells module's state and its stage (SDD-003 §6, design 03 §6).
> 
> This is the half of the loop that makes a reservoir matter: a completion drains
> a compartment, the compartment loses pressure, and next month the same well
> produces less. Neither half is a game on its own.
> 
> The completion NEVER reads a compartment. OGSim.Wells cannot see subsurface
> truth and must not — reservoir pressure arrives as a number, pushed in before

## Namespaces

- `OGSim.Wells`

## Type declarations

- `L18` `public sealed class WellsState : IStateOwner`

## Accessible members

- `L20` `private readonly List<Completion> _completions = [];`
- `L21` `private readonly Dictionary<EntityId<ICompletion>, Completion> _byId = [];`
- `L27` `private readonly Dictionary<EntityId<ICompletion>, EntityId<IReservoirCompartmentEntity>> _drains = [];`
- `L29` `private readonly IFlowElementRegistry _network;`
- `L31` `private ulong _nextId;`
- `L33` `public WellsState(IFlowElementRegistry network)`
- `L39` `public StateKey Key { get; } = new("wells.completions");`
- `L41` `public int SchemaVersion => 1;`
- `L43` `public int Count => _completions.Count;`
- `L49` `public EntityId<ICompletion> Open(`
- `L71` `public IReadOnlyList<Completion> Completions => _completions;`
- `L78` `public Completion? Find(EntityId<ICompletion> completion) =>`
- `L81` `public EntityId<IReservoirCompartmentEntity> CompartmentOf(EntityId<ICompletion> completion) =>`
- `L96` `public void RefreshFromReservoir(`
- `L139` `public void Capture(IStateWriter writer)`
- `L164` `public void Restore(IStateReader reader)`
- `L191` `private static string Prefix(long index) =>`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

