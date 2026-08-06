// R1.5-R1.7 — log, audit trail, fault policy (SDD-001 §5, design 09).
// R1 goals G5 ("nothing fails silently") and G6 ("the audit trail answers why").
// R1-V8 audit query, R1-V9 audit bounding, R1-V10 fault classification.

using OGSim.Kernel;

namespace OGSim.Kernel.Tests;

public class DiagnosticsTests
{
    private sealed class CollectingSink : ILogSink
    {
        public List<LogRecord> Records { get; } = [];
        public void Emit(LogRecord record) => Records.Add(record);
    }

    private static (SimulationClock Clock, AuditTrail Trail) NewTrail(int windowTicks)
    {
        var clock = new SimulationClock(new GameDate(1965, 1));
        return (clock, new AuditTrail(clock, new AuditRetention(windowTicks)));
    }

    private static Dictionary<string, AuditValue> Data(string key, string value) =>
        new() { [key] = new AuditValue(value) };

    // ------------------------------------------------------------- R1.5 log

    [Fact] // Design 09 §3: records carry their scope chain, outermost first
    public void R1V8_log_records_carry_the_correlation_chain()
    {
        var sink = new CollectingSink();
        var log = new Log(sink, LogLevel.Trace);

        using (log.Scope(ScopeKind.Session, "s1"))
        using (log.Scope(ScopeKind.Tick, "132"))
        using (log.Scope(ScopeKind.Element, "W-014"))
        {
            log.Write(LogLevel.Info, "well.shut-in", [new LogField("reason", "backpressure")]);
        }

        LogRecord record = Assert.Single(sink.Records);
        Assert.Equal("well.shut-in", record.EventName);
        Assert.Equal(3, record.Scopes.Count);
        Assert.Equal(new LogScope(ScopeKind.Session, "s1"), record.Scopes[0]);
        Assert.Equal(new LogScope(ScopeKind.Element, "W-014"), record.Scopes[2]);

        // The chain is a copy: after the scopes close, the record still shows
        // where it happened rather than an empty stack.
        Assert.Equal(3, sink.Records[0].Scopes.Count);
    }

    [Fact] // Trace is enormous and off by default — the level gate precedes the sink
    public void R1V8_records_below_the_minimum_level_never_reach_the_sink()
    {
        var sink = new CollectingSink();
        var log = new Log(sink, LogLevel.Warning);

        log.Write(LogLevel.Trace, "solver.iteration", []);
        log.Write(LogLevel.Debug, "stage.summary", []);
        log.Write(LogLevel.Info, "tick.open", []);
        Assert.Empty(sink.Records);

        log.Write(LogLevel.Warning, "constraint.hard", []);
        log.Write(LogLevel.Critical, "engine.halt", []);
        Assert.Equal(2, sink.Records.Count);
    }

    [Fact] // Out-of-order closure would mis-attribute every later record
    public void R1V8_scopes_must_close_in_reverse_order()
    {
        var log = new Log(new CollectingSink(), LogLevel.Trace);

        IDisposable outer = log.Scope(ScopeKind.Tick, "1");
        IDisposable inner = log.Scope(ScopeKind.Stage, "SolveFlow");

        Assert.Throws<InvariantFault>(outer.Dispose);

        inner.Dispose();
        outer.Dispose();
        outer.Dispose();   // idempotent: a double dispose is not a defect
    }

    // ------------------------------------------------------------- R1.6 audit

    [Fact] // R1-V8: by entity, by tick range, by category
    public void R1V8_the_trail_answers_by_entity_tick_and_category()
    {
        (SimulationClock clock, AuditTrail trail) = NewTrail(500);
        var well = new EntityRef(EntityKind.Well, 14);
        var other = new EntityRef(EntityKind.Well, 21);

        for (int tick = 0; tick < 10; tick++)
        {
            trail.Record(AuditCategory.StateTransition, well, null, Data("status", $"t{tick}"));
            trail.Record(AuditCategory.Financial, other, null, Data("cash", $"t{tick}"));
            clock.Advance();
        }

        IReadOnlyList<AuditEntry> byEntity = trail.Query(new AuditQuery(well, null, null, null));
        Assert.Equal(10, byEntity.Count);
        Assert.All(byEntity, e => Assert.Equal(well, e.Subject));

        IReadOnlyList<AuditEntry> byCategory =
            trail.Query(new AuditQuery(null, AuditCategory.Financial, null, null));
        Assert.Equal(10, byCategory.Count);

        IReadOnlyList<AuditEntry> byRange = trail.Query(
            new AuditQuery(null, null, new TickRange(new Tick(3), new Tick(5)), null));
        Assert.Equal(6, byRange.Count);   // 3 ticks x 2 entries

        IReadOnlyList<AuditEntry> combined = trail.Query(
            new AuditQuery(well, null, new TickRange(new Tick(3), new Tick(5)), null));
        Assert.Equal(3, combined.Count);
    }

