// R12b — every activity on one engine (SDD-007 §5, design 07 §2c).
//
// NOTHING THE PLAYER DOES TO THE WORLD HAPPENS EXCEPT AS AN ACTIVITY. Drilling,
// well testing, logging, coring, surveying, working over, installing,
// abandoning — all take time, commit a resource, accrue cost while they run, and
// end in an outcome drawn once at the start.
//
// ONE ACTIVITY IS ONE CLASS, in its own file. Its terms, the refusals only it
// has, and what it MEANS when it finishes are members of the same object, so a
// template cannot be composed half-built. The first cut of this had a template's
// cost in the content block, its refusals beside its command and its effect in a
// dictionary in the module — three files per activity, and an activity with no
// effect registered was caught at completion, after the player had paid and
// waited (SDD-007 §5's R12b amendment).
//
// This file is what they SHARE and nothing else: the register of what is under
// way, the refusals every activity has, and the stage that advances them.

using OGSim.Company;
using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Operations;

namespace OGSim.Composition;

/// <summary>
/// What a template costs, how long it takes, what it needs and how it can go
/// wrong — the content shape behind every activity (SDD-007 §1).
/// </summary>
public sealed record ActivityTerms(
    ContentId Template,
    Money Cost,
    int DurationTicks,
    EntityId<IRig>? Rig,

    /// <summary>
    /// The severity this work can be done in (SDD-016 §3's weatherClass). Days
    /// rougher than this are STANDBY: committed, paid for, and buying no
    /// progress.
    ///
    /// <para>PER TEMPLATE and required, with no default. A single engine-wide
    /// limit was built and measured first, and it stops a seismic survey for the
    /// same sea that stops a subsea lift — which is both wrong and expensive
    /// (finding 214). A default would have hidden the same mistake behind a
    /// number nobody chose.</para>
    /// </summary>
    double WeatherLimit,

    /// <summary>
    /// Whether this work needs the site to be REACHABLE to begin (SDD-016 §5b's
    /// R22.6 amendment).
    ///
    /// <para>Separate from <see cref="WeatherLimit"/> and gating a different
    /// thing: the limit costs standby days to work ALREADY UNDER WAY, and this
    /// refuses work from STARTING. A rig, a vessel and a coil unit have to be
    /// brought in; a build-up survey on a well that is already shut in does
    /// not.</para>
    ///
    /// <para>Required, with no default, for the reason the limit beside it is:
    /// a default would answer for every template that never chose.</para>
    /// </summary>
    bool RequiresAccess,

    OutcomeTable Outcomes)
{
    /// <summary>
    /// The chance it goes well — every grade except the two that mean it did
    /// not. Read from the table rather than carried beside it, so the content
    /// and the odds cannot disagree.
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

/// <summary>An activity that finished, and what it was aimed at.</summary>
internal sealed record CompletedActivity(
    EntityRef Target,
    Length Depth,
    bool Succeeded,
    OutcomeGrade Grade,
    AuditId Cause);

/// <summary>
/// One activity, whole (SDD-007 §5). The register holds these; it never holds a
/// template id with the meaning kept somewhere else.
/// </summary>
internal interface IActivity
{
    ActivityTerms Terms { get; }

    /// <summary>Always <c>Terms.Template</c>. One owner, one copy (law L5).</summary>
    ContentId Template { get; }

    /// <summary>
    /// Whether it leaves something the company owns afterwards.
    ///
    /// <para>Capex if it does, opex if it leaves only knowledge (SDD-009 §1). A
    /// survey is bought and consumed in the same month, and capitalising it would
    /// let a company inflate its balance sheet by shooting seismic.</para>
    /// </summary>
    bool LeavesAnAsset { get; }

    /// <summary>
    /// Whether two of this activity may run against the same target at once.
    ///
    /// <para>False for every measurement, and load-bearing: σ combines
    /// conjugately (SDD-008 §2.1), so a player allowed to queue twenty surveys of
    /// one compartment would average the noise away for the price of twenty cheap
    /// surveys and never need to drill to learn anything.</para>
    /// </summary>
    bool OnePerTarget { get; }

    /// <summary>
    /// Which line of the cash report this activity's spend belongs on
    /// (R21 §2.4b).
    ///
    /// <para>DECLARED PER ACTIVITY, because it was inferred from
    /// <see cref="LeavesAnAsset"/> and that inference was wrong: everything
    /// leaving no asset was booked as EXPLORATION, so repairs, services, well
    /// tests and monitoring kit all appeared in a player's report as money spent
    /// looking for oil (finding 225). Capex-versus-opex and
    /// exploration-versus-operating are two different questions and one boolean
    /// cannot answer both.</para>
    /// </summary>
    MovementCategory Spend { get; }

    /// <summary>What it means that it finished. SDD-007 §5, executed.</summary>
    void Complete(CompletedActivity done, Tick tick);

    /// <summary>Wires the command that starts it.</summary>
    void Register(IModuleComposition composition, ActivityOrders orders);
}

/// <summary>
/// The base every activity derives from, generic in the order that starts it.
///
/// <para><see cref="Register"/> is the only place that knows
/// <typeparamref name="TCommand"/>, which is why an activity wires its own
/// validator and applier: the module holds <see cref="IActivity"/> and would
/// otherwise have to switch on a concrete command type to do it.</para>
/// </summary>
internal abstract class Activity<TCommand> : IActivity
    where TCommand : Command
{
    protected Activity(ActivityTerms terms)
    {
        ArgumentNullException.ThrowIfNull(terms);
        Terms = terms;
    }

    public ActivityTerms Terms { get; }

    public ContentId Template => Terms.Template;

    public abstract bool LeavesAnAsset { get; }

    public abstract bool OnePerTarget { get; }

    public abstract MovementCategory Spend { get; }

    /// <summary>What the order is aimed at, read off the command.</summary>
    public abstract (EntityRef Target, Length Depth) Aim(TCommand command);

    /// <summary>
    /// The aim of an activity that is not pointed at a depth.
    ///
    /// <para><see cref="Aim"/> carries a <see cref="Length"/> because drilling is
    /// the only template so far aimed at more than an entity; a survey is aimed
    /// at an area and a workover at a wellbore. The per-template parameter block
    /// that generalises it is R12b.16 and is not to be invented at a call site
    /// (SDD-007 §5's open item). Until it lands, a template with no depth says so
    /// here rather than passing a bare zero.</para>
    /// </summary>
    protected static Length NoDepth { get; } = new(0.0);

    /// <summary>
    /// Refusals THIS activity has and no other does — a drilling envelope, a
    /// wellbore to test in.
    ///
    /// <para>The shared ones (cash, a target, a free rig, one-per-target) belong
    /// to <see cref="ActivityOrders"/> and are asked first. An activity that adds
    /// none to them is answering, not abstaining: a survey needs no wellbore, and
    /// that absence is the whole reason it is the activity a company with nothing
    /// drilled can still order.</para>
    /// </summary>
    public abstract IReadOnlyList<RejectionReason> OwnRefusals(TCommand command);

    public abstract void Complete(CompletedActivity done, Tick tick);

    /// <summary>
    /// Virtual for the one activity that needs its own applier rather than the
    /// shared one — <c>WellTestActivity</c>'s shut-in (SDD-007 §5's R12b.18
    /// amendment). Every other activity keeps the default.
    /// </summary>
    public virtual void Register(IModuleComposition composition, ActivityOrders orders)
    {
        ArgumentNullException.ThrowIfNull(composition);

        composition.HandleCommand(
            new ActivityValidator<TCommand>(this, orders),
            new ActivityApplier<TCommand>(this, orders));
    }
}

/// <summary>One activity in flight.</summary>
internal sealed record InFlight(
    Operation Operation,
    IActivity Activity,
    EntityRef Target,
    Length Depth,
    int StartDay)
{
    /// <summary>Cost already posted to the ledger, so each tick posts only the
    /// increment. The operation accrues; the ledger is told the difference.</summary>
    public Money Posted { get; set; } = Money.Zero;
}

/// <summary>
/// Owner of <c>field.activities</c> — everything the company is currently doing,
/// and the catalogue of what it could do.
///
/// <para>It holds operations and their targets; the schedule, the contention and
/// the outcome all belong to <see cref="OperationScheduler"/>. This is a register
/// of what is under way, not a second scheduler.</para>
/// </summary>
internal sealed class ActivityState : IStateOwner
{
    private readonly List<InFlight> _running = [];
    private readonly Dictionary<ContentId, IActivity> _byTemplate = [];
    private readonly List<IActivity> _catalogue = [];

    private readonly OperationScheduler _scheduler;
    private readonly CompanyState _company;

    private readonly OGSim.Company.MarketState _market;

    public ActivityState(
        OperationScheduler scheduler,
        CompanyState company,
        IReadOnlyList<IActivity> catalogue,
        OGSim.Company.MarketState market)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(company);
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(market);

        _scheduler = scheduler;
        _company = company;
        _market = market;

        for (int i = 0; i < catalogue.Count; i++)
        {
            if (!_byTemplate.TryAdd(catalogue[i].Template, catalogue[i]))
                throw new InvariantFault("SDD-007 §1", null,
                    $"activity template '{catalogue[i].Template.Value}' is composed twice");

            _catalogue.Add(catalogue[i]);
        }
    }

    public StateKey Key { get; } = new("field.activities");

    public int SchemaVersion => 1;

    /// <summary>Nothing has to be back before this is (SDD-013 §2b).</summary>
    public IReadOnlyList<StateKey> RestoreAfter => [];

    public OperationScheduler Scheduler => _scheduler;

    /// <summary>Everything the company could order. Walked in declared order (D-5).</summary>
    public IReadOnlyList<IActivity> Catalogue => _catalogue;

    /// <summary>How many activities are under way. What the read model reports.</summary>
    public int InProgress => _running.Count;

    public IReadOnlyList<InFlight> Running => _running;

    /// <summary>
    /// WHAT THE COMPANY IS DOING — R21 §2.4b row 15, and the view SDD-017 §2 has
    /// specified as `OperationView` since it was written (R21.6).
    ///
    /// <para>The read model published a COUNT. "Two activities running" cannot
    /// answer the question the row exists for — which two, how far along, and
    /// how much has been spent — so a player could see that the rig was busy and
    /// not whether the well was nearly down or barely started.</para>
    ///
    /// <para>In the order they began, which is `_running`'s own order: two runs
    /// of one save render the same list (D-5), and "what did I start first" is
    /// the order a player thinks in.</para>
    /// </summary>
    public IReadOnlyList<OperationView> Operations()
    {
        var seen = new List<OperationView>(_running.Count);

        for (int i = 0; i < _running.Count; i++)
        {
            InFlight flight = _running[i];

            seen.Add(new OperationView(
                new EntityRef(EntityKind.Operation, flight.Operation.Id.Value),
                flight.Operation.Spec.Template.Value,
                flight.Operation.State,
                flight.Operation.ProgressDays,
                flight.Operation.Outcome.EffectiveDurationDays,
                flight.Operation.Accrued));
        }

        return seen;
    }

    /// <summary>
    /// An activity by template id. A command naming one that is not composed is a
    /// composition defect rather than a player error — the command could not have
    /// been declared without it.
    /// </summary>
    public IActivity Of(ContentId template) =>
        _byTemplate.TryGetValue(template, out IActivity? activity)
            ? activity
            : throw new InvariantFault("SDD-007 §1", null,
                $"no activity template '{template.Value}' is composed");

    /// <summary>Whether this template is already running against this target —
    /// the check behind <see cref="IActivity.OnePerTarget"/>.</summary>
    public bool IsRunning(ContentId template, EntityRef target)
    {
        for (int i = 0; i < _running.Count; i++)
            if (_running[i].Activity.Template == template && _running[i].Target == target)
                return true;

        return false;
    }

    public OperationSpec SpecFor(ContentId template, EntityRef target, Length depth)
    {
        ActivityTerms terms = Of(template).Terms;
        double days = terms.DurationTicks * Duration.DaysPerTick;

        // QUOTED AT TODAY'S RATES (SDD-009 §6's ED4). A catalogue price is a
        // price in the opening year's money; what a rig actually costs depends
        // on how many other companies want one, which is what the oil price has
        // been doing for the last year.
        //
        // Fixed HERE, when the work is scheduled, and not re-priced afterwards:
        // a company contracts at the rate it was quoted, and a boom that arrives
        // mid-well is the contractor's problem rather than the operator's. That
        // is also what makes committing early a real advantage rather than an
        // accounting detail.
        Money cost = _market.Quoted(terms.Cost);

        return new OperationSpec(
            Template: template,
            Target: target,
            BaseDurationDays: (int)days,
            Costs: new CostProfile(
                // Split so the money follows the work: an activity abandoned
                // halfway has cost most of one, not all and not none (§3).
                Mobilisation: Money.RoundHalfEven(cost.Cents * 0.15),
                PerActiveDay: Money.RoundHalfEven(cost.Cents * 0.75 / days),
                PerStandbyDay: Money.RoundHalfEven(cost.Cents * 0.20 / days),
                Completion: Money.RoundHalfEven(cost.Cents * 0.10)),
            Resources: new ResourceNeeds(terms.Rig, []),
            Requirements: new Requirements([], MinDetectClass: null, []),
            Rentals: [],
            Outcomes: terms.Outcomes);
    }

    public void Begin(InFlight activity) => _running.Add(activity);

    /// <summary>
    /// Takes a finished activity off the register AND gives back what it held —
    /// SDD-007 §5's "release resources", which nothing was doing (finding 150).
    ///
    /// <para>Both together, because they are one event. A calendar that kept its
    /// booking after the work stopped let a company's only rig be consumed by its
    /// first well and never come free again: the second activity of a game was
    /// refused as "rig 1 is committed" months after the rig had finished. Doing
    /// it here rather than at the call site is what stops the next caller
    /// forgetting half of it.</para>
    /// </summary>
    public void Finish(InFlight activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        _running.Remove(activity);
        _scheduler.Release(activity.Operation);
    }

    /// <summary>Posts the month's share of an activity's cost, as the increment
    /// since last tick.</summary>
    public void PostAccrual(InFlight activity, Tick tick, AuditId cause)
    {
        ArgumentNullException.ThrowIfNull(activity);

        Money increment = activity.Operation.Accrued - activity.Posted;
        if (increment.Cents == 0) return;

        // TWO QUESTIONS, TWO SOURCES. Whether the money bought an asset decides
        // the ACCOUNT; what kind of work it was decides the CAUSE. Inferring the
        // second from the first booked every repair as exploration (finding 225).
        bool capital = activity.Activity.LeavesAnAsset;

        _company.Ledger.Post(new Movement(
            tick,
            capital ? Account.Capex_PPE : Account.Opex,
            Account.Cash,
            increment,
            activity.Activity.Spend,
            Asset: null,
            Cause: cause));

        activity.Posted = activity.Operation.Accrued;
    }

    public void Capture(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteInt64("count", _running.Count);

        // The identity sequence, which Reinstate can only partly rebuild:
        // a finished operation leaves no id to restore, so the counter has
        // to be carried or a reloaded game reissues ids the original had
        // already spent (finding 215).
        writer.WriteInt64("next-operation-id", (long)_scheduler.NextOperationId);

        for (int i = 0; i < _running.Count; i++)
        {
            InFlight activity = _running[i];
            string at = Prefix(i);

            writer.WriteString(at + "template", activity.Activity.Template.Value);
            writer.WriteInt64(at + "operation", (long)activity.Operation.Id.Value);
            writer.WriteInt64(at + "target-kind", (long)activity.Target.Kind);
            writer.WriteInt64(at + "target-id", (long)activity.Target.Value);
            writer.WriteDouble(at + "depth", activity.Depth.Metres);
            writer.WriteInt64(at + "start-day", activity.StartDay);
            writer.WriteInt64(at + "progress-days", activity.Operation.ProgressDays);
            writer.WriteInt64(at + "accrued", activity.Operation.Accrued.Cents);
            writer.WriteInt64(at + "posted", activity.Posted.Cents);

            // THE PRICE IT WAS CONTRACTED AT (finding 215). `SpecFor` quotes at
            // TODAY's rates, which is right when the work is scheduled and wrong
            // on a reload: rebuilding the spec re-priced a running job at
            // whatever the market had moved to since, so a reloaded operation
            // accrued a different cost per day than the one it had signed for.
            writer.WriteInt64(at + "cost-mobilisation", activity.Operation.Spec.Costs.Mobilisation.Cents);
            writer.WriteInt64(at + "cost-active-day", activity.Operation.Spec.Costs.PerActiveDay.Cents);
            writer.WriteInt64(at + "cost-standby-day", activity.Operation.Spec.Costs.PerStandbyDay.Cents);
            writer.WriteInt64(at + "cost-completion", activity.Operation.Spec.Costs.Completion.Cents);
            writer.WriteInt64(at + "state", (long)activity.Operation.State);

            // The OUTCOME, saved. It was drawn when the activity began
            // (SDD-007 §4); redrawing it on load would let a player reload the
            // month before it landed and try again.
            writer.WriteInt64(at + "grade", (long)activity.Operation.Outcome.Row.Grade);
            writer.WriteDouble(at + "draw", activity.Operation.Outcome.Draw);
            writer.WriteInt64(at + "effective-days", activity.Operation.Outcome.EffectiveDurationDays);
        }
    }

    public void Restore(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        _running.Clear();

        long count = reader.ReadInt64("count");

        _scheduler.ResumeIdsFrom((ulong)reader.ReadInt64("next-operation-id"));

        for (long i = 0; i < count; i++)
        {
            string at = Prefix(i);

            var template = new ContentId(reader.ReadString(at + "template"));
            var target = new EntityRef(
                (EntityKind)reader.ReadInt64(at + "target-kind"),
                (ulong)reader.ReadInt64(at + "target-id"));

            var depth = new Length(reader.ReadDouble(at + "depth"));
            var startDay = (int)reader.ReadInt64(at + "start-day");

            IActivity activity = Of(template);

            // Quoted once, when the work was scheduled, and restored as quoted:
            // a company contracts at the rate it was given and a boom that
            // arrives mid-well is the contractor's problem (finding 215).
            OperationSpec spec = SpecFor(template, target, depth) with
            {
                Costs = new CostProfile(
                    Mobilisation: new Money(reader.ReadInt64(at + "cost-mobilisation")),
                    PerActiveDay: new Money(reader.ReadInt64(at + "cost-active-day")),
                    PerStandbyDay: new Money(reader.ReadInt64(at + "cost-standby-day")),
                    Completion: new Money(reader.ReadInt64(at + "cost-completion"))),
            };
            OutcomeRow row = RowFor(activity, (OutcomeGrade)reader.ReadInt64(at + "grade"));

            Operation operation = _scheduler.Reinstate(
                new EntityId<IOperation>((ulong)reader.ReadInt64(at + "operation")),
                spec,
                new DrawnOutcome(row, reader.ReadDouble(at + "draw"),
                                 (int)reader.ReadInt64(at + "effective-days")),
                startDay,
                progressDays: (int)reader.ReadInt64(at + "progress-days"),
                accrued: new Money(reader.ReadInt64(at + "accrued")),
                state: (OperationState)reader.ReadInt64(at + "state"));

            _running.Add(new InFlight(operation, activity, target, depth, startDay)
            {
                Posted = new Money(reader.ReadInt64(at + "posted")),
            });
        }
    }

    /// <summary>
    /// The table row for a saved grade. A save naming a grade this content does
    /// not contain is refused rather than approximated — the outcome decides what
    /// the activity produced, and guessing it would decide the game.
    /// </summary>
    private static OutcomeRow RowFor(IActivity activity, OutcomeGrade grade)
    {
        OutcomeTable table = activity.Terms.Outcomes;

        for (int i = 0; i < table.Rows.Count; i++)
            if (table.Rows[i].Grade == grade) return table.Rows[i];

        throw new SaveDataFault("SDD-007 §4", null,
            $"the save holds a {grade} outcome for '{activity.Template.Value}', which this " +
            "content's outcome table does not contain");
    }

    private static string Prefix(long index) =>
        "activity." + index.ToString("D6", System.Globalization.CultureInfo.InvariantCulture) + ".";
}

