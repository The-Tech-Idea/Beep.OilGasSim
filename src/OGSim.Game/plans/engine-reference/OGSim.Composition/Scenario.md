# Scenario

Source: `src\OGSim.Composition\Scenario.cs` · Lines: 406

## File intent

> R21e — the scenario runner (SDD-014 §5a, design 03 §3.3).
> 
> A SCENARIO IS CONTENT, NOT CODE. R21c gave the game an end by compiling one
> into the engine: a `ScenarioGoal(targetCash, deadline)` record and a stage
> that read cash and compared it. That was a win condition nobody could author,
> load or vary without editing the engine — the thing design 03 §3.3 exists to
> prevent — and R21d declared the contracts that replace it. This is the debt,
> paid: the goal is now an `Objective` over a read-model path, evaluated by

## Namespaces

- `OGSim.Composition`

## Type declarations

- `L41` `internal sealed record ProjectedPath(string Path, Func<FieldPosition, double> Read);`
- `L57` `internal sealed class ReadModelPaths`
- `L116` `internal sealed class ScenarioRunner : IScenarioRunner`
- `L347` `internal sealed class ObjectiveStage(`

## Accessible members

- `L59` `private readonly List<ProjectedPath> _paths;`
- `L61` `public ReadModelPaths(IReadOnlyList<ProjectedPath> paths)`
- `L77` `public ReadModelSchema Schema { get; }`
- `L89` `public ObjectiveSnapshot SnapshotOf(FieldPosition position)`
- `L118` `private readonly Scenario _scenario;`
- `L119` `private readonly ObjectiveEvaluator _evaluator = new();`
- `L124` `private readonly List<(Objective Objective, PredicateState State, bool IsFailure)> _tracked = [];`
- `L128` `private readonly List<ObjectiveState> _states = [];`
- `L130` `private ObjectiveState _overall = ObjectiveState.Pending;`
- `L132` `public ScenarioRunner(Scenario scenario, ReadModelSchema schema)`
- `L145` `public ContentId Id => _scenario.Id;`
- `L147` `public IReadOnlyList<ScriptedEntry> EntriesFor(Tick tick)`
- `L157` `public ScenarioProgress Evaluate(ObjectiveSnapshot position, Tick tick)`
- `L200` `private ObjectiveState Overall(bool anyFailed, bool allMet, Tick tick)`
- `L218` `private static ObjectiveState Settle(`
- `L232` `private void Track(Objective objective, bool isFailure)`
- `L242` `private static List<string> Problems(Scenario scenario, ReadModelSchema schema)`
- `L262` `private static void Check(Objective objective, ReadModelSchema schema, List<string> problems)`
- `L281` `private static bool MentionsAnEvent(Predicate predicate)`
- `L321` `private static bool AnyOf(IReadOnlyList<Predicate> items)`
- `L329` `private static void Refuse(List<string> problems)`
- `L354` `private ObjectiveState _reported = ObjectiveState.Pending;`
- `L356` `public StageId Id => StageId.Objectives;`
- `L367` `public bool Insolvent { get; private set; }`
- `L378` `public FieldPosition? Position { get; private set; }`
- `L380` `public ScenarioProgress Progress { get; private set; } =`
- `L383` `public void Execute(TickContext context)`

## Imports

- `using OGSim.Company;`
- `using OGSim.Contracts;`
- `using OGSim.Kernel;`
- `using OGSim.Objectives;`

