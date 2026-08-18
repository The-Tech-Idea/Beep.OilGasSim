// R16.2 / R16.3 — licences, commitments and rounds (SDD-011 §1).
//
// A licence is a CLOCK and a PROMISE. The clock is the term and the
// relinquishment schedule; the promise is the work commitment, and failing it
// forfeits the bond and loses the acreage. Commitment tracking is mechanical —
// qualifying operations decrement items, and at the deadline what is left
// undone is what costs.
//
// The forfeit is announced twice and never silently (EM7), because losing a
// licence is the kind of consequence a player must be able to see coming: an
// obligation that arrives as a surprise teaches nothing except not to trust the
// interface.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Company;

/// <summary>What a licence still owes, per commitment item.</summary>
public sealed record CommitmentProgress(CommitmentItem Item, double Delivered)
{
    public double Outstanding => Math.Max(0.0, Item.Quantity - Delivered);

    public bool Met => Outstanding <= 0.0;
}

/// <summary>What happened when a deadline arrived.</summary>
public sealed record CommitmentAssessment(
    IReadOnlyList<CommitmentProgress> Unmet,
    Money BondForfeit,
    bool LicenceLost)
{
    // Finding 131.
    public bool Equals(CommitmentAssessment? other) =>
        other is not null && BondForfeit == other.BondForfeit && LicenceLost == other.LicenceLost
        && Structural.Equal(Unmet, other.Unmet);

    public override int GetHashCode() =>
        HashCode.Combine(BondForfeit, LicenceLost, Structural.HashOf(Unmet));
}

/// <summary>
/// SDD-011 §1. A live licence with its terms and its clocks.
///
/// <para><c>IStateOwner</c> since R20d.9: commitment progress and whether the
/// licence has been forfeited both change over the game's life, so both are
/// STATE rather than something recomputed from <c>Terms</c> alone. What IS
/// recomputed every compose, never captured: <c>Terms</c> itself (a fixed
/// content value), and <c>Granted</c>/<c>Expiry</c>, which follow from it — a
/// company holds one licence granted at tick 0 in every game this composition
/// ships, so there is nothing there for a save to disagree with itself
/// about.</para>
/// </summary>
public sealed class Licence : ILicence, IStateOwner
{
    private readonly List<CommitmentProgress> _progress = [];

    public Licence(EntityId<ILicence> id, LicenceTerms terms, Tick granted)
    {
        ArgumentNullException.ThrowIfNull(terms);
        Validate(terms);

        Id = id;
        Terms = terms;
        Granted = granted;
        Expiry = new Tick(granted.Value + terms.TermMonths);

        for (int i = 0; i < terms.WorkCommitment.Count; i++)
            _progress.Add(new CommitmentProgress(terms.WorkCommitment[i], 0.0));
    }

    public EntityId<ILicence> Id { get; }
    public LicenceTerms Terms { get; }
    public Tick Granted { get; }
    public Tick Expiry { get; }

    public ContentId FiscalRegime => Terms.FiscalRegime;

    public IReadOnlyList<CommitmentProgress> Progress => _progress;

    /// <summary>Whether the licence has been forfeited or has run out.</summary>
    public bool IsLive { get; private set; } = true;

    /// <summary>
    /// A qualifying operation completed. Mechanical: it decrements the matching
    /// item and nothing else.
    ///
    /// <para>Work delivered against a kind the licence did not commit to is
    /// simply not credited — a company that drilled the wrong thing has still
    /// drilled the wrong thing, and quietly counting it would let any activity
    /// discharge any promise.</para>
    /// </summary>
    public void RecordDelivery(ContentId kind, double quantity)
    {
        if (quantity < 0.0)
            throw new InvariantFault("SDD-011 §1", null,
                "negative delivery; work cannot be un-done against a commitment");

        for (int i = 0; i < _progress.Count; i++)
        {
            if (_progress[i].Item.Kind != kind) continue;

            _progress[i] = _progress[i] with { Delivered = _progress[i].Delivered + quantity };
            return;
        }
    }

