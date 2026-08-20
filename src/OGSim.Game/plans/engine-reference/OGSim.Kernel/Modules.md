# Modules

Source: `src\OGSim.Kernel\Modules.cs` · Lines: 185

## File intent

> SDD-001 §9–10 — composition, the tick pipeline, segments, state.
> Composition either fully succeeds or refuses to start with EVERY unmet
> requirement named (design 03 §3.1).

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L7` `public readonly record struct ModuleName(string Value);`
- `L14` `public readonly record struct StateKey(string Value) : IComparable<StateKey>`
- `L27` `public sealed record StageParticipation(StageId Stage, int Order);`
- `L30` `public sealed record ModuleManifest(`
- `L56` `public interface IModuleComposition`
- `L97` `public interface IModuleRegistry`
- `L103` `public interface IModule`
- `L110` `public interface ITickStage`
- `L120` `public sealed class TickContext`
- `L138` `public sealed record Segment(`
- `L152` `public sealed record SegmentPlan(IReadOnlyList<Segment> Segments)`
- `L164` `public interface IStateWriter`
- `L172` `public interface IStateReader`
- `L179` `public interface IStateOwner`

## Accessible members

- `L16` `public int CompareTo(StateKey other) => string.CompareOrdinal(Value, other.Value);`
- `L40` `public bool Equals(ModuleManifest? other) =>`
- `L48` `public override int GetHashCode() =>`
- `L122` `public required Tick Tick { get; init; }`
- `L123` `public required GameDate Date { get; init; }`
- `L127` `public SegmentPlan? Segments { get; set; }`
- `L144` `public bool Equals(Segment? other) =>`
- `L148` `public override int GetHashCode() =>`
- `L155` `public bool Equals(SegmentPlan? other) =>`
- `L158` `public override int GetHashCode() => Structural.HashOf(Segments);`

