// R3.7 — the shipped content kinds (SDD-004 §2, §6; design 10 §2).
//
// Each kind is a definition record plus an IContentKind that reads it. The
// loader knows none of them: it is handed the list at composition, which is why
// adding "helium" or a new lithology is a JSON file and a registration, never a
// change in ContentLoader.cs (R3 §3's acceptance criterion).
//
// DATASHEETS ARE CLOSED. Every field below is a declared property of a record —
// there is no generic bag, because a bag is where unknown-key safety goes to
// die (SDD-004 §6): a typo'd key in a bag is data, and a typo'd key in a record
// is a load failure naming the nearest valid one.

using System.Text.Json;

namespace OGSim.Kernel;

// ---------------------------------------------------------------- property-kind

/// <summary>
/// A named physical quantity, its dimension and where its values stop being
/// physical (design 10 §2). The range lives here so R2-V10's check happens once
/// at the boundary every value of that kind crosses.
/// </summary>
public sealed record PropertyKindDefinition(
    ContentId Id,
    Dimension Dimension,
    double MinimumValid,
    double MaximumValid,
    BeliefSpace Space) : ContentDefinition(Id);

public sealed class PropertyKindContentKind : IContentKind
{
    public string Name => "property-kind";

    public ContentDefinition Read(JsonElement element)
    {
        var id = new ContentId(Required(element, "id").GetString()!);
        Dimension dimension = ParseEnum<Dimension>(Required(element, "dimension"), "dimension");
        BeliefSpace space = element.TryGetProperty("space", out JsonElement s)
            ? ParseEnum<BeliefSpace>(s, "space")
            : BeliefSpace.Linear;

        // Ranges are plain numbers, not quantity strings: they are expressed in
        // the kind's OWN canonical SI unit, and a unit here would be circular —
        // the kind is what tells the grammar which dimension to expect.
        double min = Required(element, "minimumValid").GetDouble();
        double max = Required(element, "maximumValid").GetDouble();

        return new PropertyKindDefinition(id, dimension, min, max, space);
    }

    public IReadOnlyList<ContentReference> ReferencesOf(ContentDefinition definition) => [];

    public IReadOnlyList<string> ConsistencyProblems(ContentDefinition definition)
    {
        var problems = new List<string>();
        if (definition is not PropertyKindDefinition kind) return problems;

        if (kind.MinimumValid > kind.MaximumValid)
            problems.Add($"minimumValid {kind.MinimumValid} exceeds maximumValid {kind.MaximumValid}");

        // The same rule PropertyKind enforces at construction, checked at load so
        // an author hears about it before the engine ever runs.
        if (kind.Space == BeliefSpace.Log && kind.MinimumValid <= 0.0)
            problems.Add("a Log-space kind must be strictly positive; a non-positive " +
                         "value has no logarithm and would NaN the first belief update");

        return problems;
    }

    public IReadOnlyList<PluginBinding> PluginsOf(ContentDefinition definition) => [];

    internal static JsonElement Required(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value)
            ? value
            : throw new JsonException($"missing required property '{name}'");

    internal static TEnum ParseEnum<TEnum>(JsonElement element, string name)
        where TEnum : struct, Enum
    {
        string text = element.GetString() ?? string.Empty;
        return Enum.TryParse(text, ignoreCase: true, out TEnum parsed)
            ? parsed
            : throw new JsonException(
                $"'{text}' is not a valid {name}; expected one of {string.Join(", ", Enum.GetNames<TEnum>())}");
    }
}

// ---------------------------------------------------------------- material

/// <summary>One property of a material, already unit-bound to canonical SI.</summary>
public sealed record MaterialPropertyEntry(ContentId Kind, double SiValue, Provenance Source);

