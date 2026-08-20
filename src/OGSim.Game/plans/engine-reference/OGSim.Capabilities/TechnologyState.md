# TechnologyState

Source: `src\OGSim.Capabilities\TechnologyState.cs` · Lines: 204

## File intent

> R17.1 / R17.3 / R17.6 — the company's capabilities (SDD-005 §2, design 07).
> 
> ICapabilitySet IS TWO MEMBERS WIDE, deliberately. Tiers and envelopes are not
> queried here — they are content gated BY these two answers, and keeping the
> interface this narrow is what keeps every rejection explainable: a refusal
> names a technology or a detect tier, never "requirements not met".
> 
> DIFFUSION IS A DATE, NOT AN EVENT. A node auto-grants at

## Namespaces

- `OGSim.Capabilities`

## Type declarations

- `L19` `public sealed record TechnologyNode(`
- `L46` `public sealed class TechnologyState : ICapabilitySet`
- `L199` `public sealed class AllCapabilities : ICapabilitySet`

## Accessible members

- `L29` `public bool Equals(TechnologyNode? other) =>`
- `L37` `public override int GetHashCode() =>`
- `L48` `private readonly Dictionary<TechnologyId, TechnologyNode> _graph = [];`
- `L49` `private readonly List<TechnologyId> _order = [];`
- `L50` `private readonly HashSet<TechnologyId> _acquired = [];`
- `L51` `private readonly List<TechnologyId> _acquiredOrder = [];`
- `L53` `public TechnologyState(IReadOnlyList<TechnologyNode> graph)`
- `L83` `public bool Has(TechnologyId tech) => _acquired.Contains(tech);`
- `L92` `public DetectClass MaxDetectClass`
- `L108` `public IReadOnlyList<TechnologyId> Acquired => _acquiredOrder;`
- `L118` `public void Acquire(TechnologyId tech, Era currentEra)`
- `L148` `public void ApplyDiffusion(Era currentEra, Tick eraStart, Tick now)`
- `L181` `public IReadOnlyList<Effect> ActiveEffects()`
- `L201` `public bool Has(TechnologyId tech) => true;`
- `L203` `public DetectClass MaxDetectClass => DetectClass.D3;`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

