// Design 02 §4 — a facility is a container and a cost centre, NEVER a process.
// All physics lives in units, each an IFlowElement. There is no facility-type
// hierarchy in code at all (02 §4.1): "gas plant" is a template id in content.

using System.Collections.Immutable;
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

/// <summary>
/// FD2's boundary, made a type so it is visible in the code and not only in
/// prose (SDD-006 §4).
///
/// <para><c>C1</c> is everything lighter than ethane — methane plus the inerts —
/// because nothing downstream separates them, and a component nobody recovers
/// does not need its own field.</para>
/// </summary>
public enum GasComponent { C1, C2, C3, C4, C5Plus }

/// <summary>
/// Mass fractions of a GAS stream, summing to 1.
///
/// <para>Declared by the fluid system and carried NOWHERE ELSE. A stream outside
/// the NGL plant has no component split, and asking for one is a design error
/// rather than a defaulted answer — which is why there is no member on
/// <c>MaterialStream</c> for the boundary to leak through.</para>
/// </summary>
public sealed record ComponentSplit(ImmutableArray<double> MassFractionByComponent)
{
    public const int ComponentCount = 5;

    public static ComponentSplit Validated(params double[] massFractions)
    {
        ArgumentNullException.ThrowIfNull(massFractions);

        if (massFractions.Length != ComponentCount)
            throw new InvariantFault("SDD-006 §4", null,
                $"a component split has {ComponentCount} entries; got {massFractions.Length}");

        double sum = 0.0;
        for (int i = 0; i < massFractions.Length; i++)
        {
            double fraction = massFractions[i];
            if (double.IsNaN(fraction) || fraction < 0.0 || fraction > 1.0)
                throw new InvariantFault("SDD-006 §4", null,
                    $"component {(GasComponent)i} fraction is " +
                    fraction.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            sum += fraction;
        }

        // A split that does not sum to 1 would silently create or destroy gas at
        // the one element where components exist, and the element-level INV1
        // check would then blame the plant for the content's arithmetic.
        if (Math.Abs(sum - 1.0) > 1e-9)
            throw new InvariantFault("SDD-006 §4", null,
                "component mass fractions sum to " +
                sum.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                ", not 1");

        return new ComponentSplit([.. massFractions]);
    }

    public double this[GasComponent component] => MassFractionByComponent[(int)component];
}

/// <summary>Per-component recovery, from the NGL plant's tier (SDD-006 §4).</summary>
public sealed record NglRecovery(ImmutableArray<double> FractionByComponent)
{
    public static NglRecovery Validated(params double[] fractions)
    {
        ArgumentNullException.ThrowIfNull(fractions);

        if (fractions.Length != ComponentSplit.ComponentCount)
            throw new InvariantFault("SDD-006 §4", null,
                $"a recovery set has {ComponentSplit.ComponentCount} entries; got {fractions.Length}");

        for (int i = 0; i < fractions.Length; i++)
            if (double.IsNaN(fractions[i]) || fractions[i] < 0.0 || fractions[i] > 1.0)
                throw new InvariantFault("SDD-006 §4", null,
                    $"recovery for {(GasComponent)i} is " +
                    fractions[i].ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                    ", not a fraction in [0, 1]");

        return new NglRecovery([.. fractions]);
    }

    public double this[GasComponent component] => FractionByComponent[(int)component];
}
