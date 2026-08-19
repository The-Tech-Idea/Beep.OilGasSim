# EventBus

Source: `src\OGSim.Kernel\EventBus.cs` · Lines: 141

## File intent

> R1.8 / R1.16 / R1.18 — the event bus, the taxonomy's runtime rules, and the
> total order (SDD-001 §6, design 16, design 21 §5.3).
> 
> Notifications never carry control flow (design 16 §1). There is no Subscribe()
> to implement, and its absence is the enforcement: engine code CANNOT react to
> an event, because there is no mechanism by which it could. Consumers poll
> Sealed() after AdvanceTick returns.
> 

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L16` `public sealed class EventBus : IEventBus`

## Accessible members

- `L18` `private readonly ISimulationClock _clock;`
- `L19` `private readonly List<EngineEvent> _pending = [];`
- `L21` `private IReadOnlyList<EngineEvent> _sealedEvents = [];`
- `L22` `private Tick? _sealedTick;`
- `L23` `private ulong _nextId = 1;`
- `L25` `public EventBus(ISimulationClock clock)`
- `L31` `public EventId Publish(EngineEvent engineEvent)`
- `L76` `public void Seal()`
- `L94` `public IReadOnlyList<EngineEvent> Sealed(Tick tick)`
- `L117` `private static int Compare(EngineEvent left, EngineEvent right)`
- `L133` `private static int CompareSubjects(EntityRef? left, EntityRef? right) =>`

