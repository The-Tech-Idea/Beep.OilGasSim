# CapabilityContracts

Source: `src\OGSim.Contracts\CapabilityContracts.cs` · Lines: 70

## File intent

> SDD-005 — capabilities and gating. ICapabilitySet is TWO members wide,
> deliberately: tiers and envelopes are content gated BY these answers, which
> is what keeps every rejection explainable. The solver and models are
> capability-blind (architecture-tested).

## Namespaces

- `OGSim.Contracts`

## Type declarations

- `L10` `public readonly record struct TechnologyId(ContentId Value);`
- `L12` `public interface ICapabilitySet`
- `L22` `public sealed record ServiceRental(TechnologyId Capability, Money Premium);`
- `L24` `public sealed record EnvelopeCheck(EnvelopeKind Kind, double RequiredValue);`
- `L27` `public sealed record Requirements(`
- `L42` `public abstract record MissingItem;`
- `L43` `public sealed record MissingTechnology(TechnologyId Tech) : MissingItem;`
- `L44` `public sealed record MissingDetectTier(DetectClass Required, DetectClass Held) : MissingItem;`
- `L45` `public sealed record EnvelopeExceeded(EnvelopeKind Kind, double Required, double Effective) : MissingItem;`
- `L48` `public abstract record GateResult;`
- `L49` `public sealed record GatePass : GateResult;`
- `L50` `public sealed record GateFail(IReadOnlyList<MissingItem> Missing) : GateResult`
- `L63` `public interface IGatingValidator`

## Accessible members

- `L33` `public bool Equals(Requirements? other) =>`
- `L38` `public override int GetHashCode() =>`
- `L53` `public bool Equals(GateFail? other) =>`
- `L56` `public override int GetHashCode() => Structural.HashOf(Missing);`

## Imports

- `using OGSim.Kernel;`

