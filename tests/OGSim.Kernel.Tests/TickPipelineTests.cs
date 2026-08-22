// R1.13 / R1.14 / R1.15 / R1.17 — the tick (SDD-001 §3, §9; design 03 §6, 15 §6).
// R1-V16 segmentation is exact, R1-V17 segmentation is not averaging,
// R1-V18 the budget merges AND audits, R1-V19 calendar boundaries.

using OGSim.Kernel;

namespace OGSim.Kernel.Tests;

public class TickPipelineTests
{
    private sealed class NullSink : ILogSink
    {
        public void Emit(LogRecord record) { }
    }

    private sealed class RecordingStage(StageId id, List<StageId> journal, Action? onExecute = null)
        : ITickStage
    {
        public StageId Id => id;
        public void Execute(TickContext context)
        {
            journal.Add(id);
            onExecute?.Invoke();
        }
    }

    private sealed record Harness(
        TickPipeline Pipeline, SimulationClock Clock, EventBus Events,
        AuditTrail Trail, List<StageId> Journal);

    private static Harness NewPipeline(Func<List<StageId>, IReadOnlyList<ITickStage>> stages, bool strict = false)
    {
        var clock = new SimulationClock(new GameDate(1965, 1));
        var events = new EventBus(clock);
        var trail = new AuditTrail(clock, new AuditRetention(500));
        var log = new Log(new NullSink(), LogLevel.Warning);
        IFaultPolicy faults = strict
            ? new StrictFaultPolicy(log, trail)
            : new ResilientFaultPolicy(log, trail);

        var journal = new List<StageId>();
        return new Harness(
            new TickPipeline(clock, events, trail, faults, log, stages(journal)),
            clock, events, trail, journal);
    }

    // ------------------------------------------------------------- R1.13 / R1.17

    [Fact] // Design 03 §6: the order is declared in one place and walked, not negotiated
    public void R1V17_stages_run_in_declared_order_regardless_of_registration_order()
    {
        Harness harness = NewPipeline(journal =>
        [
            new RecordingStage(StageId.Close, journal),
            new RecordingStage(StageId.Commands, journal),
            new RecordingStage(StageId.SolveFlow, journal),
            new RecordingStage(StageId.Open, journal),
        ]);

        Assert.IsType<TickCompleted>(harness.Pipeline.AdvanceTick());
        Assert.Equal([StageId.Open, StageId.Commands, StageId.SolveFlow, StageId.Close],
                     harness.Journal);
    }

    [Fact] // G9: AdvanceTick is the only thing that moves time
    public void R1V13_advance_tick_is_the_only_thing_that_moves_time()
    {
        Harness harness = NewPipeline(journal => [new RecordingStage(StageId.Open, journal)]);

        Assert.Equal(0, harness.Clock.CurrentTick.Value);
        harness.Pipeline.AdvanceTick();
        Assert.Equal(1, harness.Clock.CurrentTick.Value);
        Assert.Equal(new GameDate(1965, 2), harness.Clock.Date);

        harness.Pipeline.AdvanceTick();
        Assert.Equal(2, harness.Clock.CurrentTick.Value);
    }

    [Fact] // EM2: the event set becomes observable at close, not before
    public void R1V21_events_become_observable_only_when_the_tick_closes()
    {
        Tick observedMidTick = default;
        bool faultedMidTick = false;

        Harness harness = NewPipeline(journal =>
        [
            new RecordingStage(StageId.Operations, journal),
        ]);

        // Inside a stage, the running tick is not sealed.
        try { harness.Events.Sealed(new Tick(1)); }
        catch (InvariantFault) { faultedMidTick = true; }
        Assert.True(faultedMidTick);

        Assert.IsType<TickCompleted>(harness.Pipeline.AdvanceTick());
        observedMidTick = harness.Clock.CurrentTick;
        Assert.Empty(harness.Events.Sealed(observedMidTick));
    }

    // ------------------------------------------------------------- fault routing

