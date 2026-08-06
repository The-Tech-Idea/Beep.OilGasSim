// SDD-002 §2–4 — the one currency. Data shapes only at this stage; the full
// Composition algebra (Plus/Scaled/Split, ascending-ordinal iteration) arrives
// with R2 per SDD-002, against these exact shapes.

using System.Collections.Immutable;

namespace OGSim.Kernel;

/// <summary>Catalogue-ordinal material index. Ordinals NEVER persist (SDD-004 §6).</summary>
public readonly record struct MaterialId(int Ordinal);

/// <summary>
/// Immutable mass-flow-per-material, kg/s, dense by material ordinal
/// (SDD-002 §2). Values are never negative.
/// </summary>
public readonly record struct Composition(ImmutableArray<double> KgPerSecondByOrdinal);

/// <summary>
/// Provenance: sorted (compartment, fraction) pairs summing to 1 within 1e-12
/// (SDD-002 §3). Lift-gas recycle provenance uses the compression point's
/// prior-tick blend (SDD-002 §6).
/// </summary>
public readonly record struct Allocation(
    ImmutableArray<(EntityRef Compartment, double Fraction)> Shares);

/// <summary>
/// Material in motion (SDD-002 §4; renamed from `Stream` — the first rule-F-4
/// event: the pinned name collides with System.IO.Stream under implicit
/// usings). Deliberately NO cached phase split —
/// elements ask the fluid model at (composition, P, T).
/// </summary>
public readonly record struct MaterialStream(
    Composition MassRates,
    Pressure P,
    Temperature T,
    Allocation Provenance);
