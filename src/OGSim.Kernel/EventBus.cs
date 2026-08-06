// R1.8 / R1.16 / R1.18 — the event bus, the taxonomy's runtime rules, and the
// total order (SDD-001 §6, design 16, design 21 §5.3).
//
// Notifications never carry control flow (design 16 §1). There is no Subscribe()
// to implement, and its absence is the enforcement: engine code CANNOT react to
// an event, because there is no mechanism by which it could. Consumers poll
// Sealed() after AdvanceTick returns.
//
// Publishing and sealing are separate acts. EM2 requires that no event be
// observable mid-tick, so Seal() lives on this concrete type rather than on
// IEventBus — only the pipeline holding the concrete bus can close a tick, the
// same shape SimulationClock uses for Advance().

namespace OGSim.Kernel;

public sealed class EventBus : IEventBus
{
    private readonly ISimulationClock _clock;
    private readonly List<EngineEvent> _pending = [];

    private IReadOnlyList<EngineEvent> _sealedEvents = [];
    private Tick? _sealedTick;
    private ulong _nextId = 1;

    public EventBus(ISimulationClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
    }

    public EventId Publish(EngineEvent engineEvent)
    {
        ArgumentNullException.ThrowIfNull(engineEvent);

        // An event stamped with a tick other than the running one would sort
        // into a set it does not belong to, so it is refused rather than
        // silently re-stamped.
        if (engineEvent.Tick != _clock.CurrentTick)
            throw new InvariantFault("SDD-001 §6", engineEvent.Subject,
                $"Event published for tick {engineEvent.Tick.Value} while tick " +
                $"{_clock.CurrentTick.Value} is running.");

        if (engineEvent.Day < 0 || engineEvent.Day >= (int)Duration.DaysPerTick)
            throw new InvariantFault("SDD-001 §6", engineEvent.Subject,
                $"Event day {engineEvent.Day} is outside the /30ths grid [0, 30).");

        // INV12 / rule IR6 (design 21 §7): every critical event carries a cause
        // chain back to the decision that started it. An unexplained crisis is
        // the failure mode the whole audit design exists to prevent, so the
        // event cannot be published at all without one.
        if (engineEvent.Severity is Severity.Critical or Severity.Decision
            && engineEvent.Cause.Value == 0)
            throw new InvariantFault("INV12", engineEvent.Subject,
                $"{engineEvent.Severity} event '{engineEvent.GetType().Name}' has no cause; " +
                "rule IR6 requires a chain back to the decision that started it.");

        // Rule IR4 (design 21 §6): a loop-entry event is at least a Warning. An
        // entry alert published at Info is an alert nobody sees, which is
        // precisely the slow-trap failure IR4 exists to prevent.
        if (engineEvent.LoopRole == LoopRole.Entry && engineEvent.Severity < Severity.Warning)
            throw new InvariantFault("IR4", engineEvent.Subject,
                $"Loop-entry event '{engineEvent.GetType().Name}' has severity " +
                $"{engineEvent.Severity}; rule IR4 requires at least Warning.");

        // The bus stamps the id: modules never see a counter, so no module can
        // make its own events sort earlier (pass 4, finding 75).
        var id = new EventId(_nextId++);
        _pending.Add(engineEvent with { Id = id });
        return id;
    }

    /// <summary>
    /// Tick close, stage 13. Orders the tick's events and makes them visible.
    /// Before this, Sealed() for the running tick faults — that is EM2.
    /// </summary>
    public void Seal()
    {
        _pending.Sort(Compare);
        _sealedEvents = _pending.ToArray();
        _sealedTick = _clock.CurrentTick;
        _pending.Clear();

        // The id sequence is per tick (SDD-001 §6), so the next tick's first
        // event is 1 again and the order key stays small and readable.
        _nextId = 1;
    }

    /// <summary>
    /// Only the most recent tick is retained; history is the audit trail
    /// (EM-D2). Asking for any other tick faults rather than returning an empty
    /// list, because "nothing happened" and "you asked too late" are different
    /// answers and only one of them is true.
    /// </summary>
    public IReadOnlyList<EngineEvent> Sealed(Tick tick)
    {
        if (_sealedTick is not Tick sealedTick)
            throw new InvariantFault("EM2", null,
                $"No tick has been sealed yet; tick {tick.Value} is not observable.");

        if (tick != sealedTick)
            throw new InvariantFault("EM2", null,
                tick == _clock.CurrentTick && _pending.Count >= 0 && tick.Value > sealedTick.Value
                    ? $"Tick {tick.Value} is still running; events are not observable until it closes."
                    : $"Tick {tick.Value} has been evicted; only tick {sealedTick.Value} is retained " +
                      "(EM-D2 — history is the audit trail).");

        return _sealedEvents;
    }

    /// <summary>
    /// Design 21 §5.3 — the total order: stage, then day within the tick, then
    /// subject, then publish sequence. Every component is needed: stage alone
    /// ties for events in the same stage, and without the id tail the order
    /// would not be total, so two runs could seal the same set differently and
    /// the determinism digest would diverge on identical inputs.
    /// </summary>
    private static int Compare(EngineEvent left, EngineEvent right)
    {
        int byStage = ((int)left.Stage).CompareTo((int)right.Stage);
        if (byStage != 0) return byStage;

        int byDay = left.Day.CompareTo(right.Day);
        if (byDay != 0) return byDay;

        int bySubject = CompareSubjects(left.Subject, right.Subject);
        if (bySubject != 0) return bySubject;

        return left.Id.Value.CompareTo(right.Id.Value);
    }

    /// <summary>A subjectless event sorts before any subject — an arbitrary
    /// choice, but a FIXED one, which is all determinism requires.</summary>
    private static int CompareSubjects(EntityRef? left, EntityRef? right) =>
        (left, right) switch
        {
            (null, null) => 0,
            (null, _) => -1,
            (_, null) => 1,
            (EntityRef a, EntityRef b) => a.CompareTo(b),
        };
}
