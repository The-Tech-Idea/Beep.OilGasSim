// SDD-003 §6.2's R12b.2 amendment (finding 255, widened) — every lift
// method's tier, as content. All four now ship: the split between them is
// entirely in the datasheet, per §6.2's own "each method fills the fields it
// uses" — ESP a curve and an efficiency, gas lift an injection rate and a gas
// density, rod pump and PCP the same displacement-cap shape distinguished
// only by envelope.
//
// UNGATED, like a sales contract and unlike a facility unit: a pump is
// installed once, not upgraded through a progression this composition has no
// second rung for yet, so all four mirror TakeOrPayContentKind's flat shape.

using System.Text.Json;

namespace OGSim.Kernel;

// ------------------------------------------------------- rod pump / PCP

/// <summary>SDD-003 §6.2's displacement-pump tier, as content — shared by
/// rod pump and PCP, which §6.2 gives the same relation and differ only in
/// the envelope a rung declares.</summary>
public sealed record DisplacementPumpDefinition(
    ContentId Id,
    ReservoirRate MinRate,
    ReservoirRate MaxRate,
    Length MaxDepth,
    double MaxDeviationDegrees,
    double MaxGasFraction,
    Temperature MaxTemperature,
    double MaxSolidsFraction,
    ReservoirRate Displacement) : ContentDefinition(Id);

/// <summary>Reads a displacement-pump tier. ONE class serves both kinds — a
/// rod pump and a PCP declare the same fields — distinguished only by the
/// content <c>kind</c> string a caller constructs it with, the same way a
/// facility unit's six kinds share <c>FacilityContentKind&lt;T&gt;</c>.</summary>
public sealed class DisplacementPumpContentKind(string name) : IContentKind
{
    public string Name => name;

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

        return new DisplacementPumpDefinition(
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
        if (definition is not DisplacementPumpDefinition pump) return problems;

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

// ------------------------------------------------------------------- ESP

/// <summary>SDD-003 §6.2's ESP tier, as content — a piecewise-linear head
/// curve at 1000 kg/m³ reference density plus an efficiency, the two fields
/// the datasheet actually uses.</summary>
public sealed record EspDefinition(
    ContentId Id,
    ReservoirRate MinRate,
    ReservoirRate MaxRate,
    Length MaxDepth,
    double MaxDeviationDegrees,
    double MaxGasFraction,
    Temperature MaxTemperature,
    double MaxSolidsFraction,
    IReadOnlyList<(double RateM3PerS, double HeadM)> HeadCurve,
    double Efficiency) : ContentDefinition(Id)
{
    // Finding 131 — a record carrying a collection compares it structurally
    // or not at all: the compiler's default is reference equality, which
    // would make two content edits that produce an identical curve compare
    // unequal.
    public bool Equals(EspDefinition? other) =>
        other is not null && Id == other.Id && MinRate == other.MinRate
        && MaxRate == other.MaxRate && MaxDepth == other.MaxDepth
        && MaxDeviationDegrees == other.MaxDeviationDegrees
        && MaxGasFraction == other.MaxGasFraction && MaxTemperature == other.MaxTemperature
        && MaxSolidsFraction == other.MaxSolidsFraction && Efficiency == other.Efficiency
        && Structural.Equal(HeadCurve, other.HeadCurve);

    public override int GetHashCode() =>
        HashCode.Combine(
            Id, MinRate, MaxRate, MaxDepth,
            HashCode.Combine(
                MaxDeviationDegrees, MaxGasFraction, MaxTemperature, MaxSolidsFraction,
                Efficiency),
            Structural.HashOf(HeadCurve));
}

public sealed class EspContentKind : IContentKind
{
    public string Name => "esp";

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
        double efficiency = PropertyKindContentKind.Required(element, "efficiency").GetDouble();

        JsonElement curveArray = PropertyKindContentKind.Required(element, "headCurve");

        var curve = new List<(double, double)>();
        foreach (JsonElement point in curveArray.EnumerateArray())
            curve.Add((
                PropertyKindContentKind.Required(point, "rateCubicMetresPerSecond").GetDouble(),
                PropertyKindContentKind.Required(point, "headMetres").GetDouble()));

        return new EspDefinition(
            id,
            new ReservoirRate(minRate),
            new ReservoirRate(maxRate),
            new Length(maxDepth),
            maxDeviation,
            maxGasFraction,
            Temperature.FromCelsius(maxTemperatureC),
            maxSolidsFraction,
            curve,
            efficiency);
    }

    public IReadOnlyList<ContentReference> ReferencesOf(ContentDefinition definition) => [];

    public IReadOnlyList<string> ConsistencyProblems(ContentDefinition definition)
    {
        var problems = new List<string>();
        if (definition is not EspDefinition esp) return problems;

        if (esp.MinRate.CubicMetresPerSecond < 0.0)
            problems.Add("minRateCubicMetresPerSecond cannot be negative");

        if (esp.MaxRate.CubicMetresPerSecond <= esp.MinRate.CubicMetresPerSecond)
            problems.Add("maxRateCubicMetresPerSecond must exceed the minimum");

        if (esp.MaxDepth.Metres <= 0.0)
            problems.Add("maxDepthMetres must be positive");

        if (esp.MaxDeviationDegrees is < 0.0 or > 90.0)
            problems.Add("maxDeviationDegrees must be a fraction of a right angle, 0 to 90");

        if (esp.MaxGasFraction is < 0.0 or > 1.0)
            problems.Add("maxGasFraction must be a fraction in [0, 1]");

        if (esp.MaxSolidsFraction is < 0.0 or > 1.0)
            problems.Add("maxSolidsFraction must be a fraction in [0, 1]");

        if (esp.Efficiency is <= 0.0 or > 1.0)
            problems.Add("efficiency must be a fraction in (0, 1]");

        if (esp.HeadCurve.Count < 2)
            problems.Add("headCurve needs at least two points for a piecewise-linear curve");
        else
            for (int i = 1; i < esp.HeadCurve.Count; i++)
                if (esp.HeadCurve[i].RateM3PerS <= esp.HeadCurve[i - 1].RateM3PerS)
                    problems.Add($"headCurve point {i} does not ascend in rate");

        return problems;
    }

    public IReadOnlyList<PluginBinding> PluginsOf(ContentDefinition definition) => [];
}

// --------------------------------------------------------------- gas lift

/// <summary>SDD-003 §6.2's gas-lift tier, as content.</summary>
public sealed record GasLiftDefinition(
    ContentId Id,
    ReservoirRate MinRate,
    ReservoirRate MaxRate,
    Length MaxDepth,
    double MaxDeviationDegrees,
    double MaxGasFraction,
    Temperature MaxTemperature,
    double MaxSolidsFraction,
    ReservoirRate InjectionRate,
    double GasDensityKgPerM3) : ContentDefinition(Id);

public sealed class GasLiftContentKind : IContentKind
{
    public string Name => "gas-lift";

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
        double injectionRate = PropertyKindContentKind.Required(
            element, "injectionRateCubicMetresPerSecond").GetDouble();
        double gasDensity = PropertyKindContentKind.Required(
            element, "gasDensityKgPerCubicMetre").GetDouble();

