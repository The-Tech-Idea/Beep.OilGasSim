// R23.1 / R23.2 / R23.4 — the bow-tie (SDD-012 §4b, design 14 §2).
//
// THREATS → PREVENTIVE BARRIERS → TOP EVENT → MITIGATING BARRIERS →
// CONSEQUENCES. It is how the industry actually reasons, it gives the player
// things to BUY AND MAINTAIN rather than a probability to endure, and it
// produces a fat tail they control — which is what makes safety investment feel
// like the insurance it is.
//
// BARRIER CONDITION IS EQUIPMENT CONDITION. There is no parallel "safety stat":
// a barrier's strength is the state of the physical equipment, plus competency
// and procedural compliance, all of which already exist from R12 and R18. A
// separate safety number would be a second representation of one fact — law L5
// forbids it — and it would drift from the plant.
//
// NEAR MISSES COME FREE. A threat that passes some barriers but not all is a
// near miss naming exactly which ones failed. It costs nothing beyond the draws
// already made, and it turns the barrier model from a hidden calculation into a
// readable signal pointing at what is weakening.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Integrity;

/// <summary>
/// One barrier. Its strength is DERIVED — never stored (INV10).
///
/// <para>Weakest-link <c>min</c>, which is the safety doctrine rather than a
/// modelling convenience: a barrier is only as good as its worst element, and
/// averaging would let a well-maintained valve hide an untrained crew.</para>
/// </summary>
public sealed record Barrier(
    ContentId Id,
    IReadOnlyList<EntityId<IFlowElement>> Elements,
    bool IsPreventive)
{
    // Finding 131.
    public bool Equals(Barrier? other) =>
        other is not null && Id == other.Id && IsPreventive == other.IsPreventive
        && Structural.Equal(Elements, other.Elements);

    public override int GetHashCode() =>
        HashCode.Combine(Id, IsPreventive, Structural.HashOf(Elements));

    public double StrengthGiven(
        Func<EntityId<IFlowElement>, double> conditionOf,
        double crewCompetency,
        double procedureCompliance)
    {
        ArgumentNullException.ThrowIfNull(conditionOf);

        Validate(crewCompetency, nameof(crewCompetency));
        Validate(procedureCompliance, nameof(procedureCompliance));

        double weakest = 1.0;
        for (int i = 0; i < Elements.Count; i++)
        {
            double condition = conditionOf(Elements[i]);
            Validate(condition, $"condition of element {Elements[i].Value}");

            if (condition < weakest) weakest = condition;
        }

        return Math.Min(weakest, Math.Min(crewCompetency, procedureCompliance));
    }

    private void Validate(double value, string name)
    {
        if (value is < 0.0 or > 1.0 || double.IsNaN(value))
            throw new ModelFault("SDD-012 §4b", null,
                $"barrier {Id.Value}: {name} is " +
                value.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                ", not a strength in [0, 1]");
    }
}

/// <summary>SDD-012 §4b's three outcomes.</summary>
public enum ThreatOutcome
{
    /// <summary>Every preventive barrier held. A leading indicator, queryable
    /// but unshown by default — the player is not told how often nothing
    /// happened, only that it did not.</summary>
    Suppressed,

    /// <summary>Some barriers failed and some held. THE FREE WARNING.</summary>
    NearMiss,

    /// <summary>All preventive barriers failed. Loss of containment.</summary>
    TopEvent,
}

/// <summary>What the bow-tie decided about one materialised threat.</summary>
public sealed record ThreatResolution(
    ContentId Threat,
    ThreatOutcome Outcome,
    IReadOnlyList<ContentId> FailedBarriers,
    int MitigatingBarriersHeld)
{
    // Finding 131.
    public bool Equals(ThreatResolution? other) =>
        other is not null && Threat == other.Threat && Outcome == other.Outcome
        && MitigatingBarriersHeld == other.MitigatingBarriersHeld
        && Structural.Equal(FailedBarriers, other.FailedBarriers);

    public override int GetHashCode() =>
        HashCode.Combine(Threat, Outcome, MitigatingBarriersHeld,
                         Structural.HashOf(FailedBarriers));
}

/// <summary>
/// SDD-012 §4b's stage-4 threat resolution.
/// </summary>
public sealed class BowTie
{
    private readonly IRandomStream _hazard;
    private readonly IAuditTrail _audit;

    public BowTie(IRandomStream hazardStream, IAuditTrail audit)
    {
        ArgumentNullException.ThrowIfNull(hazardStream);
        ArgumentNullException.ThrowIfNull(audit);

        _hazard = hazardStream;
        _audit = audit;
    }

