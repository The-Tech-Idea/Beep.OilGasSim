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

using OGSim.Contracts;
using OGSim.Kernel;

// SDD-003 §3's `InPlace` IS the kernel's mass-per-material type. It lived here
// as its own struct until R8.0's finding 113: the tank's inventory is the same
// concept, `InPlace` was internal to this assembly, and two copies of
// "kilograms by ordinal" is exactly the duplication CLAUDE.md's rule about
// kernel types exists to prevent. The alias keeps the domain name at the call
// sites while the arithmetic lives in one place.
using InPlace = OGSim.Kernel.MaterialInventory;

namespace OGSim.Subsurface;

/// <summary>Fluid contacts, at datum TVD. They move as volume is replaced.</summary>
internal readonly record struct ContactSet(
    Length GasOilContact,
    Length OilWaterContact);

internal readonly record struct RockTruth(
    double Porosity,                 // fraction
    Permeability Permeability,
    Length NetThickness,             // h
    Area DrainageArea,               // A
    double RockCompressibility,      // c_f, 1/Pa

    /// <summary>
    /// The rock's Corey curve (SDD-003 §3.1c) — what turns a water saturation
    /// into a water cut.
    ///
    /// <para>On the ROCK because that is what it is a property of: two
    /// compartments in one field with different rock water out differently, and
    /// a curve held anywhere else would make that expressible only by
    /// accident.</para>
    /// </summary>
    RelativePermeabilityCurve Wettability);

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

    /// <summary>
    /// PV, reservoir m³ — the volume the saturations are fractions OF.
    ///
    /// <para>Kept because water saturation is DERIVED from it and the cumulative
    /// terms (SDD-003 §3.1c), never stored: a saturation held as its own field
    /// would be a second owner of a fact the material balance already
    /// determines, and the two would drift the first time one was updated
    /// without the other (law L5).</para>
    /// </summary>
    ReservoirVolume PoreVolume,
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
    ReservoirVolume Injected,        // Vinj

    /// <summary>
    /// How much of <c>Injected</c> was BOUGHT (SDD-012 §5's R20d.25 amendment).
    ///
    /// <para>Part of the injected volume and never added to it — the balance
    /// counts every cubic metre of water that arrives, whatever its provenance,
    /// and a second term in Φ would replace the voidage twice.</para>
    ///
    /// <para>It is carried separately because SOURING cares which water it was.
    /// Produced water put back has already been through the rock: anoxic,
    /// reduced, stripped of sulphate, and the fluid that sours a reservoir
    /// least. Sea water carries the sulphate the bacteria eat. A compartment
    /// that only ever reinjected its own water does not sour however long it
    /// runs, and this is the number that says so.</para>
    /// </summary>
    ReservoirVolume Imported)
{
    public static CumulativeProduction None { get; } = new(
        new SurfaceVolume(0.0), new StandardGasVolume(0.0), new SurfaceVolume(0.0),
        new ReservoirVolume(0.0), new ReservoirVolume(0.0), new ReservoirVolume(0.0));

    public CumulativeProduction Plus(
        SurfaceVolume oil, StandardGasVolume gas, SurfaceVolume water,
        ReservoirVolume influx, ReservoirVolume injected, ReservoirVolume imported) =>
        new(new SurfaceVolume(Oil.CubicMetres + oil.CubicMetres),
            new StandardGasVolume(Gas.CubicMetres + gas.CubicMetres),
            new SurfaceVolume(Water.CubicMetres + water.CubicMetres),
            new ReservoirVolume(WaterInflux.CubicMetres + influx.CubicMetres),
            new ReservoirVolume(Injected.CubicMetres + injected.CubicMetres),
            new ReservoirVolume(Imported.CubicMetres + imported.CubicMetres));
}
