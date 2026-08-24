// R24.6 — the eight score dimensions (SDD-014 §4, finding 290).
//
// A SPAN ACCUMULATOR, not a new simulation fact. §4's own closing line is the
// law here: "every input is an existing ledger/registry value — scoring reads,
// never computes new simulation facts." What a span score needs that a tick
// cannot supply is INTEGRATION — Σ produced, Σ capex, the opening 2P a delta is
// against — and that is all this class does: it watches the same published
// position every objective evaluates against, and adds.
//
// It is an IStateOwner because a score over a span is a fact about the RUN: a
// reload that forgot the first five years' production would score the decade on
// its second half and call the result the campaign's.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition;

public sealed class ScoreLedger : IStateOwner
{
    private bool _opened;
    private double _openingTwoP;        // m³, at the first observed tick
    private long _openingValueCents;    // company value, same instant
    private double _sanctionTwoP;       // m³, captured on the first development spend
    private double _producedM3;         // Σ custody-metered oil
    private double _producedMassKg;     // Σ custody throughput — the solver's own basis
    private double _deferredMassKg;     // Σ attributed deferrals (SDD-002 §8)
    private long _capexCents;
    private long _explorationCents;
    private long _opexCents;

    // The tick most recently integrated. SaveGame.Load runs the objective
    // stage once immediately after restore (finding 266) for the read model's
    // sake, and that call arrives with a tick this ledger has already added —
    // integrating it twice would charge the span one extra month of spend.
    private long _lastObservedTick = -1;

    /// <summary>
    /// Distributions paid over the span (SDD-014 §4's capital-efficiency term).
    /// ZERO by construction, not by omission: this engine has no distribution
    /// mechanic — no dividend, no buy-back — so the term is honestly nothing
    /// until one exists, at which point it gets its own `MovementCategory` and
    /// this reads it (SDD-014 §4's finding-290 amendment).
    /// </summary>
    private const long DistributionsCents = 0;

    public StateKey Key { get; } = new("scenario.scores");

    public int SchemaVersion => 1;

    public IReadOnlyList<StateKey> RestoreAfter => [];

    /// <summary>
    /// One tick, integrated. Called by <see cref="ObjectiveStage"/> with the
    /// same sealed position the objectives are about to be evaluated against
    /// and the sources SDD-014 §4 names — the cash ledger's period effect, the
    /// reserves book's 2P, and the solver's own custody and deferral masses
    /// (SDD-002 §8) — observe, never influence (SDD-014 §3).
    /// </summary>
    public void Observe(
        Tick tick,
        FieldPosition position,
        IReadOnlyList<Money> cashByCause,
        double twoPCubicMetres,
        Mass custodyThroughputThisTick,
        Mass deferredThisTick)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(cashByCause);

        if (tick.Value == _lastObservedTick) return;
        _lastObservedTick = tick.Value;

        if (!_opened)
        {
            _opened = true;
            _openingTwoP = twoPCubicMetres;
            _openingValueCents = position.CompanyValue.Cents;
        }

        _producedM3 += position.ProducedThisTick.CubicMetres;
        _producedMassKg += custodyThroughputThisTick.Kilograms;
        _deferredMassKg += deferredThisTick.Kilograms;

        long development = Outflow(cashByCause, OGSim.Company.MovementCategory.Development);

        _capexCents += development;
        _explorationCents += Outflow(cashByCause, OGSim.Company.MovementCategory.Exploration);
        _opexCents += Outflow(cashByCause, OGSim.Company.MovementCategory.Operating);

