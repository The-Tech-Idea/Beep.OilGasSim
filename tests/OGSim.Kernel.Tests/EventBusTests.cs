// R1.8 / R1.16 / R1.18 — the event bus (SDD-001 §6, design 16, design 21 §5.3).
// R1-V21 publication at tick close, R1-V18 the total order, plus the two
// taxonomy rules the bus owns at runtime: INV12/IR6 (a critical event carries
// its cause) and IR4 (a loop-entry event is at least a Warning).
//
// R1-V20 (no engine assembly subscribes) and R1-V22 (no formatted display
// string in a payload) are architecture tests over compiled metadata and belong
// to R1.12 — they cannot be asserted from inside a unit test.

using OGSim.Kernel;

namespace OGSim.Kernel.Tests;

public class EventBusTests
{
    /// <summary>A concrete event — the abstract record is the shape modules fill in.</summary>
    private sealed record TestEvent(
        EventId Id, EventCategory Category, StageId Stage, Tick Tick, int Day,
        EntityRef? Subject, Severity Severity, AuditId Cause, LoopRole LoopRole,
        bool IsSegmentBoundary)
        : EngineEvent(Id, Category, Stage, Tick, Day, Subject, Severity, Cause,
                      LoopRole, IsSegmentBoundary);

    private static TestEvent Event(
        StageId stage, int day, Tick tick,
        EntityRef? subject = null,
        Severity severity = Severity.Info,
        AuditId cause = default,
        LoopRole loopRole = LoopRole.None) =>
        new(default, EventCategory.Production, stage, tick, day, subject, severity,
            cause, loopRole, IsSegmentBoundary: false);

    private static (SimulationClock Clock, EventBus Bus) NewBus()
    {
        var clock = new SimulationClock(new GameDate(1965, 1));
        return (clock, new EventBus(clock));
    }

    // ------------------------------------------------------------- R1-V21

    [Fact] // EM2: no event is observable mid-tick
    public void R1V21_events_are_not_observable_until_the_tick_closes()
    {
        (SimulationClock clock, EventBus bus) = NewBus();
        bus.Publish(Event(StageId.Operations, 3, clock.CurrentTick));

        // "Nothing happened" and "you asked too late" are different answers.
        Assert.Throws<InvariantFault>(() => bus.Sealed(clock.CurrentTick));

        bus.Seal();
        Assert.Single(bus.Sealed(clock.CurrentTick));
    }

    [Fact] // EM-D2: only the most recent tick is retained; history is the audit trail
    public void R1V21_only_the_most_recent_tick_is_retained()
    {
        (SimulationClock clock, EventBus bus) = NewBus();

        bus.Publish(Event(StageId.Operations, 1, clock.CurrentTick));
        bus.Seal();
        Tick first = clock.CurrentTick;

        clock.Advance();
        bus.Publish(Event(StageId.Operations, 2, clock.CurrentTick));
        bus.Publish(Event(StageId.Economics, 2, clock.CurrentTick));
        bus.Seal();

        Assert.Equal(2, bus.Sealed(clock.CurrentTick).Count);

        var evicted = Assert.Throws<InvariantFault>(() => bus.Sealed(first));
        Assert.Contains("evicted", evicted.Fault.Detail);
    }

    [Fact] // The sealed set is exactly one tick's events, never a running total
    public void R1V21_sealing_clears_the_pending_set()
    {
        (SimulationClock clock, EventBus bus) = NewBus();
        bus.Publish(Event(StageId.Operations, 1, clock.CurrentTick));
        bus.Seal();

        clock.Advance();
        bus.Seal();
        Assert.Empty(bus.Sealed(clock.CurrentTick));
    }

    // ------------------------------------------------------------- R1-V18 ordering

    [Fact] // Design 21 §5.3: (Stage, Day, Subject, EventId), total and deterministic
    public void R1V18_the_sealed_order_is_stage_then_day_then_subject_then_id()
    {
        (SimulationClock clock, EventBus bus) = NewBus();
        Tick tick = clock.CurrentTick;

        var wellTwo = new EntityRef(EntityKind.Well, 2);
        var wellTen = new EntityRef(EntityKind.Well, 10);

        // Published in deliberately scrambled order.
        bus.Publish(Event(StageId.Economics, 5, tick, wellTwo));
        bus.Publish(Event(StageId.Operations, 9, tick, wellTwo));
        bus.Publish(Event(StageId.Operations, 3, tick, wellTen));
        bus.Publish(Event(StageId.Operations, 3, tick, wellTwo));
        bus.Publish(Event(StageId.Operations, 3, tick, null));
        EventId lastOfTheTie = bus.Publish(Event(StageId.Operations, 3, tick, wellTwo));

        IReadOnlyList<EngineEvent> sealedEvents = bus.Sealed(Seal(bus, clock));

        Assert.Equal(StageId.Operations, sealedEvents[0].Stage);
        Assert.Null(sealedEvents[0].Subject);                       // subjectless first
        Assert.Equal(wellTwo, sealedEvents[1].Subject);             // id 2 before id 10
        Assert.Equal(lastOfTheTie, sealedEvents[2].Id);             // publish order breaks the tie
        Assert.Equal(wellTen, sealedEvents[3].Subject);
        Assert.Equal(9, sealedEvents[4].Day);                       // later day, same stage
        Assert.Equal(StageId.Economics, sealedEvents[5].Stage);     // later stage last
    }

