// R14.2 / R14.3 — beliefs and the one update rule (SDD-008 §2–3).
//
// APPLY IS THE ONLY WRITER. There is deliberately no Set, no seed-from-truth and
// no bulk import: world generation delivers initial beliefs through this same
// door (R15-V10), so there is no belief-copy path for truth to leak down. The
// absence is the enforcement — a method that does not exist cannot be called by
// mistake, reached by reflection in a hurry, or added "just for the loader".
//
// ONE MECHANISM, ONE UPDATE. Every belief is Normal in its declared space:
// additive kinds Linear, multiplicative kinds Log — and a Log-space Normal IS
// the log-normal the design requires (05 §1.4), which makes every update the
// same conjugate formula rather than one per distribution family.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Information;

/// <summary>
/// SDD-008 §2. The store, and the wall.
/// </summary>
public sealed class BeliefStore : IBeliefStore, IStateOwner
{
    // ONE owner of the belief set (law L5), in the order the company learned
    // them; the dictionary is a lookup into it and holds no belief of its own.
    // Two collections both holding the key set is how they come to disagree.
    private readonly List<HeldBelief> _held = [];
    private readonly Dictionary<(EntityRef Subject, ContentId Kind), int> _at = [];

    private readonly IAuditTrail _audit;
    private readonly Func<ContentId, double> _sigmaFloorFor;
    private readonly Func<GameDate> _now;

    public BeliefStore(
        IAuditTrail audit,
        Func<ContentId, double> sigmaFloorFor,
        Func<GameDate> now)
    {
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(sigmaFloorFor);
        ArgumentNullException.ThrowIfNull(now);

        _audit = audit;
        _sigmaFloorFor = sigmaFloorFor;
        _now = now;
    }

    /// <summary>
    /// SDD-008 §2.1's Normal–Normal conjugate update — the ONE update rule.
    ///
    /// <para>Precision adds. That is the whole of it, and it is why a second
    /// mediocre survey still helps and why a core beats ten logs: information
    /// combines by inverse variance, not by recency or by argument.</para>
    /// </summary>
    public void Apply(Observation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        if (observation.Sigma <= 0.0 || !double.IsFinite(observation.Sigma))
            throw new ModelFault("SDD-008 §3", null,
                $"observation of {observation.PropertyKind.Value} declares sigma " +
                $"{Format(observation.Sigma)}; a source must state an HONEST uncertainty, " +
                "and zero would claim the reading is truth");

        if (!double.IsFinite(observation.Value))
            throw new ModelFault("SDD-008 §3", null,
                $"observation of {observation.PropertyKind.Value} is not finite");

        var key = (observation.Subject, observation.PropertyKind);
        bool known = _at.TryGetValue(key, out int at);

        Belief posterior = known
            ? Combine(_held[at].Belief, observation)
            : FromFirstObservation(observation);

        var entry = new HeldBelief(observation.Subject, observation.PropertyKind, posterior);

        if (known)
        {
            _held[at] = entry;
        }
        else
        {
            _at[key] = _held.Count;
            _held.Add(entry);
        }

        // The fairness record (design 09 §4.2): a player who suspects the
        // engine is cheating can read what was observed, with what sigma, and
        // check the posterior against the formula.
        _audit.Record(AuditCategory.BeliefUpdate, null, null, new Dictionary<string, AuditValue>
        {
            ["subject"] = new(observation.Subject.Kind.ToString()),
            ["kind"] = new(observation.PropertyKind.Value),
            ["value"] = new(Format(observation.Value)),
            ["sigma"] = new(Format(observation.Sigma)),
            ["source"] = new(observation.Source.ToString()),
            ["posteriorMu"] = new(Format(posterior.Mu)),
            ["posteriorSigma"] = new(Format(posterior.Sigma)),
        });
    }

    /// <summary>
    /// Null when nothing is known — deliberately, rather than a wide prior.
    ///
    /// <para>"We have never looked" and "we looked and learned little" are
    /// different states, and only the first should leave a map region
    /// unrendered. A default prior would make the map claim knowledge the
    /// company has not paid for.</para>
    /// </summary>
    public Belief? Get(EntityRef subject, ContentId propertyKind) =>
        _at.TryGetValue((subject, propertyKind), out int at) ? _held[at].Belief : null;