        return new GasLiftDefinition(
            id,
            new ReservoirRate(minRate),
            new ReservoirRate(maxRate),
            new Length(maxDepth),
            maxDeviation,
            maxGasFraction,
            Temperature.FromCelsius(maxTemperatureC),
            maxSolidsFraction,
            new ReservoirRate(injectionRate),
            gasDensity);
    }

    public IReadOnlyList<ContentReference> ReferencesOf(ContentDefinition definition) => [];

    public IReadOnlyList<string> ConsistencyProblems(ContentDefinition definition)
    {
        var problems = new List<string>();
        if (definition is not GasLiftDefinition gasLift) return problems;

        if (gasLift.MinRate.CubicMetresPerSecond < 0.0)
            problems.Add("minRateCubicMetresPerSecond cannot be negative");

        if (gasLift.MaxRate.CubicMetresPerSecond <= gasLift.MinRate.CubicMetresPerSecond)
            problems.Add("maxRateCubicMetresPerSecond must exceed the minimum");

        if (gasLift.MaxDepth.Metres <= 0.0)
            problems.Add("maxDepthMetres must be positive");

        if (gasLift.MaxDeviationDegrees is < 0.0 or > 90.0)
            problems.Add("maxDeviationDegrees must be a fraction of a right angle, 0 to 90");

        if (gasLift.MaxGasFraction is < 0.0 or > 1.0)
            problems.Add("maxGasFraction must be a fraction in [0, 1]");

        if (gasLift.MaxSolidsFraction is < 0.0 or > 1.0)
            problems.Add("maxSolidsFraction must be a fraction in [0, 1]");

        if (gasLift.InjectionRate.CubicMetresPerSecond < 0.0)
            problems.Add("injectionRateCubicMetresPerSecond cannot be negative");

        if (gasLift.GasDensityKgPerM3 <= 0.0)
            problems.Add("gasDensityKgPerCubicMetre must be positive");

        return problems;
    }

    public IReadOnlyList<PluginBinding> PluginsOf(ContentDefinition definition) => [];
}
