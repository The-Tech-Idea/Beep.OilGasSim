// R1.6 — the audit trail (SDD-001 §5, design 09 §4). "The important one": an
// append-only, ordered, queryable record of every decision the simulation made,
// saved with the game. It is what makes "why is well W-014 shut in?" a query
// against a record the engine keeps anyway rather than bespoke UI plumbing
// (09 §4.3), and what makes "the game cheated" answerable (09 §4.2).
//
// Ordering is structural: entries live in a SortedDictionary keyed by AuditId,
// so every query returns ascending-id order without anyone remembering to sort.
// The secondary indices are Dictionaries that are looked up and never
// enumerated, which is rule D-5 satisfied by construction.

namespace OGSim.Kernel;

public sealed class AuditTrail : IAuditTrail
{
    private readonly ISimulationClock _clock;
    private readonly AuditRetention _retention;

    private readonly SortedDictionary<AuditId, AuditEntry> _entries = [];
    private readonly Dictionary<EntityRef, List<AuditId>> _bySubject = [];
    private readonly Dictionary<AuditCategory, List<AuditId>> _byCategory = [];

    private ulong _nextId = 1;

    public AuditTrail(ISimulationClock clock, AuditRetention retention)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(retention);
        if (retention.DetailWindowTicks < 0)
            throw new ArgumentOutOfRangeException(nameof(retention),
                retention.DetailWindowTicks, "The detail window cannot be negative.");

        _clock = clock;
        _retention = retention;
    }

    public int Count => _entries.Count;

    public AuditId Record(
        AuditCategory category,
        EntityRef? subject,
        AuditId? cause,
        IReadOnlyDictionary<string, AuditValue> data)
    {
        ArgumentNullException.ThrowIfNull(data);

        // A cause that does not resolve is a broken chain, and a broken chain
        // makes 09 §4.3's explanation stop halfway with no indication it did.
        if (cause is AuditId causeId && !_entries.ContainsKey(causeId))
            throw new InvariantFault("INV12", subject,
                $"Audit cause {causeId.Value} does not exist; the chain would be broken.");

        var id = new AuditId(_nextId++);

        // The data is copied: an entry is immutable once written (R1 §2.4), and
        // a caller reusing its dictionary would otherwise rewrite history.
        var entry = new AuditEntry(
            id, _clock.CurrentTick, category, subject, cause,
            new Dictionary<string, AuditValue>(data));

        _entries.Add(id, entry);
        if (subject is EntityRef subjectRef) IndexOf(_bySubject, subjectRef).Add(id);
        IndexOf(_byCategory, category).Add(id);

        return entry.Id;
    }

    public IReadOnlyList<AuditEntry> Query(AuditQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.CauseChainLeaf is AuditId leaf)
            return CauseChain(leaf, query);

        var results = new List<AuditEntry>();
        foreach (AuditId id in Candidates(query))
        {
            AuditEntry entry = _entries[id];
            if (Matches(entry, query)) results.Add(entry);
        }
        return results;
    }

    /// <summary>
    /// Design 09 §4.4. Called at tick close. Recent detail is kept in full;
    /// beyond the window only per-tick per-element detail is dropped, and even
    /// that survives if something still standing depends on it.
    /// </summary>
    public void Prune()
    {
        int cutoff = _clock.CurrentTick.Value - _retention.DetailWindowTicks;
        if (cutoff <= 0) return;

        // "Nothing that explains the current state is ever discarded" (§4.4) is
        // a statement about the CAUSE GRAPH, not about categories. A tick-4
        // constraint binding is per-element detail, but if it is why a tick-400
        // well is shut in, dropping it breaks the very query §4.3 promises. So
        // the keep-set is the transitive cause closure of everything retained.
        var keep = new HashSet<AuditId>();
        foreach (KeyValuePair<AuditId, AuditEntry> pair in _entries)
        {
            AuditEntry entry = pair.Value;
            if (entry.Tick.Value > cutoff || IsDurable(entry.Category))
                MarkChain(entry.Id, keep);
        }

        var doomed = new List<AuditId>();
        foreach (KeyValuePair<AuditId, AuditEntry> pair in _entries)
            if (!keep.Contains(pair.Key)) doomed.Add(pair.Key);

        if (doomed.Count == 0) return;

        for (int i = 0; i < doomed.Count; i++) _entries.Remove(doomed[i]);
        RebuildIndices();
    }

    /// <summary>
    /// 09 §4.4 keeps "every state transition, every financial event and every
    /// fault", discarding only per-tick per-element detail. These three are that
    /// detail; everything else is a decision with lasting consequence.
    /// </summary>
    private static bool IsDurable(AuditCategory category) => category switch
    {
        AuditCategory.ConstraintBinding => false,
        AuditCategory.InvariantCheck => false,
        AuditCategory.Merge => false,
        _ => true,
    };

    private void MarkChain(AuditId start, HashSet<AuditId> keep)
    {
        AuditId? current = start;
        // Add returns false once the chain reaches ground already walked, which
        // is what terminates this even if two chains converge.
        while (current is AuditId id && keep.Add(id))
            current = _entries.TryGetValue(id, out AuditEntry? entry) ? entry.Cause : null;
    }

    private IReadOnlyList<AuditEntry> CauseChain(AuditId leaf, AuditQuery query)
    {
        var walked = new List<AuditEntry>();
        var seen = new HashSet<AuditId>();
        AuditId? current = leaf;

        while (current is AuditId id && seen.Add(id))
        {
            if (!_entries.TryGetValue(id, out AuditEntry? entry)) break;
            if (Matches(entry, query)) walked.Add(entry);
            current = entry.Cause;
        }

        walked.Reverse();   // ascending id: the cause before its consequence
        return walked;
    }

    /// <summary>Narrow by the most selective index available before filtering.</summary>
    private IReadOnlyList<AuditId> Candidates(AuditQuery query)
    {
        if (query.Subject is EntityRef subject)
            return _bySubject.TryGetValue(subject, out List<AuditId>? bySubject)
                ? bySubject : [];

        if (query.Category is AuditCategory category)
            return _byCategory.TryGetValue(category, out List<AuditId>? byCategory)
                ? byCategory : [];

        return [.. _entries.Keys];
    }

    private static bool Matches(AuditEntry entry, AuditQuery query)
    {
        if (query.Subject is EntityRef subject && entry.Subject != subject) return false;
        if (query.Category is AuditCategory category && entry.Category != category) return false;
        if (query.Range is TickRange range &&
            (entry.Tick.Value < range.From.Value || entry.Tick.Value > range.To.Value)) return false;
        return true;
    }

    private void RebuildIndices()
    {
        _bySubject.Clear();
        _byCategory.Clear();
        foreach (KeyValuePair<AuditId, AuditEntry> pair in _entries)
        {
            AuditEntry entry = pair.Value;
            if (entry.Subject is EntityRef subject) IndexOf(_bySubject, subject).Add(entry.Id);
            IndexOf(_byCategory, entry.Category).Add(entry.Id);
        }
    }

    private static List<AuditId> IndexOf<TKey>(Dictionary<TKey, List<AuditId>> index, TKey key)
        where TKey : notnull
    {
        if (index.TryGetValue(key, out List<AuditId>? existing)) return existing;
        var created = new List<AuditId>();
        index.Add(key, created);
        return created;
    }
}
