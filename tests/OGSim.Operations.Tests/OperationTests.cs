// R12's verification suite (R12 §4, SDD-007).

using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Operations;

namespace OGSim.Operations.Tests;

public static class Fx
{
    public static readonly EntityRef Well = new(EntityKind.Well, 1);
    public static readonly EntityId<IRig> Rig = new(7);

    /// <summary>Content-shaped outcome table: mostly fine, occasionally not.</summary>
    public static OutcomeTable Table { get; } = new(
    [
        new OutcomeRow(OutcomeGrade.OnTime, 0.60, 1.0, 1.0, null),
        new OutcomeRow(OutcomeGrade.Delayed, 0.20, 1.5, 1.0, null),
        new OutcomeRow(OutcomeGrade.OverBudget, 0.10, 1.0, 1.6, null),
        new OutcomeRow(OutcomeGrade.Partial, 0.05, 1.2, 1.2, null),
        new OutcomeRow(OutcomeGrade.Failure, 0.04, 0.8, 1.1, null),
        new OutcomeRow(OutcomeGrade.Disaster, 0.01, 2.5, 3.0, DisasterDay: 12),
    ]);

    public static CostProfile Costs { get; } = new(
        Mobilisation: Money.FromMillions(2.0),
        PerActiveDay: Money.FromMillions(0.15),
        PerStandbyDay: Money.FromMillions(0.09),
        Completion: Money.FromMillions(1.0));

    public static OperationSpec Spec(
        int durationDays = 60,
        EntityId<IRig>? rig = null,
        IReadOnlyList<TechnologyId>? requiresTech = null,
        OutcomeTable? outcomes = null) =>
        new(new ContentId("drill-development-well"),
            Well,
            durationDays,
            Costs,
            new ResourceNeeds(rig ?? Rig, [(new ContentId("driller"), 6)]),
            new Requirements(requiresTech ?? [], null, []),
            [],
            outcomes ?? Table);

    public static (OperationScheduler Scheduler, AuditTrail Trail) New(ulong seed = 1965UL)
    {
        var clock = new SimulationClock(new GameDate(1970, 1));
        var trail = new AuditTrail(clock, new AuditRetention(5000));
        var scheduler = new OperationScheduler(
            new RandomSource(seed).Stream(StreamId.Operations), trail, materialCount: 3);

        scheduler.Register(Rig);
        return (scheduler, trail);
    }

    public static bool Exists(EntityRef target) => true;

    /// <summary>A table that always gives one grade — for tests about accrual and
    /// duration, where a random grade would make the arithmetic unpredictable.</summary>
    public static OutcomeTable Always(OutcomeGrade grade, double duration = 1.0, double cost = 1.0) =>
        new([new OutcomeRow(grade, 1.0, duration, cost, null)]);
}

public class SchedulingTests
{
    // ------------------------------------------------------------ R12-V3

    [Fact] // R12-V3: contention is a REJECTION WITH A REASON, never a silent queue
    public void R12V3_a_second_operation_needing_a_committed_rig_is_refused()
    {
        (OperationScheduler scheduler, _) = Fx.New();

        Assert.IsType<Scheduled>(scheduler.Submit(Fx.Spec(), 0, [], Fx.Exists));

        var refused = Assert.IsType<Refused>(scheduler.Submit(Fx.Spec(), 10, [], Fx.Exists));

        string reason = Assert.Single(refused.Refusal.Reasons);
        Assert.Contains("committed", reason);

        // AND THE REASON IS ACTIONABLE: it quotes the next free day, so the
        // player learns "you need another rig" rather than merely "no".
        Assert.Contains("next free on day", reason);
    }

    [Fact] // The quoted date is real — submitting there succeeds
    public void R12V3_the_next_free_date_in_the_refusal_actually_works()
    {
        (OperationScheduler scheduler, _) = Fx.New();
        scheduler.Submit(Fx.Spec(durationDays: 60), 0, [], Fx.Exists);

        // Worst case is 2.5x 60 = 150 days, so the rig is held to day 150.
        Assert.IsType<Scheduled>(scheduler.Submit(Fx.Spec(), 150, [], Fx.Exists));
    }

    [Fact] // Reservations are for the WORST case, so a delay never double-books
    public void R12V3_reservations_cover_the_worst_case_duration()
    {
        (OperationScheduler scheduler, _) = Fx.New();
        scheduler.Submit(Fx.Spec(durationDays: 60), 0, [], Fx.Exists);

        // Day 100 is past the BASE duration of 60 but inside the worst case of
        // 150. A schedule that reserved only the base would accept this and then
        // discover the delay had double-booked the rig.
        Assert.IsType<Refused>(scheduler.Submit(Fx.Spec(), 100, [], Fx.Exists));
    }

    // ------------------------------------------------------------ R12-V4 / V11

