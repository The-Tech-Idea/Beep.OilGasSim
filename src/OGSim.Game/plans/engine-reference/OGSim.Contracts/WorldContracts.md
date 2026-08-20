# WorldContracts

Source: `src\OGSim.Contracts\WorldContracts.cs` · Lines: 244

## File intent

> SDD-010 §4 + SDD-016 §1 — world generation and weather, the last two
> replaceable slots (03 §3.2). Pass 5: previously deferred; deferral was
> inconsistent with a contracts phase — the shapes below are now pinned in
> their SDDs first (F-1), exactly like every other slot.
> 
> The generator never touches module truth directly: it emits these handoff
> records through IWorldSink, and each owning module builds its internal truth
> from them. Initial beliefs go through the SAME Observation door as every

## Namespaces

- `OGSim.Contracts`

## Type declarations

- `L19` `public sealed record GeneratedCompartment(`
- `L40` `public sealed record GeneratedAccumulation(`
- `L78` `public sealed record Heightfield(`
- `L99` `public sealed record River(ImmutableArray<Coordinate> Path)`
- `L107` `public sealed record GeneratedTerrain(`
- `L131` `public sealed record Settlement(Coordinate Site, long Population);`
- `L134` `public sealed record TransportLink(Coordinate A, Coordinate B, ContentId Kind);`
- `L137` `public sealed record Harbour(Coordinate Site, Length Depth);`
- `L140` `public sealed record ThirdPartyAsset(ContentId Template, Coordinate Site);`
- `L142` `public sealed record SensitivityZone(ContentId Kind, Polygon Area);`
- `L144` `public sealed record GeneratedSurface(`
- `L171` `public sealed record ClimateRegion(ContentId Profile, Polygon Area);`
- `L173` `public sealed record Jurisdiction(ContentId FiscalRegime, Polygon Area);`
- `L181` `public interface IWorldSink`
- `L207` `public sealed record WorldParameters(`
- `L225` `public interface IWorldGenerator`
- `L240` `public interface IWeatherModel`

## Accessible members

- `L59` `public bool Equals(GeneratedAccumulation? other) =>`
- `L69` `public override int GetHashCode() =>`
- `L88` `public bool Equals(Heightfield? other) =>`
- `L95` `public override int GetHashCode() =>`
- `L101` `public bool Equals(River? other) => other is not null && Structural.Equal(Path, other.Path);`
- `L103` `public override int GetHashCode() => Structural.HashOf(Path);`
- `L114` `public bool Equals(GeneratedTerrain? other) =>`
- `L122` `public override int GetHashCode() =>`
- `L154` `public bool Equals(GeneratedSurface? other) =>`
- `L162` `public override int GetHashCode() =>`

## Imports

- `using System.Collections.Immutable;`
- `using OGSim.Kernel;`

