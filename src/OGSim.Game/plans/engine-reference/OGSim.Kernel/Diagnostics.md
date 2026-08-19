# Diagnostics

Source: `src\OGSim.Kernel\Diagnostics.cs` · Lines: 157

## File intent

> SDD-001 §5 — log, audit, fault. Design 09: three services, kept apart.
> The audit trail is player-facing, saved with the game; the fault policy is
> the only legal catch; the log is developer-facing and ephemeral.
> ---------------------------------------------------------------- log

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L9` `public enum LogLevel { Trace, Debug, Info, Warning, Error, Critical }`
- `L11` `public enum ScopeKind { Session, Tick, Stage, Element, Operation }`
- `L14` `public readonly record struct LogField(string Name, string Value);`
- `L17` `public readonly record struct LogScope(ScopeKind Kind, string Id);`
- `L24` `public sealed record LogRecord(`
- `L45` `public interface ILogSink`
- `L50` `public interface ILog`
- `L60` `public readonly record struct AuditId(ulong Value) : IComparable<AuditId>`
- `L65` `public enum AuditCategory`
- `L86` `public readonly record struct AuditValue(string Value);`
- `L92` `public sealed record AuditEntry(`
- `L111` `public sealed record AuditQuery(`
- `L122` `public sealed record AuditRetention(int DetailWindowTicks);`
- `L124` `public interface IAuditTrail`
- `L138` `public enum FaultClass { Content, Composition, Command, Model, Invariant, Host }`
- `L140` `public sealed record Fault(`
- `L147` `public enum FaultResolution { Continue, AbandonTick, Halt }`
- `L154` `public interface IFaultPolicy`

## Accessible members

- `L31` `public bool Equals(LogRecord? other) =>`
- `L37` `public override int GetHashCode() =>`
- `L62` `public int CompareTo(AuditId other) => Value.CompareTo(other.Value);`
- `L102` `public bool Equals(AuditEntry? other) =>`
- `L107` `public override int GetHashCode() =>`

