// SDD-008 — beliefs. One conjugate update rule, Normal in a declared space;
// POS as Beta-Bernoulli with the play-shared factors AS the correlation.
// Truth is unreachable from here: only observation deliveries cross the wall.

using OGSim.Kernel;

namespace OGSim.Contracts;

// BeliefSpace and Provenance moved to OGSim.Kernel/Provenance.cs at R2.1:
// IProperty needs them and R2 runs eleven phases before R14, so they cannot
// live in an assembly the material layer is below. They are vocabulary, not
// belief state.

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

/// <summary>
/// Design 03 §3.2 — "per source; tunes how much uncertainty survives". The third
/// replaceable slot the eight contract passes missed (finding 82): SDD-008 §3's
/// sampling algorithm had no contract to sit behind.
///
/// The model owns SIGMA and deliberately not the draw. The draw consumes a named
/// RNG stream (`exploration` or `measurement`) and must stay in the engine where
/// the stream and the audit record live: a plugin drawing its own numbers could
/// consume a different count and shift every later draw in that stream, which is
/// exactly the independence property R1-V5 exists to protect.
/// </summary>
public interface IObservationModel
{
    ContentId Id { get; }

    /// <summary>
    /// The honest sigma for this source reading this kind of this subject.
    /// Null means the source cannot see the kind at all — absence is a
    /// legitimate answer, and is NOT the same as a very wide sigma.
    /// </summary>
    double? SigmaFor(ContentId source, ContentId propertyKind, EntityRef subject);
}

public interface IBeliefStore
{
    /// <summary>The one conjugate update (SDD-008 §2.1).</summary>
    void Apply(Observation observation);
    Belief? Get(EntityRef subject, ContentId propertyKind);
}

// ILicence moved to CompanyContracts.cs at R16, per SDD-011 §1's standing note:
// it is a company/licence type, not a belief one, and it only lived here because
// this was the file that existed when IWell.Licence first needed it.