    [Fact] // R12-V11: an unmet capability is rejected at SCHEDULING, naming it
    public void R12V11_an_unmet_capability_is_refused_and_named()
    {
        (OperationScheduler scheduler, _) = Fx.New();

        var needed = new TechnologyId(new ContentId("horizontal-drilling"));
        var refused = Assert.IsType<Refused>(
            scheduler.Submit(Fx.Spec(requiresTech: [needed]), 0, [], Fx.Exists));

        Assert.Contains("horizontal-drilling", Assert.Single(refused.Refusal.Reasons));
    }

    [Fact] // R12-V11: the same command validates under AllCapabilities
    public void R12V11_the_same_operation_validates_when_the_capability_is_held()
    {
        (OperationScheduler scheduler, _) = Fx.New();

        var needed = new TechnologyId(new ContentId("horizontal-drilling"));

        Assert.IsType<Scheduled>(
            scheduler.Submit(Fx.Spec(requiresTech: [needed]), 0, [needed], Fx.Exists));
    }

    [Fact] // R12-V4: an absent target is rejected at scheduling
    public void R12V4_an_operation_on_a_missing_target_is_refused()
    {
        (OperationScheduler scheduler, _) = Fx.New();

        var refused = Assert.IsType<Refused>(
            scheduler.Submit(Fx.Spec(), 0, [], _ => false));

        Assert.Contains("does not exist", Assert.Single(refused.Refusal.Reasons));
    }

    [Fact] // EVERY failure is reported — one report, not one resubmission per reason
    public void R12V4_all_refusal_reasons_are_reported_together()
    {
        (OperationScheduler scheduler, _) = Fx.New();

        var refused = Assert.IsType<Refused>(scheduler.Submit(
            Fx.Spec(durationDays: 0, requiresTech: [new TechnologyId(new ContentId("subsea"))]),
            0, [], _ => false));

        // Missing capability, missing target, and a nonsensical duration.
        Assert.Equal(3, refused.Refusal.Reasons.Count);
    }

    [Fact] // An unregistered rig is a composition error, not an empty calendar
    public void R12V3_an_unregistered_rig_is_refused()
    {
        (OperationScheduler scheduler, _) = Fx.New();

        var refused = Assert.IsType<Refused>(
            scheduler.Submit(Fx.Spec(rig: new EntityId<IRig>(999)), 0, [], Fx.Exists));

        Assert.Contains("not registered", Assert.Single(refused.Refusal.Reasons));
    }
}

public class OutcomeTests
{
    // ------------------------------------------------------------ R12-V5

    [Fact] // R12-V5: all six grades occur at their declared rates over a large sample
    public void R12V5_the_grades_occur_at_their_declared_rates()
    {
        var counts = new Dictionary<OutcomeGrade, int>();

        // A fresh rig per submission, so contention does not bias the sample.
        var clock = new SimulationClock(new GameDate(1970, 1));
        var trail = new AuditTrail(clock, new AuditRetention(100));
        var scheduler = new OperationScheduler(
            new RandomSource(20240701UL).Stream(StreamId.Operations), trail, materialCount: 3);

        const int trials = 20_000;
        for (int i = 0; i < trials; i++)
        {
            var rig = new EntityId<IRig>((ulong)(i + 1));
            scheduler.Register(rig);

            var scheduled = Assert.IsType<Scheduled>(
                scheduler.Submit(Fx.Spec(rig: rig), 0, [], Fx.Exists));

            OutcomeGrade grade = scheduled.Operation.Outcome.Row.Grade;
            counts[grade] = counts.GetValueOrDefault(grade) + 1;
        }

        // Every grade must appear — including Disaster at 1%, which is the one a
        // sloppy cumulative comparison would drop off the end.
        foreach (OutcomeRow row in Fx.Table.Rows)
        {
            Assert.True(counts.ContainsKey(row.Grade), $"{row.Grade} never occurred");

            double observed = counts[row.Grade] / (double)trials;
            Assert.InRange(observed, row.Probability * 0.85, row.Probability * 1.15);
        }
    }

    // ------------------------------------------------------------ R12-V6

    [Fact] // R12-V6: the same seed gives an identical outcome sequence
    public void R12V6_the_same_seed_produces_identical_outcomes()
    {
        static List<OutcomeGrade> Run(ulong seed)
        {
            var clock = new SimulationClock(new GameDate(1970, 1));
            var scheduler = new OperationScheduler(
                new RandomSource(seed).Stream(StreamId.Operations),
                new AuditTrail(clock, new AuditRetention(100)),
                materialCount: 3);

            var grades = new List<OutcomeGrade>();
            for (int i = 0; i < 200; i++)
            {
                var rig = new EntityId<IRig>((ulong)(i + 1));
                scheduler.Register(rig);

                var scheduled = (Scheduled)scheduler.Submit(Fx.Spec(rig: rig), 0, [], Fx.Exists);
                grades.Add(scheduled.Operation.Outcome.Row.Grade);
            }

            return grades;
        }

        Assert.Equal(Run(4242UL), Run(4242UL));
        Assert.NotEqual(Run(4242UL), Run(9999UL));
    }

