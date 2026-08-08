// R14.3 / R14.7 — observation sampling and POS (SDD-008 §3–4).
//
// SOURCES NEVER RETURN TRUTH AND NEVER RETURN BIAS. Sigma honest, centre honest.
// What distinguishes a core from a log is WHICH KINDS it can see and HOW SMALL
// its sigma is — not a fudge applied to the answer. That is what makes the
// player's uncertainty real rather than theatrical, and what makes a survey
// worth paying for rather than a formality.
//
// The DRAW stays in the engine and the MODEL owns only sigma. A plugin that drew
// its own numbers could silently consume a different count from a named stream
// and shift every later draw in it — the exact independence property R1-V5
// exists to protect.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Information;

/// <summary>
/// SDD-008 §3's sampler. The one door truth passes through, narrowed to a value
/// and an honest sigma.
/// </summary>
public sealed class ObservationSampler
{
    private readonly IObservationModel _model;
    private readonly IRandomStream _exploration;
    private readonly IRandomStream _measurement;
    private readonly IAuditTrail _audit;

    public ObservationSampler(
        IObservationModel model,
        IRandomStream explorationStream,
        IRandomStream measurementStream,
        IAuditTrail audit)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(explorationStream);
        ArgumentNullException.ThrowIfNull(measurementStream);
        ArgumentNullException.ThrowIfNull(audit);

        _model = model;
        _exploration = explorationStream;
        _measurement = measurementStream;
        _audit = audit;
    }

    /// <summary>
    /// Samples one property of one subject.
    ///
    /// <para>Returns null when the source cannot see the kind — the detectability
    /// gate yielding NOTHING is a legitimate outcome, and is not the same as a
    /// wide sigma. A survey below the trap's detect class spawns no lead at all;
    /// giving it a vague answer instead would tell the player something was
    /// there.</para>
    ///
    /// <para><paramref name="truthValue"/> is already in the kind's belief
    /// space. It arrives as a bare double from inside the truth boundary and
    /// leaves as an <see cref="Observation"/> — this method IS the wall.</para>
    /// </summary>
    public Observation? Sample(
        ContentId source,
        ContentId propertyKind,
        EntityRef subject,
        double truthValue,
        BeliefSpace space,
        Provenance provenance)
    {
        if (_model.SigmaFor(source, propertyKind, subject) is not double sigma) return null;

        if (sigma <= 0.0 || !double.IsFinite(sigma))
            throw new ModelFault("SDD-008 §3", null,
                $"observation model returned sigma {Format(sigma)} for {source.Value} reading " +
                $"{propertyKind.Value}; a source that claims zero error is claiming to be truth");

        // Surveys draw from `exploration`, logs and meters from `measurement`.
        // Separate streams so that adding a survey to a campaign cannot shift
        // the noise on a well test taken later (R1-V5).
        IRandomStream stream = provenance is Provenance.Seismic or Provenance.Analogue
            ? _exploration
            : _measurement;

        double noise = stream.NextNormal() * sigma;
        double observed = truthValue + noise;

        // The fairness record: source, kind, sigma and the draw itself. A player
        // who suspects the dice can reconstruct the reading from the truth they
        // eventually learn (design 09 §4.2).
        _audit.Record(AuditCategory.StochasticOutcome, null, null,
            new Dictionary<string, AuditValue>
            {
                ["source"] = new(source.Value),
                ["kind"] = new(propertyKind.Value),
                ["sigma"] = new(Format(sigma)),
                ["draw"] = new(Format(noise / sigma)),
                ["stream"] = new(provenance is Provenance.Seismic or Provenance.Analogue
                    ? "exploration" : "measurement"),
            });

        return new Observation(subject, propertyKind, observed, sigma, space, provenance);
    }

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// R14.7 / SDD-008 §4 — probability of success, Beta-Bernoulli per factor.
///
/// <para>Five factors, and POS is the PRODUCT of their means. That multiplication
/// is why exploration is hard: five plausible-looking factors at 0.7 each give a
/// one-in-six chance, and a player who reasons factor by factor will consistently
/// over-estimate their odds — which is the correct lesson rather than a
/// punishment.</para>
/// </summary>
public sealed class ProspectRisk
{
    private readonly Dictionary<PosFactor, FactorBelief> _factors = [];

    /// <summary>Declared order, so the product is formed identically every run
    /// and the audit reads the same way twice (D-5).</summary>
    private static readonly PosFactor[] Factors =
    [
        PosFactor.Source, PosFactor.Reservoir, PosFactor.Seal,
        PosFactor.Trap, PosFactor.Timing,
    ];