    [Fact] // 09 §5.1 C4: a model fault discards the tick whole — it does not halt
    public void R1V10_a_model_fault_abandons_the_tick_without_halting()
    {
        var journal = new List<StageId>();
        Harness harness = NewPipeline(j =>
        [
            new RecordingStage(StageId.Open, j),
            new RecordingStage(StageId.SolveFlow, j,
                () => throw new ModelFault("MX1", null, "correlation out of range")),
            new RecordingStage(StageId.Economics, j),
        ]);

        TickResult result = harness.Pipeline.AdvanceTick();

        var abandoned = Assert.IsType<TickAbandoned>(result);
        Assert.Equal(FaultClass.Model, abandoned.Fault.Class);

        // Later stages do not run: the tick is abandoned WHOLE, never part-committed.
        Assert.Equal([StageId.Open, StageId.SolveFlow], harness.Journal);

        // The diagnostic explaining why survives the discarded tick.
        Assert.Single(harness.Trail.Query(new AuditQuery(null, AuditCategory.Fault, null, null)));
    }

    [Fact] // 09 §5.1 C5: an invariant fault means state is not trustworthy
    public void R1V10_an_invariant_fault_halts()
    {
        Harness harness = NewPipeline(journal =>
        [
            new RecordingStage(StageId.MaterialBalance, journal,
                () => throw new InvariantFault("INV1", null, "mass not conserved")),
            new RecordingStage(StageId.Close, journal),
        ]);

        var halted = Assert.IsType<TickHalted>(harness.Pipeline.AdvanceTick());
        Assert.Equal(FaultClass.Invariant, halted.Fault.Class);
        Assert.DoesNotContain(StageId.Close, harness.Journal);
    }

    [Fact] // Continue means continue: the remaining stages still run
    public void R1V10_a_continue_resolution_runs_the_rest_of_the_tick()
    {
        Harness harness = NewPipeline(journal =>
        [
            new RecordingStage(StageId.Environment, journal,
                () => throw new SaveDataFault("C1", null, "content warning")),
            new RecordingStage(StageId.Close, journal),
        ]);

        Assert.IsType<TickCompleted>(harness.Pipeline.AdvanceTick());
        Assert.Contains(StageId.Close, harness.Journal);
    }

    // ------------------------------------------------------------- R1.14 segmentation

    private static (SegmentPlanner Planner, AuditTrail Trail, SimulationClock Clock) NewPlanner()
    {
        var clock = new SimulationClock(new GameDate(1965, 1));
        var trail = new AuditTrail(clock, new AuditRetention(500));
        return (new SegmentPlanner(trail), trail, clock);
    }

    private static EntityRef Unit(ulong id) => new(EntityKind.FlowElement, id);

    [Fact] // R1-V16: a mid-tick change produces the exact duration-weighted split
    public void R1V16_a_mid_tick_change_segments_exactly()
    {
        (SegmentPlanner planner, _, _) = NewPlanner();
        EntityRef compressor = Unit(1);

        SegmentPlan plan = planner.Plan(
            [compressor],
            [new AvailabilityChange(Day: 18, compressor, Available: false, LastCommittedThroughput: 100.0)]);

        Assert.Equal(2, plan.Segments.Count);
        Assert.Equal(0, plan.Segments[0].StartDay);
        Assert.Equal(18, plan.Segments[0].DurationDays);
        Assert.Contains(compressor, plan.Segments[0].Available);

        Assert.Equal(18, plan.Segments[1].StartDay);
        Assert.Equal(12, plan.Segments[1].DurationDays);
        Assert.DoesNotContain(compressor, plan.Segments[1].Available);

        // INV9 as integer arithmetic.
        Assert.Equal(30, plan.Segments.Sum(s => s.DurationDays));
    }

