# SetChoke

Source: `src\OGSim.Composition\SetChoke.cs` · Lines: 86

## File intent

> R20.4 — shutting a well in (SDD-003 §5.1, design 04 §5 stage 3).
> 
> THE LEVER THE ECONOMICS DEMAND. Operating cost scales with the liquid a field
> lifts, and it is charged on water as readily as on oil — so a well at a high
> water cut eventually costs more to produce than it earns. Until this, a player
> could watch that happen and do nothing about it.
> 
> NOT AN ACTIVITY. Everything on the scheduled-activity engine is a project: it

## Namespaces

- `OGSim.Composition`

## Type declarations

- `L21` `public sealed record SetWellChokeCommand(`
- `L29` `internal sealed class SetWellChokeValidator(FieldControl field)`
- `L61` `internal sealed class SetWellChokeApplier(FieldControl field, IAuditTrail audit)`

## Accessible members

- `L32` `public IReadOnlyList<RejectionReason> Validate(SetWellChokeCommand command)`
- `L64` `public Applied Apply(SetWellChokeCommand command, AuditId submission)`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

