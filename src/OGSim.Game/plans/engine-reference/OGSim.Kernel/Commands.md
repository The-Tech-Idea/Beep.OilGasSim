# Commands

Source: `src\OGSim.Kernel\Commands.cs` · Lines: 80

## File intent

> SDD-001 §7 — the only way anything changes. Two-phase inside: validation is
> pure, application cannot fail (R1 §2.5). §7.1: commands are named
> VerbNounCommand, declared per module, and the REQUIRED set derives from the
> 61-decision catalogue via the PD1 fixture.

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L8` `public abstract record Command(EntityRef? Subject);`
- `L14` `public sealed record RejectionReason(string LocId, string Detail);`
- `L16` `public abstract record CommandResult;`
- `L18` `public sealed record Accepted(`
- `L32` `public sealed record Rejected(`
- `L42` `public interface ICommandBus`
- `L48` `public interface ICommandValidator<in TCommand> where TCommand : Command`
- `L59` `public sealed record Applied(AuditId Audit, IReadOnlyList<EngineEvent> Raised)`
- `L77` `public interface ICommandApplier<in TCommand> where TCommand : Command`

## Accessible members

- `L24` `public bool Equals(Accepted? other) =>`
- `L28` `public override int GetHashCode() => HashCode.Combine(Audit, Structural.HashOf(Immediate));`
- `L36` `public bool Equals(Rejected? other) =>`
- `L39` `public override int GetHashCode() => Structural.HashOf(Reasons);`
- `L62` `public bool Equals(Applied? other) =>`
- `L65` `public override int GetHashCode() => HashCode.Combine(Audit, Structural.HashOf(Raised));`

