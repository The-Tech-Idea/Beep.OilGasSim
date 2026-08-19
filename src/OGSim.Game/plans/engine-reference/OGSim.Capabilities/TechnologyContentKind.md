# TechnologyContentKind

Source: `src\OGSim.Capabilities\TechnologyContentKind.cs` · Lines: 196

## File intent

> R20c.9 — the technology tree as loadable content (SDD-004 §8, SDD-005 §2).
> 
> The kind lives HERE rather than in the kernel's ContentKinds because its
> datasheet is written in this module's vocabulary — TechnologyId, Era,
> AcquisitionRoute, DetectClass. The kernel's three bootstrap kinds are the ones
> every module needs; a kind belongs with the module that consumes it (R3.7).
> <summary>The four routes of design 07 §3 — TECH_TREE's `R L S D` column.</summary>

## Namespaces

- `OGSim.Capabilities`

## Type declarations

- `L16` `public enum AcquisitionRoute`
- `L28` `public sealed record TechnologyDefinition(`
- `L59` `public static class TechnologySlug`
- `L107` `public sealed class TechnologyContentKind : IContentKind`

## Accessible members

- `L39` `public bool Equals(TechnologyDefinition? other) =>`
- `L47` `public override int GetHashCode() =>`
- `L66` `public static string Of(string displayName)`
- `L92` `private static void Append(StringBuilder slug, char c)`
- `L100` `private static char Fold(char c) => c switch`
- `L109` `public string Name => "tech";`
- `L111` `public ContentDefinition Read(JsonElement element)`
- `L142` `public IReadOnlyList<ContentReference> ReferencesOf(ContentDefinition definition)`
- `L154` `public IReadOnlyList<string> ConsistencyProblems(ContentDefinition definition)`
- `L183` `public IReadOnlyList<PluginBinding> PluginsOf(ContentDefinition definition) => [];`
- `L185` `private static JsonElement Required(JsonElement element, string name) =>`
- `L190` `private static TEnum ParseEnum<TEnum>(JsonElement element) where TEnum : struct, Enum =>`

## Imports

- `using System.Text;`
- `using System.Text.Json;`
- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

