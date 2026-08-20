# Activities

Source: `src\OGSim.Composition\Activities.cs` · Lines: 662

## File intent

> R12b — every activity on one engine (SDD-007 §5, design 07 §2c).
> 
> NOTHING THE PLAYER DOES TO THE WORLD HAPPENS EXCEPT AS AN ACTIVITY. Drilling,
> well testing, logging, coring, surveying, working over, installing,
> abandoning — all take time, commit a resource, accrue cost while they run, and
> end in an outcome drawn once at the start.
> 
> ONE ACTIVITY IS ONE CLASS, in its own file. Its terms, the refusals only it

## Namespaces

- `OGSim.Composition`

## Type declarations

- `L30` `public sealed record ActivityTerms(`
- `L58` `internal sealed record CompletedActivity(`
- `L69` `internal interface IActivity`
- `L110` `internal abstract class Activity<TCommand> : IActivity`
- `L167` `internal sealed record InFlight(`
- `L187` `internal sealed class ActivityState : IStateOwner`
- `L422` `internal sealed class ActivityOrders(`
- `L502` `internal sealed class ActivityValidator<TCommand>(`
- `L521` `internal sealed class ActivityApplier<TCommand>(`
- `L541` `internal sealed class ActivityStage(ActivityState activities, IAuditTrail audit) : ITickStage`
- `L616` `internal sealed class ObservationDoor(`

## Accessible members

- `L42` `public double ProbabilityOfSuccess`
- `L113` `protected Activity(ActivityTerms terms)`
- `L119` `public ActivityTerms Terms { get; }`
- `L121` `public ContentId Template => Terms.Template;`
- `L123` `public abstract bool LeavesAnAsset { get; }`
- `L125` `public abstract bool OnePerTarget { get; }`
- `L128` `public abstract (EntityRef Target, Length Depth) Aim(TCommand command);`
- `L140` `protected static Length NoDepth { get; } = new(0.0);`
- `L152` `public abstract IReadOnlyList<RejectionReason> OwnRefusals(TCommand command);`
- `L154` `public abstract void Complete(CompletedActivity done, Tick tick);`
- `L156` `public void Register(IModuleComposition composition, ActivityOrders orders)`
- `L176` `public Money Posted { get; set; } = Money.Zero;`
- `L189` `private readonly List<InFlight> _running = [];`
- `L190` `private readonly Dictionary<ContentId, IActivity> _byTemplate = [];`
- `L191` `private readonly List<IActivity> _catalogue = [];`
- `L193` `private readonly OperationScheduler _scheduler;`
- `L194` `private readonly CompanyState _company;`
- `L196` `public ActivityState(`
- `L218` `public StateKey Key { get; } = new("field.activities");`
- `L220` `public int SchemaVersion => 1;`
- `L222` `public OperationScheduler Scheduler => _scheduler;`
- `L225` `public IReadOnlyList<IActivity> Catalogue => _catalogue;`
- `L228` `public int InProgress => _running.Count;`
- `L230` `public IReadOnlyList<InFlight> Running => _running;`
- `L237` `public IActivity Of(ContentId template) =>`
- `L245` `public bool IsRunning(ContentId template, EntityRef target)`
- `L254` `public OperationSpec SpecFor(ContentId template, EntityRef target, Length depth)`
- `L276` `public void Begin(InFlight activity) => _running.Add(activity);`
- `L289` `public void Finish(InFlight activity)`
- `L299` `public void PostAccrual(InFlight activity, Tick tick, AuditId cause)`
- `L320` `public void Capture(IStateWriter writer)`
- `L351` `public void Restore(IStateReader reader)`
- `L397` `private static OutcomeRow RowFor(IActivity activity, OutcomeGrade grade)`
- `L409` `private static string Prefix(long index) =>`
- `L433` `public List<RejectionReason> Refusals(ContentId template, EntityRef target, Length depth)`
- `L476` `public void Book(ContentId template, EntityRef target, Length depth)`
- `L495` `private int Today => clock.CurrentTick.Value * (int)Duration.DaysPerTick;`
- `L507` `public IReadOnlyList<RejectionReason> Validate(TCommand command)`
- `L526` `public Applied Apply(TCommand command, AuditId submission)`
- `L543` `public StageId Id => StageId.Operations;`
- `L545` `public void Execute(TickContext context)`
- `L602` `private static string Format(ulong value) =>`
- `L630` `public void Deliver(`
- `L651` `private static double Logarithm(ContentId kind, double truth)`

## Imports

- `using OGSim.Company;`
- `using OGSim.Contracts;`
- `using OGSim.Kernel;`
- `using OGSim.Operations;`