        // 2P AT SANCTION (§4's recovery denominator): what the book said the
        // moment the company first put development money down — the honest
        // bookable proxy §4 itself chose over unreachable truth.
        if (_sanctionTwoP == 0.0 && development > 0) _sanctionTwoP = twoPCubicMetres;
    }

    /// <summary>
    /// The eight dimensions, computed from the span and the sealed now
    /// (SDD-014 §4). A dimension whose denominator has not happened yet — no
    /// barrel produced, no dollar of capex, no obligation incurred — is
    /// OMITTED rather than reported as zero: "the finding cost of nothing
    /// found" has no answer, and zero would flatter it (the same refusal §1
    /// makes for `Max` over an empty collection). Dimensions are always
    /// reported individually (18 §4); the composite is the host's
    /// presentation, weighted by the scenario's own <c>Scoring</c> list.
    /// </summary>
    public IReadOnlyList<(ScoreDimension Dimension, double Score)> Scores(
        FieldPosition now, double twoPCubicMetres, double esgStanding,
        int obligationsDischarged, int obligationsIncurred)
    {
        ArgumentNullException.ThrowIfNull(now);

        var scores = new List<(ScoreDimension, double)>();

        double added = twoPCubicMetres - _openingTwoP;

        // Reserves: RRR = 2P added / produced. The addition itself is
        // recoverable from the same two terms; the ratio is the score.
        if (_producedM3 > 0.0)
            scores.Add((ScoreDimension.Reserves, added / _producedM3));

        if (_sanctionTwoP > 0.0)
            scores.Add((ScoreDimension.Recovery, _producedM3 / _sanctionTwoP));

        if (_capexCents > 0)
            scores.Add((ScoreDimension.CapitalEfficiency,
                (now.CompanyValue.Cents - _openingValueCents + DistributionsCents)
                / (double)_capexCents));

        // Finding cost: exploration and appraisal spend per m³ of 2P added —
        // in this engine every addition over a span IS discovery and appraisal
        // (nothing else books reserves), which is §4's "added by discovery".
        if (added > 0.0)
            scores.Add((ScoreDimension.FindingCost, _explorationCents / added));

        if (_producedM3 > 0.0)
            scores.Add((ScoreDimension.OperatingCost, _opexCents / _producedM3));

        if (_producedMassKg + _deferredMassKg > 0.0)
            scores.Add((ScoreDimension.Uptime,
                _producedMassKg / (_producedMassKg + _deferredMassKg)));

        // HSE: the engine already composes §4's exact terms — tier-weighted
        // incident points (`ConsequencePoints`, decayed) and normalised
        // flaring intensity (`EsgRecord`) — into ONE standing, published every
        // tick. A second composite of the same events here would be a second
        // owner of one fact (law L5), so the dimension reads the standing.
        scores.Add((ScoreDimension.Hse, esgStanding));

        // Legacy: discharged / incurred — which is also the restoration
        // completion fraction in an engine where discharging an obligation IS
        // the restoration (SDD-007 §6).
        if (obligationsIncurred > 0)
            scores.Add((ScoreDimension.Legacy,
                obligationsDischarged / (double)obligationsIncurred));

        return scores;
    }

    /// <summary>What the period SPENT under a cause — the negative side of the
    /// signed cash effect, zero on a tick that only earned there.</summary>
    private static long Outflow(
        IReadOnlyList<Money> cashByCause, OGSim.Company.MovementCategory cause)
    {
        long cents = cashByCause[OGSim.Company.CostLedger.At(cause)].Cents;

        return cents < 0 ? -cents : 0;
    }

    public void Capture(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteInt64("opened", _opened ? 1 : 0);
        writer.WriteInt64("last-tick", _lastObservedTick);
        writer.WriteDouble("opening-2p", _openingTwoP);
        writer.WriteInt64("opening-value", _openingValueCents);
        writer.WriteDouble("sanction-2p", _sanctionTwoP);
        writer.WriteDouble("produced-m3", _producedM3);
        writer.WriteDouble("produced-kg", _producedMassKg);
        writer.WriteDouble("deferred-kg", _deferredMassKg);
        writer.WriteInt64("capex", _capexCents);
        writer.WriteInt64("exploration", _explorationCents);
        writer.WriteInt64("opex", _opexCents);
    }

    public void Restore(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        _opened = reader.ReadInt64("opened") != 0;
        _lastObservedTick = reader.ReadInt64("last-tick");
        _openingTwoP = reader.ReadDouble("opening-2p");
        _openingValueCents = reader.ReadInt64("opening-value");
        _sanctionTwoP = reader.ReadDouble("sanction-2p");
        _producedM3 = reader.ReadDouble("produced-m3");
        _producedMassKg = reader.ReadDouble("produced-kg");
        _deferredMassKg = reader.ReadDouble("deferred-kg");
        _capexCents = reader.ReadInt64("capex");
        _explorationCents = reader.ReadInt64("exploration");
        _opexCents = reader.ReadInt64("opex");
    }
}