    [Fact] // R1-V17: 60% available is NOT 60% capacity, which is why averaging is refused
    public void R1V17_segmentation_is_not_averaging()
    {
        (SegmentPlanner planner, _, _) = NewPlanner();
        EntityRef compressor = Unit(1);

        SegmentPlan plan = planner.Plan(
            [compressor],
            [new AvailabilityChange(18, compressor, Available: false, LastCommittedThroughput: 100.0)]);

        // A non-linear response: throughput is the SQUARE of a notional driver,
        // standing in for the solver's actual non-linearity.
        static double Response(bool available) => available ? 100.0 * 100.0 : 0.0;

        double segmented = 0.0;
        foreach (Segment segment in plan.Segments)
            segmented += Response(segment.Available.Count > 0) * segment.DurationDays;
        segmented /= 30.0;

        // The averaging alternative: 60% availability treated as 60% capacity.
        double averaged = Response(true) * 0.6 * 0.6;

        Assert.Equal(6000.0, segmented, 9);
        Assert.Equal(3600.0, averaged, 9);
        Assert.NotEqual(segmented, averaged, 6);
    }

    [Fact] // R1-V18 / TM4: over budget the boundaries merge AND the merge is audited
    public void R1V18_exceeding_the_segment_budget_merges_and_audits()
    {
        (SegmentPlanner planner, AuditTrail trail, _) = NewPlanner();

        // Five boundaries for a four-segment budget. The two lowest-impact ones
        // must go, ranked by throughput x remaining duration.
        SegmentPlan plan = planner.Plan(
            [Unit(1), Unit(2), Unit(3), Unit(4), Unit(5)],
            [
                new AvailabilityChange(5, Unit(1), false, 1000.0),   // impact 25,000 — keep
                new AvailabilityChange(10, Unit(2), false, 900.0),   // impact 18,000 — keep
                new AvailabilityChange(15, Unit(3), false, 2.0),     // impact 30      — drop
                new AvailabilityChange(20, Unit(4), false, 800.0),   // impact 8,000   — keep
                new AvailabilityChange(25, Unit(5), false, 1.0),     // impact 5       — drop
            ]);

        Assert.Equal(SegmentPlanner.MaxSegments, plan.Segments.Count);
        Assert.Equal(30, plan.Segments.Sum(s => s.DurationDays));
        Assert.Equal([0, 5, 10, 20], plan.Segments.Select(s => s.StartDay).ToArray());

        // Every merge is recorded, so the approximation is never invisible.
        IReadOnlyList<AuditEntry> merges =
            trail.Query(new AuditQuery(null, AuditCategory.Merge, null, null));
        Assert.Equal(2, merges.Count);
        Assert.Contains(merges, e => e.Data["boundaryDay"].Value == "15");
        Assert.Contains(merges, e => e.Data["boundaryDay"].Value == "25");
    }

    [Fact] // A plan with no changes is one full-length segment, not zero
    public void R1V16_no_changes_is_a_single_segment()
    {
        (SegmentPlanner planner, _, _) = NewPlanner();
        SegmentPlan plan = planner.Plan([Unit(1)], []);

        Segment only = Assert.Single(plan.Segments);
        Assert.Equal(0, only.StartDay);
        Assert.Equal(30, only.DurationDays);
    }

    [Fact] // Boundaries live in the interior of the grid; day 0 and day 30 are not boundaries
    public void R1V16_a_boundary_outside_the_grid_interior_is_refused()
    {
        (SegmentPlanner planner, _, _) = NewPlanner();

        Assert.Throws<InvariantFault>(
            () => planner.Plan([Unit(1)], [new AvailabilityChange(0, Unit(1), false, 1.0)]));
        Assert.Throws<InvariantFault>(
            () => planner.Plan([Unit(1)], [new AvailabilityChange(30, Unit(1), false, 1.0)]));
    }

