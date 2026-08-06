// SDD-001 §2 — identity is sequential, typed and save-stable. No Guid (rule D-6).
// SDD-004 §2 — ContentId: charset-validated, ordinal-compared, never a bare string.

namespace OGSim.Kernel;

/// <summary>
/// A content-catalogue id: kebab-case, 1–64 chars of [a-z0-9-], compared ordinal.
/// SDD-004 §2, pinned there because every SDD uses it and none had declared it.
/// </summary>
public readonly record struct ContentId : IComparable<ContentId>
{
    public string Value { get; }

    public ContentId(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 64)
            throw new ArgumentException("ContentId must be 1-64 chars (SDD-004 §2).", nameof(value));
        foreach (var c in value)
            if (c is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'))
                throw new ArgumentException($"ContentId charset is [a-z0-9-]; got '{value}' (SDD-004 §2).", nameof(value));
        Value = value;
    }

    public int CompareTo(ContentId other) => string.CompareOrdinal(Value, other.Value);
    public override string ToString() => Value;
}

/// <summary>
/// Typed entity id: sequential ulong per entity type, issued by the registry,
/// part of saved state. T is the entity's contract interface (e.g. EntityId&lt;IWell&gt;).
/// </summary>
public readonly record struct EntityId<T>(ulong Value) : IComparable<EntityId<T>>
{
    public int CompareTo(EntityId<T> other) => Value.CompareTo(other.Value);
}

/// <summary>Entity kinds for the type-erased reference (SDD-001 S001-3, decided).</summary>
public enum EntityKind
{
    Well, Wellbore, Completion, Perforation, Compartment, Reservoir, Field,
    Facility, FacilityUnit, Pipeline, Tank, Berth, Cargo, CustodyPoint,
    Licence, Company, Operation, Rig, Prospect, Play, Basin, Settlement,
    FlowElement, Objective, Barrier, Threat
}

/// <summary>
/// Type-erased id for events and audit entries — one struct with a kind tag;
/// an interface would box in every event (SDD-001 S001-3, decided).
/// </summary>
public readonly record struct EntityRef(EntityKind Kind, ulong Value) : IComparable<EntityRef>
{
    public int CompareTo(EntityRef other)
    {
        var k = ((int)Kind).CompareTo((int)other.Kind);
        return k != 0 ? k : Value.CompareTo(other.Value);
    }
}

/// <summary>
/// SDD-001 §2. Resolve throws on a dangling id (invariant fault INV3 — never
/// null); TryResolve exists for the few places absence is a legitimate state.
/// All() is ordered by id — the D-5 safe enumeration.
/// </summary>
public interface IEntityRegistry
{
    EntityId<T> Issue<T>() where T : class;
    T Resolve<T>(EntityId<T> id) where T : class;
    bool TryResolve<T>(EntityId<T> id, out T? entity) where T : class;
    IReadOnlyList<T> All<T>() where T : class;
}
