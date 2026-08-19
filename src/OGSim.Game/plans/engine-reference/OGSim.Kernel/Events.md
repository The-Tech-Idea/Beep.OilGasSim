# Events

Source: `src\OGSim.Kernel\Events.cs` · Lines: 65

## File intent

> SDD-001 §6 — events. Notifications never carry control flow (design 16 §1):
> there is deliberately NO Subscribe(). Engine code cannot react to events;
> consumers poll the sealed set after AdvanceTick.
> <summary>Design 16 §5. Loop-entry events are at least Warning (rule IR4).</summary>

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L8` `public enum Severity { Info, Notice, Warning, Critical, Decision }`
- `L11` `public enum EventCategory`
- `L19` `public enum LoopRole { None, Entry, MidLoop, Consequence }`
- `L22` `public enum StageId`
- `L29` `public readonly record struct EventId(ulong Value);`
- `L39` `public abstract record EngineEvent(`
- `L57` `public interface IEventBus`

## Accessible members

_No public/internal/protected/private member lines matched the extractor._

