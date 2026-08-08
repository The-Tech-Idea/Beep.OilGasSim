// R8.3 / R8.4 — separation (SDD-006 §2, design 02 §4.2, R8 §2.6).
//
// SEPARATION EFFICIENCY IS NEVER 100%. Carry-over and carry-under are modelled,
// and an undersized vessel at high rate separates worse through the
// residence-time term. That is what makes vessel sizing a decision rather than a
// threshold, and it produces the authentic late-life problem: a separator sized
// for early oil rates handles late-life liquid volumes badly.
//
// The MODEL and the VESSEL are separate on purpose (SDD-006 §2). The fluid model
// answers "what phases exist at this (P,T)" — thermodynamics. This answers "what
// did this vessel actually manage to separate" — equipment. Swapping a
// fixed-efficiency implementation for a flash one must not change what a phase
// IS, only how well the vessel achieved it.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Facilities;

/// <summary>
/// SDD-006 §2's fixed-efficiency split: the datasheet efficiencies applied to
/// the fluid model's ideal split.
///
/// <para>The alternative — a flash calculation — is a different plugin selected
/// by name from content, never an edit here (non-negotiable 11). It would
/// compute equilibrium directly and ignore these efficiencies entirely.</para>
/// </summary>
public sealed class FixedEfficiencySeparationModel : ISeparationModel
{
    public ContentId Id { get; } = new("fixed-efficiency-separation");

    public PhaseSplit SeparateAt(
        MaterialStream inlet, SeparationEfficiency efficiency, IFluidPropertyModel fluid)
    {
        ArgumentNullException.ThrowIfNull(fluid);

        Validate(efficiency);

        // The IDEAL split first — what thermodynamics says is there.
        PhaseSplit ideal = fluid.SplitAt(inlet.MassRates, inlet.P, inlet.T);

        var achieved =
            new List<(MaterialId Material, double GasFraction, double LiquidFraction, double AqueousFraction)>(
                ideal.Fractions.Count);

        for (int i = 0; i < ideal.Fractions.Count; i++)
        {
            (MaterialId material, double gas, double liquid, double aqueous) = ideal.Fractions[i];

            // Carry-over: liquid that leaves with the gas because the vessel did
            // not give it time to drop out. Carry-under: gas that leaves with the
            // liquid because it did not have time to break out.
            double liquidCarriedOver = liquid * efficiency.GasFromLiquid;
            double gasCarriedUnder = gas * efficiency.LiquidFromGas;

            double achievedGas = gas - gasCarriedUnder + liquidCarriedOver;
            double achievedLiquid = liquid - liquidCarriedOver + gasCarriedUnder;

            // Three-phase: some of the water in the liquid leg is knocked out.
            // A two-phase vessel passes 0 here and its aqueous stays where the
            // fluid model put it.
            double knockedOut = achievedLiquid * efficiency.WaterFromLiquid;

            achieved.Add((material,
                          achievedGas,
                          achievedLiquid - knockedOut,
                          aqueous + knockedOut));
        }

        return new PhaseSplit(achieved);
    }

    private static void Validate(SeparationEfficiency efficiency)
    {
        Check(efficiency.LiquidFromGas, nameof(efficiency.LiquidFromGas));
        Check(efficiency.GasFromLiquid, nameof(efficiency.GasFromLiquid));
        Check(efficiency.WaterFromLiquid, nameof(efficiency.WaterFromLiquid));

        static void Check(double value, string name)
        {
            if (value is >= 0.0 and <= 1.0) return;

            throw new ModelFault("SDD-006 §2", null,
                $"separation efficiency {name} is " +
                value.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                ", not a fraction in [0, 1]");
        }
    }
}

/// <summary>
/// A separator vessel's datasheet. Everything specifiable about it lives here
/// rather than in a type — there is no <c>ISeparator</c>, and design 02 §4.1
/// says there is no facility-type hierarchy in code at all.
/// </summary>
public sealed record SeparatorTier(
    ContentId Id,
    MassRate GasCapacity,               // the gas leg's throughput limit
    MassRate LiquidCapacity,            // the liquid leg's, independently
    ReservoirVolume Volume,             // vessel volume, for residence time
    SeparationEfficiency RatedEfficiency,   // achieved at or below the design rate
    ReservoirRate DesignRate,           // where the rated efficiency holds

    /// <summary>
    /// SDD-006 §1. What the back-pressure controller holds the vessel at — and
    /// therefore what the vessel IMPOSES on the network upstream, rather than
    /// anything it reads from it (finding 157).
    ///
    /// <para>It does two things and they are the same thing: it is the pressure
    /// the flash is taken at, so gas breaks out of oil that was at reservoir
    /// pressure a moment earlier; and it is the discharge pressure the upstream
    /// element sees, which is how a separator reaches the reservoir at all. A
    /// vessel without one separates nothing and holds nothing back.</para>
    /// </summary>
    Pressure OperatingPressure);

