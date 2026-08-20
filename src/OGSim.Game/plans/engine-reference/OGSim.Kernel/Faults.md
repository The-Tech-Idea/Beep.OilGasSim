# Faults

Source: `src\OGSim.Kernel\Faults.cs` · Lines: 76

## File intent

> SDD-001 §11 — the error surface's carriers. Design 09 §5.1 classifies a fault;
> these transport a classified Fault out to whichever catch routes it through
> IFaultPolicy (law L4). The policy DECIDES the resolution; a carrier only
> carries — which is what keeps the stack context at the point of failure.
> 
> R1.1: §11 named ModelFault, InvariantFault and SaveDataFault as carriers but
> declared none of them, so DetMath's domain rule had no way to be raised. The
> POLICY implementations (strict and resilient, design 09 §5.3) are R1.7.

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L17` `public abstract class FaultException : Exception`
- `L31` `public sealed class ModelFault : FaultException`
- `L41` `public sealed class InvariantFault : FaultException`
- `L51` `public sealed class SaveDataFault : FaultException`
- `L72` `public sealed class ContentFault : FaultException`

## Accessible members

- `L19` `public Fault Fault { get; }`
- `L21` `protected FaultException(Fault fault)`
- `L33` `public ModelFault(string rule, EntityRef? subject, string detail)`
- `L43` `public InvariantFault(string rule, EntityRef? subject, string detail)`
- `L53` `public SaveDataFault(string rule, EntityRef? subject, string detail)`
- `L74` `public ContentFault(string rule, EntityRef? subject, string detail)`

