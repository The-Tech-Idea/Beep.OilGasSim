// R12b.15 — drilling, on the one scheduled-activity engine (SDD-007, finding 142).
//
// R21b built this as a bespoke timer beside `OperationScheduler`, because the
// loop was being assembled end to end and the scheduler was one of the eight
// subsystems the loop did not yet call. Writing past it was the fastest route to
// a working tick, and it is exactly how a second engine gets built. Three things
// were lost and are back:
//
//   RIG CONTENTION. A rig drills one well at a time. The bespoke path let a
//   company drill six at once with one rig, which made the only real constraint
//   on early expansion the cash — and cash alone is a spreadsheet, not a field.
//
//   COST OVER TIME. A four-month well spends for four months. Paying on day one
//   removed the "runs out of money mid-well" dynamic R12-V2 exists to assert.
//
//   GRADED TROUBLE. A well can be delayed or run over budget, not merely be dry.
//   The outcome table is content, so what "trouble" means is a balance decision
//   rather than a code one.

using OGSim.Company;
using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Operations;

namespace OGSim.Composition;

/// <summary>What a well costs, how deep the company can reach, and how it can go wrong.</summary>
public sealed record DrillingTerms(
    Money CostPerWell,
    Length MaximumDepth,
    int DurationTicks,
    ContentId Template,
    EntityId<IRig> Rig,
    OutcomeTable Outcomes)
{
    /// <summary>
    /// The chance the hole finds oil — every grade except the two that mean it
    /// did not. Read from the table rather than carried beside it, so the
    /// content and the odds cannot disagree.
    /// </summary>
    public double ProbabilityOfSuccess
    {
        get
        {
            var success = 0.0;

            for (int i = 0; i < Outcomes.Rows.Count; i++)
                if (Outcomes.Rows[i].Grade is not (OutcomeGrade.Failure or OutcomeGrade.Disaster))
                    success += Outcomes.Rows[i].Probability;

            return success;
        }
    }
}

/// <summary>A well being drilled: the operation, and what it is drilling into.</summary>
internal sealed record WellUnderConstruction(
    Operation Operation,
    EntityId<IReservoirCompartmentEntity> Target,
    Length TotalDepth,
    int StartDay)
{
    /// <summary>Cost already posted to the ledger, so each tick posts only the
    /// increment. The operation accrues; the ledger is told the difference.</summary>
    public Money Posted { get; set; } = Money.Zero;
}

