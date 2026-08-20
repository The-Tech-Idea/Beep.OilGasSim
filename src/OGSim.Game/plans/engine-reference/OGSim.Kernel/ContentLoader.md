# ContentLoader

Source: `src\OGSim.Kernel\ContentLoader.cs` · Lines: 361

## File intent

> R3.2–R3.6 — the six-stage content loader (SDD-004 §5–§7, design 10 §3.1).
> 
> THE LOADER KNOWS NO CONTENT KIND. Every kind arrives as an IContentKind
> registration, which is what makes R3 §3's acceptance criterion structural: if
> a later phase needed a change in this file to add a content kind, R3's design
> would be wrong. Nothing below mentions a material, a tier or a technology.
> 
> ALL FILES RUN ALL STAGES AND FAILURES ACCUMULATE (R3-V2). A loader that

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L17` `public sealed class ContentLoader`
- `L290` `private readonly record struct LoadedEntry(`
- `L300` `private sealed class CatalogueSet : ICatalogSet`
- `L333` `private sealed class Catalogue<TDef> : ICatalog<TDef> where TDef : ContentDefinition`

## Accessible members

- `L19` `private readonly Dictionary<string, IContentKind> _kinds;`
- `L20` `private readonly IModuleRegistry _plugins;`
- `L22` `public ContentLoader(IReadOnlyList<IContentKind> kinds, IModuleRegistry plugins)`
- `L39` `public ContentLoadResult LoadAll(IReadOnlyList<IContentSource> sources)`
- `L74` `private void ReadSource(`
- `L152` `private static void Record(`
- `L186` `private void ResolveReferences(`
- `L209` `private void RunConsistency(`
- `L231` `private void BindPlugins(`
- `L254` `private string NearestKind(string unknown)`
- `L269` `private static int Distance(string a, string b)`
- `L287` `private static System.Globalization.CultureInfo Invariant =>`
- `L302` `private readonly Dictionary<Type, object> _byType = [];`
- `L304` `public CatalogueSet(SortedDictionary<string, LoadedEntry> entries)`
- `L327` `public ICatalog<TDef> Of<TDef>() where TDef : ContentDefinition =>`
- `L335` `private readonly Dictionary<ContentId, TDef> _byId;`
- `L337` `public Catalogue(IReadOnlyList<ContentDefinition> definitions)`
- `L350` `public IReadOnlyList<TDef> All { get; }`
- `L352` `public TDef this[ContentId id] =>`
- `L358` `public bool TryGet(ContentId id, out TDef definition) =>`

## Imports

- `using System.Text.Json;`
- `using (document)`