/// <summary>
/// A separation vessel as an <see cref="IFlowElement"/>.
///
/// <para>DUAL CAPACITY (R8-V2): the gas and liquid legs bind INDEPENDENTLY. A
/// vessel can be gas-limited on a high-GOR field and liquid-limited on the same
/// field ten years later, and reporting one number would make the bottleneck
/// report unable to say which — the whole content of §8's attribution.</para>
/// </summary>
public sealed class Separator : IFlowElement
{
    private readonly SeparatorTier _tier;
    private readonly ISeparationModel _model;
    private readonly IFluidPropertyModel _fluid;
    private readonly int _materialCount;

    public Separator(
        EntityId<IFlowElement> id,
        SeparatorTier tier,
        ISeparationModel model,
        IFluidPropertyModel fluid,
        int materialCount)
    {
        ArgumentNullException.ThrowIfNull(tier);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(fluid);

        if (tier.DesignRate.CubicMetresPerSecond <= 0.0)
            throw new ModelFault("SDD-006 §2", null,
                $"separator tier {tier.Id.Value} declares a non-positive design rate; " +
                "the residence-time term divides by it");

        // A vessel at zero absolute would flash against a vacuum and impose
        // nothing on the network — the two failures finding 157 describes,
        // reachable again through content rather than through a missing field.
        if (tier.OperatingPressure.Pascals <= 0.0)
            throw new ModelFault("SDD-006 §1", null,
                $"separator tier {tier.Id.Value} declares a non-positive operating pressure; " +
                "the vessel is held at a pressure by its controller and imposes it upstream");

        Id = id;
        _tier = tier;
        _model = model;
        _fluid = fluid;
        _materialCount = materialCount;
    }

    public EntityId<IFlowElement> Id { get; }

    /// <summary>Inlet, then gas and liquid outlets. Three-phase adds water.</summary>
    public IReadOnlyList<PortSpec> Ports { get; } =
    [
        new PortSpec(new PortId(0), PortDirection.Inlet, PortRole.Main),
        new PortSpec(new PortId(1), PortDirection.Outlet, PortRole.Gas),
        new PortSpec(new PortId(2), PortDirection.Outlet, PortRole.Liquid),
        new PortSpec(new PortId(3), PortDirection.Outlet, PortRole.Water),
    ];

    public TransformResult Transform(TransformInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Inlets.Count == 0) return Empty(input);

        // FLASHED AT THE VESSEL'S PRESSURE, not the inlet's (SDD-006 §1). The
        // stream arriving from a completion is stamped with RESERVOIR pressure,
        // and splitting it there asks what phases exist two thousand metres down
        // — where the answer is "one" and nothing separates. Dropping to P_sep
        // first is the whole of what a separator does.
        MaterialStream inlet = AtVesselPressure(input.Inlets[0]);
        SeparationEfficiency efficiency = EfficiencyAt(inlet);

        PhaseSplit split = _model.SeparateAt(inlet, efficiency, _fluid);

        var gas = new double[_materialCount];
        var liquid = new double[_materialCount];
        var water = new double[_materialCount];

        for (int i = 0; i < split.Fractions.Count; i++)
        {
            (MaterialId material, double g, double l, double a) = split.Fractions[i];
            double total = inlet.MassRates[material].KgPerSecond;

            // The three legs are computed as SHARES of the inlet and the last is
            // the REMAINDER, so they sum to the inlet exactly. Computing all
            // three independently would leave a rounding residue, and INV1 is
            // checked after every transform with no tolerance for one.
            double sum = g + l + a;
            if (sum <= 0.0) continue;

            gas[material.Ordinal] = total * (g / sum);
            liquid[material.Ordinal] = total * (l / sum);
            water[material.Ordinal] = total - gas[material.Ordinal] - liquid[material.Ordinal];
        }

