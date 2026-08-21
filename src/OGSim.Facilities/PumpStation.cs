// R11.2 — the liquid pump station (SDD-006 §3d, R11 §2.1's boosting element).
//
// A liquid is treated incompressible, exactly as SDD-003 §3.1 treats Bw
// everywhere in the material balance — so unlike §3c's compressor there is no
// staging, no interstage cooling and no Z-factor: one stage always suffices,
// and specific work is the pressure rise over the fluid's own density.
//
// NO HEAT DERATING (contrast the compressor). §2.6/13 §3.3's curve exists
// because a gas compressor's air-cooled driver and aftercoolers lose duty in
// the heat; nothing here does the same job for a pump motor, and inventing a
// curve nothing calibrates would be a fabricated number rather than a
// modelled one (SDD-006 §3d).

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Facilities;

/// <summary>
/// SDD-006 §3d's datasheet.
///
/// <para><c>Discharge</c> is on the tier for the reason <see
/// cref="Facilities.SeparatorTier.OperatingPressure"/> and <see
/// cref="Facilities.CompressorTier.Discharge"/> already are (R11.2's own
/// amendment, finding 259): a boosting element's set pressure is "the
/// player's decision, made when the station is bought," so it travels with
/// the rung rather than a free-form command parameter.</para>
/// </summary>
public sealed record PumpTier(
    ContentId Id,
    MassRate RatedCapacity,
    double Efficiency,             // η_pump
    Pressure Discharge);

/// <summary>
/// SDD-006 §3d. A boosting element that raises a liquid stream from where it
/// sits to a fixed discharge pressure — sizing that set point correctly is the
/// player's decision, made when the station is bought, not something this
/// class solves for.
/// </summary>
public sealed class LiquidPumpStation : IFlowElement
{
    private PumpTier _tier;
    private readonly Pressure _suction;
    private readonly Density _averageDensity;
    private readonly int _materialCount;

    public LiquidPumpStation(
        EntityId<IFlowElement> id,
        PumpTier tier,
        Pressure suction,
        Density averageDensity,
        int materialCount)
    {
        ArgumentNullException.ThrowIfNull(tier);
        Validate(tier, suction, averageDensity);

        Id = id;
        _tier = tier;
        _suction = suction;
        _averageDensity = averageDensity;
        _materialCount = materialCount;
    }

    public EntityId<IFlowElement> Id { get; }

    public static PortId Inlet { get; } = new(0);
    public static PortId Outlet { get; } = new(1);

    public IReadOnlyList<PortSpec> Ports { get; } =
    [
        new PortSpec(new PortId(0), PortDirection.Inlet, PortRole.Main),
        new PortSpec(new PortId(1), PortDirection.Outlet, PortRole.Main),
    ];

    /// <summary>The rung fitted now (SDD-006 §0c's refit).</summary>
    public PumpTier Tier => _tier;

    /// <summary>
    /// Fits a bigger train into the same socket (SDD-006 §0c, R11.2's own
    /// composition, finding 259) — the tier alone swaps; suction stays
    /// exactly what the station was built against.
    /// </summary>
    public void Fit(PumpTier tier)
    {
        ArgumentNullException.ThrowIfNull(tier);
        Validate(tier, _suction, _averageDensity);

        _tier = tier;
    }

    /// <summary>Specific work, J/kg (SDD-006 §3d): <c>w = (P2 − P1) / ρ̄</c>.</summary>
    public double SpecificWorkJoulesPerKg =>
        (_tier.Discharge.Pascals - _suction.Pascals) / _averageDensity.KgPerCubicMetre;

    /// <summary>Shaft power for the whole station, W. What stage 4's balance
    /// consumes, exactly as it consumes a compressor's or an ESP's.</summary>
    public Power ShaftPowerFor(MassRate throughput) =>
        new(throughput.KgPerSecond * SpecificWorkJoulesPerKg / _tier.Efficiency);

    public MassRate RatedCapacity => _tier.RatedCapacity;

    public TransformResult Transform(TransformInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        MaterialStream inlet = input.Inlets.Count > 0
            ? input.Inlets[0]
            : new MaterialStream(Composition.Zero(_materialCount), _suction,
                                 input.Segment.Ambient,
                                 Allocation.FromSingle(new EntityRef(EntityKind.Compartment, 1)));

        var throughput = new MassRate(inlet.MassRates.Total.KgPerSecond);

        // Mass passes through untouched — a pump raises pressure, it does not
        // consume the liquid.
        return new TransformResult(
            [inlet with { P = _tier.Discharge, T = inlet.T }],
            Sourced: Composition.Zero(_materialCount),
            FuelConsumed: Composition.Zero(_materialCount),
            Disposed: NoDisposal(_materialCount),
            PowerDraw: ShaftPowerFor(throughput));
    }

    public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        double load = input.Inlets.Count > 0
            ? input.Inlets[0].MassRates.Total.KgPerSecond
            : 0.0;

        return
        [
            new ConstraintEvaluation(
                ConstraintKind.TotalCapacity, _tier.RatedCapacity.KgPerSecond, load),
        ];
    }

    private static DisposedMass NoDisposal(int materialCount) =>
        new(Composition.Zero(materialCount), Composition.Zero(materialCount),
            Composition.Zero(materialCount));

    private static void Validate(PumpTier tier, Pressure suction, Density averageDensity)
    {
        if (suction.Pascals <= 0.0)
            throw new ModelFault("SDD-006 §3d", null, "suction pressure must be positive");

        if (tier.Discharge.Pascals < suction.Pascals)
            throw new ModelFault("SDD-006 §3d", null,
                $"discharge {Format(tier.Discharge.Pascals)} Pa is below suction " +
                $"{Format(suction.Pascals)} Pa; a pump raises pressure");

        if (tier.RatedCapacity.KgPerSecond <= 0.0)
            throw new ModelFault("SDD-006 §3d", null, "rated capacity must be positive");

        if (tier.Efficiency is <= 0.0 or > 1.0)
            throw new ModelFault("SDD-006 §3d", null,
                $"pump efficiency {Format(tier.Efficiency)} is not in (0, 1]");

        if (averageDensity.KgPerCubicMetre <= 0.0)
            throw new ModelFault("SDD-006 §3d", null, "average density must be positive");
    }

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}