    /// <summary>
    /// SDD-008 §3's R20d.7 amendment — what <see cref="Get"/> cannot answer:
    /// which pairs are known, in the order they were first learned.
    ///
    /// <para>The list itself, not a copy: it is the store's own ordering and the
    /// caller holds it as <see cref="IReadOnlyList{T}"/>. Rebuilding it would
    /// allocate the whole belief set at every stage 13, which is once a month for
    /// forty years.</para>
    /// </summary>
    public IReadOnlyList<HeldBelief> Held => _held;

    /// <summary>SDD-008 §4's re-key, in place: the entry keeps its position in
    /// the learning order, because when the company learned a thing did not
    /// change when the thing was renamed.</summary>
    public void ReKey(EntityRef from, EntityRef to)
    {
        // Walked rather than indexed, because the store is keyed by (subject,
        // kind) and this moves every kind a subject has. In learning order, so
        // two runs agree (rule D-5).
        for (int i = 0; i < _held.Count; i++)
        {
            HeldBelief entry = _held[i];

            if (entry.Subject != from) continue;

            // A merge has no correct answer. Two beliefs about one fact cannot
            // be combined without deciding which evidence to discard, and
            // silently keeping either is the kind of loss nothing would ever
            // surface (SDD-008 §4).
            if (_at.ContainsKey((to, entry.PropertyKind)))
                throw new InvariantFault("SDD-008 §4", to,
                    $"{to.Kind}:{to.Value} already holds a belief about " +
                    $"'{entry.PropertyKind.Value}'; re-keying would have to merge two " +
                    "beliefs about one fact, and no combination of them is correct");

            _at.Remove((from, entry.PropertyKind));
            _at.Add((to, entry.PropertyKind), i);

            _held[i] = entry with { Subject = to };

            _audit.Record(
                AuditCategory.StateTransition, subject: to, cause: null,
                new Dictionary<string, AuditValue>(StringComparer.Ordinal)
                {
                    ["rekeyed-from"] = new($"{from.Kind}:{from.Value}"),
                    ["kind"] = new(entry.PropertyKind.Value),
                });
        }
    }

    /// <summary>
    /// SDD-008 §2 / §2d's staleness: sigma grows for DYNAMIC kinds, on the
    /// subject whose value actually moved.
    ///
    /// <para>Pressure and contacts drift because PRODUCTION changes them; the
    /// porosity of a rock does not become less certain by being ignored. Making
    /// everything drift would have the player re-log a well to learn what its
    /// core already told them.</para>
    ///
    /// <para>PER SUBJECT, and that is the whole of §2d.2's decision expressed in
    /// a signature. This took a kind alone and widened every belief of that kind
    /// wherever it was held, which cannot say "this compartment produced and that
    /// one did not" — so a shut-in field would have gone stale at the same rate
    /// as the one next to it that was being drained. Drift is charged to the
    /// activity that invalidates the belief, not to the calendar.</para>
    ///
    /// <para>A subject with nothing known about it is left alone rather than
    /// faulted: there is no belief to widen, which is a legitimate state and the
    /// common one for a compartment nobody has measured.</para>
    /// </summary>
    public void Age(EntityRef subject, ContentId propertyKind, double driftPerYear, double years)
    {
        if (driftPerYear < 0.0)
            throw new ModelFault("SDD-008 §2", subject,
                "staleness drift cannot be negative; a belief does not sharpen by waiting");

        if (driftPerYear == 0.0 || years <= 0.0) return;

        // Indexed rather than walked: the store already keys (subject, kind), so
        // the one entry this can touch is a lookup away. Walking would be a scan
        // of every belief in the game, once per producing compartment, once a
        // month, for forty years.
        if (!_at.TryGetValue((subject, propertyKind), out int at)) return;

        HeldBelief entry = _held[at];

        _held[at] = entry with
        {
            Belief = entry.Belief with { Sigma = entry.Belief.Sigma + (driftPerYear * years) },
        };
    }

    // ------------------------------------------------ the save (SDD-008 §4b)
    //
    // EVERYTHING IN THIS STORE WAS BOUGHT. A survey, a well test, a log, a core,
    // a dry hole that re-priced a play — each cost money and moved a number, and
    // none of it is recomputable from anything a reload has in hand. It was in no
    // save until R20d.12.10, so a reloaded company was solvent, drilled,
    // producing, and had forgotten every survey it had ever paid for
    // (finding 198).
    //
    // IMPLEMENTED EXPLICITLY, and that is the whole reason this can exist at all.
    // The header of this file says Apply is the only writer and that the ABSENCE
    // of a bulk import is the enforcement. A restore is a bulk import. Behind an
    // explicit implementation it is reachable only through a reference typed as
    // IStateOwner — which is what a state registry holds and what nothing else in
    // this engine does — so the surface every consumer sees is still Apply, Get,
    // Held, ReKey, Age, and the door stays shut for them (SDD-008 §4b.2).
    //
    // The source is a PRIOR BELIEF SET, never truth: what a save holds got there
    // through Apply, so restoring it cannot introduce a value the wall would have
    // stopped.