    [Fact] // Two changes on one day are one boundary, not two segments of zero length
    public void R1V16_changes_sharing_a_day_produce_one_boundary()
    {
        (SegmentPlanner planner, _, _) = NewPlanner();

        SegmentPlan plan = planner.Plan(
            [Unit(1), Unit(2)],
            [
                new AvailabilityChange(12, Unit(1), false, 10.0),
                new AvailabilityChange(12, Unit(2), false, 10.0),
            ]);

        Assert.Equal(2, plan.Segments.Count);
        Assert.Equal(30, plan.Segments.Sum(s => s.DurationDays));
        Assert.Empty(plan.Segments[1].Available);
    }

    // ------------------------------------------------------------- R1.15 calendar

    [Fact] // R1-V19: quarter, year and season boundaries land on the right ticks
    public void R1V19_calendar_boundaries_fire_on_the_correct_months()
    {
        Assert.True(new GameDate(1970, 1).StartsYear);
        Assert.False(new GameDate(1970, 2).StartsYear);

        foreach (int month in new[] { 1, 4, 7, 10 })
            Assert.True(new GameDate(1970, month).StartsQuarter, $"month {month}");
        foreach (int month in new[] { 2, 3, 5, 6, 8, 9, 11, 12 })
            Assert.False(new GameDate(1970, month).StartsQuarter, $"month {month}");

        // Season boundaries are the same months in both hemispheres; only the
        // name flips, which is why StartsSeason takes no hemisphere.
        foreach (int month in new[] { 12, 3, 6, 9 })
            Assert.True(new GameDate(1970, month).StartsSeason, $"month {month}");
    }

    [Fact] // Driven by the clock across a full year, the boundaries land four and one
    public void R1V19_a_year_of_ticks_crosses_four_quarters_and_one_year()
    {
        var clock = new SimulationClock(new GameDate(1970, 1));
        int quarters = 0;
        int years = 0;

        for (int tick = 0; tick < 12; tick++)
        {
            if (clock.Date.StartsQuarter) quarters++;
            if (clock.Date.StartsYear) years++;
            clock.Advance();
        }

        Assert.Equal(4, quarters);
        Assert.Equal(1, years);
    }

    /// <summary>
    /// Finding 207. THE TRAIL IS PRUNED AT THE TICK BOUNDARY, which nothing in
    /// the engine did until R20d.12.21.
    ///
    /// <para>Design 09 §4.4's retention was implemented, CONFIGURED — an
    /// `AuditRetention` is built with a detail window and handed to the trail at
    /// composition — and tested, and `Prune` was called by nothing outside its
    /// own unit tests. A forty-year game kept every entry it ever wrote.</para>
    ///
    /// <para>A window of ONE tick, so the effect is visible in a handful of
    /// ticks rather than needing forty years: per-tick detail written long ago
    /// goes, and what design 09 §4.4 calls durable — state transitions,
    /// financial events, faults — stays however old it is.</para>
    /// </summary>
    [Fact]
    public void R1V7_the_pipeline_prunes_the_trail_at_the_tick_boundary()
    {
        var clock = new SimulationClock(new GameDate(1965, 1));
        var events = new EventBus(clock);
        var trail = new AuditTrail(clock, new AuditRetention(DetailWindowTicks: 1));
        var log = new Log(new NullSink(), LogLevel.Warning);

        var pipeline = new TickPipeline(
            clock, events, trail, new StrictFaultPolicy(log, trail), log, []);

        // One of each: a per-tick detail that retention may discard, and a
        // financial event that it may not.
        trail.Record(AuditCategory.ConstraintBinding, null, null,
            new Dictionary<string, AuditValue>(StringComparer.Ordinal) { ["x"] = new("1") });

        trail.Record(AuditCategory.Financial, null, null,
            new Dictionary<string, AuditValue>(StringComparer.Ordinal) { ["y"] = new("2") });

        Assert.Equal(2, trail.Count);

        // Far enough past the window that the detail is no longer recent.
        for (var tick = 0; tick < 5; tick++) pipeline.AdvanceTick();

        Assert.Equal(1, trail.Count);

        Assert.Equal(
            AuditCategory.Financial,
            Assert.Single(trail.Query(new AuditQuery(null, null, null, null))).Category);
    }
}
