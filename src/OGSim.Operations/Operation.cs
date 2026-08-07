// R12.1 / R12.2 — the operation and the scheduler (SDD-007 §1–4, design 08).
//
// ONE IOperation CONTRACT, MANY OUTCOME TYPES. One abstraction means one
// scheduler, one cost-accrual path, one progress projection, one audit shape and
// one failure model — and it makes "what is my company doing" a single query
// rather than a union of subsystem views (R12 §2.1).
//
// Two things here are the phase's real content:
//
//   COST ACCRUES OVER THE OPERATION, not at the end. A six-month well spends
//   money for six months, which is what makes cash flow tight during
//   development and how an over-committed company runs out of money mid-well.
//
//   TROUBLE IS GRADED, not binary. On time, delayed, over budget, partial,
//   failure, disaster — drawn once at start from the `operations` stream and
//   audited with the draw, so a player who suspects the dice can check them.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Operations;

/// <summary>
/// SDD-007 §4's outcome, drawn ONCE at start and applied across execution.
///
/// <para>Drawn at start rather than rolled each tick, deliberately: a six-month
/// operation rolling monthly would fail far more often than its table says, and
/// the table's probabilities would describe nothing anyone could reason about.
/// The outcome is a property of the job, decided when it begins.</para>
/// </summary>
public sealed record DrawnOutcome(OutcomeRow Row, double Draw, int EffectiveDurationDays);

/// <summary>SDD-007 §1. An operation in flight.</summary>
public sealed class Operation : IOperation
{
    private readonly IAuditTrail _audit;

    private Money _accrued = Money.Zero;
    private int _progressDays;

    internal Operation(
        EntityId<IOperation> id,
        OperationSpec spec,
        DrawnOutcome outcome,
        IAuditTrail audit,
        int materialCount)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(audit);

        Id = id;
        Spec = spec;
        Outcome = outcome;
        _audit = audit;
        State = OperationState.Scheduled;
        MassThisTick = OperationMass.None(materialCount);
    }

    public EntityId<IOperation> Id { get; }
    public OperationSpec Spec { get; }
    public OperationState State { get; private set; }

    /// <summary>
    /// SDD-007 §5b. This template moves no mass of its own — drilling,
    /// completion and construction all put their volumes through the routed
    /// network or nowhere.
    ///
    /// <para>The operations that DO move mass are the well test and frac-fluid
    /// recovery, and neither has a template yet (R12b.4). Reporting
    /// <c>None</c> is the true answer for every template that exists today, not
    /// a placeholder for the ones that do not — and it is what lets the tick's
    /// conservation check cover operations at all rather than skipping a term
    /// it has no value for.</para>
    /// </summary>
    public OperationMass MassThisTick { get; }
    public int ProgressDays => _progressDays;
    public Money Accrued => _accrued;

    /// <summary>What was drawn, kept so the audit and the read model can explain
    /// the operation's behaviour rather than merely report it.</summary>
    public DrawnOutcome Outcome { get; }

    /// <summary>
    /// Restores saved progress (SDD-013 §4). Called only by the scheduler's
    /// <c>Reinstate</c>, which owns the rig re-reservation that has to happen
    /// with it — an operation put back without its calendar entry would let a
    /// second well be drilled on a rig that is already turning.
    /// </summary>
    internal void Reinstate(int progressDays, Money accrued, OperationState state)
    {
        if (progressDays < 0)
            throw new SaveDataFault("SDD-007 §3", null,
                $"operation {Id.Value} declares {progressDays} days of progress");

        if (progressDays > Outcome.EffectiveDurationDays)
            throw new SaveDataFault("SDD-007 §3", null,
                $"operation {Id.Value} has {progressDays} days of progress against an " +
                $"effective duration of {Outcome.EffectiveDurationDays}; a save cannot " +
                "restore an operation past its own end");

        _progressDays = progressDays;
        _accrued = accrued;
        State = state;
    }

    /// <summary>SDD-007 §3: mobilisation is charged on entering Active.</summary>
    public void Begin()
    {
        if (State != OperationState.Scheduled)
            throw new InvariantFault("SDD-007 §1", null,
                $"operation {Id.Value} cannot begin from {State}");

        State = OperationState.Active;
        _accrued += Spec.Costs.Mobilisation;
    }

    /// <summary>
    /// One tick. Progress and accrual use the SAME day counts (SDD-007 §3), so a
    /// standby day costs money and buys nothing — which is the honest price of a
    /// missed weather window and is visible in the cost report.
    /// </summary>
    public void Advance(int activeDays, int standbyDays, double costIndex)
    {
        if (State is not (OperationState.Active or OperationState.Standby))
            throw new InvariantFault("SDD-007 §1", null,
                $"operation {Id.Value} cannot advance from {State}");

        if (activeDays < 0 || standbyDays < 0)
            throw new InvariantFault("SDD-007 §3", null, "day counts cannot be negative");

        if (costIndex <= 0.0)
            throw new InvariantFault("SDD-007 §3", null,
                "the cost index must be positive; a zero index would make an operation free");

        int remaining = Outcome.EffectiveDurationDays - _progressDays;
        int worked = Math.Min(activeDays, Math.Max(0, remaining));

        // Standby is committed-but-not-progressing: cost without progress.
        // Consumables stop, so only the day rate applies (SDD-007 §3, pinned).
        _accrued += Money.RoundHalfEven(Spec.Costs.PerActiveDay.Cents * worked * costIndex * Outcome.Row.CostFactor)
                  + Money.RoundHalfEven(Spec.Costs.PerStandbyDay.Cents * standbyDays * costIndex);

        _progressDays += worked;

        State = standbyDays > 0 && worked == 0 ? OperationState.Standby : OperationState.Active;

        if (_progressDays >= Outcome.EffectiveDurationDays) Finish();
    }

    /// <summary>
    /// R12-V8. Resources are released at once by the scheduler; the accrued cost
    /// STAYS SPENT.
    ///
    /// <para>Sunk cost is retained because it was: the rig was paid, the crew
    /// flew out, the casing was bought. Refunding it on cancellation would make
    /// starting an operation free to reconsider, and the whole point of
    /// committing resources is that commitment costs something.</para>
    /// </summary>
    public void Cancel()
    {
        if (State is OperationState.Completed or OperationState.Failed)
            throw new InvariantFault("SDD-007 §1", null,
                $"operation {Id.Value} has already finished as {State}");

        State = OperationState.Cancelled;

        _audit.Record(AuditCategory.Command, null, null, new Dictionary<string, AuditValue>
        {
            ["operation"] = new(Format(Id.Value)),
            ["outcome"] = new("cancelled"),
            ["sunkCost"] = new(Format(_accrued.Cents)),
        });
    }

    private void Finish()
    {
        // Failure and disaster do not pay a completion fee: nothing was
        // completed. Charging it would price a lost hole like a finished well.
        bool succeeded = Outcome.Row.Grade
            is not (OutcomeGrade.Failure or OutcomeGrade.Disaster);

        if (succeeded) _accrued += Spec.Costs.Completion;

        State = succeeded ? OperationState.Completed : OperationState.Failed;
    }

    private static string Format(long value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Format(ulong value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
