# EntityRegistry

Source: `src\OGSim.Kernel\EntityRegistry.cs` · Lines: 137

## File intent

> R1.2 — the entity registry (SDD-001 §2). Sequential typed ids, save-stable,
> no Guid (rule D-6). Every failure here is INV3: a reference that does not
> resolve means state is inconsistent, and design 09 §5.1 C5 halts rather than
> letting the engine continue against a dangling pointer.
> 
> D-5 compliance is structural, not careful: entities live in a List indexed by
> (id - 1), so All<T>() enumerates in issue order by construction. The one
> Dictionary here is keyed by Type and is never enumerated.

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L12` `public sealed class EntityRegistry : IEntityRegistry`
- `L55` `private interface IEntityTable;`
- `L57` `private sealed class EntityTable<T> : IEntityTable where T : class`

## Accessible members

- `L14` `private readonly Dictionary<Type, IEntityTable> _tables = [];`
- `L16` `public EntityId<T> Issue<T>() where T : class => TableFor<T>().Issue();`
- `L18` `public void Register<T>(EntityId<T> id, T entity) where T : class`
- `L24` `public T Resolve<T>(EntityId<T> id) where T : class => TableFor<T>().Resolve(id);`
- `L26` `public bool TryResolve<T>(EntityId<T> id, out T? entity) where T : class =>`
- `L29` `public IReadOnlyList<T> All<T>() where T : class => TableFor<T>().All();`
- `L35` `public ulong HighWaterMark<T>() where T : class => TableFor<T>().HighWaterMark;`
- `L41` `public void RestoreHighWaterMark<T>(ulong highWaterMark) where T : class =>`
- `L44` `private EntityTable<T> TableFor<T>() where T : class`
- `L61` `private readonly List<T?> _entities = [];`
- `L63` `public ulong HighWaterMark => (ulong)_entities.Count;`
- `L65` `public EntityId<T> Issue()`
- `L71` `public void Register(EntityId<T> id, T entity)`
- `L81` `public T Resolve(EntityId<T> id)`
- `L89` `public bool TryResolve(EntityId<T> id, out T? entity)`
- `L106` `public IReadOnlyList<T> All()`
- `L117` `public void RestoreHighWaterMark(ulong highWaterMark)`
- `L125` `private int IndexOf(EntityId<T> id)`

