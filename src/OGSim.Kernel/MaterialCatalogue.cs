// R2.1 / R2.2 / R2.4 — the property-kind and material catalogues (SDD-002 §2b,
// SDD-004 §6).
//
// Ordinals are catalogue positions: index into the id-sorted list. Sorting by id
// means adding a material CHANGES ordinals, which is exactly why SDD-004 §6 says
// ordinals never persist — saves store the id string and ordinals rebuild at
// load. Pinned there because persisting one is the subtle bug an implementer
// commits in week two; enforced here by building them from the sort and nowhere
// else.

namespace OGSim.Kernel;

/// <summary>
/// R2.1 — what a property measures, and where its values stop being physical.
/// The range lives on the KIND rather than on each correlation, so R2-V10's
/// "out of range is a model fault, never a silent extrapolation" is checked once
/// at the boundary every value crosses.
/// </summary>
public sealed class PropertyKind : IPropertyKind
{
    public PropertyKind(
        ContentId id,
        Dimension dimension,
        double minimumValid,
        double maximumValid,
        BeliefSpace space)
    {
        if (double.IsNaN(minimumValid) || double.IsNaN(maximumValid))
            throw new InvariantFault("SDD-002 §2b", null,
                $"Property kind '{id}' has a NaN validity bound.");

        if (minimumValid > maximumValid)
            throw new InvariantFault("SDD-002 §2b", null,
                $"Property kind '{id}' has an inverted range [{minimumValid}, {maximumValid}].");

        // A Log-space kind is one whose values are multiplicative — permeability,
        // area, volume — and a non-positive value has no logarithm. Allowing one
        // would put a NaN into the first belief update that touched it.
        if (space == BeliefSpace.Log && minimumValid <= 0.0)
            throw new InvariantFault("SDD-002 §2b", null,
                $"Property kind '{id}' is Log-space but permits {minimumValid}; " +
                "a log-space value must be strictly positive.");

        Id = id;
        Dimension = dimension;
        MinimumValid = minimumValid;
        MaximumValid = maximumValid;
        Space = space;
    }

    public ContentId Id { get; }
    public Dimension Dimension { get; }
    public double MinimumValid { get; }
    public double MaximumValid { get; }
    public BeliefSpace Space { get; }

    public bool IsInRange(double value) =>
        !double.IsNaN(value) && value >= MinimumValid && value <= MaximumValid;

    /// <summary>
    /// R2-V10. The fault names the kind, the value and the range, because a
    /// correlation that has wandered out of its validity range is a bug report
    /// and the numbers are the report.
    /// </summary>
    public void AssertInRange(double value, EntityRef? subject)
    {
        if (IsInRange(value)) return;

        throw new ModelFault("R2-V10", subject,
            $"Property '{Id}' value {Format(value)} is outside its validity range " +
            $"[{Format(MinimumValid)}, {Format(MaximumValid)}] — extrapolation is never silent.");
    }

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// R2.2 — a property states what it is, how it is known, and when. All four
/// parts are required: the alternative invites every consumer to read a scalar
/// and let the uncertainty go decorative (R2 §2.1).
/// </summary>
public sealed class Property : IProperty
{
    public Property(ContentId kind, Distribution value, Provenance source, GameDate asOf)
    {
        ArgumentNullException.ThrowIfNull(value);
        _ = asOf.Quarter;      // a default(GameDate) as-of would make staleness meaningless

        Kind = kind;
        Value = value;
        Source = source;
        AsOf = asOf;
    }

    public ContentId Kind { get; }
    public Distribution Value { get; }
    public Provenance Source { get; }
    public GameDate AsOf { get; }

    /// <summary>
    /// Validates a property against its kind at construction time rather than at
    /// use: content is loaded once and read for forty years, so the check
    /// belongs at the load boundary (SDD-004 §5 stage 5).
    ///
    /// The P90 and P10 are checked, not just the median — a distribution whose
    /// centre is physical but whose downside is not would produce out-of-range
    /// values the first time anything sampled its low case.
    /// </summary>
    public static Property Validated(
        PropertyKind kind, Distribution value, Provenance source, GameDate asOf)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(value);

