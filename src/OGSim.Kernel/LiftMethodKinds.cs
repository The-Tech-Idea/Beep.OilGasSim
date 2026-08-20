// SDD-003 §6.2's R12b.2 amendment (finding 255) — a rod-pump tier, as content.
// A first cut ships one technique of the four §6.2 declares, un-gated, the
// same scoping call R12b.7 made for stimulation.
//
// UNGATED, like a sales contract and unlike a facility unit: a pump is
// installed once, not upgraded through a progression this composition has no
// second rung for yet, so this mirrors TakeOrPayContentKind's flat shape.

using System.Text.Json;

namespace OGSim.Kernel;

/// <summary>SDD-003 §6.2's rod-pump tier, as content.</summary>
public sealed record RodPumpDefinition(
    ContentId Id,
    ReservoirRate MinRate,
    ReservoirRate MaxRate,
    Length MaxDepth,
    double MaxDeviationDegrees,
    double MaxGasFraction,
    Temperature MaxTemperature,
    double MaxSolidsFraction,
    ReservoirRate Displacement) : ContentDefinition(Id);

public sealed class RodPumpContentKind : IContentKind
{
    public string Name => "rod-pump";

    public ContentDefinition Read(JsonElement element)
    {
        var id = new ContentId(PropertyKindContentKind.Required(element, "id").GetString()!);

        double minRate = PropertyKindContentKind.Required(
            element, "minRateCubicMetresPerSecond").GetDouble();
        double maxRate = PropertyKindContentKind.Required(
            element, "maxRateCubicMetresPerSecond").GetDouble();
        double maxDepth = PropertyKindContentKind.Required(element, "maxDepthMetres").GetDouble();
        double maxDeviation = PropertyKindContentKind.Required(
            element, "maxDeviationDegrees").GetDouble();
        double maxGasFraction = PropertyKindContentKind.Required(
            element, "maxGasFraction").GetDouble();
        double maxTemperatureC = PropertyKindContentKind.Required(
            element, "maxTemperatureCelsius").GetDouble();
        double maxSolidsFraction = PropertyKindContentKind.Required(
            element, "maxSolidsFraction").GetDouble();
        double displacement = PropertyKindContentKind.Required(
            element, "displacementCubicMetresPerSecond").GetDouble();

        return new RodPumpDefinition(
            id,
            new ReservoirRate(minRate),
            new ReservoirRate(maxRate),
            new Length(maxDepth),
            maxDeviation,
            maxGasFraction,
            Temperature.FromCelsius(maxTemperatureC),
            maxSolidsFraction,
            new ReservoirRate(displacement));
    }

    public IReadOnlyList<ContentReference> ReferencesOf(ContentDefinition definition) => [];

    public IReadOnlyList<string> ConsistencyProblems(ContentDefinition definition)
    {
        var problems = new List<string>();
        if (definition is not RodPumpDefinition pump) return problems;

        if (pump.MinRate.CubicMetresPerSecond < 0.0)
            problems.Add("minRateCubicMetresPerSecond cannot be negative");

        if (pump.MaxRate.CubicMetresPerSecond <= pump.MinRate.CubicMetresPerSecond)
            problems.Add("maxRateCubicMetresPerSecond must exceed the minimum");

        if (pump.MaxDepth.Metres <= 0.0)
            problems.Add("maxDepthMetres must be positive");

        if (pump.MaxDeviationDegrees is < 0.0 or > 90.0)
            problems.Add("maxDeviationDegrees must be a fraction of a right angle, 0 to 90");

        if (pump.MaxGasFraction is < 0.0 or > 1.0)
            problems.Add("maxGasFraction must be a fraction in [0, 1]");

        if (pump.MaxSolidsFraction is < 0.0 or > 1.0)
            problems.Add("maxSolidsFraction must be a fraction in [0, 1]");

        if (pump.Displacement.CubicMetresPerSecond <= 0.0)
            problems.Add(
                "displacementCubicMetresPerSecond must be positive; a pump that moves " +
                "nothing is not a pump");

        return problems;
    }

    public IReadOnlyList<PluginBinding> PluginsOf(ContentDefinition definition) => [];
}
