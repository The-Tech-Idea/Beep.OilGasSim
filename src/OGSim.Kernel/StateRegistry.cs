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

/// <summary>
/// One thing wrong with the declared restore order, and WHICH KEY declared it
/// (SDD-013 §2b) — so composition can attribute the refusal to the module that
/// owns that key rather than reporting it against the set at large.
/// </summary>
public readonly record struct StateOrderProblem(StateKey Key, string Detail);

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

    /// <summary>
    /// Owners in DEPENDENCY order — design 11 §2.1's topological sort, and the
    /// order a restore walks (SDD-013 §2b).
    ///
    /// <para>Key order is the TIE-BREAK, so this is total and deterministic
    /// (rule D-5) and adds an order only where a dependency states one: two
    /// owners with nothing between them come out exactly as
    /// <see cref="Owners"/> has them. A save's bytes stay a function of the keys
    /// and only the restore follows the graph.</para>
    ///
    /// <para>Computed rather than cached: it is walked once per load, and a
    /// cache would be a second copy of the answer to keep honest as owners
    /// register (law L5).</para>
    /// </summary>
    public IReadOnlyList<IStateOwner> RestoreOrder
    {
        get
        {
            var problems = new List<StateOrderProblem>();
            IReadOnlyList<IStateOwner> ordered = Sorted(problems);

            // Unreachable through a composed engine, because composition asks
            // `RestoreOrderProblems` and refuses the whole set first. It stays a
            // fault rather than a silent partial order for the registry that is
            // built by hand — a test, or a host assembling a set itself.
            if (problems.Count > 0)
                throw new InvariantFault("SDD-013 §2b", null,
                    string.Join("; ", Details(problems)));

            return ordered;
        }
    }

    /// <summary>
    /// What is wrong with the declared order, empty when nothing is (SDD-013
    /// §2b).
    ///
    /// <para>Composition asks this so a cycle or a mistyped key is a STARTUP
    /// refusal naming every key involved, rather than a fault thrown the first
    /// time somebody loads a save. It is a fact about the module set, knowable
    /// when the set is validated, and design 03 §3.1 has no partially-composed
    /// engine to fall back on.</para>
    /// </summary>
    public IReadOnlyList<StateOrderProblem> RestoreOrderProblems()
    {
        var problems = new List<StateOrderProblem>();
        Sorted(problems);

        return problems;
    }

    /// <summary>
    /// The topological walk, collecting rather than throwing so one caller can
    /// refuse and the other can fault on the same traversal.
    /// </summary>
    private List<IStateOwner> Sorted(List<StateOrderProblem> problems)
    {
        var ordered = new List<IStateOwner>(_owners.Count);
        var placed = new HashSet<StateKey>();

        // Kahn's algorithm would need a queue ordered by key to stay
        // deterministic anyway; walking the key-ordered list repeatedly and
        // taking whatever is ready is the same answer with the tie-break built
        // in rather than bolted on. The owner count is a dozen.
        while (ordered.Count < _owners.Count)
        {
            var progressed = false;

            foreach (KeyValuePair<StateKey, IStateOwner> pair in _owners)
            {
                if (placed.Contains(pair.Key) || !IsReady(pair.Value, placed, problems)) continue;

                ordered.Add(pair.Value);
                placed.Add(pair.Key);
                progressed = true;
            }

            if (progressed) continue;

            // NOTHING MOVED: every owner left is waiting on another that is also
            // waiting. Each is named, because "there is a cycle" leaves the
            // reader to find it — and one problem per stuck owner means the
            // refusal attributes each to the module that declared it.
            //
            // UNLESS IT IS ALREADY ACCOUNTED FOR. An owner naming a key nobody
            // owns never becomes ready either, so it would be reported twice —
            // once truthfully and once as part of a cycle that does not exist.
            // A refusal that invents a second, wrong cause is worse than a
            // longer one: the reader goes looking for a loop.
            foreach (KeyValuePair<StateKey, IStateOwner> pair in _owners)
                if (!placed.Contains(pair.Key) && !Reported(problems, pair.Key))
                    problems.Add(new StateOrderProblem(pair.Key,
                        $"State '{pair.Key.Value}' waits on an owner that waits back; the " +
                        "declared restore order is cyclic."));

            return ordered;
        }

        return ordered;
    }

    private bool IsReady(
        IStateOwner owner, HashSet<StateKey> placed, List<StateOrderProblem> problems)
    {
        IReadOnlyList<StateKey> after = owner.RestoreAfter;
        var ready = true;

        for (int i = 0; i < after.Count; i++)
        {
            // A KEY NAMING NO OWNER is refused rather than skipped. Skipping
            // would make a typo a silent re-ordering — the owner would restore
            // early, in key order, exactly as if it had declared nothing, which
            // is the failure this member exists to prevent.
            if (!_owners.ContainsKey(after[i]))
            {
                var problem = new StateOrderProblem(owner.Key,
                    $"State '{owner.Key.Value}' must be restored after '{after[i].Value}', " +
                    "which no module owns; the declaration and the module set have drifted " +
                    "apart.");

                if (!problems.Contains(problem)) problems.Add(problem);

                ready = false;
                continue;
            }

            if (!placed.Contains(after[i])) ready = false;
        }

        return ready;
    }

    private static bool Reported(List<StateOrderProblem> problems, StateKey key)
    {
        for (int i = 0; i < problems.Count; i++)
            if (problems[i].Key == key) return true;

        return false;
    }

    private static List<string> Details(List<StateOrderProblem> problems)
    {
        var details = new List<string>(problems.Count);
        for (int i = 0; i < problems.Count; i++) details.Add(problems[i].Detail);

        return details;
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
