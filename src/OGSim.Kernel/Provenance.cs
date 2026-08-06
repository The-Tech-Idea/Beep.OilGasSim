// SDD-008 §2 — how a value is known, and in what space it is believed.
//
// These two live in the KERNEL rather than with the belief machinery that made
// them necessary, because R2's IProperty needs them and R2 runs eleven phases
// before R14. They are vocabulary, not belief state: nothing here knows what a
// belief is (R2.0 layering correction — see Materials.cs).

namespace OGSim.Kernel;

/// <summary>
/// Declared per property kind in content. Additive kinds (depth, net pay,
/// saturation) are Linear; multiplicative kinds (permeability, area, volumes)
/// are Log — and a Log-space Normal IS the log-normal the design requires
/// (05 §1.4), which is what makes every belief update the same conjugate rule.
/// </summary>
public enum BeliefSpace { Linear, Log }

/// <summary>
/// Design 02 §1.2 / R2 §2.2 — ORDERED BY CONFIDENCE, and the order is the
/// contract: it drives the default uncertainty, the SDD-008 §2.1 update
/// weighting, and the player-facing "how do we know this?".
///
/// ProductionHistory ranks near the top because the dynamic data is the most
/// trustworthy thing about a reservoir — which is what makes the p/Z deduction
/// of SDD-008 §6 as powerful as it is.
/// </summary>
public enum Provenance
{
    Assumed, Analogue, Seismic, Log, WellTest, Core, ProductionHistory, Measured
}
