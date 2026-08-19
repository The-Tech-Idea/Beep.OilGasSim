# Obligations

Source: `src\OGSim.Operations\Obligations.cs` · Lines: 148

## File intent

> R13 / R20d.9 — abandonment obligations (SDD-007 §6, design 02 §3.4).
> 
> REGISTRATION IS UNCONDITIONAL. Every asset gets an obligation the moment it is
> created, because no path skips abandonment: a well that is drilled will one day
> be plugged whatever else happens to it, and a company that could create one
> without the liability would be able to walk away from the cost by never
> recording it.
> 

## Namespaces

- `OGSim.Operations`

## Type declarations

- `L23` `public sealed class ObligationRegistry : IObligationRegistry, IStateOwner`
- `L147` `private readonly record struct Obligation(ContentId Template, Money Cost);`

## Accessible members

- `L25` `private readonly Func<ContentId, Money> _costOf;`
- `L29` `private readonly List<EntityRef> _order = [];`
- `L30` `private readonly Dictionary<EntityRef, Obligation> _outstanding = [];`
- `L32` `public ObligationRegistry(Func<ContentId, Money> costOf)`
- `L38` `public StateKey Key { get; } = new("company.obligations");`
- `L40` `public int SchemaVersion => 1;`
- `L44` `public int Outstanding => _outstanding.Count;`
- `L47` `public IReadOnlyList<EntityRef> Assets => _order;`
- `L49` `public void Register(EntityRef asset, ContentId abandonmentTemplate)`
- `L67` `public Money EstimatedCost(EntityRef asset) =>`
- `L72` `public Money TotalOutstanding`
- `L82` `public bool IsOutstanding(EntityRef asset) => _outstanding.ContainsKey(asset);`
- `L93` `public void Discharge(EntityRef asset, EntityId<IOperation> completedAbandonment)`
- `L103` `public void Capture(IStateWriter writer)`
- `L120` `public void Restore(IStateReader reader)`
- `L144` `private static string Prefix(long index) =>`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