    /// <summary>
    /// SDD-011 §1's deadline check. Unmet at the due date forfeits the bond and
    /// loses the licence.
    ///
    /// <para>The bond is forfeited WHOLE rather than pro-rata to the shortfall.
    /// That is what a bond is: it secures the promise, not a fraction of it, and
    /// a pro-rata forfeit would make a token well a cheap way to keep most of
    /// the money.</para>
    /// </summary>
    public CommitmentAssessment AssessAt(Tick now)
    {
        var unmet = new List<CommitmentProgress>();

        for (int i = 0; i < _progress.Count; i++)
        {
            CommitmentProgress progress = _progress[i];
            if (progress.Item.Due.Value > now.Value) continue;      // not due yet
            if (progress.Met) continue;

            unmet.Add(progress);
        }

        if (unmet.Count == 0)
            return new CommitmentAssessment([], Money.Zero, LicenceLost: false);

        IsLive = false;
        return new CommitmentAssessment(unmet, Terms.Bond, LicenceLost: true);
    }

    /// <summary>
    /// The fraction of acreage that must have been handed back by now — the sum
    /// of every step whose date has passed.
    ///
    /// <para>Cumulative rather than per-step, so a licence that missed one
    /// relinquishment does not get to forget it at the next.</para>
    /// </summary>
    public double RelinquishedFractionDueBy(Tick now)
    {
        double due = 0.0;

        for (int i = 0; i < Terms.Relinquishment.Count; i++)
            if (Terms.Relinquishment[i].Due.Value <= now.Value)
                due += Terms.Relinquishment[i].Fraction;

        return Math.Min(1.0, due);
    }

    /// <summary>Expiry is a clock, not a judgement — it runs whether or not the
    /// work was done.</summary>
    public bool HasExpiredBy(Tick now) => now.Value >= Expiry.Value;

    // ------------------------------------------------------- SDD-013 §4

    public StateKey Key { get; } = new("company.licence");

    public int SchemaVersion => 1;

    /// <summary>Nothing has to be back before this is (SDD-013 §2b).</summary>
    public IReadOnlyList<StateKey> RestoreAfter => [];

    public void Capture(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteInt64("is-live", IsLive ? 1L : 0L);
        writer.WriteInt64("delivered-count", _progress.Count);

        // DECLARED ORDER, matching Terms.WorkCommitment — a List and not a
        // Dictionary, so this is D-5-safe without needing a sort.
        for (int i = 0; i < _progress.Count; i++)
            writer.WriteDouble(
                "delivered." + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture),
                _progress[i].Delivered);
    }

    public void Restore(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        IsLive = reader.ReadInt64("is-live") != 0L;

        long count = reader.ReadInt64("delivered-count");

        if (count != _progress.Count)
            throw new SaveDataFault("SDD-011 §1", null,
                $"the save carries {count} commitment items and this licence's " +
                $"terms declare {_progress.Count}; the content changed under the save");

        for (long i = 0; i < count; i++)
        {
            double delivered = reader.ReadDouble(
                "delivered." + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture));

            _progress[(int)i] = _progress[(int)i] with { Delivered = delivered };
        }
    }

    private static void Validate(LicenceTerms terms)
    {
        if (terms.TermMonths <= 0)
            throw new ModelFault("SDD-011 §1", null,
                $"licence term {terms.TermMonths} months is not positive");

        if (terms.Bond.Cents < 0)
            throw new ModelFault("SDD-011 §1", null, "a bond cannot be negative");

        double totalRelinquishment = 0.0;
        for (int i = 0; i < terms.Relinquishment.Count; i++)
        {
            RelinquishmentStep step = terms.Relinquishment[i];

            if (step.Fraction is <= 0.0 or > 1.0)
                throw new ModelFault("SDD-011 §1", null,
                    $"relinquishment step {i} hands back " +
                    step.Fraction.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    ", not a fraction in (0, 1]");

            totalRelinquishment += step.Fraction;
        }

        if (totalRelinquishment > 1.0 + 1e-9)
            throw new ModelFault("SDD-011 §1", null,
                "the relinquishment schedule hands back more than the whole licence");

        for (int i = 0; i < terms.WorkCommitment.Count; i++)
            if (terms.WorkCommitment[i].Quantity <= 0.0)
                throw new ModelFault("SDD-011 §1", null,
                    $"work commitment {terms.WorkCommitment[i].Kind.Value} has a " +
                    "non-positive quantity; a promise to do nothing is not a commitment");
    }
}
