// R1.3 — the one clock (SDD-001 §3). Design 15 §2.6: the engine is turn-based
// and the tick is the ONLY thing that moves time. Pacing — speed settings,
// auto-pause, advance-until-condition — is a host concern the engine never sees,
// which is why a headless CI run and a player at 8x speed drive identical code.
//
// ISimulationClock is deliberately read-only: Advance() is on the concrete type,
// so only the composition root's tick pipeline — which holds it — can move time.
// A module that took ISimulationClock cannot advance the clock even by mistake.
// This is law L2 read forwards: capability follows from what you were handed.

namespace OGSim.Kernel;

public sealed class SimulationClock : ISimulationClock
{
    private readonly GameDate _epoch;

    /// <param name="epoch">The real-labelled date at tick 0 — a scenario's start
    /// month (design 07 §2 eras place it), never a wall-clock reading (D-6).</param>
    public SimulationClock(GameDate epoch)
    {
        // Touch the date so a default(GameDate) epoch fails here, at composition,
        // rather than on the first tick that asks what month it is.
        _ = epoch.Quarter;
        _epoch = epoch;
        CurrentTick = new Tick(0);
    }

    public Tick CurrentTick { get; private set; }

    public GameDate Date => _epoch.AddMonths(CurrentTick.Value);

    /// <summary>
    /// One month forward. Called once per tick, at stage 0 (design 03 §6), by
    /// the pipeline alone.
    /// </summary>
    public void Advance() => CurrentTick = CurrentTick.Next;

    /// <summary>
    /// Restores the clock on load (design 11 §2). Separate from Advance because
    /// restoring is not advancing: it is allowed to jump, and only before the
    /// engine starts ticking.
    /// </summary>
    public void RestoreTo(Tick tick)
    {
        if (tick.Value < 0)
            throw new InvariantFault("SDD-001 §3", null,
                $"Cannot restore the clock to negative tick {tick.Value}.");
        if (CurrentTick.Value != 0)
            throw new InvariantFault("SDD-001 §3", null,
                $"Clock has already advanced to tick {CurrentTick.Value}; restore happens before ticking.");
        CurrentTick = tick;
    }
}
