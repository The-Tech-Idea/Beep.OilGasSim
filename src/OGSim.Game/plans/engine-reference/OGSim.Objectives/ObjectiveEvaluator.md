# ObjectiveEvaluator

Source: `src\OGSim.Objectives\ObjectiveEvaluator.cs` · Lines: 396

## File intent

> R24.2 / R24.4 — objective evaluation (SDD-014 §1–3).
> 
> OBSERVE, NEVER INFLUENCE. Evaluation runs at stage 12 over a SEALED snapshot
> and a SEALED event list, and this assembly holds no reference to the command
> bus at all (R24-V15, architecture-tested). An objective that could act would
> make a scenario a second player, and the game's outcome would stop being the
> player's doing.
> 

## Namespaces

- `OGSim.Objectives`

## Type declarations

- `L29` `public sealed class PredicateState`
- `L53` `public sealed class ReadModelSchema`
- `L155` `public sealed class ObjectiveEvaluator`

## Accessible members

- `L31` `private readonly Dictionary<string, int> _sustained = [];`
- `L32` `private readonly Dictionary<string, int> _sequenceStep = [];`
- `L33` `private readonly HashSet<string> _neverBroken = [];`
- `L35` `public int SustainedTicks(string node) => _sustained.GetValueOrDefault(node);`
- `L37` `public void SetSustained(string node, int ticks) => _sustained[node] = ticks;`
- `L39` `public int SequenceStep(string node) => _sequenceStep.GetValueOrDefault(node);`
- `L41` `public void SetSequenceStep(string node, int step) => _sequenceStep[node] = step;`
- `L45` `public bool IsBroken(string node) => _neverBroken.Contains(node);`
- `L47` `public void MarkBroken(string node) => _neverBroken.Add(node);`
- `L55` `private readonly HashSet<string> _scalars;`
- `L56` `private readonly HashSet<string> _collections;`
- `L57` `private readonly HashSet<string> _itemFields;`
- `L59` `public ReadModelSchema(`
- `L81` `public IReadOnlyList<string> Validate(Predicate predicate)`
- `L90` `private void Walk(Predicate predicate, List<string> unknown)`
- `L161` `public bool Evaluate(Objective objective, ObjectiveSnapshot snapshot, PredicateState state)`
- `L170` `private bool Truth(Predicate predicate, ObjectiveSnapshot snapshot, PredicateState state, string node)`
- `L226` `private bool Sustained(`
- `L249` `private bool Sequence(`
- `L267` `private bool NeverBroken(`
- `L284` `private static bool Fired(OnEvent onEvent, ObjectiveSnapshot snapshot)`
- `L304` `private double Value(Predicate predicate, ObjectiveSnapshot snapshot) => predicate switch`
- `L327` `private static double Aggregated(Aggregate aggregate, ObjectiveSnapshot snapshot)`
- `L386` `private static bool Apply(double left, CompareOp op, double right) => op switch`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

