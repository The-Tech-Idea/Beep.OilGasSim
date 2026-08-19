# Gating

Source: `src\OGSim.Capabilities\Gating.cs` · Lines: 215

## File intent

> R17.2 / R17.7 — the one gating validator and the shared effect path
> (SDD-005 §3–4, design 07).
> 
> EXACTLY ONE VALIDATOR evaluates every gated thing: operation template,
> equipment tier, well command. One implementation means one place where a
> refusal is worded, one place where rentals are honoured, and one thing to
> point an architecture test at.
> 

## Namespaces

- `OGSim.Capabilities`

## Type declarations

- `L25` `public sealed class GatingValidator : IGatingValidator`
- `L97` `public sealed class EffectState : IEffectState`

## Accessible members

- `L27` `public GateResult Check(`
- `L75` `private static bool IsRented(IReadOnlyList<ServiceRental> rentals, TechnologyId tech)`
- `L99` `private readonly Dictionary<EnvelopeKind, double> _base = [];`
- `L100` `private readonly Dictionary<EnvelopeKind, double> _extensions = [];`
- `L101` `private readonly Dictionary<EnvelopeKind, double> _restrictions = [];`
- `L102` `private readonly Dictionary<ModelSlot, ContentId> _plugins = [];`
- `L103` `private readonly Dictionary<(ModelSlot, ParameterKey), double> _parameters = [];`
- `L104` `private readonly HashSet<ContentId> _unlocked = [];`
- `L105` `private readonly List<ContentId> _unlockedOrder = [];`
- `L107` `public EffectState(IReadOnlyDictionary<EnvelopeKind, double> baseEnvelopes)`
- `L120` `public void Apply(IReadOnlyList<Effect> effects)`
- `L156` `private void Move(MoveEnvelope move)`
- `L178` `public double EffectiveEnvelope(EnvelopeKind kind)`
- `L187` `public ContentId SelectedPlugin(ModelSlot slot) =>`
- `L194` `public double Parameter(ModelSlot slot, ParameterKey key) =>`
- `L203` `public bool IsUnlocked(ContentId what) => _unlocked.Contains(what);`
- `L205` `public IReadOnlyList<ContentId> Unlocked => _unlockedOrder;`
- `L208` `private static readonly EnvelopeKind[] AllKinds =`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