    // ------------------------------------------------------------ R12-V7

    [Fact] // R12-V7: every outcome is audited with its stream, draw and threshold
    public void R12V7_the_outcome_draw_is_audited_so_the_dice_can_be_checked()
    {
        (OperationScheduler scheduler, AuditTrail trail) = Fx.New();

        var scheduled = Assert.IsType<Scheduled>(scheduler.Submit(Fx.Spec(), 0, [], Fx.Exists));

        AuditEntry entry = Assert.Single(
            trail.Query(new AuditQuery(null, AuditCategory.StochasticOutcome, null, null)));

        Assert.Equal("operations", entry.Data["stream"].Value);
        Assert.Equal(scheduled.Operation.Outcome.Row.Grade.ToString(), entry.Data["grade"].Value);

        // The DRAW itself, so a player who suspects the dice can verify that the
        // number that came up actually falls under the threshold it was compared
        // against (design 09 §4.2).
        double draw = double.Parse(entry.Data["draw"].Value,
            System.Globalization.CultureInfo.InvariantCulture);
        double threshold = double.Parse(entry.Data["threshold"].Value,
            System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(scheduled.Operation.Outcome.Draw, draw, precision: 12);
        Assert.True(draw < threshold, "the recorded draw does not fall under its threshold");
    }
}

public class AccrualTests
{
    private static Operation Started(OutcomeTable outcomes, int durationDays = 60)
    {
        (OperationScheduler scheduler, _) = Fx.New();

        var scheduled = (Scheduled)scheduler.Submit(
            Fx.Spec(durationDays: durationDays, outcomes: outcomes), 0, [], Fx.Exists);

        scheduled.Operation.Begin();
        return scheduled.Operation;
    }

    // ------------------------------------------------------------ R12-V1

    [Fact] // R12-V1: an operation completes after the correct elapsed days
    public void R12V1_an_operation_completes_after_its_effective_duration()
    {
        Operation op = Started(Fx.Always(OutcomeGrade.OnTime), durationDays: 60);

        op.Advance(activeDays: 30, standbyDays: 0, costIndex: 1.0);
        Assert.Equal(OperationState.Active, op.State);
        Assert.Equal(30, op.ProgressDays);

        op.Advance(30, 0, 1.0);
        Assert.Equal(OperationState.Completed, op.State);
        Assert.Equal(60, op.ProgressDays);
    }

    [Fact] // A DELAYED outcome takes its duration factor — 1.5x, not 1x
    public void R12V1_a_delayed_outcome_takes_longer()
    {
        Operation op = Started(Fx.Always(OutcomeGrade.Delayed, duration: 1.5), durationDays: 60);

        Assert.Equal(90, op.Outcome.EffectiveDurationDays);

        op.Advance(60, 0, 1.0);
        Assert.Equal(OperationState.Active, op.State);   // not done at the base duration

        op.Advance(30, 0, 1.0);
        Assert.Equal(OperationState.Completed, op.State);
    }

    // ------------------------------------------------------------ R12-V2

    [Fact] // R12-V2: cost is SPREAD over the operation, not charged at the end
    public void R12V2_cost_accrues_over_the_operation()
    {
        Operation op = Started(Fx.Always(OutcomeGrade.OnTime), durationDays: 60);

        // Mobilisation on entering Active — before a single day is worked.
        Assert.Equal(Money.FromMillions(2.0), op.Accrued);

        op.Advance(30, 0, 1.0);
        Money halfway = op.Accrued;

        Assert.Equal(Money.FromMillions(2.0) + Money.FromMillions(0.15) * 30, halfway);

        op.Advance(30, 0, 1.0);

        // Plus the second half and the completion fee.
        Assert.Equal(halfway + Money.FromMillions(0.15) * 30 + Money.FromMillions(1.0),
                     op.Accrued);

        // This is what makes cash flow tight during development, and how an
        // over-committed company runs out of money MID-WELL rather than
        // discovering the bill on completion.
        Assert.True(halfway > Money.FromMillions(6.0));
    }

    [Fact] // Standby costs money and buys NOTHING — the price of a missed window
    public void R12V2_standby_accrues_the_day_rate_without_progress()
    {
        Operation op = Started(Fx.Always(OutcomeGrade.OnTime), durationDays: 60);
        Money atStart = op.Accrued;

        op.Advance(activeDays: 0, standbyDays: 30, costIndex: 1.0);

        Assert.Equal(0, op.ProgressDays);
        Assert.Equal(OperationState.Standby, op.State);

        // The standby RATE, not the active one: consumables stop, the day rate
        // does not (SDD-007 §3, pinned).
        Assert.Equal(atStart + Money.FromMillions(0.09) * 30, op.Accrued);
    }

