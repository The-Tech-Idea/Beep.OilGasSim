// Finding 131 as a law with a test behind it (SDD-001 §9).
//
// A record's generated equality compares an ImmutableArray or IReadOnlyList
// member BY REFERENCE. Two records built from identical values compare unequal;
// two built from different values compare equal when they share an array. Both
// directions are wrong and neither is visible at the call site — which is why
// finding 123 was found by a world-generation test reporting a false difference
// rather than by anyone reading the record.
//
// Fixing the instances was never enough: the next record someone writes brings
// the bug back, silently, and the test that catches it is three phases away.
// This makes it a build failure instead.

using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OGSim.Architecture.Tests;

public class StructuralEqualityRule
{
    private const BindingFlags Declared =
        BindingFlags.Public | BindingFlags.NonPublic |
        BindingFlags.Instance | BindingFlags.DeclaredOnly;

    [Fact] // A record that carries a collection compares it element by element
    public void EveryRecordWithACollectionMemberOverridesEquality()
    {
        var violations = new List<string>();

        foreach (Type type in EngineCorpus.Types)
        {
            if (!IsRecord(type)) continue;

            string[] collectionMembers = [.. CollectionMembersOf(type)];
            if (collectionMembers.Length == 0) continue;

            if (DeclaresItsOwnEquals(type)) continue;

            violations.Add(
                $"{type.FullName} carries {string.Join(", ", collectionMembers)} " +
                "and uses the compiler's reference equality");
        }

        EngineCorpus.AssertNone(violations,
            "Finding 131 — a record carrying a collection must override Equals/GetHashCode " +
            "through OGSim.Kernel.Structural");
    }

    /// <summary>
    /// Records carry a compiler-generated <c>EqualityContract</c> (classes) or a
    /// <c>&lt;Clone&gt;$</c> member; the contract property is the reliable one
    /// across both record classes and record structs.
    /// </summary>
    private static bool IsRecord(Type type) =>
        type.GetMethod("<Clone>$", Declared) is not null
        || type.GetProperty("EqualityContract", Declared) is not null
        || (type.IsValueType && type.GetMethod("PrintMembers", Declared) is not null);

    /// <summary>
    /// A hand-written <c>Equals(T)</c> is not marked compiler-generated, which is
    /// exactly the distinction this rule turns on: the generated one is the
    /// broken one.
    /// </summary>
    private static bool DeclaresItsOwnEquals(Type type)
    {
        foreach (MethodInfo method in type.GetMethods(Declared))
        {
            if (method.Name != nameof(Equals)) continue;
            if (method.GetCustomAttribute<CompilerGeneratedAttribute>() is not null) continue;

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length == 1) return true;
        }

        return false;
    }

    /// <summary>
    /// STATE only. A computed pass-through — <c>Problems => Refusal.Problems</c>
    /// — has no backing field, is not part of the record's equality, and
    /// flagging it would demand an override for a member the comparison never
    /// reads.
    /// </summary>
    private static IEnumerable<string> CollectionMembersOf(Type type)
    {
        foreach (PropertyInfo property in type.GetProperties(Declared))
        {
            if (!IsCollection(property.PropertyType)) continue;
            if (!HasBackingField(type, property)) continue;

            yield return property.Name;
        }
    }

    private static bool HasBackingField(Type type, PropertyInfo property) =>
        type.GetField($"<{property.Name}>k__BackingField", Declared) is not null;

    /// <summary>
    /// What compares by reference: any <c>IEnumerable</c> that is not a string,
    /// plus <c>ImmutableArray&lt;T&gt;</c>, whose own <c>Equals</c> compares the
    /// underlying array by reference despite being a struct.
    /// </summary>
    private static bool IsCollection(Type type)
    {
        if (type == typeof(string)) return false;

        if (type.IsGenericType
            && type.GetGenericTypeDefinition().FullName is
                "System.Collections.Immutable.ImmutableArray`1")
            return true;

        return typeof(IEnumerable).IsAssignableFrom(type);
    }
}
