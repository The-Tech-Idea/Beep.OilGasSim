// SDD-005 — capabilities and gating. ICapabilitySet is TWO members wide,
// deliberately: tiers and envelopes are content gated BY these answers, which
// is what keeps every rejection explainable. The solver and models are
// capability-blind (architecture-tested).

using OGSim.Kernel;

namespace OGSim.Contracts;

public readonly record struct TechnologyId(ContentId Value);

public interface ICapabilitySet
{
    bool Has(TechnologyId tech);
    DetectClass MaxDetectClass { get; }
}

/// <summary>
/// SDD-005 §2 — a rental is NOT a capability-set change: it is a field on the
/// operation, scoped to that operation only, never persisted beyond it.
/// </summary>
public sealed record ServiceRental(TechnologyId Capability, Money Premium);

public sealed record EnvelopeCheck(EnvelopeKind Kind, double RequiredValue);

/// <summary>Every gated thing declares one of these in content (SDD-005 §3).</summary>
public sealed record Requirements(
    IReadOnlyList<TechnologyId> Tech,
    DetectClass? MinDetectClass,
    IReadOnlyList<EnvelopeCheck> Envelopes)
{
    // Finding 131.
    public bool Equals(Requirements? other) =>
        other is not null && MinDetectClass == other.MinDetectClass
        && Structural.Equal(Tech, other.Tech)
        && Structural.Equal(Envelopes, other.Envelopes);

    public override int GetHashCode() =>
        HashCode.Combine(MinDetectClass, Structural.HashOf(Tech), Structural.HashOf(Envelopes));
}

public abstract record MissingItem;
public sealed record MissingTechnology(TechnologyId Tech) : MissingItem;
public sealed record MissingDetectTier(DetectClass Required, DetectClass Held) : MissingItem;
public sealed record EnvelopeExceeded(EnvelopeKind Kind, double Required, double Effective) : MissingItem;

/// <summary>ALL misses reported, never just the first (SDD-005 §3, the R3-V2 principle).</summary>
public abstract record GateResult;
public sealed record GatePass : GateResult;
public sealed record GateFail(IReadOnlyList<MissingItem> Missing) : GateResult
{
    // Finding 131.
    public bool Equals(GateFail? other) =>
        other is not null && Structural.Equal(Missing, other.Missing);

    public override int GetHashCode() => Structural.HashOf(Missing);
}

/// <summary>
/// The ONE gating validator every gated command uses (SDD-005 §3). Runs at
/// command validation / scheduling only — never at execution, never in the solver.
/// </summary>
public interface IGatingValidator
{
    GateResult Check(
        Requirements requirements,
        ICapabilitySet capabilities,
        IReadOnlyList<ServiceRental> rentals,
        IEffectState effects);   // envelope checks read effective values (SDD-005 §3-§4)
}
