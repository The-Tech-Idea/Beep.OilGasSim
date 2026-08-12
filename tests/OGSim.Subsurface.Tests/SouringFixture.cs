// The souring curve these tests hand the subsurface (SDD-012 §5).
//
// A TEST DOUBLE rather than the shipped curve, because `SaturatingSourCurve`
// lives in OGSim.Integrity and a subsurface test reaching sideways into a
// sibling module would be testing the composition instead of the compartment.
// `ISouringModel` is a contract precisely so the reservoir does not care whose
// curve it is.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Subsurface.Tests;

/// <summary>Linear in the throughput ratio: a thousand ppm per pore volume of
/// sea water. Monotonic, which is all §5 requires of a curve.</summary>
internal sealed class LinearSourCurve : ISouringModel
{
    public ContentId Id { get; } = new("sour-curve-test");

    public double HydrogenSulphidePpm(ContentId rockType, double importedWaterOverPoreVolume) =>
        importedWaterOverPoreVolume <= 0.0 ? 0.0 : 1_000.0 * importedWaterOverPoreVolume;
}

internal static class Souring
{
    public static ISouringModel SweetRock { get; } = new LinearSourCurve();

    public static ContentId TheRock { get; } = new("test-rock");

    public const double SouringReference = 1_000.0;
}
