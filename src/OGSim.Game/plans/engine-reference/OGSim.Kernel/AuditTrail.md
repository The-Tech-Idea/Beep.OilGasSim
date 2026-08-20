# AuditTrail

Source: `src\OGSim.Kernel\AuditTrail.cs` · Lines: 199

## File intent

> R1.6 — the audit trail (SDD-001 §5, design 09 §4). "The important one": an
> append-only, ordered, queryable record of every decision the simulation made,
> saved with the game. It is what makes "why is well W-014 shut in?" a query
> against a record the engine keeps anyway rather than bespoke UI plumbing
> (09 §4.3), and what makes "the game cheated" answerable (09 §4.2).
> 
> Ordering is structural: entries live in a SortedDictionary keyed by AuditId,
> so every query returns ascending-id order without anyone remembering to sort.

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L14` `public sealed class AuditTrail : IAuditTrail`

## Accessible members

- `L16` `private readonly ISimulationClock _clock;`
- `L17` `private readonly AuditRetention _retention;`
- `L19` `private readonly SortedDictionary<AuditId, AuditEntry> _entries = [];`
- `L20` `private readonly Dictionary<EntityRef, List<AuditId>> _bySubject = [];`
- `L21` `private readonly Dictionary<AuditCategory, List<AuditId>> _byCategory = [];`
- `L23` `private ulong _nextId = 1;`
- `L25` `public AuditTrail(ISimulationClock clock, AuditRetention retention)`
- `L37` `public int Count => _entries.Count;`
- `L39` `public AuditId Record(`
- `L68` `public IReadOnlyList<AuditEntry> Query(AuditQuery query)`
- `L89` `public void Prune()`
- `L122` `private static bool IsDurable(AuditCategory category) => category switch`
- `L130` `private void MarkChain(AuditId start, HashSet<AuditId> keep)`
- `L139` `private IReadOnlyList<AuditEntry> CauseChain(AuditId leaf, AuditQuery query)`
- `L157` `private IReadOnlyList<AuditId> Candidates(AuditQuery query)`
- `L170` `private static bool Matches(AuditEntry entry, AuditQuery query)`
- `L179` `private void RebuildIndices()`
- `L191` `private static List<AuditId> IndexOf<TKey>(Dictionary<TKey, List<AuditId>> index, TKey key)`