    [Fact] // Without a total order two runs could seal the same set differently
    public void R1V18_the_order_is_total_so_repeated_runs_agree()
    {
        static string Fingerprint()
        {
            (SimulationClock clock, EventBus bus) = NewBus();
            Tick tick = clock.CurrentTick;
            for (int i = 0; i < 40; i++)
                bus.Publish(Event((StageId)(i % 14), i % 30, tick,
                                  new EntityRef(EntityKind.Well, (ulong)(i % 7))));

            bus.Seal();
            var text = new System.Text.StringBuilder();
            foreach (EngineEvent e in bus.Sealed(clock.CurrentTick))
                text.Append($"{(int)e.Stage}:{e.Day}:{e.Subject?.Value}:{e.Id.Value};");
            return text.ToString();
        }

        Assert.Equal(Fingerprint(), Fingerprint());
    }

    [Fact] // Ids restart each tick, so the order key stays small (SDD-001 §6)
    public void R1V18_the_id_sequence_is_per_tick()
    {
        (SimulationClock clock, EventBus bus) = NewBus();
        Assert.Equal(1UL, bus.Publish(Event(StageId.Open, 0, clock.CurrentTick)).Value);
        Assert.Equal(2UL, bus.Publish(Event(StageId.Open, 0, clock.CurrentTick)).Value);
        bus.Seal();

        clock.Advance();
        Assert.Equal(1UL, bus.Publish(Event(StageId.Open, 0, clock.CurrentTick)).Value);
    }

    [Fact] // The bus stamps the id; a caller cannot make its events sort earlier
    public void R1V18_the_bus_stamps_the_id_over_whatever_the_caller_supplied()
    {
        (SimulationClock clock, EventBus bus) = NewBus();

        var forged = new TestEvent(new EventId(999), EventCategory.Production,
            StageId.Open, clock.CurrentTick, 0, null, Severity.Info, default,
            LoopRole.None, false);
        EventId stamped = bus.Publish(forged);
        bus.Seal();

        Assert.Equal(1UL, stamped.Value);
        Assert.Equal(1UL, bus.Sealed(clock.CurrentTick)[0].Id.Value);
    }

    // ------------------------------------------------------------- R1.16 taxonomy rules

    [Fact] // INV12 / IR6: every critical event carries its cause chain
    public void R1V16_a_critical_event_without_a_cause_is_refused()
    {
        (SimulationClock clock, EventBus bus) = NewBus();

        var fault = Assert.Throws<InvariantFault>(() =>
            bus.Publish(Event(StageId.Availability, 4, clock.CurrentTick,
                              severity: Severity.Critical)));
        Assert.Equal("INV12", fault.Fault.Rule);

        Assert.Throws<InvariantFault>(() =>
            bus.Publish(Event(StageId.Availability, 4, clock.CurrentTick,
                              severity: Severity.Decision)));

        // With a cause it publishes.
        bus.Publish(Event(StageId.Availability, 4, clock.CurrentTick,
                          severity: Severity.Critical, cause: new AuditId(7)));
        bus.Seal();
        Assert.Single(bus.Sealed(clock.CurrentTick));
    }

    [Fact] // IR4: a loop-entry alert nobody sees is the failure it exists to prevent
    public void R1V16_a_loop_entry_event_below_warning_is_refused()
    {
        (SimulationClock clock, EventBus bus) = NewBus();

        var fault = Assert.Throws<InvariantFault>(() =>
            bus.Publish(Event(StageId.Company, 0, clock.CurrentTick,
                              severity: Severity.Notice, loopRole: LoopRole.Entry)));
        Assert.Equal("IR4", fault.Fault.Rule);

        bus.Publish(Event(StageId.Company, 0, clock.CurrentTick,
                          severity: Severity.Warning, loopRole: LoopRole.Entry));
        bus.Seal();
        Assert.Single(bus.Sealed(clock.CurrentTick));
    }

    [Fact] // An event for another tick would sort into a set it does not belong to
    public void R1V16_an_event_stamped_with_the_wrong_tick_is_refused()
    {
        (SimulationClock clock, EventBus bus) = NewBus();
        Assert.Throws<InvariantFault>(() => bus.Publish(Event(StageId.Open, 0, new Tick(5))));
    }

    [Fact] // The /30ths grid is the same grid segments use (design 21 §5)
    public void R1V16_a_day_outside_the_grid_is_refused()
    {
        (SimulationClock clock, EventBus bus) = NewBus();
        Assert.Throws<InvariantFault>(() => bus.Publish(Event(StageId.Open, -1, clock.CurrentTick)));
        Assert.Throws<InvariantFault>(() => bus.Publish(Event(StageId.Open, 30, clock.CurrentTick)));

        bus.Publish(Event(StageId.Open, 0, clock.CurrentTick));
        bus.Publish(Event(StageId.Open, 29, clock.CurrentTick));
        bus.Seal();
        Assert.Equal(2, bus.Sealed(clock.CurrentTick).Count);
    }

    private static Tick Seal(EventBus bus, ISimulationClock clock)
    {
        bus.Seal();
        return clock.CurrentTick;
    }
}