public sealed record MaterialDefinition(
    ContentId Id,
    PhaseAtStandardConditions Phase,
    bool IsSold,
    IReadOnlyList<MaterialPropertyEntry> Properties) : ContentDefinition(Id)
{
    // Finding 131 — a mod override replaces an entry whole (SDD-004 §7), and
    // "did this source actually change the material?" is asked by comparing.
    public bool Equals(MaterialDefinition? other) =>
        other is not null && Id == other.Id && Phase == other.Phase && IsSold == other.IsSold
        && Structural.Equal(Properties, other.Properties);

    public override int GetHashCode() =>
        HashCode.Combine(Id, Phase, IsSold, Structural.HashOf(Properties));
}

/// <summary>
/// The PPDM/PRODML product list as content (research §5). A material is a
/// registered entry and never an enum, so adding helium or hydrogen is a data
/// file — and the engine never switches on identity, it reads properties.
/// </summary>
public sealed class MaterialContentKind(IReadOnlyDictionary<ContentId, Dimension> propertyDimensions)
    : IContentKind
{
    public string Name => "material";

    public ContentDefinition Read(JsonElement element)
    {
        var id = new ContentId(PropertyKindContentKind.Required(element, "id").GetString()!);
        var phase = PropertyKindContentKind.ParseEnum<PhaseAtStandardConditions>(
            PropertyKindContentKind.Required(element, "phase"), "phase");
        bool sold = element.TryGetProperty("sold", out JsonElement s) && s.GetBoolean();

        var properties = new List<MaterialPropertyEntry>();
        if (element.TryGetProperty("properties", out JsonElement bag))
        {
            foreach (JsonProperty entry in bag.EnumerateObject())
            {
                var kind = new ContentId(entry.Name);

                // Stage 3: the unit string binds against the DIMENSION the
                // property kind declares. A value whose unit is the wrong
                // dimension fails here, naming both — which is only possible
                // because property kinds are loaded before materials read them.
                if (!propertyDimensions.TryGetValue(kind, out Dimension dimension))
                    throw new JsonException(
                        $"property '{entry.Name}' names no known property-kind");

                double si = UnitGrammar.ParseToSi(entry.Value.GetString()!, dimension);
                properties.Add(new MaterialPropertyEntry(kind, si, Provenance.Measured));
            }
        }

        // Sorted by kind id so two authorings of the same material produce the
        // same record — JSON object order is not meaningful (D-5's spirit).
        properties.Sort((a, b) => a.Kind.CompareTo(b.Kind));
        return new MaterialDefinition(id, phase, sold, properties);
    }

    public IReadOnlyList<ContentReference> ReferencesOf(ContentDefinition definition)
    {
        if (definition is not MaterialDefinition material) return [];

        var references = new List<ContentReference>(material.Properties.Count);
        for (int i = 0; i < material.Properties.Count; i++)
            references.Add(new ContentReference("property-kind", material.Properties[i].Kind,
                                                $"$.properties.{material.Properties[i].Kind}"));
        return references;
    }

    public IReadOnlyList<string> ConsistencyProblems(ContentDefinition definition) => [];

    public IReadOnlyList<PluginBinding> PluginsOf(ContentDefinition definition) => [];
}

// ---------------------------------------------------------------- rock-type

public sealed record RockTypeDefinition(
    ContentId Id,
    double TypicalPorosity,
    double TypicalPermeability,        // m²
    double NetToGross) : ContentDefinition(Id);

public sealed class RockTypeContentKind : IContentKind
{
    public string Name => "rock-type";

    public ContentDefinition Read(JsonElement element)
    {
        var id = new ContentId(PropertyKindContentKind.Required(element, "id").GetString()!);

        double porosity = UnitGrammar.ParseToSi(
            PropertyKindContentKind.Required(element, "typicalPorosity").GetString()!,
            Dimension.Dimensionless);
        double permeability = UnitGrammar.ParseToSi(
            PropertyKindContentKind.Required(element, "typicalPermeability").GetString()!,
            Dimension.Permeability);
        double netToGross = UnitGrammar.ParseToSi(
            PropertyKindContentKind.Required(element, "netToGross").GetString()!,
            Dimension.Dimensionless);

        return new RockTypeDefinition(id, porosity, permeability, netToGross);
    }

    public IReadOnlyList<ContentReference> ReferencesOf(ContentDefinition definition) => [];

