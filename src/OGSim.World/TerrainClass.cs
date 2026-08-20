// R20d.8.9 — terrain classes as loadable content (SDD-010 §3, catalog C16).
//
// A PLAIN ContentDefinition (C16's own pass-7 amendment, finding 80): terrain
// is a world fact assigned by the generator, never gated by technology or era
// and never something a company buys.
//
// THE KIND LIVES HERE rather than in the kernel's bootstrap kinds because its
// datasheet is written in this module's vocabulary — the same reasoning
// TechnologyContentKind's header gives for OGSim.Capabilities (R3.7).

using System.Text.Json;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.World;

/// <summary>C16's three closed band vocabularies.</summary>
public enum TerrainHeightBand { Low, Mid, High }

public enum TerrainSlopeBand { Flat, Moderate, Steep }

public enum TerrainClimateBand { Arid, Wet, Any }

/// <summary>
/// C16's amendment: the (height, slope, climate) cut a class forms at, each
/// axis a LIST rather than a single value — a class whose physical range spans
/// two bands (desert is low-to-mid) has somewhere to say so.
/// </summary>
public sealed record TerrainFormation(
    IReadOnlyList<TerrainHeightBand> HeightBand,
    IReadOnlyList<TerrainSlopeBand> SlopeBand,
    IReadOnlyList<TerrainClimateBand> ClimateBands)
{
    public bool Equals(TerrainFormation? other) =>
        other is not null
        && Structural.Equal(HeightBand, other.HeightBand)
        && Structural.Equal(SlopeBand, other.SlopeBand)
        && Structural.Equal(ClimateBands, other.ClimateBands);

    public override int GetHashCode() =>
        HashCode.Combine(
            Structural.HashOf(HeightBand), Structural.HashOf(SlopeBand),
            Structural.HashOf(ClimateBands));
}

/// <summary>C16's three-state Buildable column — a bare bool cannot hold the
/// middle one (swamp: "piled only").</summary>
public enum TerrainBuildable { Yes, PiledOnly, No }

/// <summary>
/// One `terrain-class` entry — a row of catalog C16, mapping one-to-one onto
/// its table's columns.
/// </summary>
public sealed record TerrainClassDefinition(
    ContentId Id,
    TerrainFormation Formation,
    double ConstructionCostFactor,
    double TransportCostFactor,
    TerrainBuildable Buildable,

    /// <summary>Free text, not a ContentId — no catalog of rig/crew classes
    /// exists as loadable content yet, and the shipped values (C16's own
    /// "standard + water logistics", "marsh/barge") use characters
    /// ContentId's kebab-case charset forbids.</summary>
    string RigAccess,
    string SeismicOps,

    double SettlementWeight,
    double ArableWeight) : ContentDefinition(Id)
{
    public bool Equals(TerrainClassDefinition? other) =>
        other is not null && Id == other.Id
        && Formation == other.Formation
        && ConstructionCostFactor.Equals(other.ConstructionCostFactor)
        && TransportCostFactor.Equals(other.TransportCostFactor)
        && Buildable == other.Buildable
        && RigAccess == other.RigAccess
        && SeismicOps == other.SeismicOps
        && SettlementWeight.Equals(other.SettlementWeight)
        && ArableWeight.Equals(other.ArableWeight);

    public override int GetHashCode() =>
        HashCode.Combine(Id, Formation, ConstructionCostFactor, TransportCostFactor,
            Buildable, RigAccess, SeismicOps,
            HashCode.Combine(SettlementWeight, ArableWeight));
}

public sealed class TerrainClassContentKind : IContentKind
{
    public string Name => "terrain-class";