        kind.AssertInRange(value.P90, null);
        kind.AssertInRange(value.P50, null);
        kind.AssertInRange(value.P10, null);

        return new Property(kind.Id, value, source, asOf);
    }
}

/// <summary>R2.4 — a material is a catalogue entry, never a code type.</summary>
public sealed class Material : IMaterial
{
    public Material(
        ContentId id,
        MaterialId ordinal,
        PhaseAtStandardConditions phase,
        IReadOnlyList<IProperty> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        Id = id;
        Ordinal = ordinal;
        Phase = phase;
        Properties = properties;
    }

    public ContentId Id { get; }
    public MaterialId Ordinal { get; }
    public PhaseAtStandardConditions Phase { get; }
    public IReadOnlyList<IProperty> Properties { get; }
}

/// <summary>
/// R2.4 — the catalogue. Ordinals are assigned here, from the id sort, and
/// nowhere else: that is what makes `Composition`'s dense indexing meaningful
/// and what keeps two runs of the same content producing the same ordinals.
/// </summary>
public sealed class MaterialCatalogue : IMaterialCatalog
{
    private readonly IMaterial[] _byOrdinal;
    private readonly Dictionary<ContentId, IMaterial> _byId;

    /// <summary>
    /// Takes definitions, not materials: the ordinal is the catalogue's to
    /// assign, so accepting pre-built materials would let a caller choose one.
    /// </summary>
    public MaterialCatalogue(
        IReadOnlyList<(ContentId Id, PhaseAtStandardConditions Phase,
                       IReadOnlyList<IProperty> Properties)> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        // Ordinal = index into the ID-SORTED list (SDD-004 §6). Ordinal comparison
        // so the order cannot shift with the machine's culture (rule D-8).
        var sorted = new (ContentId Id, PhaseAtStandardConditions Phase,
                          IReadOnlyList<IProperty> Properties)[definitions.Count];
        for (int i = 0; i < definitions.Count; i++) sorted[i] = definitions[i];
        Array.Sort(sorted, (a, b) => a.Id.CompareTo(b.Id));

        _byOrdinal = new IMaterial[sorted.Length];
        _byId = new Dictionary<ContentId, IMaterial>(sorted.Length);

        for (int i = 0; i < sorted.Length; i++)
        {
            var material = new Material(sorted[i].Id, new MaterialId(i),
                                        sorted[i].Phase, sorted[i].Properties);

            if (!_byId.TryAdd(material.Id, material))
                throw new InvariantFault("SDD-004 §6", null,
                    $"Material '{material.Id}' is defined twice; ids are unique per catalogue.");

            _byOrdinal[i] = material;
        }
    }

    public int Count => _byOrdinal.Length;

    public IMaterial this[MaterialId ordinal]
    {
        get
        {
            if (ordinal.Ordinal < 0 || ordinal.Ordinal >= _byOrdinal.Length)
                throw new InvariantFault("SDD-004 §6", null,
                    $"Material ordinal {ordinal.Ordinal} is outside the catalogue " +
                    $"of {_byOrdinal.Length}.");
            return _byOrdinal[ordinal.Ordinal];
        }
    }

    /// <summary>Missing id is a content fault, never null (SDD-004 §3).</summary>
    public IMaterial Resolve(ContentId id) =>
        _byId.TryGetValue(id, out IMaterial? material)
            ? material
            : throw new SaveDataFault("SDD-004 §3", null,
                $"No material '{id}' in a catalogue of {_byOrdinal.Length}.");

    public bool TryResolve(ContentId id, out IMaterial? material) =>
        _byId.TryGetValue(id, out material);

    /// <summary>
    /// A zero composition sized to this catalogue — the only correct way to
    /// start one, since a Composition's length must match the catalogue that
    /// gave its ordinals meaning (SDD-002 §2).
    /// </summary>
    public Composition ZeroComposition() => Composition.Zero(_byOrdinal.Length);
}