        // The legs leave at the vessel's operating pressure — a separator is a
        // pressure step down, which is exactly what makes multi-stage worth
        // building (R8-V4).
        return new TransformResult(
            [
                Leg(inlet, gas),
                Leg(inlet, liquid),
                Leg(inlet, water),
            ],
            Sourced: Composition.Zero(_materialCount),
            FuelConsumed: Composition.Zero(_materialCount),
            Disposed: NoDisposal,
            PowerDraw: new Power(0.0));
    }

    /// <summary>
    /// R8-V3. Rated efficiency holds at or below the design rate; above it,
    /// residence time falls as the reciprocal of throughput and the vessel
    /// separates worse.
    ///
    /// <para>Degradation applies to the SHORTFALL from perfect, not to the
    /// efficiency itself: a vessel at twice its design rate carries over twice
    /// as much as one at design, which is the physical statement. Scaling the
    /// efficiency directly would instead make a perfect vessel stay perfect at
    /// any rate.</para>
    /// </summary>
    public SeparationEfficiency EfficiencyAt(MaterialStream inlet)
    {
        double rate = VolumetricRateOf(inlet);
        double design = _tier.DesignRate.CubicMetresPerSecond;

        if (rate <= design) return _tier.RatedEfficiency;

        double overload = rate / design;

        return new SeparationEfficiency(
            LiquidFromGas: Math.Min(1.0, _tier.RatedEfficiency.LiquidFromGas * overload),
            GasFromLiquid: Math.Min(1.0, _tier.RatedEfficiency.GasFromLiquid * overload),
            WaterFromLiquid: _tier.RatedEfficiency.WaterFromLiquid / overload);
    }

    /// <summary>
    /// R8-V2. The two legs are reported SEPARATELY, so the bottleneck report can
    /// say which one bound — "gas-limited" and "liquid-limited" call for
    /// different equipment and different money.
    /// </summary>
    public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Inlets.Count == 0) return [];

        MaterialStream inlet = AtVesselPressure(input.Inlets[0]);
        SeparationEfficiency efficiency = EfficiencyAt(inlet);
        PhaseSplit split = _model.SeparateAt(inlet, efficiency, _fluid);

        double gasLoad = 0.0, liquidLoad = 0.0;

        for (int i = 0; i < split.Fractions.Count; i++)
        {
            (MaterialId material, double g, double l, double a) = split.Fractions[i];
            double total = inlet.MassRates[material].KgPerSecond;
            double sum = g + l + a;
            if (sum <= 0.0) continue;

            gasLoad += total * (g / sum);
            liquidLoad += total * ((l + a) / sum);
        }

        return
        [
            new ConstraintEvaluation(
                ConstraintKind.GasCapacity, _tier.GasCapacity.KgPerSecond, gasLoad),
            new ConstraintEvaluation(
                ConstraintKind.LiquidCapacity, _tier.LiquidCapacity.KgPerSecond, liquidLoad),
        ];
    }

    /// <summary>
    /// The inlet as the vessel sees it: same mass, same temperature, same
    /// provenance, at P_sep.
    ///
    /// <para>Used for the flash and for the constraint evaluation, so the
    /// volumetric rates the two capacities are measured against are the ones
    /// inside the vessel rather than the ones in the pipe upstream of it.</para>
    /// </summary>
    private MaterialStream AtVesselPressure(MaterialStream inlet) =>
        inlet with { P = _tier.OperatingPressure };

    /// <summary>
    /// Every leg leaves at P_sep (SDD-006 §1). That is what the solver reads as
    /// the vessel's pressure drop, and therefore what reaches the wellhead — a
    /// leg stamped with the inlet's own pressure would make the vessel invisible
    /// to the network it is supposed to hold back (finding 157).
    /// </summary>
    private MaterialStream Leg(MaterialStream inlet, double[] byOrdinal) =>
        new(Composition.Validated([.. byOrdinal]),
            _tier.OperatingPressure, inlet.T, inlet.Provenance);

    private TransformResult Empty(TransformInput input)
    {
        MaterialStream nothing = new(
            Composition.Zero(_materialCount),
            new Pressure(0.0), input.Segment.Ambient,
            Allocation.FromSingle(new EntityRef(EntityKind.Compartment, 1)));

        return new TransformResult(
            [nothing, nothing, nothing],
            Composition.Zero(_materialCount), Composition.Zero(_materialCount),
            NoDisposal, new Power(0.0));
    }

    /// <summary>Volumetric throughput for the residence-time term. Reservoir
    /// conditions, since that is what occupies the vessel.</summary>
    private static double VolumetricRateOf(MaterialStream stream) =>
        stream.MassRates.Total.KgPerSecond / ApproximateDensityKgPerM3;

    /// <summary>SDD-006 §2 states the residence-time term against volumetric
    /// throughput and pins no mixture density for it. Open item S006-1.</summary>
    private const double ApproximateDensityKgPerM3 = 800.0;

    private DisposedMass NoDisposal => new(
        Composition.Zero(_materialCount),
        Composition.Zero(_materialCount),
        Composition.Zero(_materialCount));
}
