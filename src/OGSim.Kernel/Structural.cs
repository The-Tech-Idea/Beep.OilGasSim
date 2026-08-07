// Structural comparison for records that carry collections (finding 131).
//
// A C# record's generated equality compares each member with
// EqualityComparer<T>.Default. For ImmutableArray<T> that is REFERENCE equality
// of the underlying array, and for IReadOnlyList<T> it is reference equality of
// the list object — so two records built from identical values compare UNEQUAL,
// and two built from different values compare EQUAL if they happen to share an
// array. Both directions are wrong and neither is visible at the call site.
//
// Finding 123 caught this on Polygon, where PV7 reported two regenerations of
// one seed as different worlds, and noted that "the same trap sits on any
// contract record holding a collection". This is the one implementation those
// records share, so the correction cannot be applied inconsistently — which is
// how a dozen hand-written Equals overrides would have ended up.

using System.Collections.Immutable;

namespace OGSim.Kernel;

/// <summary>
/// Element-by-element equality and hashing for the collection members of a
/// record. Every user is a record that would otherwise compare by reference.
/// </summary>
public static class Structural
{
    public static bool Equal<T>(ImmutableArray<T> left, ImmutableArray<T> right)
    {
        // A default ImmutableArray is not an empty one: it has no array at all,
        // and treating the two as equal would make an uninitialised record equal
        // to a deliberately empty one.
        if (left.IsDefault || right.IsDefault) return left.IsDefault && right.IsDefault;

        if (left.Length != right.Length) return false;

        for (int i = 0; i < left.Length; i++)
            if (!EqualityComparer<T>.Default.Equals(left[i], right[i])) return false;

        return true;
    }

    public static bool Equal<T>(IReadOnlyList<T>? left, IReadOnlyList<T>? right)
    {
        if (left is null || right is null) return left is null && right is null;
        if (ReferenceEquals(left, right)) return true;
        if (left.Count != right.Count) return false;

        for (int i = 0; i < left.Count; i++)
            if (!EqualityComparer<T>.Default.Equals(left[i], right[i])) return false;

        return true;
    }

    public static int HashOf<T>(ImmutableArray<T> items)
    {
        if (items.IsDefault) return 0;

        var hash = new HashCode();
        for (int i = 0; i < items.Length; i++) hash.Add(items[i]);
        return hash.ToHashCode();
    }

    public static int HashOf<T>(IReadOnlyList<T>? items)
    {
        if (items is null) return 0;

        var hash = new HashCode();
        for (int i = 0; i < items.Count; i++) hash.Add(items[i]);
        return hash.ToHashCode();
    }
}
