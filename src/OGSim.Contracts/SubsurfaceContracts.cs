// SDD-003 — subsurface contracts. Truth types (IReservoirCompartment and the
// accumulation) are INTERNAL to their owning modules and do not appear here:
// this file carries only what other modules may legitimately see.

using OGSim.Kernel;

namespace OGSim.Contracts;

/// <summary>Marker for compartment entity ids (truth object itself is internal to OGSim.Subsurface).</summary>
public interface IReservoirCompartmentEntity { }

/// <summary>Detectability class of an accumulation (design 06 §2.3).</summary>
public enum DetectClass { D0, D1, D2, D3 }

public enum DepthClass { Shallow, Standard, Deep, UltraDeep }

public enum WaterDepthClass { Onshore, Shallow, Deep, UltraDeep }

/// <summary>
/// What it takes to DEVELOP what you found (design 06 §2.3; SDD-003 §3.0b).
/// Consumed only by the gating validator on development commands.
/// </summary>
public sealed record AccessRequirements(
    DepthClass Depth,
    WaterDepthClass WaterDepth,
    bool Hpht,
    bool Tight,
    bool Sour);

public enum FluidForm { BlackOil, ModifiedBlackOil }

/// <summary>Per-material phase fractions at a given (P, T).</summary>
public sealed record PhaseSplit(
    IReadOnlyList<(MaterialId Material, double GasFraction, double LiquidFraction, double AqueousFraction)> Fractions);

public sealed record ValidityRange(Pressure MinP, Pressure MaxP, Temperature MinT, Temperature MaxT);

/// <summary>
/// SDD-003 §4 — black-oil / modified-black-oil fluid behaviour. Every function
/// carries its published validity range; outside it is a MODEL FAULT, never an
/// extrapolation (R2-V10 policy).
/// </summary>
public interface IFluidPropertyModel
{
    FluidForm Form { get; }
    Pressure Pb { get; }
    double Rs(Pressure p);
    double Rv(Pressure p);                       // ModifiedBlackOil only; BlackOil ⇒ 0
    FormationVolumeFactor Bo(Pressure p);
    GasFormationVolumeFactor Bg(Pressure p);     // rm³/sm³ — its OWN bridge to StandardGasVolume (finding 77 corrects finding 72)
    Viscosity MuOil(Pressure p);
    Viscosity MuGas(Pressure p);
    double Z(Pressure p, Temperature t);
    PhaseSplit SplitAt(Composition composition, Pressure p, Temperature t);
    ValidityRange Validity { get; }
}

/// <summary>Inputs to a drive mechanism's end-of-tick pressure solve (SDD-003 §3.1).</summary>
public sealed record MaterialBalanceInput(
    Pressure StartPressure,
    ReservoirVolume Withdrawn,
    ReservoirVolume Injected,
    ReservoirVolume AquiferInflux);

/// <summary>
/// Design 02 §2.2 — a plugin, deliberately: recovery factor EMERGES from the
/// mechanism; adding EOR is adding an implementation, never editing a reservoir.
/// </summary>
public interface IDriveMechanism
{
    ContentId Id { get; }
    Pressure SolveEndPressure(MaterialBalanceInput input, IFluidPropertyModel fluid);
    /// <summary>Which injectant materials this drive accepts (SDD-005 §4.0b — no identity branches).</summary>
    IReadOnlyList<ContentId> AcceptedInjectants { get; }
}

/// <summary>Fetkovich-form aquifer (SDD-003 §3.3): an aquifer is a water compartment.</summary>
public interface IAquiferModel
{
    ReservoirVolume InfluxDuring(Pressure reservoirPressure, Duration duration);
}