/// <summary>
/// Where an activity is ordered: the refusals every activity shares, and the
/// booking that follows when there are none.
///
/// <para>Asking is PURE (R1 §2.5) — the scheduler is asked whether a rig is free
/// without being made to reserve one, because a validator that booked a calendar
/// to find out whether it could would have changed the world to answer a
/// question (finding 148).</para>
/// </summary>
internal sealed class ActivityOrders(
    CompanyState company,
    OGSim.Company.MarketState market,
    FieldControl field,
    ActivityState activities,
    SimulationClock clock,
    OGSim.Environment.WeatherState weather,
    OGSim.Capabilities.CapabilityState capabilities)
{
    /// <summary>
    /// Every reason this order cannot be given, not the first. A player told only
    /// that the well is too deep, who then discovers the rig was busy as well,
    /// has been made to learn the truth in instalments.
    /// </summary>
    public List<RejectionReason> Refusals(ContentId template, EntityRef target, Length depth)
    {
        IActivity activity = activities.Of(template);
        var reasons = new List<RejectionReason>();

        if (company.Ledger.Cash < market.Quoted(activity.Terms.Cost))
            reasons.Add(new RejectionReason(
                "$loc:reject.insufficient-cash",
                $"{template.Value} costs {activity.Terms.Cost.Cents} cents and the company " +
                $"holds {company.Ledger.Cash.Cents}"));

        if (field.CompartmentCount == 0)
        {
            reasons.Add(new RejectionReason(
                "$loc:reject.no-target", "there is nothing here to work on"));

            // The scheduler's own target check would repeat this in its words.
            return reasons;
        }

        // THE SITE HAS TO BE REACHABLE TO START (SDD-016 §5b's R22.6 amendment).
        // Refused rather than deferred: a company that cannot move a rig until
        // June must be told in January, because the whole decision a window
        // creates is a deadline — and an order silently queued until the road
        // opened would take that decision away and hand back a surprise.
        //
        // Only at the START. Work already under way continues through a closing
        // window: the crew and the kit are on site, and the road shutting behind
        // them does not stop the job. A mechanic that suspended running
        // operations at a window boundary would strand every long job at the same
        // moment each year.
        if (activity.Terms.RequiresAccess
            && !weather.AccessOpenIn(ActivityStage.FieldRegion, clock.Date))
            reasons.Add(new RejectionReason(
                "$loc:reject.access-closed",
                $"the site cannot be reached in month {clock.Date.Month}; " +
                $"{template.Value} has to be committed while the window is open"));

        if (activity.OnePerTarget && activities.IsRunning(template, target))
            reasons.Add(new RejectionReason(
                "$loc:reject.already-under-way",
                $"a {template.Value} is already running against this target, and running a " +
                "second alongside it would buy the same knowledge twice"));

        IReadOnlyList<string> refusals = activities.Scheduler.Refusals(
            activities.SpecFor(template, target, depth),
            startDay: Today,
            // WHAT THE COMPANY ACTUALLY HOLDS (SDD-005 §2's R20d.10 amendment).
            // This was a hardcoded empty list at both call sites, standing in for
            // `TechnologyState.Acquired`, so `Requirements.RequiredCapabilities`
            // could neither refuse nor permit anything. No shipped template
            // declares a requirement today, so this refuses nothing new — it is
            // the difference between a gate that is open and one that is not
            // connected (finding 233's shape, a third time).
            availableCapabilities: capabilities.Technology.Acquired,
            targetExists: _ => true);

        for (int i = 0; i < refusals.Count; i++)
            reasons.Add(new RejectionReason("$loc:reject.resource-committed", refusals[i]));

        return reasons;
    }

    /// <summary>
    /// Books it. Everything that could refuse already has, so this cannot fail
    /// (R1 §2.5) and a scheduler that refuses here is a composition defect rather
    /// than a player error.
    /// </summary>
    public void Book(ContentId template, EntityRef target, Length depth)
    {
        ScheduleResult result = activities.Scheduler.Submit(
            activities.SpecFor(template, target, depth),
            startDay: Today,
            // WHAT THE COMPANY ACTUALLY HOLDS (SDD-005 §2's R20d.10 amendment).
            // This was a hardcoded empty list at both call sites, standing in for
            // `TechnologyState.Acquired`, so `Requirements.RequiredCapabilities`
            // could neither refuse nor permit anything. No shipped template
            // declares a requirement today, so this refuses nothing new — it is
            // the difference between a gate that is open and one that is not
            // connected (finding 233's shape, a third time).
            availableCapabilities: capabilities.Technology.Acquired,
            targetExists: _ => true);

        if (result is not Scheduled scheduled)
            throw new InvariantFault("R1 §2.5", null,
                $"the {template.Value} command passed validation and the scheduler then " +
                "refused it; an applier cannot fail");

        activities.Begin(new InFlight(
            scheduled.Operation, activities.Of(template), target, depth, Today));
    }

    /// <summary>Day index on the 30/360 grid — the scheduler's calendars are
    /// day-granular and the tick is a month (SDD-001 §3).</summary>
    private int Today => clock.CurrentTick.Value * (int)Duration.DaysPerTick;
}

