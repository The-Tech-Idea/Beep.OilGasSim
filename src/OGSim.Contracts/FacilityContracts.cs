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
