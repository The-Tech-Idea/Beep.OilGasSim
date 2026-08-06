// R1.11 — state registration (SDD-001 §10). REGISTRATION ONLY, deliberately.
//
// What lives here: who owns which state block, in what order they are visited,
// and the refusal of a second claim on one key. What does NOT live here: the
// save format, the module-block framing that keeps two owners' keys apart, the
// canonical byte rules, and the migration chain — all of those are SDD-013 and
// belong to R19. Writing a provisional format here would guarantee two formats.
//
// The tracker names this task "IStateSerializer, IStateOwner". There is no
// IStateSerializer: SDD-001 §10 declares only IStateOwner with IStateWriter and
// IStateReader, and the serializer proper is SDD-013's. The name is left alone
// rather than invented here (rule F-1 cuts both ways).

namespace OGSim.Kernel;

public sealed class StateRegistry
{
    // SortedDictionary, not Dictionary: ordering is the point. Rule D-5 allows
    // enumerating this precisely because the order is a property of the keys.
    private readonly SortedDictionary<StateKey, IStateOwner> _owners = [];

    /// <summary>
    /// Law L5 at the persistence boundary: one owner per fact. A second claim on
    /// a key is refused rather than overwritten, because the loser would silently
    /// stop being saved and the loss would only surface as a corrupt load.
    /// </summary>
    public void Register(IStateOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);

        if (string.IsNullOrEmpty(owner.Key.Value))
            throw new InvariantFault("SDD-001 §10", null,
                $"{owner.GetType().Name} registered with an empty state key.");

        if (owner.SchemaVersion < 1)
            throw new InvariantFault("SDD-001 §10", null,
                $"State '{owner.Key.Value}' has schema version {owner.SchemaVersion}; " +
                "versions start at 1 so an unset field cannot pass for a valid one.");

        if (_owners.TryGetValue(owner.Key, out IStateOwner? existing))
            throw new InvariantFault("L5", null,
                $"State key '{owner.Key.Value}' is already owned by {existing.GetType().Name}; " +
                "two owners cannot claim one block.");

        _owners.Add(owner.Key, owner);
    }

    /// <summary>
    /// Owners in state-key order. Capture and restore walk this sequence, and it
    /// is fixed rather than registration-ordered so that composing modules in a
    /// different order cannot change a single byte of the save.
    /// </summary>
    public IReadOnlyList<IStateOwner> Owners
    {
        get
        {
            var ordered = new List<IStateOwner>(_owners.Count);
            foreach (KeyValuePair<StateKey, IStateOwner> pair in _owners) ordered.Add(pair.Value);
            return ordered;
        }
    }

    public bool TryGet(StateKey key, out IStateOwner? owner) => _owners.TryGetValue(key, out owner);

    /// <summary>
    /// Resolving an unregistered block is a fault, not an empty result: a save
    /// naming a block nobody owns means the module that owned it is gone, and
    /// loading past that would silently drop its state (design 11 §2.1).
    /// </summary>
    public IStateOwner Resolve(StateKey key) =>
        _owners.TryGetValue(key, out IStateOwner? owner)
            ? owner
            : throw new InvariantFault("SDD-001 §10", null,
                $"No module owns state key '{key.Value}'.");

    public int Count => _owners.Count;
}
