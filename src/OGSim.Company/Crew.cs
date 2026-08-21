// SDD-007 §4.1's finding-265 amendment — the crew effect, as a company fact.
//
// A LEAN CREW IS CHEAP AND MEASURABLY WORSE AT BOTH THINGS IT IS WORSE AT
// (R12 §2.8): it takes longer to finish a job and it is a weaker link in
// every barrier the bow-tie samples. Training is the one-time decision that
// buys both back at once, because a company does not train its crew to be
// faster OR safer — it trains them and gets both, the same way a real one
// does.
//
// EXPENSED, NOT CAPITALISED (SDD-009 §1's own distinction for a vessel being
// PP&E). Training buys a permanently better crew, but unlike a vessel it is
// not a balance-sheet asset in the accounting this engine follows.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Company;

/// <summary>
/// SDD-007 §4.1's crew competency — a company-wide scalar, not a
/// per-discipline skill system. Feeds `Barrier.StrengthGiven` (SDD-012 §4b)
/// and `OperationScheduler.Draw`'s duration term (SDD-007 §4).
/// </summary>
public sealed class CrewState : IStateOwner
{
    private readonly double _baseCompetency;
    private readonly double _trainedCompetency;
    private readonly double _baseDurationFactor;
    private readonly double _trainedDurationFactor;
    private bool _trained;

    public CrewState(
        double baseCompetency, double trainedCompetency,
        double baseDurationFactor, double trainedDurationFactor,
        Money trainingCost)
    {
        if (baseCompetency is < 0.0 or > 1.0 || trainedCompetency is < 0.0 or > 1.0)
            throw new ContentFault("SDD-007 §4.1", null,
                "a crew competency is a strength in [0, 1], the same scale " +
                "Barrier.StrengthGiven takes every other term on");

        if (trainedCompetency <= baseCompetency)
            throw new ContentFault("SDD-007 §4.1", null,
                $"trained competency {Format(trainedCompetency)} is not above base " +
                $"{Format(baseCompetency)}; training that does not improve the crew is not " +
                "training, it is the same crew under a different name");

        if (baseDurationFactor <= 0.0 || trainedDurationFactor <= 0.0)
            throw new ContentFault("SDD-007 §4.1", null,
                "a crew duration factor is a positive multiplier on the outcome table's own " +
                "term; zero or negative would make an operation take no time or run backwards");

        if (trainedDurationFactor >= baseDurationFactor)
            throw new ContentFault("SDD-007 §4.1", null,
                $"trained duration factor {Format(trainedDurationFactor)} is not below base " +
                $"{Format(baseDurationFactor)}; a faster crew is the whole of what R12-V9 asks " +
                "training to buy");

        if (trainingCost <= Money.Zero)
            throw new ContentFault("SDD-007 §4.1", null,
                "a training cost that is free or negative is not an investment decision");

        _baseCompetency = baseCompetency;
        _trainedCompetency = trainedCompetency;
        _baseDurationFactor = baseDurationFactor;
        _trainedDurationFactor = trainedDurationFactor;
        TrainingCost = trainingCost;
    }

    /// <summary>What a fully trained crew costs to raise, once. Read by the
    /// command that spends it, so the price lives beside the thing it buys
    /// rather than being guessed at the call site (law L5).</summary>
    public Money TrainingCost { get; }

    public bool Trained => _trained;

    public double Competency => _trained ? _trainedCompetency : _baseCompetency;

    public double DurationFactor => _trained ? _trainedDurationFactor : _baseDurationFactor;

    /// <summary>One-way, like a technology acquisition — a crew does not
    /// forget what it was trained to do.</summary>
    public void Train()
    {
        if (_trained)
            throw new InvariantFault("SDD-007 §4.1", null,
                "this crew is already trained; the command that reaches this point twice " +
                "should have been refused at validation");

        _trained = true;
    }

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

    // ------------------------------------------------------- SDD-013 §4

    /// <summary>The one fact this owns: whether the crew has been trained.
    /// Everything else is content, supplied fresh at composition every
    /// load.</summary>
    public StateKey Key { get; } = new("company.crew");

    public int SchemaVersion => 1;

    public IReadOnlyList<StateKey> RestoreAfter => [];

    public void Capture(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteInt64("trained", _trained ? 1L : 0L);
    }

    public void Restore(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        _trained = reader.ReadInt64("trained") != 0L;
    }
}
