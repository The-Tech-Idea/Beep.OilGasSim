# Rivals

Source: `src\OGSim.Company\Rivals.cs` · Lines: 202

## File intent

> R16.3 / R16.4 — rivals and licence rounds (SDD-011 §2–3).
> 
> A RIVAL IS A POLICY OVER BELIEFS, NEVER A READER OF TRUTH.
> 
> That is the architectural rule and it buys the fairness claim outright. A
> rival holds its own IBeliefStore, buys surveys through the same observation
> door, and bids from what it believes. There is no rival-specific data path,
> so there is nothing to audit for cheating — the architecture test that keeps

## Namespaces

- `OGSim.Company`

## Type declarations

- `L26` `public sealed record RivalPersonality(`
- `L33` `public sealed record Bid(EntityId<ICompany> Company, ContentId Block, Money Amount);`
- `L36` `public interface ICompany { }`
- `L41` `public sealed class Rival`
- `L140` `public static class LicenceRound`
- `L142` `public sealed record Award(ContentId Block, EntityId<ICompany> Winner, Money Price);`
- `L179` `public static class PublicDisclosure`

## Accessible members

- `L43` `private readonly RivalPersonality _personality;`
- `L44` `private readonly IBeliefStore _beliefs;`
- `L45` `private readonly IRandomStream _market;`
- `L47` `public Rival(`
- `L73` `public EntityId<ICompany> Id { get; }`
- `L75` `public ContentId Personality => _personality.Id;`
- `L85` `public bool HasTechnologyAt(Tick eraStart, Tick now) =>`
- `L100` `public Bid? BidFor(`
- `L127` `private static double Median(Belief belief) =>`
- `L148` `public static Award? Resolve(ContentId block, IReadOnlyList<Bid> bids)`
- `L189` `public static Observation Publish(Observation theirs, double extraSigma)`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