    public IReadOnlyList<string> ConsistencyProblems(ContentDefinition definition)
    {
        var problems = new List<string>();
        if (definition is not RockTypeDefinition rock) return problems;

        if (rock.TypicalPorosity is <= 0.0 or >= 1.0)
            problems.Add($"typicalPorosity {rock.TypicalPorosity} must be in (0, 1)");
        if (rock.NetToGross is <= 0.0 or > 1.0)
            problems.Add($"netToGross {rock.NetToGross} must be in (0, 1]");
        if (rock.TypicalPermeability <= 0.0)
            problems.Add("typicalPermeability must be positive");

        return problems;
    }

    public IReadOnlyList<PluginBinding> PluginsOf(ContentDefinition definition) => [];
}

// ---------------------------------------------------------------- fluid-system

/// <summary>
/// One reservoir fluid's declared crude quality (SDD-004 §6's finding-270
/// amendment; SDD-003 §3.0b). Ungated — a reservoir's fluid is a truth fact
/// world generation draws, not equipment a company buys.
/// </summary>
public sealed record FluidSystemDefinition(
    ContentId Id,
    double ApiGravityDegrees,
    double GasSpecificGravity,          // γg, air = 1
    double ReservoirTemperatureKelvin,
    double SolutionGorAtBubblePoint     // Rsb, sm³/sm³
    ) : ContentDefinition(Id);

/// <summary>
/// Reads a fluid system as the four bare numbers <c>BlackOilInputs</c> needs
/// (SDD-004 §6's finding-270 amendment). Only <c>reservoirTemperature</c>
/// crosses the unit-string grammar — API gravity relates to specific gravity
/// by a NONLINEAR transform the grammar's factor-plus-offset tokens cannot
/// represent, and the other two fields are read the same bare way for
/// consistency on the one record rather than because the grammar could not
/// carry them.
/// </summary>
public sealed class FluidSystemContentKind : IContentKind
{
    public string Name => "fluid-system";

    public ContentDefinition Read(JsonElement element)
    {
        var id = new ContentId(PropertyKindContentKind.Required(element, "id").GetString()!);

        double apiGravity = PropertyKindContentKind.Required(element, "apiGravityDegrees").GetDouble();
        double gasSpecificGravity =
            PropertyKindContentKind.Required(element, "gasSpecificGravity").GetDouble();
        double reservoirTemperature = UnitGrammar.ParseToSi(
            PropertyKindContentKind.Required(element, "reservoirTemperature").GetString()!,
            Dimension.Temperature);
        double solutionGor =
            PropertyKindContentKind.Required(element, "solutionGorAtBubblePoint").GetDouble();

        return new FluidSystemDefinition(
            id, apiGravity, gasSpecificGravity, reservoirTemperature, solutionGor);
    }

    public IReadOnlyList<ContentReference> ReferencesOf(ContentDefinition definition) => [];

    /// <summary>
    /// The range <c>BlackOilModel</c>'s own correlations were fitted to
    /// (SDD-003 §4.1) — a load-time guard against declaring a fluid system
    /// they were never validated for, not a second copy of that range.
    /// </summary>
    public IReadOnlyList<string> ConsistencyProblems(ContentDefinition definition)
    {
        var problems = new List<string>();
        if (definition is not FluidSystemDefinition fluid) return problems;

        if (fluid.ApiGravityDegrees is < 10.0 or > 70.0)
            problems.Add($"apiGravityDegrees {fluid.ApiGravityDegrees} must be in [10, 70] — " +
                         "below is denser than water and not a producible liquid hydrocarbon by " +
                         "this engine's black-oil correlations, above is condensate territory " +
                         "this content kind cannot represent");
        if (fluid.GasSpecificGravity <= 0.0)
            problems.Add("gasSpecificGravity must be positive");
        if (fluid.SolutionGorAtBubblePoint <= 0.0)
            problems.Add("solutionGorAtBubblePoint must be positive");

        return problems;
    }

    public IReadOnlyList<PluginBinding> PluginsOf(ContentDefinition definition) => [];
}
