// Design 02 §4 — a facility is a container and a cost centre, NEVER a process.
// All physics lives in units, each an IFlowElement. There is no facility-type
// hierarchy in code at all (02 §4.1): "gas plant" is a template id in content.

using OGSim.Kernel;

namespace OGSim.Contracts;

public interface IFacility
{
    EntityId<IFacility> Id { get; }
    Coordinate Site { get; }
    IReadOnlyList<EntityId<IFacility>> Children { get; }        // recursive (PPDM)
    IReadOnlyList<EntityId<IFlowElement>> Units { get; }
}

/// <summary>One limit a stream must satisfy at a point (design 02 §4.3) — pinned proxies per SDD-006 §7.</summary>
public enum SpecProperty
{
    BasicSedimentAndWater, H2SFraction, Co2Fraction, WaterInGasFraction,
    LightEndsFraction, HeatingValueMin, HeatingValueMax
}

public sealed record SpecLimit(SpecProperty Property, double Limit);

/// <summary>A stream that fails a spec DOES NOT PASS — it routes to the Reject port (design 02 §4.3).</summary>
public sealed record Specification(IReadOnlyList<SpecLimit> Limits);

/// <summary>
/// The metered, contractual revenue event — the ONLY place revenue originates
/// (research §8; SDD-009 §1 architecture test R13-V2).
/// </summary>
public interface ICustodyTransferPoint : IFlowElement
{
    Specification Spec { get; }
}

/// <summary>SDD-006 §6 — capacity is never configured: it emerges from these
/// geometry facts through the hydraulics for the fluid actually flowing.</summary>
public interface IPipeline : IFlowElement
{
    Length PipeLength { get; }
    Length InnerDiameter { get; }
    Pressure Rating { get; }
    ContentId PipeSpec { get; }
}

/// <summary>Design 13 §3.3, SDD-006 §3b — merit-ordered supply against declared duty at stage 4.</summary>
public interface IPowerSource
{
    Power MaxSupply { get; }
    int MeritRank { get; }
}

// ------------------------------------------------- the two replaceable slots
// SDD-006 §0. Both are 03 §3.2 plug-and-play slots that the eight contract
// passes missed while recording that none remained (finding 82).

/// <summary>Datasheet recovery efficiencies; 2-phase vessels pass 0 for water.</summary>
public readonly record struct SeparationEfficiency(
    double LiquidFromGas,
    double GasFromLiquid,
    double WaterFromLiquid);

/// <summary>
/// Design 03 §3.2 — fixed-efficiency split ↔ flash calculation.
///
/// Deliberately NOT <see cref="IFluidPropertyModel.SplitAt"/>: that answers
/// "which phases exist at this (P,T)", which is thermodynamics and belongs to
/// the fluid; this answers "what did this vessel actually recover", which is
/// equipment. A fixed-efficiency implementation applies the datasheet numbers to
/// the fluid's ideal split; a flash implementation computes equilibrium directly
/// and ignores them. Swapping between them must never change what a phase IS.
/// </summary>
public interface ISeparationModel
{
    PhaseSplit SeparateAt(MaterialStream inlet, SeparationEfficiency efficiency,
                          IFluidPropertyModel fluid);
}

/// <summary>Geometry a pipe segment's drop is computed from (SDD-006 §6).</summary>
public readonly record struct PipeGeometry(
    Length PipeLength,
    Length InnerDiameter,
    double Roughness,
    Length ElevationRise);

/// <summary>
/// Design 03 §3.2 — Darcy-Weisbach ↔ Panhandle ↔ simplified. Capacity is never
/// configured: it emerges from geometry and the fluid actually flowing, which is
/// why the geometry is an argument rather than a rating.
/// </summary>
public interface IHydraulicModel
{
    Pressure DropAlong(MaterialStream stream, PipeGeometry geometry, IFluidPropertyModel fluid);
}