    /// <summary>
    /// Resolves one materialised threat.
    ///
    /// <para>Barriers are drawn in DECLARED ORDER and every preventive barrier
    /// is sampled — including after one has already failed. That costs a draw
    /// and buys the near-miss report: knowing which barriers failed is the whole
    /// value of the mechanic, and short-circuiting on the first failure would
    /// throw it away to save arithmetic nobody is short of.</para>
    ///
    /// <para>Barrier independence is a stated simplification (design 14): the
    /// bow-tie carries the decision, not the correlation structure.</para>
    /// </summary>
    /// <summary>Whether a threat materialises this tick (SDD-012 §4). One draw,
    /// from the `hazard` stream, before any barrier is sampled.</summary>
    public bool Materialises(double ratePerTick)
    {
        if (!double.IsFinite(ratePerTick) || ratePerTick <= 0.0 || ratePerTick > 1.0)
            throw new ModelFault("SDD-012 §4", null,
                $"a threat rate is a probability in (0, 1]; got {ratePerTick}");

        return _hazard.NextUnit() < ratePerTick;
    }

    public ThreatResolution Resolve(
        ContentId threat,
        IReadOnlyList<Barrier> barriers,
        Func<Barrier, double> strengthOf)
    {
        ArgumentNullException.ThrowIfNull(barriers);
        ArgumentNullException.ThrowIfNull(strengthOf);

        var failed = new List<ContentId>();
        int preventive = 0;

        for (int i = 0; i < barriers.Count; i++)
        {
            Barrier barrier = barriers[i];
            if (!barrier.IsPreventive) continue;

            preventive++;

            // A barrier HOLDS with probability equal to its strength.
            double draw = _hazard.NextUnit();
            if (draw >= strengthOf(barrier)) failed.Add(barrier.Id);
        }

        if (preventive == 0)
            throw new ModelFault("SDD-012 §4b", null,
                $"threat {threat.Value} has no preventive barriers; a threat nothing " +
                "stands against is not modelled by a bow-tie");

        ThreatOutcome outcome = failed.Count switch
        {
            0 => ThreatOutcome.Suppressed,
            _ when failed.Count == preventive => ThreatOutcome.TopEvent,
            _ => ThreatOutcome.NearMiss,
        };

        int mitigatingHeld = 0;
        if (outcome == ThreatOutcome.TopEvent)
            mitigatingHeld = SampleMitigating(barriers, strengthOf);

        Audit(threat, outcome, failed, preventive, mitigatingHeld);

        return new ThreatResolution(threat, outcome, failed, mitigatingHeld);
    }

    /// <summary>
    /// Mitigating barriers are sampled only on a TOP EVENT, and how many hold
    /// selects the consequence tier (design 14 §8).
    ///
    /// <para>Sampled only then, deliberately: the emergency shutdown is not
    /// tested on a day nothing happened, and drawing for it anyway would consume
    /// stream values on every suppressed threat and make the sequence depend on
    /// how many quiet days preceded a bad one.</para>
    /// </summary>
    private int SampleMitigating(IReadOnlyList<Barrier> barriers, Func<Barrier, double> strengthOf)
    {
        int held = 0;

        for (int i = 0; i < barriers.Count; i++)
        {
            if (barriers[i].IsPreventive) continue;

            if (_hazard.NextUnit() < strengthOf(barriers[i])) held++;
        }

        return held;
    }

    private void Audit(
        ContentId threat, ThreatOutcome outcome,
        IReadOnlyList<ContentId> failed, int preventive, int mitigatingHeld)
    {
        var data = new Dictionary<string, AuditValue>
        {
            ["threat"] = new(threat.Value),
            ["stream"] = new("hazard"),
            ["outcome"] = new(outcome.ToString()),
            ["preventiveBarriers"] = new(Format(preventive)),
            ["failedBarriers"] = new(Format(failed.Count)),
            ["mitigatingHeld"] = new(Format(mitigatingHeld)),
        };

        // The failed barriers BY NAME. That is what makes a near miss point at
        // something the player can act on rather than merely worry about.
        for (int i = 0; i < failed.Count; i++)
            data[$"failed{i}"] = new(failed[i].Value);

        _audit.Record(AuditCategory.StochasticOutcome, null, null, data);
    }

