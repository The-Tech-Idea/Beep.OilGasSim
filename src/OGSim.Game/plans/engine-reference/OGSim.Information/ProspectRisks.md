# ProspectRisks

Source: `src\OGSim.Information\ProspectRisks.cs` · Lines: 113

## File intent

> R20d.7 — probability of success, per prospect (SDD-008 §4, design 06 §2.1–2.2).
> 
> POS HAD NO SUBJECT UNTIL THE WORLD MADE ONE. `ProspectRisk` was built,
> unit-tested and consumed by nothing for four phases, because a probability of
> success is a statement ABOUT A PROSPECT and nothing generated prospects. Now
> that a basin produces dozens, the question a player actually plays — which of
> these do I put the rig on? — is answerable, and this is what answers it.
> 

## Namespaces

- `OGSim.Information`

## Type declarations

- `L24` `public sealed class ProspectRisks`

## Accessible members

- `L28` `private readonly Dictionary<ContentId, ProspectRisk> _plays = [];`
- `L29` `private readonly Dictionary<EntityRef, ProspectRisk> _prospects = [];`
- `L30` `private readonly Dictionary<EntityRef, ContentId> _playOf = [];`
- `L31` `private readonly List<EntityRef> _order = [];`
- `L33` `private readonly FactorBelief _prior;`
- `L41` `public ProspectRisks(FactorBelief prior) => _prior = prior;`
- `L44` `public IReadOnlyList<EntityRef> Known => _order;`
- `L55` `public void Register(EntityRef prospect, ContentId play, double trapConfidence)`
- `L85` `public ProspectRisk Of(EntityRef prospect) => _prospects[prospect];`
- `L89` `public ContentId PlayOf(EntityRef prospect) => _playOf[prospect];`
- `L91` `public bool Knows(EntityRef prospect) => _prospects.ContainsKey(prospect);`
- `L97` `public void Drilled(EntityRef prospect, PosFactor factor, bool present) =>`
- `L104` `public void Learned(EntityRef prospect, PosFactor factor, bool present, double weight)`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

