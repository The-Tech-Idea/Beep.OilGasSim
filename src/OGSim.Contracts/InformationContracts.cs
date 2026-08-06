// SDD-008 — beliefs. One conjugate update rule, Normal in a declared space;
// POS as Beta-Bernoulli with the play-shared factors AS the correlation.
// Truth is unreachable from here: only observation deliveries cross the wall.

using OGSim.Kernel;

namespace OGSim.Contracts;

public enum BeliefSpace { Linear, Log }

/// <summary>Design 02 §1.2 ordering — ProductionHistory near the top: dynamic data is the most trustworthy.</summary>
public enum Provenance
{
    Assumed, Analogue, Seismic, Log, WellTest, Core, ProductionHistory, Measured
}

/// <summary>Every belief is Normal in its declared space (SDD-008 §2). Quantiles closed-form.</summary>
public readonly record struct Belief(
    double Mu,
    double Sigma,
    BeliefSpace Space,
    Provenance BestSource,
    GameDate AsOf);

/// <summary>Beta(α, β): mean = α/(α+β). The play-shared Beta IS the play correlation (SDD-008 §4).</summary>
public readonly record struct FactorBelief(double Alpha, double Beta);

/// <summary>The five petroleum-system factors (design 06 §2.2). POS = product of means.</summary>
public enum PosFactor { Source, Reservoir, Seal, Trap, Timing }

/// <summary>
/// The ONLY shape that crosses the truth wall (SDD-008 §3): a sampled value
/// with an honest sigma — never truth itself. Audited on delivery.
/// </summary>
public sealed record Observation(
    EntityRef Subject,
    ContentId PropertyKind,
    double Value,
    double Sigma,
    BeliefSpace Space,
    Provenance Source);

public interface IBeliefStore
{
    /// <summary>The one conjugate update (SDD-008 §2.1).</summary>
    void Apply(Observation observation);
    Belief? Get(EntityRef subject, ContentId propertyKind);
}

public interface ILicence
{
    EntityId<ILicence> Id { get; }
    ContentId FiscalRegime { get; }
    Tick Expiry { get; }
}
