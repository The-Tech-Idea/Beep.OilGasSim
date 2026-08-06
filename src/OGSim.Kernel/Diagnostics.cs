// SDD-001 §5 — log, audit, fault. Design 09: three services, kept apart.
// The audit trail is player-facing, saved with the game; the fault policy is
// the only legal catch; the log is developer-facing and ephemeral.

namespace OGSim.Kernel;

// ---------------------------------------------------------------- log

public enum LogLevel { Trace, Debug, Info, Warning, Error, Critical }

public enum ScopeKind { Session, Tick, Stage, Element, Operation }

/// <summary>A typed log field — no string interpolation at call sites (SDD-001 §5).</summary>
public readonly record struct LogField(string Name, string Value);

public interface ILog
{
    void Write(LogLevel level, string eventName, IReadOnlyList<LogField> fields);

    /// <summary>Nested correlation scope: Session → Tick → Stage → Element (design 09 §3).</summary>
    IDisposable Scope(ScopeKind kind, string id);
}

// ---------------------------------------------------------------- audit

public readonly record struct AuditId(ulong Value) : IComparable<AuditId>
{
    public int CompareTo(AuditId other) => Value.CompareTo(other.Value);
}

public enum AuditCategory
{
    Command, StateTransition, ConstraintBinding, Rejection, Financial,
    StochasticOutcome, BeliefUpdate, Fault, InvariantCheck, ForcedShutIn, Merge
}

/// <summary>A typed audit value — never a formatted display string.</summary>
public readonly record struct AuditValue(string Value);

/// <summary>
/// One immutable entry. Cause is the chain of 21 §7 — required for every
/// C/D-severity event's provenance (rule IR6 / INV12).
/// </summary>
public sealed record AuditEntry(
    AuditId Id,
    Tick Tick,
    AuditCategory Category,
    EntityRef? Subject,
    AuditId? Cause,
    IReadOnlyDictionary<string, AuditValue> Data);

public sealed record AuditQuery(
    EntityRef? Subject,
    AuditCategory? Category,
    TickRange? Range,
    AuditId? CauseChainLeaf);

public interface IAuditTrail
{
    AuditId Record(
        AuditCategory category,
        EntityRef? subject,
        AuditId? cause,
        IReadOnlyDictionary<string, AuditValue> data);

    IReadOnlyList<AuditEntry> Query(AuditQuery query);
}

// ---------------------------------------------------------------- fault

/// <summary>Design 09 §5.1 — the six classes.</summary>
public enum FaultClass { Content, Composition, Command, Model, Invariant, Host }

public sealed record Fault(
    FaultClass Class,
    string Rule,                      // e.g. "INV1", "R2-V10", "SDD-003 §3.1 voidage limit"
    EntityRef? Subject,
    string Detail);

/// <summary>The caller OBEYS the resolution; the policy only decides (SDD-001 §5).</summary>
public enum FaultResolution { Continue, AbandonTick, Halt }

/// <summary>
/// The only legal catch destination (law L4). Strict impl throws on everything;
/// resilient impl follows the design 09 §5.1 table. Both are complete shipped
/// configurations — neither is a stub.
/// </summary>
public interface IFaultPolicy
{
    FaultResolution Report(Fault fault);
}
