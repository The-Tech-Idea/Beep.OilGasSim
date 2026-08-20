# ContentKinds

Source: `src\OGSim.Kernel\ContentKinds.cs` · Lines: 216

## File intent

> R3.7 — the shipped content kinds (SDD-004 §2, §6; design 10 §2).
> 
> Each kind is a definition record plus an IContentKind that reads it. The
> loader knows none of them: it is handed the list at composition, which is why
> adding "helium" or a new lithology is a JSON file and a registration, never a
> change in ContentLoader.cs (R3 §3's acceptance criterion).
> 
> DATASHEETS ARE CLOSED. Every field below is a declared property of a record —

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L24` `public sealed record PropertyKindDefinition(`
- `L31` `public sealed class PropertyKindContentKind : IContentKind`
- `L92` `public sealed record MaterialPropertyEntry(ContentId Kind, double SiValue, Provenance Source);`
- `L94` `public sealed record MaterialDefinition(`
- `L115` `public sealed class MaterialContentKind(IReadOnlyDictionary<ContentId, Dimension> propertyDimensions)`
- `L171` `public sealed record RockTypeDefinition(`
- `L177` `public sealed class RockTypeContentKind : IContentKind`

## Accessible members

- `L33` `public string Name => "property-kind";`
- `L35` `public ContentDefinition Read(JsonElement element)`
- `L52` `public IReadOnlyList<ContentReference> ReferencesOf(ContentDefinition definition) => [];`
- `L54` `public IReadOnlyList<string> ConsistencyProblems(ContentDefinition definition)`
- `L71` `public IReadOnlyList<PluginBinding> PluginsOf(ContentDefinition definition) => [];`
- `L73` `internal static JsonElement Required(JsonElement element, string name) =>`
- `L78` `internal static TEnum ParseEnum<TEnum>(JsonElement element, string name)`
- `L102` `public bool Equals(MaterialDefinition? other) =>`
- `L106` `public override int GetHashCode() =>`
- `L118` `public string Name => "material";`
- `L120` `public ContentDefinition Read(JsonElement element)`
- `L153` `public IReadOnlyList<ContentReference> ReferencesOf(ContentDefinition definition)`
- `L164` `public IReadOnlyList<string> ConsistencyProblems(ContentDefinition definition) => [];`
- `L166` `public IReadOnlyList<PluginBinding> PluginsOf(ContentDefinition definition) => [];`
- `L179` `public string Name => "rock-type";`
- `L181` `public ContentDefinition Read(JsonElement element)`
- `L198` `public IReadOnlyList<ContentReference> ReferencesOf(ContentDefinition definition) => [];`
- `L200` `public IReadOnlyList<string> ConsistencyProblems(ContentDefinition definition)`
- `L215` `public IReadOnlyList<PluginBinding> PluginsOf(ContentDefinition definition) => [];`

## Imports

- `using System.Text.Json;`

