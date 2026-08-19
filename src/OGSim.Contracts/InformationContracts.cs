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
/// <summary>
/// One decision a survey might inform, and what it is worth to inform it
/// (SDD-008 §7).
/// </summary>
public sealed record InformationValue(
    ContentId Source,
    EntityRef Subject,
    Money Cost,
    Money ExpectedValue)
{
    /// <summary>Worth buying when what it tells you is worth more than it costs.
    /// The whole exploration decision, and the reason a cheap survey over a
    /// hopeless prospect is still a bad buy.</summary>
    public bool WorthBuying => ExpectedValue > Cost;
}

/// <summary>
/// SDD-008 §7 — <c>VOI = E[max_a EV(a | posterior)] − max_a EV(a | prior)</c>.
///
/// <para><b>It consumes no random stream.</b> The 128 scenarios are drawn by a
/// Halton sequence, which is deterministic and reproducible: this is advisory
/// arithmetic, not world randomness, and a player opening the value-of-
/// information panel must not shift a single later draw. Pinned so replay is
/// untouched.</para>
///
/// <para><b>It is deliberately wrong when the player's beliefs are wrong.</b>
/// The expectation runs over the player's prior and their current economics, so
/// a company that misjudges a prospect also misjudges what learning about it is
/// worth — which is the honest answer and the interesting one.</para>
/// </summary>
public interface IInformationValueModel
{
    ContentId Id { get; }

    IReadOnlyList<InformationValue> Rank(
        IReadOnlyList<ContentId> availableSources,
        EntityRef subject,
        IBeliefStore beliefs);
}

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

/// <summary>
/// One belief and what it is about (SDD-008 §3).
///
/// <para>It carries the whole <see cref="Belief"/> rather than the key alone
/// because a caller handed a key it must then re-<c>Get</c> can be answered
/// <c>null</c> for a pair the store has just named — a state the type should not
/// be able to express.</para>
/// </summary>
public readonly record struct HeldBelief(
    EntityRef Subject,
    ContentId PropertyKind,
    Belief Belief);

public interface IBeliefStore
{
    /// <summary>The one conjugate update (SDD-008 §2.1).</summary>
    void Apply(Observation observation);
    Belief? Get(EntityRef subject, ContentId propertyKind);

    /// <summary>
    /// What <see cref="Get"/> cannot answer: WHICH pairs are known. Ordered by
    /// first learning, so two runs of one save project the same list in the same
    /// order (D-5).
    ///
    /// <para>Not a widening of what a player may see: a pair enters this list
    /// only through <see cref="Apply"/>, so it holds exactly what <c>Get</c>
    /// would already answer, one call at a time. An unobserved subject has no
    /// entry to find — which is why the stage-13 projection can walk it without
    /// the walk itself deciding what is knowable (SDD-008 §3's R20d.7
    /// amendment).</para>
    /// </summary>
    IReadOnlyList<HeldBelief> Held { get; }

    /// <summary>
    /// Moves everything known about one subject onto another (SDD-008 §4).
    ///
    /// <para>The moment a prospect becomes a field. A company that bought
    /// seismic over a structure and then drilled it does not stop knowing what
    /// it paid for — the belief follows the thing it was always about, with the
    /// same mean, the same sigma, the same provenance and the same as-of date,
    /// because nothing new was learned by the entity changing name.</para>
    ///
    /// <para>A MOVE. Copying would leave the prospect and the accumulation each
    /// answering for one fact, and an appraisal updating one would leave the
    /// other stale — law L5, in the information layer.</para>
    /// </summary>
    void ReKey(EntityRef from, EntityRef to);

    /// <summary>
    /// SDD-008 §2d's staleness: widen what is believed about one subject,
    /// because time has passed AND that subject has been produced from.
    ///
    /// <para>PER SUBJECT, which is §2d.2's decision in a signature. Drift is
    /// charged to production rather than to the calendar — a pressure belief
    /// goes stale because the pressure moved, and withdrawal is what moves it —
    /// so a shut-in compartment's beliefs hold while the one next to it being
    /// drained does not. Charging it to the clock would make WAITING a source of
    /// uncertainty and re-measuring on a timer the optimal play.</para>
    ///
    /// <para>On the CONTRACT rather than on the store alone: the stage that
    /// calls it takes this interface, and law L1 admits no concrete type as a
    /// dependency.</para>
    /// </summary>
    void Age(EntityRef subject, ContentId propertyKind, double driftPerYear, double years);
}

// ILicence moved to CompanyContracts.cs at R16, per SDD-011 §1's standing note:
// it is a company/licence type, not a belief one, and it only lived here because
// this was the file that existed when IWell.Licence first needed it.
