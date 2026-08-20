# Content

Source: `src\OGSim.Kernel\Content.cs` · Lines: 123

## File intent

> SDD-004 — the content pipeline surface: the front door of non-negotiable 11
> ("everything is definition-driven and moddable"). Third contract pass: these
> were pinned in the SDD but declared nowhere — the moddability rule had no type.
> The loader CLASS (six stages) is R3 implementation; these are its contracts.
> <summary>The four technology eras (design 07 §2).</summary>

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L10` `public enum Era { E1, E2, E3, E4 }`
- `L13` `public abstract record ContentDefinition(ContentId Id);`
- `L22` `public abstract record GatedDefinition(`
- `L29` `public interface ICatalog<TDef> where TDef : ContentDefinition`
- `L39` `public interface ICatalogSet`
- `L44` `public sealed record ContentFile(string RelativePath, string Json);`
- `L47` `public readonly record struct ContentReference(string Kind, ContentId Id, string JsonPath);`
- `L61` `public interface IContentKind`
- `L89` `public readonly record struct PluginBinding(ContentId Plugin, Type Contract, string JsonPath);`
- `L96` `public interface IContentSource`
- `L104` `public enum LoadStage { Parse = 1, Shape, Units, References, Consistency, Binding }`
- `L106` `public sealed record LoadFailure(`
- `L114` `public abstract record ContentLoadResult;`
- `L115` `public sealed record ContentLoaded(ICatalogSet Catalogues) : ContentLoadResult;`
- `L116` `public sealed record ContentFailures(IReadOnlyList<LoadFailure> Failures) : ContentLoadResult`

## Accessible members

- `L119` `public bool Equals(ContentFailures? other) =>`
- `L122` `public override int GetHashCode() => Structural.HashOf(Failures);`

