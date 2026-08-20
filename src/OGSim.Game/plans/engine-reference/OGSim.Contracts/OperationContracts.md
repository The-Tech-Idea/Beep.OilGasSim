# OperationContracts

Source: `src\OGSim.Contracts\OperationContracts.cs` · Lines: 131

## File intent

> SDD-007 — the one scheduled-activity engine. Outcome drawn ONCE at start
> (audited, unexploitable); standby costs day rates only; reservations are
> worst-case so a delayed operation never finds its rig double-booked.

## Namespaces

- `OGSim.Contracts`

## Type declarations

- `L9` `public enum OperationState { Scheduled, Active, Standby, Completed, Failed, Cancelled }`
- `L11` `public enum OutcomeGrade { OnTime, Delayed, OverBudget, Partial, Failure, Disaster }`
- `L14` `public sealed record OutcomeRow(`
- `L21` `public sealed record OutcomeTable(IReadOnlyList<OutcomeRow> Rows)   // probabilities sum to 1.0, load-checked`
- `L31` `public sealed record CostProfile(`
- `L37` `public interface IRig { }   // resource marker; calendars live in the scheduler`
- `L39` `public sealed record ResourceNeeds(`
- `L50` `public sealed record OperationSpec(`
- `L83` `public sealed record OperationMass(Composition Sourced, DisposedMass Disposed)`
- `L102` `public interface IOperation`
- `L126` `public interface IObligationRegistry`

## Accessible members

- `L24` `public bool Equals(OutcomeTable? other) =>`
- `L27` `public override int GetHashCode() => Structural.HashOf(Rows);`
- `L44` `public bool Equals(ResourceNeeds? other) =>`
- `L47` `public override int GetHashCode() => HashCode.Combine(Rig, Structural.HashOf(Crew));`
- `L61` `public bool Equals(OperationSpec? other) =>`
- `L68` `public override int GetHashCode() =>`
- `L95` `public static OperationMass None(int materialCount)`

## Imports

- `using OGSim.Kernel;`