/// <summary>
/// The one validator, for every activity: the shared refusals, then the ones only
/// this activity has. Pure (R1 §2.5).
/// </summary>
internal sealed class ActivityValidator<TCommand>(
    Activity<TCommand> activity,
    ActivityOrders orders) : ICommandValidator<TCommand>
    where TCommand : Command
{
    public IReadOnlyList<RejectionReason> Validate(TCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        (EntityRef target, Length depth) = activity.Aim(command);

        List<RejectionReason> reasons = orders.Refusals(activity.Template, target, depth);
        reasons.AddRange(activity.OwnRefusals(command));

        return reasons;
    }
}

/// <summary>The one applier, for every activity. Cannot fail (R1 §2.5).</summary>
internal sealed class ActivityApplier<TCommand>(
    Activity<TCommand> activity,
    ActivityOrders orders) : ICommandApplier<TCommand>
    where TCommand : Command
{
    public Applied Apply(TCommand command, AuditId submission)
    {
        ArgumentNullException.ThrowIfNull(command);

        (EntityRef target, Length depth) = activity.Aim(command);
        orders.Book(activity.Template, target, depth);

        return new Applied(submission, []);
    }
}

/// <summary>
/// Stage 3. Every activity advances a month; the ones that finished apply their
/// own meaning.
/// </summary>
internal sealed class ActivityStage(
    ActivityState activities,
    IAuditTrail audit,
    OGSim.Environment.WeatherState weather) : ITickStage
{
    public StageId Id => StageId.Operations;

    /// <summary>The one region this world has (SDD-016 §1).</summary>
    internal const int FieldRegion = 0;

    public void Execute(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Copied because completing removes from the register.
        var running = new List<InFlight>(activities.Running);

        for (int i = 0; i < running.Count; i++)
        {
            InFlight inFlight = running[i];

            if (inFlight.Operation.State is OperationState.Scheduled) inFlight.Operation.Begin();

            // WEATHER DOES NOT COST DAYS, and the reason is now a MEASUREMENT
            // rather than a schedule (findings 214, 216). Both halves are ready —
            // `WeatherState.DaysAbove` and the per-template `WeatherLimit` beside
            // it — and joining them CRASHES THE TEST HOST partway through the
            // forty-year suite, on a clean build, at a point that moves between
            // runs. That is a process-level death rather than a failing
            // assertion, so it is a defect in the join and not a fixture to
            // adjust. R22.3 owns it.
            if (inFlight.Operation.State is OperationState.Active or OperationState.Standby)
            {
                int lost = weather.DaysAbove(
                    FieldRegion, inFlight.Activity.Terms.WeatherLimit);

                inFlight.Operation.Advance(
                    activeDays: (int)Duration.DaysPerTick - lost,
                    standbyDays: lost,
                    costIndex: 1.0);
            }

            AuditId cause = audit.Record(
                AuditCategory.StateTransition, subject: null, cause: null,
                new Dictionary<string, AuditValue>(StringComparer.Ordinal)
                {
                    ["operation"] = new(Format(inFlight.Operation.Id.Value)),
                    ["template"] = new(inFlight.Activity.Template.Value),
                    ["state"] = new(inFlight.Operation.State.ToString()),
                });

            // The month's share, whatever happened. An activity that finished
            // this tick still spent this tick's money.
            activities.PostAccrual(inFlight, context.Tick, cause);

            if (inFlight.Operation.State is not (OperationState.Completed or OperationState.Failed))
                continue;

            activities.Finish(inFlight);

            bool succeeded = inFlight.Operation.State is OperationState.Completed;

            audit.Record(
                AuditCategory.StochasticOutcome, subject: null, cause: cause,
                new Dictionary<string, AuditValue>(StringComparer.Ordinal)
                {
                    ["template"] = new(inFlight.Activity.Template.Value),
                    ["succeeded"] = new(succeeded ? "true" : "false"),
                    ["grade"] = new(inFlight.Operation.Outcome.Row.Grade.ToString()),
                });

            // WHAT IT MEANS is the activity's own answer. There is no second
            // registry to consult and therefore no way to compose a template
            // whose meaning is missing (law L3, structurally).
            inFlight.Activity.Complete(
                new CompletedActivity(
                    inFlight.Target, inFlight.Depth, succeeded,
                    inFlight.Operation.Outcome.Row.Grade, cause),
                context.Tick);
        }
    }

    private static string Format(ulong value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// Composition's door through the truth wall (SDD-008 §3).
///
/// <para><see cref="OGSim.Information.ObservationSampler"/> IS the wall — it owns
/// the draw, the stream and the fairness record. This pairs it with the store and
/// with the one table saying which space a kind lives in, so an activity that
/// measures something says only WHAT it measured and never how the sampling
/// works. An <c>Apply</c> whose observation did not come out of <c>Sample</c>
/// would be a hole in the wall.</para>
/// </summary>
internal sealed class ObservationDoor(
    OGSim.Information.ObservationSampler sampler,
    IBeliefStore beliefs,
    Func<ContentId, BeliefSpace> spaceOf)
{
    /// <summary>
    /// Samples one property of one subject and files what the source could
    /// honestly have seen.
    ///
    /// <para>Nothing happens when the source cannot see the kind: absence is a
    /// legitimate answer and is NOT the same as a wide sigma (SDD-008 §3). A log
    /// that cannot see the areal extent of an accumulation must leave the belief
    /// untouched rather than blur it.</para>
    /// </summary>
    public void Deliver(
        ContentId source,
        ContentId kind,
        EntityRef subject,
        double truth,
        Provenance provenance)
    {
        BeliefSpace space = spaceOf(kind);

        // Into the kind's space BEFORE sampling: a log-space kind is sampled
        // multiplicatively, which is what stops a 35% survey returning a negative
        // accumulation (SDD-008 §3).
        double inSpace = space is BeliefSpace.Log ? Logarithm(kind, truth) : truth;

        if (sampler.Sample(source, kind, subject, inSpace, space, provenance)
            is not Observation observation)
            return;

        beliefs.Apply(observation);
    }

    private static double Logarithm(ContentId kind, double truth)
    {
        if (truth <= 0.0)
            throw new ModelFault("SDD-008 §3", null,
                $"{kind.Value} is a log-space kind and the truth behind it is " +
                $"{truth.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}; " +
                "a multiplicative quantity that reaches zero has no honest logarithm and " +
                "sampling it would poison the belief rather than widen it");

        return DetMath.Ln(truth);
    }
}
