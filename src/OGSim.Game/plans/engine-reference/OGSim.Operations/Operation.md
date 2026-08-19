# Operation

Source: `src\OGSim.Operations\Operation.cs` · Lines: 193

## File intent

> R12.1 / R12.2 — the operation and the scheduler (SDD-007 §1–4, design 08).
> 
> ONE IOperation CONTRACT, MANY OUTCOME TYPES. One abstraction means one
> scheduler, one cost-accrual path, one progress projection, one audit shape and
> one failure model — and it makes "what is my company doing" a single query
> rather than a union of subsystem views (R12 §2.1).
> 
> Two things here are the phase's real content:

## Namespaces

- `OGSim.Operations`

## Type declarations

- `L31` `public sealed record DrawnOutcome(OutcomeRow Row, double Draw, int EffectiveDurationDays);`
- `L34` `public sealed class Operation : IOperation`

## Accessible members

- `L36` `private readonly IAuditTrail _audit;`
- `L38` `private Money _accrued = Money.Zero;`
- `L39` `private int _progressDays;`
- `L41` `internal Operation(`
- `L60` `public EntityId<IOperation> Id { get; }`
- `L61` `public OperationSpec Spec { get; }`
- `L62` `public OperationState State { get; private set; }`
- `L76` `public OperationMass MassThisTick { get; }`
- `L77` `public int ProgressDays => _progressDays;`
- `L78` `public Money Accrued => _accrued;`
- `L82` `public DrawnOutcome Outcome { get; }`
- `L90` `internal void Reinstate(int progressDays, Money accrued, OperationState state)`
- `L108` `public void Begin()`
- `L123` `public void Advance(int activeDays, int standbyDays, double costIndex)`
- `L160` `public void Cancel()`
- `L176` `private void Finish()`
- `L188` `private static string Format(long value) =>`
- `L191` `private static string Format(ulong value) =>`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

