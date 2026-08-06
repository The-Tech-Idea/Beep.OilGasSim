// R1.5 — the log (SDD-001 §5, design 09 §3). Developer-facing and ephemeral:
// unlike the audit trail it is not saved with the game and carries no player
// meaning. Structured records, never formatted strings — a level, an event name,
// typed fields, and the correlation scope the record occurred in.

namespace OGSim.Kernel;

public sealed class Log : ILog
{
    private readonly ILogSink _sink;
    private readonly LogLevel _minimumLevel;
    private readonly List<LogScope> _scopes = [];

    /// <param name="minimumLevel">Records below this are dropped before the sink
    /// is touched. Trace is per-solver-iteration and enormous (design 09 §3), so
    /// the cheapness of a disabled level is a real requirement, not a nicety.</param>
    public Log(ILogSink sink, LogLevel minimumLevel)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _sink = sink;
        _minimumLevel = minimumLevel;
    }

    public void Write(LogLevel level, string eventName, IReadOnlyList<LogField> fields)
    {
        if (level < _minimumLevel) return;

        ArgumentException.ThrowIfNullOrEmpty(eventName);
        ArgumentNullException.ThrowIfNull(fields);

        // The scope chain is copied, not shared: the record outlives the scope
        // it was written in, and a sink that buffered a live list would report
        // whatever the stack happened to hold when it was finally read.
        _sink.Emit(new LogRecord(level, eventName, [.. fields], [.. _scopes]));
    }

    public IDisposable Scope(ScopeKind kind, string id)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        _scopes.Add(new LogScope(kind, id));
        return new ScopeHandle(this, _scopes.Count);
    }

    /// <summary>
    /// Scopes close in reverse order of opening. Closing out of order would
    /// silently mis-attribute every record written afterwards, so it is an
    /// invariant fault rather than a best-effort unwind.
    /// </summary>
    private sealed class ScopeHandle(Log owner, int depth) : IDisposable
    {
        private bool _closed;

        public void Dispose()
        {
            if (_closed) return;   // idempotent: a double dispose is not a defect
            if (owner._scopes.Count != depth)
                throw new InvariantFault("SDD-001 §5", null,
                    $"Log scope closed out of order: opened at depth {depth}, " +
                    $"current depth is {owner._scopes.Count}.");

            owner._scopes.RemoveAt(depth - 1);
            _closed = true;
        }
    }
}
