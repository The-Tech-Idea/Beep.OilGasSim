// SDD-001 §6 — events. Notifications never carry control flow (design 16 §1):
// there is deliberately NO Subscribe(). Engine code cannot react to events;
// consumers poll the sealed set after AdvanceTick.

namespace OGSim.Kernel;

/// <summary>Design 16 §5. Loop-entry events are at least Warning (rule IR4).</summary>
public enum Severity { Info, Notice, Warning, Critical, Decision }

/// <summary>Design 16 §3.</summary>
public enum EventCategory
{
    Time, Command, Operation, Discovery, Production, Reservoir, Equipment,
    Hse, Environment, Regulatory, Financial, Market, Licence, Technology,
    Objective, Diagnostic
}

/// <summary>Design 21 §6 — severity is assigned by loop position, not consequence size.</summary>
public enum LoopRole { None, Entry, MidLoop, Consequence }

/// <summary>The FOURTEEN tick stages, numbered exactly as design 03 §6.</summary>
public enum StageId
{
    Open = 0, Commands = 1, Environment = 2, Operations = 3, Availability = 4,
    SolveFlow = 5, MaterialBalance = 6, Custody = 7, Economics = 8,
    HseRegulation = 9, Information = 10, Company = 11, Objectives = 12, Close = 13
}

public readonly record struct EventId(ulong Value);

/// <summary>
/// Design 16 §2, one field per anatomy row. Payloads are typed properties on
/// concrete sealed records per module — never formatted strings (EM4).
/// Cause is REQUIRED for Critical/Decision severity (IR6 — publish throws
/// without it, INV12). Day is the /30ths-grid sub-tick position.
/// IsSegmentBoundary is true only for events that change network topology or
/// constraints (design 21 §5).
/// </summary>
public abstract record EngineEvent(
    EventId Id,
    EventCategory Category,
    StageId Stage,
    Tick Tick,
    int Day,
    EntityRef? Subject,
    Severity Severity,
    AuditId Cause,
    LoopRole LoopRole,
    bool IsSegmentBoundary);

/// <summary>
/// Publish is engine-internal (stages only); Sealed returns a tick's complete,
/// ordered event set — ordering (Stage, Day, Subject, EventId) is total and
/// deterministic (design 21 §5.3). Only the most recent tick is retained;
/// history lives in the audit trail (EM-D2, SDD-017 §1).
/// </summary>
public interface IEventBus
{
    /// <summary>Callers construct with default(EventId); the bus stamps the
    /// per-tick publish sequence and returns it — modules never see a global
    /// counter, yet the (Stage, Day, Subject, EventId) total order stays
    /// deterministic (pass 4, finding 75; same pattern as IAuditTrail.Record).</summary>
    EventId Publish(EngineEvent e);
    IReadOnlyList<EngineEvent> Sealed(Tick tick);
}