    [Fact] // Entries come back in id order regardless of which index served them
    public void R1V8_results_are_in_ascending_id_order()
    {
        (SimulationClock clock, AuditTrail trail) = NewTrail(500);
        var well = new EntityRef(EntityKind.Well, 1);

        for (int i = 0; i < 20; i++)
        {
            trail.Record(AuditCategory.StateTransition, well, null, Data("i", i.ToString()));
            clock.Advance();
        }

        IReadOnlyList<AuditEntry> results = trail.Query(new AuditQuery(well, null, null, null));
        for (int i = 1; i < results.Count; i++)
            Assert.True(results[i - 1].Id.Value < results[i].Id.Value);
    }

    [Fact] // Design 09 §4.3: the "why is W-014 shut in?" walk
    public void R1V8_the_cause_chain_walks_back_to_the_root_decision()
    {
        (SimulationClock clock, AuditTrail trail) = NewTrail(500);

        AuditId root = trail.Record(AuditCategory.Command, null, null, Data("cmd", "open W-021"));
        clock.Advance();
        AuditId pressure = trail.Record(AuditCategory.ConstraintBinding, null, root, Data("m02", "rose"));
        clock.Advance();
        AuditId shutIn = trail.Record(AuditCategory.ForcedShutIn, null, pressure, Data("well", "W-014"));

        IReadOnlyList<AuditEntry> chain = trail.Query(new AuditQuery(null, null, null, shutIn));
        Assert.Equal(3, chain.Count);
        Assert.Equal(root, chain[0].Id);        // cause before consequence
        Assert.Equal(pressure, chain[1].Id);
        Assert.Equal(shutIn, chain[2].Id);
    }

    [Fact] // A cause that does not resolve is a chain that lies about being complete
    public void R1V8_an_unresolvable_cause_is_refused()
    {
        (_, AuditTrail trail) = NewTrail(500);
        Assert.Throws<InvariantFault>(() =>
            trail.Record(AuditCategory.Fault, null, new AuditId(999), Data("k", "v")));
    }

    [Fact] // Entries are immutable once written (R1 §2.4)
    public void R1V8_a_recorded_entry_does_not_change_when_the_caller_reuses_its_data()
    {
        (_, AuditTrail trail) = NewTrail(500);
        var data = Data("status", "producing");

        AuditId id = trail.Record(AuditCategory.StateTransition, null, null, data);
        data["status"] = new AuditValue("abandoned");   // caller reuses the dictionary

        AuditEntry entry = Assert.Single(trail.Query(new AuditQuery(null, null, null, id)));
        Assert.Equal("producing", entry.Data["status"].Value);
    }

    // ------------------------------------------------------------- R1.6 bounds

    [Fact] // R1-V9: bounded over 500 ticks, and no state transition discarded
    public void R1V9_growth_is_bounded_and_durable_entries_survive()
    {
        (SimulationClock clock, AuditTrail trail) = NewTrail(24);

        for (int tick = 0; tick < 500; tick++)
        {
            // One durable decision and ten per-element details per tick.
            trail.Record(AuditCategory.StateTransition, new EntityRef(EntityKind.Well, 1), null,
                         Data("tick", tick.ToString()));
            for (int element = 0; element < 10; element++)
                trail.Record(AuditCategory.ConstraintBinding, null, null, Data("el", element.ToString()));

            clock.Advance();
            trail.Prune();
        }

        // Unbounded would be 5,500 entries. Bounded keeps 500 state transitions
        // plus roughly one window of detail.
        Assert.Equal(500, trail.Query(
            new AuditQuery(null, AuditCategory.StateTransition, null, null)).Count);
        Assert.True(trail.Count < 1_200, $"trail grew to {trail.Count}");
        Assert.True(trail.Count > 500, "detail inside the window should survive");
    }

