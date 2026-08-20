# IntegrityStage

Source: `src\OGSim.Integrity\IntegrityStage.cs` · Lines: 197

## File intent

> R18.2 / R18.5 — the stage-4 integrity pass (SDD-012 §2–3).
> 
> THE ENGINE DRAWS, NOT THE MODEL. The hazard model maps condition to
> probability and stops there; this is where the `hazard` stream is consumed,
> in a FIXED component order (ascending id), so the sequence is reproducible
> and adding a component cannot re-roll an existing one's fate.
> 
> A FAILED COMPONENT IS SIMPLY ABSENT from the segment's network (design 04 §4)

## Namespaces

- `OGSim.Integrity`

## Type declarations

- `L23` `public sealed record ComponentState(`
- `L30` `public sealed record FailureOutcome(`
- `L41` `public sealed class IntegrityPass`
- `L162` `public enum MaintenanceStrategy`
- `L175` `public sealed record MaintenancePolicy(`

## Accessible members

- `L43` `private readonly IDegradationModel _degradation;`
- `L44` `private readonly ExponentialHazardModel _hazard;`
- `L45` `private readonly IRandomStream _hazardStream;`
- `L46` `private readonly IAuditTrail _audit;`
- `L48` `public IntegrityPass(`
- `L74` `public IReadOnlyList<FailureOutcome> Advance(`
- `L144` `private static string Format(double value) =>`
- `L147` `private static string Format(int value) =>`
- `L150` `private static string Format(ulong value) =>`
- `L190` `public bool IsDue(double condition, int ticksSinceService) => Strategy switch`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