/// <summary>
/// Owner of <c>field.drilling</c> — the rigs currently turning.
///
/// <para>It holds operations and the compartment each is aimed at; the schedule,
/// the contention and the outcome all belong to <see cref="OperationScheduler"/>.
/// This is a register of what the company is drilling, not a second scheduler.</para>
/// </summary>
internal sealed class DrillingState(
    OperationScheduler scheduler, DrillingTerms terms, CompanyState company) : IStateOwner
{
    private readonly List<WellUnderConstruction> _drilling = [];

    public StateKey Key { get; } = new("field.drilling");

    public int SchemaVersion => 2;

    public int InProgress => _drilling.Count;

    public DrillingTerms Terms => terms;

    public OperationScheduler Scheduler => scheduler;

    /// <summary>The operation spec for one well — everything SDD-007 §1 needs.</summary>
    public OperationSpec SpecFor(EntityId<IReservoirCompartmentEntity> target, Length totalDepth) =>
        new(Template: terms.Template,
            Target: new EntityRef(EntityKind.Compartment, target.Value),
            BaseDurationDays: terms.DurationTicks * (int)Duration.DaysPerTick,
            Costs: new CostProfile(
                // Split so that most of the money follows the work: a well
                // abandoned halfway has cost most of a well, not all of one and
                // not none (SDD-007 §3).
                Mobilisation: Money.RoundHalfEven(terms.CostPerWell.Cents * 0.15),
                PerActiveDay: Money.RoundHalfEven(
                    terms.CostPerWell.Cents * 0.75 / (terms.DurationTicks * Duration.DaysPerTick)),
                PerStandbyDay: Money.RoundHalfEven(
                    terms.CostPerWell.Cents * 0.20 / (terms.DurationTicks * Duration.DaysPerTick)),
                Completion: Money.RoundHalfEven(terms.CostPerWell.Cents * 0.10)),
            Resources: new ResourceNeeds(terms.Rig, []),
            Requirements: new Requirements([], MinDetectClass: null, []),
            Rentals: [],
            Outcomes: terms.Outcomes);

    public void Begin(WellUnderConstruction well) => _drilling.Add(well);

    public IReadOnlyList<WellUnderConstruction> InFlight => _drilling;

    public void Remove(WellUnderConstruction well) => _drilling.Remove(well);

    public void Capture(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteInt64("count", _drilling.Count);

        for (int i = 0; i < _drilling.Count; i++)
        {
            WellUnderConstruction well = _drilling[i];
            string at = Prefix(i);

            writer.WriteInt64(at + "operation", (long)well.Operation.Id.Value);
            writer.WriteInt64(at + "target", (long)well.Target.Value);
            writer.WriteDouble(at + "depth", well.TotalDepth.Metres);
            writer.WriteInt64(at + "start-day", well.StartDay);
            writer.WriteInt64(at + "progress-days", well.Operation.ProgressDays);
            writer.WriteInt64(at + "accrued", well.Operation.Accrued.Cents);
            writer.WriteInt64(at + "posted", well.Posted.Cents);
            writer.WriteInt64(at + "state", (long)well.Operation.State);

            // The OUTCOME, saved. It was drawn when the well began (SDD-007 §4),
            // and redrawing it on load would let a player reload the month
            // before the rig finished and try again.
            writer.WriteInt64(at + "grade", (long)well.Operation.Outcome.Row.Grade);
            writer.WriteDouble(at + "draw", well.Operation.Outcome.Draw);
            writer.WriteInt64(at + "effective-days", well.Operation.Outcome.EffectiveDurationDays);
        }
    }

    public void Restore(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        _drilling.Clear();

        long count = reader.ReadInt64("count");

        for (long i = 0; i < count; i++)
        {
            string at = Prefix(i);

            var target = new EntityId<IReservoirCompartmentEntity>(
                (ulong)reader.ReadInt64(at + "target"));

            var depth = new Length(reader.ReadDouble(at + "depth"));
            OperationSpec spec = SpecFor(target, depth);

            var grade = (OutcomeGrade)reader.ReadInt64(at + "grade");
            OutcomeRow row = RowFor(grade);

            Operation operation = scheduler.Reinstate(
                new EntityId<IOperation>((ulong)reader.ReadInt64(at + "operation")),
                spec,
                new DrawnOutcome(row, reader.ReadDouble(at + "draw"),
                                 (int)reader.ReadInt64(at + "effective-days")),
                startDay: (int)reader.ReadInt64(at + "start-day"),
                progressDays: (int)reader.ReadInt64(at + "progress-days"),
                accrued: new Money(reader.ReadInt64(at + "accrued")),
                state: (OperationState)reader.ReadInt64(at + "state"));

            _drilling.Add(new WellUnderConstruction(
                operation, target, depth, (int)reader.ReadInt64(at + "start-day"))
            {
                Posted = new Money(reader.ReadInt64(at + "posted")),
            });
        }
    }

    /// <summary>
    /// The table row for a saved grade. A save naming a grade this content does
    /// not contain is refused rather than approximated — the outcome decides
    /// whether the well produces, and guessing it would decide the game.
    /// </summary>
    private OutcomeRow RowFor(OutcomeGrade grade)
    {
        for (int i = 0; i < terms.Outcomes.Rows.Count; i++)
            if (terms.Outcomes.Rows[i].Grade == grade) return terms.Outcomes.Rows[i];

        throw new SaveDataFault("SDD-007 §4", null,
            $"the save holds a drilling outcome of {grade}, which this content's " +
            "outcome table does not contain");
    }

    /// <summary>Posts the month's share of a well's cost to the ledger, as the
    /// increment since last tick.</summary>
    public void PostAccrual(WellUnderConstruction well, Tick tick, AuditId cause)
    {
        Money increment = well.Operation.Accrued - well.Posted;
        if (increment.Cents == 0) return;

        company.Ledger.Post(new Movement(
            tick, Account.Capex_PPE, Account.Cash, increment,
            MovementCategory.Development, Asset: null, Cause: cause));

        well.Posted = well.Operation.Accrued;
    }

    private static string Prefix(long index) =>
        "drilling." + index.ToString("D6", System.Globalization.CultureInfo.InvariantCulture) + ".";
}