    StateKey IStateOwner.Key { get; } = new("information.beliefs");

    int IStateOwner.SchemaVersion => 1;

    /// <summary>Nothing has to be back before this is (SDD-013 §2b).</summary>
    IReadOnlyList<StateKey> IStateOwner.RestoreAfter => [];

    void IStateOwner.Capture(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteInt64("count", _held.Count);

        // THE WHOLE SET, not a suffix. SDD-010 §4c splits the world into what the
        // seed made and what the game decided, and the same split cannot be drawn
        // here: a belief is updated IN PLACE, so a survey over a generated
        // prospect rewrites the entry generation created rather than appending
        // one. The learning happens inside the generated region, which is exactly
        // what a boundary cannot express (SDD-008 §4b.1).
        for (int i = 0; i < _held.Count; i++)
        {
            string at = Prefix(i);
            HeldBelief entry = _held[i];

            writer.WriteInt64(at + "subject-kind", (long)entry.Subject.Kind);
            writer.WriteInt64(at + "subject-id", (long)entry.Subject.Value);
            writer.WriteString(at + "kind", entry.PropertyKind.Value);

            writer.WriteDouble(at + "mu", entry.Belief.Mu);
            writer.WriteDouble(at + "sigma", entry.Belief.Sigma);
            writer.WriteInt64(at + "space", (long)entry.Belief.Space);

            // The BEST contributor, which is what the belief carries — a cheap
            // pass after an expensive core does not demote it (§2.1).
            writer.WriteInt64(at + "source", (long)entry.Belief.BestSource);

            writer.WriteInt64(at + "as-of-year", entry.Belief.AsOf.Year);
            writer.WriteInt64(at + "as-of-month", entry.Belief.AsOf.Month);
        }
    }

    /// <summary>
    /// SDD-008 §4b.2. Written VERBATIM rather than replayed through
    /// <see cref="Apply"/>: replaying would re-floor sigma, re-combine against
    /// whatever stood there, and re-stamp the as-of date with the load date —
    /// three silent corruptions of a number the player paid for.
    /// </summary>
    void IStateOwner.Restore(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        // REPLACES. A restore is the belief set, not an addition to one: when a
        // load regenerates a world, generation's initial beliefs are applied
        // first and this overwrites them, correctly, because the save's set
        // already contains them plus everything learned since (§4b.1).
        _held.Clear();
        _at.Clear();

        long count = reader.ReadInt64("count");

        for (long i = 0; i < count; i++)
        {
            string at = Prefix(i);

            var subject = new EntityRef(
                Kind(reader.ReadInt64(at + "subject-kind")),
                (ulong)reader.ReadInt64(at + "subject-id"));

            var propertyKind = new ContentId(reader.ReadString(at + "kind"));

            var belief = new Belief(
                reader.ReadDouble(at + "mu"),
                reader.ReadDouble(at + "sigma"),
                Space(reader.ReadInt64(at + "space")),
                Source(reader.ReadInt64(at + "source")),

                // GameDate validates its own month, so a corrupt one is refused
                // here rather than surfacing forty ticks later as a belief that
                // has been stale since month zero.
                new GameDate(
                    (int)reader.ReadInt64(at + "as-of-year"),
                    (int)reader.ReadInt64(at + "as-of-month")));

            // Two entries for one pair would leave the lookup pointing at one of
            // them and the list holding both, which is the disagreement the
            // header of this file keeps the two collections aligned to prevent.
            if (!_at.TryAdd((subject, propertyKind), _held.Count))
                throw new SaveDataFault("SDD-008 §4b.3", subject,
                    $"the save holds two beliefs about '{propertyKind.Value}' for " +
                    $"{subject.Kind}:{subject.Value}; one fact has one belief, and no " +
                    "combination of two is correct");

            _held.Add(new HeldBelief(subject, propertyKind, belief));
        }
    }