    [Fact] // §4.4: "nothing that explains the CURRENT state is ever discarded"
    public void R1V9_a_pruned_category_survives_if_it_still_explains_something()
    {
        (SimulationClock clock, AuditTrail trail) = NewTrail(10);

        // A per-element detail at tick 0 — normally prunable.
        AuditId ancientDetail = trail.Record(AuditCategory.ConstraintBinding, null, null,
                                             Data("m02", "backpressure"));
        // ...which is the reason a well is still shut in much later.
        clock.Advance();
        AuditId shutIn = trail.Record(AuditCategory.ForcedShutIn,
                                      new EntityRef(EntityKind.Well, 14), ancientDetail,
                                      Data("well", "W-014"));

        // An unrelated detail from the same old tick, explaining nothing.
        AuditId orphan = trail.Record(AuditCategory.ConstraintBinding, null, null, Data("x", "y"));

        for (int tick = 0; tick < 100; tick++) { clock.Advance(); trail.Prune(); }

        // The orphaned detail is gone; the one holding up the chain is not.
        Assert.Empty(trail.Query(new AuditQuery(null, null, null, orphan)));

        IReadOnlyList<AuditEntry> chain = trail.Query(new AuditQuery(null, null, null, shutIn));
        Assert.Equal(2, chain.Count);
        Assert.Equal(ancientDetail, chain[0].Id);   // the explanation survived
    }

    // ------------------------------------------------------------- R1.7 faults

    private static (StrictFaultPolicy Strict, ResilientFaultPolicy Resilient, CollectingSink Sink, AuditTrail Trail) Policies()
    {
        var sink = new CollectingSink();
        var log = new Log(sink, LogLevel.Trace);
        (_, AuditTrail trail) = NewTrail(500);
        return (new StrictFaultPolicy(log, trail), new ResilientFaultPolicy(log, trail), sink, trail);
    }

    private static Fault FaultOf(FaultClass faultClass) =>
        new(faultClass, "TEST-1", null, $"a {faultClass} fault");

    [Fact] // R1-V10: each of the six classes routes to its designed outcome
    public void R1V10_the_resilient_policy_follows_the_design_09_table()
    {
        (_, ResilientFaultPolicy resilient, _, _) = Policies();

        // §5.3: halts only on invariant and composition.
        Assert.Equal(FaultResolution.Halt, resilient.Report(FaultOf(FaultClass.Invariant)));
        Assert.Equal(FaultResolution.Halt, resilient.Report(FaultOf(FaultClass.Composition)));

        // §5.1 C4: out-of-range model inputs abandon the tick whole, never
        // part-commit plausible-looking wrong numbers.
        Assert.Equal(FaultResolution.AbandonTick, resilient.Report(FaultOf(FaultClass.Model)));

        // Surfaced to the player, run continues.
        Assert.Equal(FaultResolution.Continue, resilient.Report(FaultOf(FaultClass.Content)));
        Assert.Equal(FaultResolution.Continue, resilient.Report(FaultOf(FaultClass.Command)));

        // §5.1 C6: a host fault is a programming error, thrown either way.
        Assert.Throws<InvariantFault>(() => resilient.Report(FaultOf(FaultClass.Host)));
    }

    [Fact] // §5.3: strict tolerates nothing — that is the whole configuration
    public void R1V10_the_strict_policy_throws_on_every_class()
    {
        (StrictFaultPolicy strict, _, _, _) = Policies();

        Assert.Throws<ModelFault>(() => strict.Report(FaultOf(FaultClass.Model)));
        Assert.Throws<SaveDataFault>(() => strict.Report(FaultOf(FaultClass.Content)));
        Assert.Throws<InvariantFault>(() => strict.Report(FaultOf(FaultClass.Invariant)));
        Assert.Throws<InvariantFault>(() => strict.Report(FaultOf(FaultClass.Composition)));
        Assert.Throws<InvariantFault>(() => strict.Report(FaultOf(FaultClass.Command)));
        Assert.Throws<InvariantFault>(() => strict.Report(FaultOf(FaultClass.Host)));
    }

    [Fact] // G5: nothing fails silently. Resilient never HIDES, it only continues
    public void G5_every_fault_is_logged_and_audited_before_any_decision()
    {
        (_, ResilientFaultPolicy resilient, CollectingSink sink, AuditTrail trail) = Policies();

        resilient.Report(FaultOf(FaultClass.Model));
        resilient.Report(FaultOf(FaultClass.Content));

        Assert.Equal(2, sink.Records.Count);
        Assert.All(sink.Records, r => Assert.Equal("fault", r.EventName));

        IReadOnlyList<AuditEntry> audited =
            trail.Query(new AuditQuery(null, AuditCategory.Fault, null, null));
        Assert.Equal(2, audited.Count);
        Assert.Equal("Model", audited[0].Data["class"].Value);
    }

    [Fact] // Strict records too — it stops afterwards, it does not skip the record
    public void G5_the_strict_policy_records_before_it_throws()
    {
        (StrictFaultPolicy strict, _, CollectingSink sink, AuditTrail trail) = Policies();

        Assert.Throws<ModelFault>(() => strict.Report(FaultOf(FaultClass.Model)));

        Assert.Single(sink.Records);
        Assert.Single(trail.Query(new AuditQuery(null, AuditCategory.Fault, null, null)));
    }
}
