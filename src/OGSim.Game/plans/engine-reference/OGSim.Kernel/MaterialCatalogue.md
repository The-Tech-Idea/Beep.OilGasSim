# MaterialCatalogue

Source: `src\OGSim.Kernel\MaterialCatalogue.cs` · Lines: 220

## File intent

> R2.1 / R2.2 / R2.4 — the property-kind and material catalogues (SDD-002 §2b,
> SDD-004 §6).
> 
> Ordinals are catalogue positions: index into the id-sorted list. Sorting by id
> means adding a material CHANGES ordinals, which is exactly why SDD-004 §6 says
> ordinals never persist — saves store the id string and ordinals rebuild at
> load. Pinned there because persisting one is the subtle bug an implementer
> commits in week two; enforced here by building them from the sort and nowhere

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L19` `public sealed class PropertyKind : IPropertyKind`
- `L83` `public sealed class Property : IProperty`
- `L125` `public sealed class Material : IMaterial`
- `L152` `public sealed class MaterialCatalogue : IMaterialCatalog`

## Accessible members

- `L21` `public PropertyKind(`
- `L51` `public ContentId Id { get; }`
- `L52` `public Dimension Dimension { get; }`
- `L53` `public double MinimumValid { get; }`
- `L54` `public double MaximumValid { get; }`
- `L55` `public BeliefSpace Space { get; }`
- `L57` `public bool IsInRange(double value) =>`
- `L65` `public void AssertInRange(double value, EntityRef? subject)`
- `L74` `private static string Format(double value) =>`
- `L85` `public Property(ContentId kind, Distribution value, Provenance source, GameDate asOf)`
- `L96` `public ContentId Kind { get; }`
- `L97` `public Distribution Value { get; }`
- `L98` `public Provenance Source { get; }`
- `L99` `public GameDate AsOf { get; }`
- `L110` `public static Property Validated(`
- `L127` `public Material(`
- `L141` `public ContentId Id { get; }`
- `L142` `public MaterialId Ordinal { get; }`
- `L143` `public PhaseAtStandardConditions Phase { get; }`
- `L144` `public IReadOnlyList<IProperty> Properties { get; }`
- `L154` `private readonly IMaterial[] _byOrdinal;`
- `L155` `private readonly Dictionary<ContentId, IMaterial> _byId;`
- `L161` `public MaterialCatalogue(`
- `L190` `public int Count => _byOrdinal.Length;`
- `L192` `public IMaterial this[MaterialId ordinal]`
- `L205` `public IMaterial Resolve(ContentId id) =>`
- `L211` `public bool TryResolve(ContentId id, out IMaterial? material) =>`
- `L219` `public Composition ZeroComposition() => Composition.Zero(_byOrdinal.Length);`

