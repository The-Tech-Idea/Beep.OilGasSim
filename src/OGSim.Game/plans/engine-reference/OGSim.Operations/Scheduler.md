# Scheduler

Source: `src\OGSim.Operations\Scheduler.cs` · Lines: 315

## File intent

> R12.2 / R12.3 — scheduling, reservation and contention (SDD-007 §2, R12 §2.3).
> 
> CONTENTION IS A REJECTION WITH A REASON, NEVER A SILENT QUEUE. A silent queue
> hides the constraint; an explicit "no rig of this class until 1978-03" tells
> the player they need another rig, which is the decision the phase exists to
> put in front of them.
> 
> Resources are reserved for the WORST CASE duration (SDD-007 §2, pinned), so a

## Namespaces

- `OGSim.Operations`

## Type declarations

- `L19` `public sealed record ScheduleRefusal(IReadOnlyList<string> Reasons)`
- `L28` `public abstract record ScheduleResult;`
- `L30` `public sealed record Scheduled(Operation Operation) : ScheduleResult;`
- `L32` `public sealed record Refused(ScheduleRefusal Refusal) : ScheduleResult;`
- `L37` `internal sealed class RigCalendar`
- `L85` `public sealed class OperationScheduler`

## Accessible members

- `L22` `public bool Equals(ScheduleRefusal? other) =>`
- `L25` `public override int GetHashCode() => Structural.HashOf(Reasons);`
- `L39` `private readonly List<(int Start, int End, EntityId<IOperation> By)> _reserved = [];`
- `L44` `public int NextFreeFrom(int from, int days)`
- `L59` `public bool IsFree(int start, int days)`
- `L72` `public void Reserve(int start, int days, EntityId<IOperation> by)`
- `L78` `public void Release(EntityId<IOperation> by) =>`
- `L87` `private readonly IRandomStream _outcomes;`
- `L88` `private readonly IAuditTrail _audit;`
- `L89` `private readonly int _materialCount;`
- `L90` `private readonly Dictionary<EntityId<IRig>, RigCalendar> _calendars = [];`
- `L91` `private readonly List<EntityId<IRig>> _rigOrder = [];`
- `L93` `private ulong _nextOperationId = 1;`
- `L99` `public OperationScheduler(`
- `L117` `public void Register(EntityId<IRig> rig)`
- `L130` `public ScheduleResult Submit(`
- `L161` `public IReadOnlyList<string> Refusals(`
- `L215` `public Operation Reinstate(`
- `L241` `public void Release(Operation operation)`
- `L255` `private DrawnOutcome Draw(OperationSpec spec, EntityId<IOperation> id)`
- `L294` `internal static int WorstCaseDays(OperationSpec spec)`
- `L304` `private static string Format(int value) =>`
- `L307` `private static string Format(long value) =>`
- `L310` `private static string Format(ulong value) =>`
- `L313` `private static string FormatDouble(double value) =>`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

