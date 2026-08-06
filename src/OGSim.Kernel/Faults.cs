// SDD-001 §11 — the error surface's carriers. Design 09 §5.1 classifies a fault;
// these transport a classified Fault out to whichever catch routes it through
// IFaultPolicy (law L4). The policy DECIDES the resolution; a carrier only
// carries — which is what keeps the stack context at the point of failure.
//
// R1.1: §11 named ModelFault, InvariantFault and SaveDataFault as carriers but
// declared none of them, so DetMath's domain rule had no way to be raised. The
// POLICY implementations (strict and resilient, design 09 §5.3) are R1.7.

namespace OGSim.Kernel;

/// <summary>
/// Base carrier. Never caught by type outside the fault-policy call site: a
/// catch clause exists to hand the <see cref="Fault"/> to IFaultPolicy and obey
/// the resolution, never to inspect the exception and decide for itself.
/// </summary>
public abstract class FaultException : Exception
{
    public Fault Fault { get; }

    protected FaultException(Fault fault)
        : base($"{fault.Class} fault [{fault.Rule}]: {fault.Detail}")
        => Fault = fault;
}

/// <summary>
/// Design 09 §5.1 C4 — a model was given inputs outside its validity range.
/// Continuing would produce plausible-looking wrong numbers, so the tick is
/// abandoned whole. Non-convergence is NOT this: it gets the shut-in ladder.
/// </summary>
public sealed class ModelFault : FaultException
{
    public ModelFault(string rule, EntityRef? subject, string detail)
        : base(new Fault(FaultClass.Model, rule, subject, detail)) { }
}

/// <summary>
/// Design 09 §5.1 C5 — conservation violated, a reference did not resolve, or
/// state is inconsistent. State is not trustworthy; the engine halts.
/// </summary>
public sealed class InvariantFault : FaultException
{
    public InvariantFault(string rule, EntityRef? subject, string detail)
        : base(new Fault(FaultClass.Invariant, rule, subject, detail)) { }
}

/// <summary>
/// SDD-001 §11 — a missing or unreadable value on load. Content-class: the save
/// is rejected with every fault in the batch, never defaulted past (SDD-001 §10).
/// </summary>
public sealed class SaveDataFault : FaultException
{
    public SaveDataFault(string rule, EntityRef? subject, string detail)
        : base(new Fault(FaultClass.Content, rule, subject, detail)) { }
}
