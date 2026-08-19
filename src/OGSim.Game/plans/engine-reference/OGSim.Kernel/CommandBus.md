# CommandBus

Source: `src\OGSim.Kernel\CommandBus.cs` · Lines: 114

## File intent

> R1.9 — the command bus (SDD-001 §7, design 03 §5). Commands are the ONLY way
> anything changes, which is what makes "seed + command sequence reproduces the
> game exactly" true, and what makes every mutation auditable.
> 
> Two phases with a hard rule (R1 §2.5): validation may not mutate, and
> application may not fail. A command that reaches application has already been
> proven applicable, which is what makes a half-applied command structurally
> impossible rather than merely avoided.

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L12` `public sealed class CommandBus : ICommandBus`
- `L111` `private sealed record Handler(`

## Accessible members

- `L14` `private readonly IAuditTrail _audit;`
- `L15` `private readonly IEventBus _events;`
- `L16` `private readonly Dictionary<Type, Handler> _handlers = [];`
- `L18` `public CommandBus(IAuditTrail audit, IEventBus events)`
- `L31` `public void Register<TCommand>(`
- `L51` `public CommandResult Submit(Command command)`
- `L93` `private static Dictionary<string, AuditValue> RejectionData(`

