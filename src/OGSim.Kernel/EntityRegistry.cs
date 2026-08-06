// R1.2 — the entity registry (SDD-001 §2). Sequential typed ids, save-stable,
// no Guid (rule D-6). Every failure here is INV3: a reference that does not
// resolve means state is inconsistent, and design 09 §5.1 C5 halts rather than
// letting the engine continue against a dangling pointer.
//
// D-5 compliance is structural, not careful: entities live in a List indexed by
// (id - 1), so All<T>() enumerates in issue order by construction. The one
// Dictionary here is keyed by Type and is never enumerated.

namespace OGSim.Kernel;

public sealed class EntityRegistry : IEntityRegistry
{
    private readonly Dictionary<Type, IEntityTable> _tables = [];

    public EntityId<T> Issue<T>() where T : class => TableFor<T>().Issue();

    public void Register<T>(EntityId<T> id, T entity) where T : class
    {
        ArgumentNullException.ThrowIfNull(entity);
        TableFor<T>().Register(id, entity);
    }

    public T Resolve<T>(EntityId<T> id) where T : class => TableFor<T>().Resolve(id);

    public bool TryResolve<T>(EntityId<T> id, out T? entity) where T : class =>
        TableFor<T>().TryResolve(id, out entity);

    public IReadOnlyList<T> All<T>() where T : class => TableFor<T>().All();

    /// <summary>
    /// Highest id issued for T — the value persistence restores so that ids
    /// stay save-stable across a load (SDD-001 §2, design 11 §2.1).
    /// </summary>
    public ulong HighWaterMark<T>() where T : class => TableFor<T>().HighWaterMark;

    /// <summary>
    /// Restores the issue counter after a load, so ids continue rather than
    /// restart and collide with entities already in the save.
    /// </summary>
    public void RestoreHighWaterMark<T>(ulong highWaterMark) where T : class =>
        TableFor<T>().RestoreHighWaterMark(highWaterMark);

    private EntityTable<T> TableFor<T>() where T : class
    {
        if (_tables.TryGetValue(typeof(T), out IEntityTable? existing))
            return (EntityTable<T>)existing;

        var created = new EntityTable<T>();
        _tables.Add(typeof(T), created);
        return created;
    }

    /// <summary>Type-erased handle so the per-type tables can share one store.</summary>
    private interface IEntityTable;

    private sealed class EntityTable<T> : IEntityTable where T : class
    {
        // Index (id - 1): ids start at 1 so that default(EntityId<T>) — value 0 —
        // is never a valid reference. A null slot is issued-but-not-registered.
        private readonly List<T?> _entities = [];

        public ulong HighWaterMark => (ulong)_entities.Count;

        public EntityId<T> Issue()
        {
            _entities.Add(null);
            return new EntityId<T>((ulong)_entities.Count);
        }

        public void Register(EntityId<T> id, T entity)
        {
            int index = IndexOf(id);
            if (_entities[index] is not null)
                throw new InvariantFault("INV3", null,
                    $"{typeof(T).Name} id {id.Value} is already registered; " +
                    "registration is write-once (law L5).");
            _entities[index] = entity;
        }

        public T Resolve(EntityId<T> id)
        {
            int index = IndexOf(id);
            return _entities[index]
                ?? throw new InvariantFault("INV3", null,
                    $"{typeof(T).Name} id {id.Value} was issued but never registered.");
        }

        public bool TryResolve(EntityId<T> id, out T? entity)
        {
            if (id.Value == 0 || id.Value > (ulong)_entities.Count)
            {
                entity = null;
                return false;
            }
            entity = _entities[(int)(id.Value - 1)];
            return entity is not null;
        }

        /// <summary>
        /// Registered entities in ascending id order. Issued-but-unregistered
        /// slots are absent rather than null: a caller iterating the world's
        /// wells should never have to null-check, and the id that is missing
        /// still faults loudly through Resolve.
        /// </summary>
        public IReadOnlyList<T> All()
        {
            var result = new List<T>(_entities.Count);
            for (int i = 0; i < _entities.Count; i++)
            {
                T? entity = _entities[i];
                if (entity is not null) result.Add(entity);
            }
            return result;
        }

        public void RestoreHighWaterMark(ulong highWaterMark)
        {
            if (_entities.Count > 0)
                throw new InvariantFault("INV3", null,
                    $"{typeof(T).Name} ids cannot be restored onto a registry that has already issued.");
            for (ulong i = 0; i < highWaterMark; i++) _entities.Add(null);
        }

        private int IndexOf(EntityId<T> id)
        {
            if (id.Value == 0)
                throw new InvariantFault("INV3", null,
                    $"default(EntityId<{typeof(T).Name}>) is not a valid id; ids begin at 1.");
            if (id.Value > (ulong)_entities.Count)
                throw new InvariantFault("INV3", null,
                    $"{typeof(T).Name} id {id.Value} was never issued " +
                    $"(highest issued is {_entities.Count}).");
            return (int)(id.Value - 1);
        }
    }
}
