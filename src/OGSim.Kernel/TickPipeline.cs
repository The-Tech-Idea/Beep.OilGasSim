// R1.13 / R1.17 — the turn-based engine surface and the fourteen-stage tick
// (SDD-001 §3, §9; design 03 §6).
//
// The order is load-bearing and is declared HERE, in one place, rather than
// distributed across modules that each "know" when they run (design 03 §6). A
// module declares which stage it participates in; it does not get to decide what
// a stage means or when its stage happens.
//
// AdvanceTick is the only thing that moves time. Pacing — speed settings,
// auto-pause, advance-until-condition — is entirely a host concern, so a headless
// CI run, a scenario test and a player at 8x speed drive the identical code path.
// Determinism and replay follow by construction rather than by care.

namespace OGSim.Kernel;

/// <summary>
/// The three outcomes of a tick. `AbandonTick` earns its own case because
/// design 09 §5.2 argues the distinction: a discarded tick is not a stopped
/// engine, and it is not a tick that happened.
/// </summary>
public abstract record TickResult;

public sealed record TickCompleted : TickResult;

/// <summary>Model fault (09 §5.1 C4) — the tick is discarded whole, no partial
/// commit, and the game continues from the previous state.</summary>
public sealed record TickAbandoned(Fault Fault) : TickResult;

/// <summary>Invariant or composition fault — state is not trustworthy and the
/// engine stops. Non-convergence never reaches here: it gets the shut-in ladder
/// (design 04 §4.0b).</summary>
public sealed record TickHalted(Fault Fault) : TickResult;

public sealed class TickPipeline
{
    private readonly SimulationClock _clock;
    private readonly EventBus _events;
    private readonly AuditTrail _audit;
    private readonly IFaultPolicy _faults;
    private readonly ILog _log;
    private readonly IReadOnlyList<ITickStage> _stages;

    public TickPipeline(
        SimulationClock clock,
        EventBus events,

        // THE CONCRETE TRAIL, on the same terms as the clock above it: `Prune`
        // is on `AuditTrail` and not on `IAuditTrail`, so a module that takes
        // the interface cannot prune the trail even by mistake, and the one
        // thing that owns tick boundaries can. Exactly why `Advance` is on
        // `SimulationClock` alone (SDD-001 §5).
        AuditTrail audit,
        IFaultPolicy faults,
        ILog log,
        IReadOnlyList<ITickStage> stages)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(faults);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(stages);

        _clock = clock;
        _events = events;
        _audit = audit;
        _faults = faults;
        _log = log;

        // Sorted once, here. A pipeline that sorted per tick would be re-deciding
        // the order every month, which is exactly what 03 §6 forbids.
        var ordered = new List<ITickStage>(stages);
        ordered.Sort((left, right) => ((int)left.Id).CompareTo((int)right.Id));
        _stages = ordered;
    }

    public Tick CurrentTick => _clock.CurrentTick;

    /// <summary>
    /// One month. Stage 0 opens (the clock moves here and nowhere else), stages
    /// 1–12 run in declared order, stage 13 closes by sealing the event set.
    /// </summary>
    public TickResult AdvanceTick()
    {
        _clock.Advance();

        var context = new TickContext { Tick = _clock.CurrentTick, Date = _clock.Date };

        using (_log.Scope(ScopeKind.Tick, _clock.CurrentTick.Value.ToString(
                   System.Globalization.CultureInfo.InvariantCulture)))
        {
            for (int i = 0; i < _stages.Count; i++)
            {
                ITickStage stage = _stages[i];
                try
                {
                    using (_log.Scope(ScopeKind.Stage, stage.Id.ToString()))
                        stage.Execute(context);
                }
                catch (FaultException faulted)
                {
                    // Law L4: the only legal catch. The policy decides, this
                    // obeys — it does not inspect the exception and choose.
                    FaultResolution resolution = _faults.Report(faulted.Fault);
                    switch (resolution)
                    {
                        case FaultResolution.Continue:
                            break;

                        case FaultResolution.AbandonTick:
                            // Sealed anyway: the diagnostic that explains WHY the
                            // tick was discarded is the one thing that must
                            // survive it (09 §5.1 C4, "full diagnostic audited").
                            _events.Seal();
                            return new TickAbandoned(faulted.Fault);

                        case FaultResolution.Halt:
                            _events.Seal();
                            return new TickHalted(faulted.Fault);

                        default:
                            throw new InvariantFault("SDD-001 §5", null,
                                $"Unhandled fault resolution {resolution}.");
                    }
                }
            }
        }

        // Stage 13's closing act: the event set becomes observable exactly here
        // and not one statement earlier (EM2).
        _events.Seal();

        // AND THE TRAIL IS PRUNED, which nothing in this engine did until
        // R20d.12.21. Design 09 §4.4's retention was implemented, configured
        // with a detail window at composition and handed to the trail — and
        // `Prune` was called only by its own unit tests, so a forty-year game
        // kept every entry it ever wrote (finding 207).
        //
        // HERE, at the tick boundary, because the window is measured in TICKS
        // and the cause closure has to be computed against what is durable NOW.
        // Pruning at query time would make the answer depend on when it was
        // asked, which is the one thing a fairness record cannot afford.
        //
        // EVERY TICK, not on a cadence. The closure is O(entries), so pruning
        // often is what keeps its own cost down — a cadence would let entries
        // accumulate between runs and make each run more expensive than all the
        // runs it replaced.
        //
        // After the seal rather than before it: an abandoned or halted tick
        // returns above without reaching here, so the diagnostic explaining why
        // it was discarded is never pruned in the same breath as being written.
        _audit.Prune();

        return new TickCompleted();
    }

    /// <summary>
    /// Every stage the pipeline will run, in order. Composition checks that no
    /// two modules claim one (stage, order) slot; this is what the tick then
    /// walks, so the declared order and the executed order cannot drift.
    /// </summary>
    public IReadOnlyList<StageId> DeclaredOrder()
    {
        var order = new List<StageId>(_stages.Count);
        for (int i = 0; i < _stages.Count; i++) order.Add(_stages[i].Id);
        return order;
    }
}