    public ContentDefinition Read(JsonElement element)
    {
        var id = new ContentId(Required(element, "id").GetString()!);

        JsonElement formation = Required(element, "formation");

        var heightBand = new List<TerrainHeightBand>();
        foreach (JsonElement band in Required(formation, "heightBand").EnumerateArray())
            heightBand.Add(ParseEnum<TerrainHeightBand>(band));

        var slopeBand = new List<TerrainSlopeBand>();
        foreach (JsonElement band in Required(formation, "slopeBand").EnumerateArray())
            slopeBand.Add(ParseEnum<TerrainSlopeBand>(band));

        var climateBands = new List<TerrainClimateBand>();
        foreach (JsonElement band in Required(formation, "climateBands").EnumerateArray())
            climateBands.Add(ParseEnum<TerrainClimateBand>(band));

        double constructionCostFactor = Required(element, "constructionCostFactor").GetDouble();
        double transportCostFactor = Required(element, "transportCostFactor").GetDouble();
        TerrainBuildable buildable = ParseBuildable(Required(element, "buildable"));
        string rigAccess = Required(element, "rigAccess").GetString()!;
        string seismicOps = Required(element, "seismicOps").GetString()!;
        double settlementWeight = Required(element, "settlementWeight").GetDouble();
        double arableWeight = Required(element, "arableWeight").GetDouble();

        return new TerrainClassDefinition(
            id,
            new TerrainFormation(heightBand, slopeBand, climateBands),
            constructionCostFactor, transportCostFactor, buildable,
            rigAccess, seismicOps, settlementWeight, arableWeight);
    }

    /// <summary>Terrain names no other catalogue entry — the formation bands
    /// are a self-contained cut, not a reference.</summary>
    public IReadOnlyList<ContentReference> ReferencesOf(ContentDefinition definition) => [];

    public IReadOnlyList<string> ConsistencyProblems(ContentDefinition definition)
    {
        var problems = new List<string>();
        if (definition is not TerrainClassDefinition terrain) return problems;

        if (terrain.Formation.HeightBand.Count == 0)
            problems.Add($"'{terrain.Id.Value}' declares no heightBand; a class that forms " +
                         "nowhere on the (height, slope) grid can never be assigned");

        if (terrain.Formation.SlopeBand.Count == 0)
            problems.Add($"'{terrain.Id.Value}' declares no slopeBand; a class that forms " +
                         "nowhere on the (height, slope) grid can never be assigned");

        if (terrain.Formation.ClimateBands.Count == 0)
            problems.Add($"'{terrain.Id.Value}' declares no climateBands; the shipped " +
                         "generator only reaches a class whose climateBands includes 'any', " +
                         "so an empty list makes this one unreachable by construction");

        if (!double.IsFinite(terrain.ConstructionCostFactor) || terrain.ConstructionCostFactor <= 0.0)
            problems.Add($"'{terrain.Id.Value}' constructionCostFactor " +
                         $"{terrain.ConstructionCostFactor} is not a positive, finite multiplier");

        if (!double.IsFinite(terrain.TransportCostFactor) || terrain.TransportCostFactor <= 0.0)
            problems.Add($"'{terrain.Id.Value}' transportCostFactor " +
                         $"{terrain.TransportCostFactor} is not a positive, finite multiplier");

        if (!double.IsFinite(terrain.SettlementWeight) || terrain.SettlementWeight < 0.0)
            problems.Add($"'{terrain.Id.Value}' settlementWeight {terrain.SettlementWeight} " +
                         "is not a non-negative, finite weight");

        if (!double.IsFinite(terrain.ArableWeight) || terrain.ArableWeight < 0.0)
            problems.Add($"'{terrain.Id.Value}' arableWeight {terrain.ArableWeight} is not a " +
                         "non-negative, finite weight");

        return problems;
    }

    /// <summary>A terrain class names no model plugin.</summary>
    public IReadOnlyList<PluginBinding> PluginsOf(ContentDefinition definition) => [];

    private static JsonElement Required(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value)
            ? value
            : throw new JsonException($"missing required property '{name}'");

    private static TEnum ParseEnum<TEnum>(JsonElement element) where TEnum : struct, Enum =>
        Enum.TryParse(element.GetString(), ignoreCase: true, out TEnum parsed)
            ? parsed
            : throw new JsonException(
                $"'{element.GetString()}' is not one of " +
                $"{string.Join(", ", Enum.GetNames<TEnum>())}");

    private static TerrainBuildable ParseBuildable(JsonElement element) =>
        element.GetString() switch
        {
            "yes" => TerrainBuildable.Yes,
            "piled-only" => TerrainBuildable.PiledOnly,
            "no" => TerrainBuildable.No,
            var other => throw new JsonException(
                $"'{other}' is not one of yes, piled-only, no"),
        };
}
