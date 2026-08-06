// R5.1 — the compartment: the simulated unit and the whole of subsurface truth
// (SDD-003 §3, design 02 §2.1).
//
// EVERYTHING IN THIS FILE IS INTERNAL, and that is the phase's most important
// deliverable after the material balance itself. The player's belief about a
// reservoir is the game; if any consumer could read Pr directly, every
// exploration and appraisal decision downstream would be theatre. The assembly
// boundary is what makes that impossible rather than merely discouraged
// (design 03 §2) — R14 inherits a boundary that already exists instead of
// retrofitting one around code that grew up without it.
//
// The compartment is NOT an IFlowElement. The completion is (R5 §2.2, corrected
// at R5.0): an element publishes its outlet pressure into a MaterialStream the
// solver hands to everything downstream, so a compartment element would
// broadcast reservoir pressure to any holder of a stream.

using System.Collections.Immutable;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Subsurface;

/// <summary>
/// Dense mass per material, kg, indexed by <c>MaterialId.Ordinal</c>.
///
/// <para>The same layout as <see cref="Composition"/> and deliberately not the
/// same type: <c>Composition</c> is a mass FLOW (kg/s, SDD-002 §2) and this is a
/// mass. Sharing a type would make "commit a rate as an inventory" a silent unit
/// error of exactly the kind the volume families exist to make uncompilable.</para>
/// </summary>
internal readonly record struct InPlace(ImmutableArray<double> KilogramsByOrdinal)
{
    public static InPlace Of(params double[] kilogramsByOrdinal)
    {
        ArgumentNullException.ThrowIfNull(kilogramsByOrdinal);

        for (int i = 0; i < kilogramsByOrdinal.Length; i++)
        {
            double kg = kilogramsByOrdinal[i];
            if (double.IsNaN(kg) || double.IsInfinity(kg) || kg < 0.0)
                throw new InvariantFault("SDD-003 §3", null,
                    $"in-place mass for ordinal {i} is {Format(kg)}; " +
                    "a compartment cannot hold a negative or non-finite mass");
        }

        return new InPlace([.. kilogramsByOrdinal]);
    }

    public Mass this[MaterialId material] =>
        new(KilogramsByOrdinal[material.Ordinal]);

    public int MaterialCount => KilogramsByOrdinal.Length;

    /// <summary>Withdrawal at commit. Negative remainder is an invariant
    /// failure, not a clamp: producing more of a material than exists means the
    /// flow solve and the inventory disagree, and continuing would hide it.</summary>
    public InPlace Less(Composition producedMass)
    {
        var remaining = new double[KilogramsByOrdinal.Length];

        for (int i = 0; i < remaining.Length; i++)
        {
            double left = KilogramsByOrdinal[i] - producedMass[new MaterialId(i)].KgPerSecond;

            // The tolerance is on the SUBTRACTION, not on the physics: taking
            // the last kilogram of a material may land a few ulp below zero.
            if (left < 0.0)
            {
                if (-left > Math.Max(1e-9, 1e-12 * KilogramsByOrdinal[i]))
                    throw new InvariantFault("INV1", null,
                        $"ordinal {i}: withdrawal exceeds in place by {Format(-left)} kg");
                left = 0.0;
            }

            remaining[i] = left;
        }

        return new InPlace([.. remaining]);
    }

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>Fluid contacts, at datum TVD. They move as volume is replaced.</summary>
internal readonly record struct ContactSet(
    Length GasOilContact,
    Length OilWaterContact);

internal readonly record struct RockTruth(
    double Porosity,                 // fraction
    Permeability Permeability,
    Length NetThickness,             // h
    Area DrainageArea,               // A
    double RockCompressibility);     // c_f, 1/Pa

/// <summary>
/// A declared hydraulic connection to another compartment. Zero
/// transmissibility is not "no link" — it is a link the player may later
/// discover carries nothing, which is a different piece of knowledge.
/// </summary>
internal readonly record struct CompartmentLink(
    EntityId<IReservoirCompartmentEntity> Other,
    double Transmissibility);        // m³/s/Pa

/// <summary>
/// The volumetric truth a compartment was created with. Separated from the
/// mutable state because SDD-003 §3.1 measures every expansion term from initial
/// conditions — these numbers are read on every tick and written never.
/// </summary>
internal sealed record InitialConditions(
    Pressure Pressure,                      // Pi
    SurfaceVolume OilInPlace,               // N, stock-tank m³
    StandardGasVolume GasInPlace,           // G (free gas cap)
    double GasCapRatio,                     // m
    double ConnateWaterSaturation,          // Swc
    double WaterCompressibility,            // cw, 1/Pa
    InPlace Mass);

/// <summary>
/// SDD-003 §3. The hydraulically connected volume on which material balance is
/// solved — not the reservoir, not the field (design 02 §2.1, PPDM alignment §4).
///
/// <para>Making the COMPARTMENT the simulated unit is what makes
/// compartmentalisation a discovery (open decision M1): the player believes in a
/// reservoir, the engine simulates compartments, and the gap between the two is
/// inferred from pressure data.</para>
/// </summary>
internal interface IReservoirCompartment
{
    EntityId<IReservoirCompartmentEntity> Id { get; }
    Pressure Pr { get; }
    InPlace InPlace { get; }
    ContactSet Contacts { get; }
    RockTruth Rock { get; }
    IDriveMechanism Drive { get; }
    IReadOnlyList<CompartmentLink> Links { get; }

    InitialConditions Initial { get; }
    CumulativeProduction Cumulative { get; }
}

/// <summary>
/// What has crossed the compartment's boundary since initial conditions.
///
/// <para>Cumulative rather than per tick because §3.1 re-solves the pressure
/// from `Pi` every tick: a rounding error in one tick's pressure cannot then
/// propagate into the next, and R5-V9's invariant is stated in these terms.</para>
/// </summary>
internal readonly record struct CumulativeProduction(
    SurfaceVolume Oil,               // Np
    StandardGasVolume Gas,           // Gp
    SurfaceVolume Water,             // Wp
    ReservoirVolume WaterInflux,     // We
    ReservoirVolume Injected)        // Vinj
{
    public static CumulativeProduction None { get; } = new(
        new SurfaceVolume(0.0), new StandardGasVolume(0.0), new SurfaceVolume(0.0),
        new ReservoirVolume(0.0), new ReservoirVolume(0.0));

    public CumulativeProduction Plus(
        SurfaceVolume oil, StandardGasVolume gas, SurfaceVolume water,
        ReservoirVolume influx, ReservoirVolume injected) =>
        new(new SurfaceVolume(Oil.CubicMetres + oil.CubicMetres),
            new StandardGasVolume(Gas.CubicMetres + gas.CubicMetres),
            new SurfaceVolume(Water.CubicMetres + water.CubicMetres),
            new ReservoirVolume(WaterInflux.CubicMetres + influx.CubicMetres),
            new ReservoirVolume(Injected.CubicMetres + injected.CubicMetres));
}
