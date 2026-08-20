# SimulationClock

Source: `src\OGSim.Kernel\SimulationClock.cs` · Lines: 53

## File intent

> R1.3 — the one clock (SDD-001 §3). Design 15 §2.6: the engine is turn-based
> and the tick is the ONLY thing that moves time. Pacing — speed settings,
> auto-pause, advance-until-condition — is a host concern the engine never sees,
> which is why a headless CI run and a player at 8x speed drive identical code.
> 
> ISimulationClock is deliberately read-only: Advance() is on the concrete type,
> so only the composition root's tick pipeline — which holds it — can move time.
> A module that took ISimulationClock cannot advance the clock even by mistake.

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L13` `public sealed class SimulationClock : ISimulationClock`

## Accessible members

- `L15` `private readonly GameDate _epoch;`
- `L19` `public SimulationClock(GameDate epoch)`
- `L28` `public Tick CurrentTick { get; private set; }`
- `L30` `public GameDate Date => _epoch.AddMonths(CurrentTick.Value);`
- `L36` `public void Advance() => CurrentTick = CurrentTick.Next;`
- `L43` `public void RestoreTo(Tick tick)`

