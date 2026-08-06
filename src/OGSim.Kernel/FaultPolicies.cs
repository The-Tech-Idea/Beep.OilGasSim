// R1.7 — the fault policies (SDD-001 §5, design 09 §5). Law L4: this is the only
// legal destination for a catch. The policy DECIDES and the caller OBEYS, which
// is what keeps the stack context at the point of failure rather than unwinding
// it into a handler that has lost the information.
//
// Both shipped policies are complete configurations, not a real one and a stub
// (law L3). Strict is what development, CI and tests run: nothing is tolerated.
// Resilient is what a release build runs, and it never HIDES anything — it
// differs only in whether it stops. A player on a release build still sees every
// fault in the audit trail (design 09 §5.3).

namespace OGSim.Kernel;

/// <summary>
/// Shared recording. Every fault reaches the log and the trail before any
/// decision is taken, so the two policies can never disagree about what
/// happened — only about what to do next.
/// </summary>
public abstract class FaultPolicy : IFaultPolicy
{
    private readonly ILog _log;
    private readonly IAuditTrail _audit;

    protected FaultPolicy(ILog log, IAuditTrail audit)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(audit);
        _log = log;
        _audit = audit;
    }

    public FaultResolution Report(Fault fault)
    {
        ArgumentNullException.ThrowIfNull(fault);

        _log.Write(LevelFor(fault.Class), "fault",
        [
            new LogField("class", fault.Class.ToString()),
            new LogField("rule", fault.Rule),
            new LogField("detail", fault.Detail),
        ]);

        _audit.Record(AuditCategory.Fault, fault.Subject, null, new Dictionary<string, AuditValue>
        {
            ["class"] = new(fault.Class.ToString()),
            ["rule"] = new(fault.Rule),
            ["detail"] = new(fault.Detail),
        });

        return Decide(fault);
    }

    protected abstract FaultResolution Decide(Fault fault);

    /// <summary>
    /// A host fault is a programming error, not a game state (09 §5.1 C6), so it
    /// leaves through the exception path under BOTH policies. There is no
    /// resolution a simulation can take in response to its API being misused.
    /// </summary>
    protected static FaultResolution ThrowHostFault(Fault fault) =>
        throw new InvariantFault(fault.Rule, fault.Subject,
            $"Host misused the engine API: {fault.Detail}");

    private static LogLevel LevelFor(FaultClass faultClass) => faultClass switch
    {
        FaultClass.Command => LogLevel.Warning,       // a normal outcome, not an error
        FaultClass.Invariant => LogLevel.Critical,
        FaultClass.Composition => LogLevel.Critical,
        _ => LogLevel.Error,
    };
}

/// <summary>
/// Development, CI and tests. Throws on everything: nothing is tolerated, so a
/// fault cannot be introduced and then quietly lived with (design 09 §5.3).
///
/// Note what does NOT arrive here: a command the player got wrong is rejected
/// with a domain reason and is "not an error — a normal outcome" (09 §5.1 C3),
/// so it never routes through a fault policy at all. FaultClass.Command means
/// the command MACHINERY faulted, which under Strict should indeed stop the run.
/// </summary>
public sealed class StrictFaultPolicy(ILog log, IAuditTrail audit) : FaultPolicy(log, audit)
{
    protected override FaultResolution Decide(Fault fault) => fault.Class switch
    {
        FaultClass.Host => ThrowHostFault(fault),
        FaultClass.Model => throw new ModelFault(fault.Rule, fault.Subject, fault.Detail),
        FaultClass.Content => throw new SaveDataFault(fault.Rule, fault.Subject, fault.Detail),
        _ => throw new InvariantFault(fault.Rule, fault.Subject, fault.Detail),
    };
}

/// <summary>
/// Release builds. Design 09 §5.3: records everything, halts only on invariant
/// and composition faults, surfaces the rest to the player.
///
/// The asymmetry is deliberate and is argued in 09 §5.2: severity follows from
/// whether continuing could produce a WRONG GAME, not from how inconvenient
/// stopping is. A conservation violation means barrels appeared or vanished and
/// every number after it is fiction, so it halts. A model handed out-of-range
/// inputs would produce plausible-looking wrong numbers, so the tick is
/// abandoned whole rather than part-committed.
/// </summary>
public sealed class ResilientFaultPolicy(ILog log, IAuditTrail audit) : FaultPolicy(log, audit)
{
    protected override FaultResolution Decide(Fault fault) => fault.Class switch
    {
        FaultClass.Invariant => FaultResolution.Halt,
        FaultClass.Composition => FaultResolution.Halt,
        FaultClass.Model => FaultResolution.AbandonTick,
        FaultClass.Content => FaultResolution.Continue,
        FaultClass.Command => FaultResolution.Continue,
        FaultClass.Host => ThrowHostFault(fault),
        _ => throw new InvariantFault("SDD-001 §5", fault.Subject,
            $"Unclassified fault reached the policy: {fault.Class}."),
    };
}
