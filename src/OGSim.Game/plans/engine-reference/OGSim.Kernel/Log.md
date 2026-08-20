# Log

Source: `src\OGSim.Kernel\Log.cs` · Lines: 65

## File intent

> R1.5 — the log (SDD-001 §5, design 09 §3). Developer-facing and ephemeral:
> unlike the audit trail it is not saved with the game and carries no player
> meaning. Structured records, never formatted strings — a level, an event name,
> typed fields, and the correlation scope the record occurred in.

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L8` `public sealed class Log : ILog`
- `L49` `private sealed class ScopeHandle(Log owner, int depth) : IDisposable`

## Accessible members

- `L10` `private readonly ILogSink _sink;`
- `L11` `private readonly LogLevel _minimumLevel;`
- `L12` `private readonly List<LogScope> _scopes = [];`
- `L17` `public Log(ILogSink sink, LogLevel minimumLevel)`
- `L24` `public void Write(LogLevel level, string eventName, IReadOnlyList<LogField> fields)`
- `L37` `public IDisposable Scope(ScopeKind kind, string id)`
- `L51` `private bool _closed;`
- `L53` `public void Dispose()`

