# Structural

Source: `src\OGSim.Kernel\Structural.cs` · Lines: 138

## File intent

> Structural comparison for records that carry collections (finding 131).
> 
> A C# record's generated equality compares each member with
> EqualityComparer<T>.Default. For ImmutableArray<T> that is REFERENCE equality
> of the underlying array, and for IReadOnlyList<T> it is reference equality of
> the list object — so two records built from identical values compare UNEQUAL,
> and two built from different values compare EQUAL if they happen to share an
> array. Both directions are wrong and neither is visible at the call site.

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L24` `public static class Structural`

## Accessible members

- `L26` `public static bool Equal<T>(ImmutableArray<T> left, ImmutableArray<T> right)`
- `L41` `public static bool Equal<T>(IReadOnlyList<T>? left, IReadOnlyList<T>? right)`
- `L58` `public static bool Equal<T>(IReadOnlyCollection<T>? left, IReadOnlyCollection<T>? right)`
- `L73` `public static int HashOf<T>(IReadOnlyCollection<T>? items)`
- `L87` `public static bool Equal<TKey, TValue>(`
- `L109` `public static int HashOf<TKey, TValue>(IReadOnlyDictionary<TKey, TValue>? pairs)`
- `L121` `public static int HashOf<T>(ImmutableArray<T> items)`
- `L130` `public static int HashOf<T>(IReadOnlyList<T>? items)`

## Imports

- `using System.Collections.Immutable;`
- `using IEnumerator<T> a = left.GetEnumerator();`
- `using IEnumerator<T> b = right.GetEnumerator();`

