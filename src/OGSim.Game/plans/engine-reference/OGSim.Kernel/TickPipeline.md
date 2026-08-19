# TickPipeline

Source: `src\OGSim.Kernel\TickPipeline.cs` · Lines: 140

## File intent

> R1.13 / R1.17 — the turn-based engine surface and the fourteen-stage tick
> (SDD-001 §3, §9; design 03 §6).
> 
> The order is load-bearing and is declared HERE, in one place, rather than
> distributed across modules that each "know" when they run (design 03 §6). A
> module declares which stage it participates in; it does not get to decide what
> a stage means or when its stage happens.
> 

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L21` `public abstract record TickResult;`
- `L23` `public sealed record TickCompleted : TickResult;`
- `L27` `public sealed record TickAbandoned(Fault Fault) : TickResult;`
- `L32` `public sealed record TickHalted(Fault Fault) : TickResult;`
- `L34` `public sealed class TickPipeline`

## Accessible members

- `L36` `private readonly SimulationClock _clock;`
- `L37` `private readonly EventBus _events;`
- `L38` `private readonly IAuditTrail _audit;`
- `L39` `private readonly IFaultPolicy _faults;`
- `L40` `private readonly ILog _log;`
- `L41` `private readonly IReadOnlyList<ITickStage> _stages;`
- `L43` `public TickPipeline(`
- `L71` `public Tick CurrentTick => _clock.CurrentTick;`
- `L77` `public TickResult AdvanceTick()`
- `L134` `public IReadOnlyList<StageId> DeclaredOrder()`

## Imports

- `using (_log.Scope(ScopeKind.Tick, _clock.CurrentTick.Value.ToString(`
- `using (_log.Scope(ScopeKind.Stage, stage.Id.ToString()))`