/// <summary>
/// Stage 3. Rigs advance a month; the ones that finished hand over a well or a
/// dry hole.
/// </summary>
internal sealed class DrillingStage(
    DrillingState drilling,
    FieldControl field,
    IAuditTrail audit) : ITickStage
{
    public StageId Id => StageId.Operations;

    public void Execute(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Copied because completing a well removes it from the register.
        var turning = new List<WellUnderConstruction>(drilling.InFlight);

        for (int i = 0; i < turning.Count; i++)
        {
            WellUnderConstruction well = turning[i];

            if (well.Operation.State is OperationState.Scheduled) well.Operation.Begin();

            if (well.Operation.State is OperationState.Active or OperationState.Standby)
                well.Operation.Advance(
                    activeDays: (int)Duration.DaysPerTick, standbyDays: 0, costIndex: 1.0);

            AuditId cause = audit.Record(
                AuditCategory.StateTransition, subject: null, cause: null,
                new Dictionary<string, AuditValue>(StringComparer.Ordinal)
                {
                    ["operation"] = new(well.Operation.Id.Value.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)),
                    ["template"] = new(well.Operation.Spec.Template.Value),
                    ["state"] = new(well.Operation.State.ToString()),
                });

            // The month's share, whatever happened. A well that finished this
            // tick still spent this tick's money.
            drilling.PostAccrual(well, context.Tick, cause);

            if (well.Operation.State is not (OperationState.Completed or OperationState.Failed))
                continue;

            drilling.Remove(well);

            audit.Record(
                AuditCategory.StochasticOutcome, subject: null, cause: cause,
                new Dictionary<string, AuditValue>(StringComparer.Ordinal)
                {
                    ["outcome"] = new(well.Operation.State == OperationState.Completed
                        ? "completed" : "dry-hole"),
                    ["grade"] = new(well.Operation.Outcome.Row.Grade.ToString()),
                });

            // A dry hole opens nothing. The money is spent, the months are gone,
            // and what the player has bought is knowledge about the field.
            if (well.Operation.State is OperationState.Failed) continue;

            field.OpenWell(
                Defaults.CompletionFor(field.NextWellId(), well.Target, well.TotalDepth),
                well.Target);
        }
    }
}

/// <summary>
/// Pure (R1 §2.5) and reports EVERY reason: a player told only that a well is
/// too deep, who then finds the rig was busy as well, has been made to learn
/// the truth in instalments.
/// </summary>
internal sealed class DrillWellValidator(
    CompanyState company,
    FieldControl field,
    DrillingState drilling,
    SimulationClock clock) : ICommandValidator<DrillWellCommand>
{
    public IReadOnlyList<RejectionReason> Validate(DrillWellCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        DrillingTerms terms = drilling.Terms;
        var reasons = new List<RejectionReason>();

        if (company.Ledger.Cash < terms.CostPerWell)
            reasons.Add(new RejectionReason(
                "$loc:reject.insufficient-cash",
                $"a well costs {terms.CostPerWell.Cents} cents and the company holds " +
                $"{company.Ledger.Cash.Cents}"));

        if (command.TotalDepth.Metres > terms.MaximumDepth.Metres)
            reasons.Add(new RejectionReason(
                "$loc:reject.beyond-drilling-envelope",
                $"{command.TotalDepth.Metres} m is past the {terms.MaximumDepth.Metres} m " +
                "the company can currently drill"));

        if (command.TotalDepth.Metres <= 0.0)
            reasons.Add(new RejectionReason(
                "$loc:reject.invalid-depth", "a well must have a positive depth"));

        if (field.CompartmentCount == 0)
        {
            reasons.Add(new RejectionReason(
                "$loc:reject.no-target", "there is nothing here to drill into"));

            // The scheduler's target check would repeat this in its own words.
            return reasons;
        }

        // The SCHEDULER decides whether a rig is free, and it is asked without
        // being made to reserve one — a validator that booked a calendar to find
        // out whether it could would not be pure (SDD-007 §2).
        IReadOnlyList<string> refusals = drilling.Scheduler.Refusals(
            drilling.SpecFor(command.Target, command.TotalDepth),
            startDay: StartDay(clock),
            availableCapabilities: [],
            targetExists: _ => true);

        for (int i = 0; i < refusals.Count; i++)
            reasons.Add(new RejectionReason("$loc:reject.resource-committed", refusals[i]));

        return reasons;
    }

    /// <summary>Day index on the 30/360 grid — the scheduler's calendars are
    /// day-granular and the tick is a month (SDD-001 §3).</summary>
    internal static int StartDay(SimulationClock clock) =>
        clock.CurrentTick.Value * (int)Duration.DaysPerTick;
}

/// <summary>Cannot fail (R1 §2.5) — everything that could refuse already has.</summary>
internal sealed class DrillWellApplier(
    DrillingState drilling, SimulationClock clock) : ICommandApplier<DrillWellCommand>
{
    public Applied Apply(DrillWellCommand command, AuditId submission)
    {
        ArgumentNullException.ThrowIfNull(command);

        int startDay = DrillWellValidator.StartDay(clock);

        ScheduleResult result = drilling.Scheduler.Submit(
            drilling.SpecFor(command.Target, command.TotalDepth),
            startDay,
            availableCapabilities: [],
            targetExists: _ => true);

        // The validator asked the same question a moment ago and was told yes.
        // Reaching here is a composition defect, not a player error, so it is an
        // invariant fault rather than a rejection.
        if (result is not Scheduled scheduled)
            throw new InvariantFault("R1 §2.5", null,
                "the drilling command passed validation and the scheduler then refused it; " +
                "an applier cannot fail");

        drilling.Begin(new WellUnderConstruction(
            scheduled.Operation, command.Target, command.TotalDepth, startDay));

        return new Applied(submission, []);
    }
}