    private static string Format(int value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// R23.7 / SDD-012 §4b's ESG standing (0–100).
///
/// <para><c>standing = 100 − Σ w·bandScore(intensity) − incidentPoints</c>, with
/// incident points DECAYING on a content half-life — so a clean decade genuinely
/// rehabilitates. Without the decay, one bad year would price a company's debt
/// forever and the loop would have no exit, which design rule CI4 forbids.</para>
/// </summary>
public sealed class EsgStanding : IStateOwner
{
    private readonly double _halfLifeTicks;
    private double _incidentPoints;

    // THE INTENSITY WINDOW (SDD-012 §4b's R23.1 amendment). Kilograms and cubic
    // metres rather than the quantity types, because these are not measurements
    // of anything — they are an exponentially weighted accumulator whose units
    // survive only in the ratio. `WindowedFlared`/`WindowedProduced` put them
    // back into the type system at the boundary.
    private double _flaredWindow;
    private double _producedWindow;

    public EsgStanding(double halfLifeTicks)
    {
        if (halfLifeTicks <= 0.0)
            throw new ModelFault("SDD-012 §4b", null,
                "the incident half-life must be positive; zero would forgive everything " +
                "instantly and infinity would forgive nothing ever");

        _halfLifeTicks = halfLifeTicks;
    }

    public double IncidentPoints => _incidentPoints;

    /// <summary>What the record currently remembers burning, aged.</summary>
    public Mass WindowedFlared => new(_flaredWindow);

    /// <summary>And what it remembers producing over the same window, so the two
    /// divide into an intensity the band table can score.</summary>
    public SurfaceVolume WindowedProduced => new(_producedWindow);

    /// <summary>
    /// One month's flaring and production enter the window (SDD-012 §4b's R23.1
    /// amendment).
    ///
    /// <para>The declared order within a tick is OBSERVE (stage 8) then AGE
    /// (stage 9): the month lands in the window, the lender prices against a
    /// record that includes it, and the whole window — the new month with the
    /// rest — then ages by one tick. Numerator and denominator move together at
    /// every step, which is what keeps this a ratio rather than two series.</para>
    /// </summary>
    public void Observe(Mass flared, SurfaceVolume produced)
    {
        if (flared.Kilograms < 0.0 || produced.CubicMetres < 0.0)
            throw new ModelFault("SDD-012 §4b", null,
                "a month cannot un-burn gas or un-produce oil");

        _flaredWindow += flared.Kilograms;
        _producedWindow += produced.CubicMetres;
    }

    /// <summary>An incident adds points. They are what a clean record has to work off.</summary>
    public void RecordIncident(double points)
    {
        if (points < 0.0)
            throw new ModelFault("SDD-012 §4b", null,
                "an incident cannot improve the record");

        _incidentPoints += points;
    }

    /// <summary>
    /// Exponential decay on the declared half-life — the rehabilitation.
    ///
    /// <para>ONE CLOCK FOR BOTH TERMS (SDD-012 §4b's R23.1 amendment). The
    /// intensity window ages by the same factor as the incident record, which is
    /// what makes "reduce the intensities" a reachable exit rather than a comment
    /// claiming one. Numerator and denominator are aged TOGETHER, so a field that
    /// changes nothing keeps the ratio it had: decaying only the flaring would
    /// forgive a company for continuing to flare.</para>
    /// </summary>
    public void Age(Duration dt)
    {
        if (dt.Days <= 0.0) return;

        double ticks = dt.Days / Duration.DaysPerTick;
        double remaining = DetMath.Pow(0.5, ticks / _halfLifeTicks);

        _incidentPoints *= remaining;
        _flaredWindow *= remaining;
        _producedWindow *= remaining;
    }

    /// <summary>
    /// The standing the lender spread table reads (SDD-009 §5).
    ///
    /// <para><b>Two exits, per CI4</b>: reduce the intensities, or let the
    /// incident record decay. A loop with one exit is a trap, and a loop with
    /// none is a punishment.</para>
    /// </summary>
    public double Standing(IReadOnlyList<(double Weight, double BandScore)> intensities)
    {
        ArgumentNullException.ThrowIfNull(intensities);

        double penalty = 0.0;
        for (int i = 0; i < intensities.Count; i++)
            penalty += intensities[i].Weight * intensities[i].BandScore;

        return Math.Clamp(100.0 - penalty - _incidentPoints, 0.0, 100.0);
    }

    // ------------------------------------------------------- SDD-013 §4

    /// <summary>
    /// The incident record, which is STATE and not a derivation: `Age` multiplies
    /// it by its own previous value, so it is the same shape as the covenant
    /// clock and the reserve ring (findings 210, 208). A reload that resumed
    /// from zero would hand back a spotless company however recently it had hurt
    /// someone — and the rehabilitation this decay models is the whole point of
    /// keeping the number.
    /// </summary>
    public StateKey Key { get; } = new("integrity.esg");

    public int SchemaVersion => 2;

    /// <summary>Nothing: one scalar that depends on no other owner.</summary>
    public IReadOnlyList<StateKey> RestoreAfter => [];

    public void Capture(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteDouble("incident-points", _incidentPoints);

        // The window is state for the same reason the incident record is: `Age`
        // multiplies each by its own previous value, so a reload that resumed
        // from zero would hand back a company that had never flared.
        writer.WriteDouble("flared-window-kg", _flaredWindow);
        writer.WriteDouble("produced-window-m3", _producedWindow);
    }

    public void Restore(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        _incidentPoints = reader.ReadDouble("incident-points");
        _flaredWindow = reader.ReadDouble("flared-window-kg");
        _producedWindow = reader.ReadDouble("produced-window-m3");
    }
}
