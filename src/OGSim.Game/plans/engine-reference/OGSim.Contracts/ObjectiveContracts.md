# ObjectiveContracts

Source: `src\OGSim.Contracts\ObjectiveContracts.cs` · Lines: 157

## File intent

> SDD-014 §1 — the objective predicate AST.
> 
> A CLOSED HIERARCHY, like Effect (SDD-005 §4). Content expresses these as a
> small JSON tree validated at load, so an objective is data rather than code
> and a scenario cannot smuggle behaviour in through a mission file.
> 
> Declared at R24. SDD-014's own pass-10 note records that four of these types
> were used in its declarations and declared nowhere — the same defect as

## Namespaces

- `OGSim.Contracts`

## Type declarations

- `L23` `public readonly record struct ReadModelPath(string Path);`
- `L25` `public enum CompareOp { Lt, Le, Eq, Ne, Ge, Gt }`
- `L27` `public enum AggOp { Max, Min, Sum, Count, Any, All }`
- `L37` `public sealed record EventFilter(EntityRef? Subject, Severity? MinimumSeverity);`
- `L39` `public abstract record Predicate;`
- `L42` `public sealed record Metric(ReadModelPath Path) : Predicate;`
- `L44` `public sealed record Const(double Value) : Predicate;`
- `L46` `public sealed record Compare(Predicate L, CompareOp Op, Predicate R) : Predicate;`
- `L51` `public sealed record All(IReadOnlyList<Predicate> Items) : Predicate`
- `L58` `public sealed record Any(IReadOnlyList<Predicate> Items) : Predicate`
- `L65` `public sealed record CountOf(int N, IReadOnlyList<Predicate> Items) : Predicate`
- `L74` `public sealed record SustainedFor(Predicate Inner, int Ticks) : Predicate;`
- `L77` `public sealed record InSequence(IReadOnlyList<Predicate> Steps) : Predicate`
- `L86` `public sealed record Never(Predicate Inner) : Predicate;`
- `L89` `public sealed record OnEvent(EventCategory Category, EventFilter Filter) : Predicate;`
- `L98` `public sealed record Aggregate(`
- `L102` `public sealed record Objective(`
- `L120` `public sealed record ObjectiveSnapshot(`

## Accessible members

- `L53` `public bool Equals(All? other) => other is not null && Structural.Equal(Items, other.Items);`
- `L55` `public override int GetHashCode() => Structural.HashOf(Items);`
- `L60` `public bool Equals(Any? other) => other is not null && Structural.Equal(Items, other.Items);`
- `L62` `public override int GetHashCode() => Structural.HashOf(Items);`
- `L67` `public bool Equals(CountOf? other) =>`
- `L70` `public override int GetHashCode() => HashCode.Combine(N, Structural.HashOf(Items));`
- `L79` `public bool Equals(InSequence? other) =>`
- `L82` `public override int GetHashCode() => Structural.HashOf(Steps);`
- `L128` `public bool Equals(ObjectiveSnapshot? other) =>`
- `L134` `private static bool CollectionsEqual(`
- `L154` `public override int GetHashCode() =>`

## Imports

- `using OGSim.Kernel;`