    // Which factors belong to the play rather than to this prospect. Membership
    // is only ever asked, never enumerated (rule D-5).
    private readonly HashSet<PosFactor> _shared = [];

    private ProspectRisk? _play;

    public ProspectRisk(FactorBelief prior)
    {
        Validate(prior);
        foreach (PosFactor factor in Factors) _factors[factor] = prior;
    }

    /// <summary>
    /// The belief about one factor — READ THROUGH to the play when this prospect
    /// shares it, so a prospect never holds a stale copy of a number the play
    /// has since moved.
    /// </summary>
    public FactorBelief this[PosFactor factor] =>
        _shared.Contains(factor) ? _play![factor] : _factors[factor];

    /// <summary>Mean of a Beta: <c>α/(α+β)</c>.</summary>
    public static double MeanOf(FactorBelief belief) =>
        belief.Alpha / (belief.Alpha + belief.Beta);

    /// <summary>The product of the five means (SDD-008 §4).</summary>
    public double ProbabilityOfSuccess
    {
        get
        {
            double product = 1.0;
            for (int i = 0; i < Factors.Length; i++) product *= MeanOf(this[Factors[i]]);
            return product;
        }
    }

    /// <summary>
    /// Beta-Bernoulli: a success adds to α, a failure to β.
    ///
    /// <para>Conjugate, so evidence accumulates without a fitting step, and the
    /// prior's magnitude is exactly "how many wells' worth of conviction we
    /// started with" — which is a quantity a geologist can argue about.</para>
    /// </summary>
    public void Observe(PosFactor factor, bool present)
    {
        // A well that proves or disproves a SHARED element has said something
        // about the play, not about this prospect — so the evidence is recorded
        // where the belief lives. Writing it here instead would make a dry hole
        // on source rock inform only the prospect that drilled it, which is the
        // opposite of what a shared factor means.
        if (_shared.Contains(factor))
        {
            _play!.Observe(factor, present);
            return;
        }

        FactorBelief current = _factors[factor];

        _factors[factor] = present
            ? current with { Alpha = current.Alpha + 1.0 }
            : current with { Beta = current.Beta + 1.0 };
    }

    /// <summary>
    /// R14.10's play correlation, in one line: factors SHARED between prospects
    /// are the same Beta.
    ///
    /// <para>A dry hole that fails on source rock informs every prospect in the
    /// play, because they were never independent — and that is the whole content
    /// of "the play died". Correlating the OUTCOMES instead would have needed a
    /// covariance nobody could state.</para>
    /// </summary>
    public void ShareFrom(ProspectRisk play, PosFactor factor)
    {
        ArgumentNullException.ThrowIfNull(play);

        // BOUND, not copied. This was a copy, and the difference is the whole
        // mechanism: a copy means the play moves and every prospect keeps the
        // number it had until someone remembers to re-sync. Nothing enforced
        // that, no caller did it, and R14-V10 passed only because the test
        // performed the refresh itself — the correlation was being demonstrated
        // by hand rather than by the code.
        if (_play is not null && !ReferenceEquals(_play, play))
            throw new ModelFault("SDD-008 §4", null,
                "a prospect belongs to one play; sharing factors from a second would " +
                "leave its risk depending on which call came last");

        _play = play;
        _shared.Add(factor);
    }

    /// <summary>
    /// Re-weights one factor to a given mean WITHOUT changing how much evidence
    /// stands behind it — α+β is preserved, so a prospect mapped with less
    /// confidence starts lower but is moved by the first well exactly as far as
    /// a confident one would be.
    /// </summary>
    public void Weigh(PosFactor factor, double mean)
    {
        if (mean is <= 0.0 or > 1.0)
            throw new ModelFault("SDD-008 §4", null,
                "a factor's mean is a probability in (0, 1]; zero could never be moved " +
                "off zero by any amount of evidence");

        if (_shared.Contains(factor))
            throw new ModelFault("SDD-008 §4", null,
                $"factor {factor} belongs to the play; re-weighting it here would let one " +
                "prospect quietly restate what the whole play believes");

        FactorBelief current = _factors[factor];
        double evidence = current.Alpha + current.Beta;

        _factors[factor] = new FactorBelief(evidence * mean, evidence * (1.0 - mean));
    }

    private static void Validate(FactorBelief belief)
    {
        if (belief.Alpha <= 0.0 || belief.Beta <= 0.0)
            throw new ModelFault("SDD-008 §4", null,
                "a Beta prior needs positive alpha and beta; zero would make the mean " +
                "undefined and no amount of evidence could move it");
    }
}
