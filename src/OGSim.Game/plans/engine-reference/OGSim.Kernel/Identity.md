# Identity

Source: `src\OGSim.Kernel\Identity.cs` · Lines: 79

## File intent

> SDD-001 §2 — identity is sequential, typed and save-stable. No Guid (rule D-6).
> SDD-004 §2 — ContentId: charset-validated, ordinal-compared, never a bare string.
> <summary>
> A content-catalogue id: kebab-case, 1–64 chars of [a-z0-9-], compared ordinal.
> SDD-004 §2, pinned there because every SDD uses it and none had declared it.
> </summary>

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L10` `public readonly record struct ContentId : IComparable<ContentId>`
- `L32` `public readonly record struct EntityId<T>(ulong Value) : IComparable<EntityId<T>>`
- `L38` `public enum EntityKind`
- `L50` `public readonly record struct EntityRef(EntityKind Kind, ulong Value) : IComparable<EntityRef>`
- `L69` `public interface IEntityRegistry`

## Accessible members

- `L12` `public string Value { get; }`
- `L14` `public ContentId(string value)`
- `L24` `public int CompareTo(ContentId other) => string.CompareOrdinal(Value, other.Value);`
- `L25` `public override string ToString() => Value;`
- `L34` `public int CompareTo(EntityId<T> other) => Value.CompareTo(other.Value);`
- `L52` `public int CompareTo(EntityRef other)`

