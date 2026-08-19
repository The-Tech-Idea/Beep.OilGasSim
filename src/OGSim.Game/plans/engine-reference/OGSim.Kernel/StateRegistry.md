# StateRegistry

Source: `src\OGSim.Kernel\StateRegistry.cs` · Lines: 77

## File intent

> R1.11 — state registration (SDD-001 §10). REGISTRATION ONLY, deliberately.
> 
> What lives here: who owns which state block, in what order they are visited,
> and the refusal of a second claim on one key. What does NOT live here: the
> save format, the module-block framing that keeps two owners' keys apart, the
> canonical byte rules, and the migration chain — all of those are SDD-013 and
> belong to R19. Writing a provisional format here would guarantee two formats.
> 

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L16` `public sealed class StateRegistry`

## Accessible members

- `L20` `private readonly SortedDictionary<StateKey, IStateOwner> _owners = [];`
- `L27` `public void Register(IStateOwner owner)`
- `L53` `public IReadOnlyList<IStateOwner> Owners`
- `L63` `public bool TryGet(StateKey key, out IStateOwner? owner) => _owners.TryGetValue(key, out owner);`
- `L70` `public IStateOwner Resolve(StateKey key) =>`
- `L76` `public int Count => _owners.Count;`

