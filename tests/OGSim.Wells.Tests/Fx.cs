// Shared well-fixture values (SDD-003 §6.1b).

using OGSim.Kernel;

namespace OGSim.Wells.Tests;

internal static class Fx
{
    /// <summary>
    /// A DRY well: no solution gas.
    ///
    /// <para>These suites are about inflow, outflow and the operating point, and
    /// a produced gas stream would be a second thing under test in every one of
    /// them. Zero is the honest value for a reservoir above its bubble point
    /// with nothing dissolved, not a disabled feature — the produced-stream form
    /// is exercised where it belongs, against a completion built to carry
    /// gas.</para>
    /// </summary>
    public const double NoSolutionGas = 0.0;

    /// <summary>ρ_sc of gas at γg = 0.75 (SDD-003 §6.1b). Present so the fluid
    /// block is complete; it multiplies a zero rate here.</summary>
    public static Density GasDensity { get; } =
        new(0.75 * PhysicalConstants.AirDensityAtStandardKgPerM3);

    /// <summary>A DRY well: no water. These suites are about inflow and outflow,
    /// and a produced water stream would be a second thing under test.</summary>
    public const double Dry = 0.0;

    public static Density WaterDensity { get; } = Density.FromSpecificGravity(1.05);
}