    // Every enum crosses as its declared value and is CHECKED coming back. A
    // straight cast of an out-of-range integer produces an enum with no name,
    // which compares unequal to every member and reads as a valid value
    // everywhere downstream — a corruption that surfaces as a belief nobody can
    // explain rather than as a refused load.

    private static EntityKind Kind(long value) =>
        Enum.IsDefined((EntityKind)value)
            ? (EntityKind)value
            : throw new SaveDataFault("SDD-008 §4b.3", null,
                $"the save names entity kind {value}, which this build does not declare");

    private static BeliefSpace Space(long value) =>
        Enum.IsDefined((BeliefSpace)value)
            ? (BeliefSpace)value
            : throw new SaveDataFault("SDD-008 §4b.3", null,
                $"the save names belief space {value}, which this build does not declare");

    private static Provenance Source(long value) =>
        Enum.IsDefined((Provenance)value)
            ? (Provenance)value
            : throw new SaveDataFault("SDD-008 §4b.3", null,
                $"the save names provenance {value}, which this build does not declare");

    private static string Prefix(long index) =>
        "belief." + index.ToString("D6", System.Globalization.CultureInfo.InvariantCulture) + ".";

    private Belief FromFirstObservation(Observation observation) =>
        new(observation.Value,
            FlooredSigma(observation.PropertyKind, observation.Sigma, observation.Source),
            observation.Space,
            observation.Source,
            _now());

    private Belief Combine(Belief prior, Observation observation)
    {
        if (prior.Space != observation.Space)
            throw new InvariantFault("SDD-008 §2", null,
                $"belief for {observation.PropertyKind.Value} is in {prior.Space} space but the " +
                $"observation is in {observation.Space}. A kind's space is declared once, in " +
                "content, and combining across spaces would average a log with a linear");

        double priorPrecision = 1.0 / (prior.Sigma * prior.Sigma);
        double observedPrecision = 1.0 / (observation.Sigma * observation.Sigma);
        double posteriorPrecision = priorPrecision + observedPrecision;

        double mu = (prior.Mu * priorPrecision + observation.Value * observedPrecision)
                  / posteriorPrecision;

        double sigma = DetMath.Sqrt(1.0 / posteriorPrecision);

        // Provenance is the BEST contributor, not the latest: a cheap seismic
        // pass after an expensive core does not make the belief seismic-grade,
        // and the player-facing "how do we know this?" would be a lie if it did.
        Provenance best = observation.Source > prior.BestSource
            ? observation.Source
            : prior.BestSource;

        return new Belief(
            mu,
            FlooredSigma(observation.PropertyKind, sigma, best),
            prior.Space,
            best,
            _now());
    }

    /// <summary>
    /// INV8. Sigma has a per-kind floor UNLESS the provenance is Measured.
    ///
    /// <para>Without it, repeated observations drive sigma toward zero and the
    /// player ends up certain of a reservoir property nobody can be certain of.
    /// Measured is exempt because a custody meter's reading IS the quantity —
    /// there is no hidden truth left to be uncertain about.</para>
    /// </summary>
    private double FlooredSigma(ContentId kind, double sigma, Provenance source)
    {
        if (source == Provenance.Measured) return sigma;

        double floor = _sigmaFloorFor(kind);
        if (floor < 0.0)
            throw new ModelFault("INV8", null,
                $"the sigma floor for {kind.Value} is negative");

        return Math.Max(sigma, floor);
    }

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// SDD-008 §2's closed-form quantiles, and R14.8's P10/P50/P90.
///
/// <para>Petroleum convention throughout: <b>P90 is the LOW case and P10 the
/// high</b> — the probability of exceeding. Reading them the statistical way
/// round would book possible reserves as proved, which is why the convention is
/// pinned on the contract rather than left to the caller.</para>
/// </summary>
public static class Quantiles
{
    public static double P50(Belief belief) => Transform(belief, belief.Mu);

    /// <summary>The LOW case — 90% chance of at least this.</summary>
    public static double P90(Belief belief) =>
        Transform(belief, belief.Mu - PhysicalConstants.NormalZ10 * belief.Sigma);

    /// <summary>The HIGH case — only a 10% chance of at least this.</summary>
    public static double P10(Belief belief) =>
        Transform(belief, belief.Mu + PhysicalConstants.NormalZ10 * belief.Sigma);

    private static double Transform(Belief belief, double value) =>
        belief.Space == BeliefSpace.Log ? DetMath.Exp(value) : value;
}