    [Fact] // An OverBudget outcome costs more per day, by its declared factor
    public void R12V2_the_cost_factor_applies_to_the_day_rate()
    {
        Operation lean = Started(Fx.Always(OutcomeGrade.OnTime), durationDays: 30);
        Operation dear = Started(Fx.Always(OutcomeGrade.OverBudget, cost: 1.6), durationDays: 30);

        lean.Advance(30, 0, 1.0);
        dear.Advance(30, 0, 1.0);

        // Both completed, so both paid mobilisation and completion; only the
        // day rate differs.
        Money leanDays = lean.Accrued - Money.FromMillions(3.0);
        Money dearDays = dear.Accrued - Money.FromMillions(3.0);

        Assert.Equal(1.6, dearDays.Cents / (double)leanDays.Cents, precision: 6);
    }

    [Fact] // Escalation applies at accrual — a later operation costs more
    public void R12V2_the_cost_index_escalates_the_accrual()
    {
        Operation early = Started(Fx.Always(OutcomeGrade.OnTime), durationDays: 30);
        Operation late = Started(Fx.Always(OutcomeGrade.OnTime), durationDays: 30);

        early.Advance(30, 0, costIndex: 1.0);
        late.Advance(30, 0, costIndex: 1.5);

        Money earlyDays = early.Accrued - Money.FromMillions(3.0);
        Money lateDays = late.Accrued - Money.FromMillions(3.0);

        Assert.Equal(1.5, lateDays.Cents / (double)earlyDays.Cents, precision: 6);
    }

    // ------------------------------------------------------------ R12-V8

    [Fact] // R12-V8: cancelling releases the rig and RETAINS the sunk cost
    public void R12V8_cancellation_releases_resources_and_keeps_the_sunk_cost()
    {
        (OperationScheduler scheduler, _) = Fx.New();

        var scheduled = (Scheduled)scheduler.Submit(Fx.Spec(), 0, [], Fx.Exists);
        Operation op = scheduled.Operation;

        op.Begin();
        op.Advance(20, 0, 1.0);
        Money spent = op.Accrued;

        op.Cancel();
        scheduler.Release(op);

        Assert.Equal(OperationState.Cancelled, op.State);

        // The money STAYS SPENT: the rig was paid, the crew flew out, the casing
        // was bought. Refunding it would make starting an operation free to
        // reconsider, and commitment is supposed to cost something.
        Assert.Equal(spent, op.Accrued);
        Assert.True(op.Accrued > Money.Zero);

        // And the rig is available again at once.
        Assert.IsType<Scheduled>(scheduler.Submit(Fx.Spec(), 0, [], Fx.Exists));
    }

    [Fact] // A cancelled operation is audited with what it cost
    public void R12V8_cancellation_is_audited_with_the_sunk_cost()
    {
        (OperationScheduler scheduler, AuditTrail trail) = Fx.New();

        var scheduled = (Scheduled)scheduler.Submit(Fx.Spec(), 0, [], Fx.Exists);
        scheduled.Operation.Begin();
        scheduled.Operation.Advance(10, 0, 1.0);
        scheduled.Operation.Cancel();

        AuditEntry entry = Assert.Single(
            trail.Query(new AuditQuery(null, AuditCategory.Command, null, null)));

        Assert.Equal("cancelled", entry.Data["outcome"].Value);
        Assert.NotEqual("0", entry.Data["sunkCost"].Value);
    }

    // ------------------------------------------------------------ failure

    [Fact] // A failed operation pays no completion fee — nothing was completed
    public void R12V2_a_failed_operation_does_not_pay_the_completion_fee()
    {
        Operation lost = Started(Fx.Always(OutcomeGrade.Failure), durationDays: 30);
        lost.Advance(30, 0, 1.0);

        Assert.Equal(OperationState.Failed, lost.State);

        // Mobilisation plus days, and no completion — charging one would price a
        // lost hole like a finished well.
        Assert.Equal(Money.FromMillions(2.0) + Money.FromMillions(0.15) * 30, lost.Accrued);
    }

    [Fact] // A finished operation cannot be advanced or begun again
    public void R12V1_a_finished_operation_refuses_further_transitions()
    {
        Operation op = Started(Fx.Always(OutcomeGrade.OnTime), durationDays: 30);
        op.Advance(30, 0, 1.0);

        Assert.Throws<InvariantFault>(() => op.Advance(1, 0, 1.0));
        Assert.Throws<InvariantFault>(() => op.Cancel());
    }
}
