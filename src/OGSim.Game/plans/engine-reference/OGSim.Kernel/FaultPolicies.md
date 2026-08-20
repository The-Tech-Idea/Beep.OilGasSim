# FaultPolicies

Source: `src\OGSim.Kernel\FaultPolicies.cs` · Lines: 117

## File intent

> R1.7 — the fault policies (SDD-001 §5, design 09 §5). Law L4: this is the only
> legal destination for a catch. The policy DECIDES and the caller OBEYS, which
> is what keeps the stack context at the point of failure rather than unwinding
> it into a handler that has lost the information.
> 
> Both shipped policies are complete configurations, not a real one and a stub
> (law L3). Strict is what development, CI and tests run: nothing is tolerated.
> Resilient is what a release build runs, and it never HIDES anything — it

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L19` `public abstract class FaultPolicy : IFaultPolicy`
- `L82` `public sealed class StrictFaultPolicy(ILog log, IAuditTrail audit) : FaultPolicy(log, audit)`
- `L104` `public sealed class ResilientFaultPolicy(ILog log, IAuditTrail audit) : FaultPolicy(log, audit)`

## Accessible members

- `L21` `private readonly ILog _log;`
- `L22` `private readonly IAuditTrail _audit;`
- `L24` `protected FaultPolicy(ILog log, IAuditTrail audit)`
- `L32` `public FaultResolution Report(Fault fault)`
- `L53` `protected abstract FaultResolution Decide(Fault fault);`
- `L60` `protected static FaultResolution ThrowHostFault(Fault fault) =>`
- `L64` `private static LogLevel LevelFor(FaultClass faultClass) => faultClass switch`
- `L84` `protected override FaultResolution Decide(Fault fault) => fault.Class switch`
- `L106` `protected override FaultResolution Decide(Fault fault) => fault.Class switch`

