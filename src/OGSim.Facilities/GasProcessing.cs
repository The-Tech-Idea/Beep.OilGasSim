// R9 — gas processing (SDD-006 §3c, §4, design 04, R9).
//
// EACH TREATING STEP IS AN INDEPENDENT UNIT. There is no "gas plant": sweet dry
// gas needs compression only, sour wet gas needs everything, and the player pays
// for exactly the chain their gas requires (R9 §2.1). That difference is what
// makes gas quality a property of an asset rather than a label.
//
// The flare is the most important element in this file, and not for its own
// sake. When flaring is capped and the gas cannot be sold or re-injected, the
// network throttles UPSTREAM — which limits oil, because the oil carries the
// gas. R9-V8. It needs no special handling anywhere: the flare is an element
// with a capacity, and SDD-002 S3 does the rest.

using System.Collections.Immutable;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Facilities;

/// <summary>SDD-006 §3c's datasheet.</summary>
public sealed record CompressorTier(
    ContentId Id,
    MassRate RatedCapacity,
    double MaxStageRatio,           // r_max, default 3.5
    double PolytropicExponent,      // n, default 1.25
    double PolytropicEfficiency,    // η
    double MolarMassKgPerMol,       // MW of the gas
    double DerateFractionPerKelvin, // k_derate, 13 §3.3
    Temperature DerateReference);   // T_ref

/// <summary>
/// SDD-006 §3c. Staged polytropic compression with interstage cooling.
///
/// <para>Stages take the EQUAL ratio, which minimises total power for a fixed
/// overall ratio — the reason real trains are balanced rather than front-loaded.
/// Interstage cooling returns each stage to suction temperature; without
/// aftercoolers stage two would start hot and the train would run away, so they
/// are part of the tier rather than an option.</para>
/// </summary>
public sealed class Compressor : IFlowElement
{
    private readonly CompressorTier _tier;
    private readonly Pressure _suction;
    private readonly Pressure _discharge;
    private readonly double _compressibility;   // Z̄
    private readonly int _materialCount;

    public Compressor(
        EntityId<IFlowElement> id,
        CompressorTier tier,
        Pressure suction,
        Pressure discharge,
        double averageCompressibility,
        int materialCount)
    {
        ArgumentNullException.ThrowIfNull(tier);
        Validate(tier, suction, discharge, averageCompressibility);

        Id = id;
        _tier = tier;
        _suction = suction;
        _discharge = discharge;
        _compressibility = averageCompressibility;
        _materialCount = materialCount;
    }

    public EntityId<IFlowElement> Id { get; }

    public IReadOnlyList<PortSpec> Ports { get; } =
    [
        new PortSpec(new PortId(0), PortDirection.Inlet, PortRole.Main),
        new PortSpec(new PortId(1), PortDirection.Outlet, PortRole.Main),
    ];

    /// <summary>
    /// <c>N = ceil(ln(P2/P1) / ln(r_max))</c> — R9-V2's subject. As field
    /// pressure falls the suction falls with it, the overall ratio climbs, and
    /// at some point the train needs another stage. Nothing schedules that; it
    /// is arithmetic on a ratio that was always going to grow.
    /// </summary>
    public int Stages
    {
        get
        {
            double ratio = _discharge.Pascals / _suction.Pascals;
            if (ratio <= 1.0) return 1;

            double exact = DetMath.Ln(ratio) / DetMath.Ln(_tier.MaxStageRatio);
            return Math.Max(1, (int)Math.Ceiling(exact - CeilingTolerance));
        }
    }

    /// <summary>The equal ratio each stage takes.</summary>
    public double StageRatio => DetMath.Pow(_discharge.Pascals / _suction.Pascals, 1.0 / Stages);

    /// <summary>Discharge temperature of one stage, K. The reason r_max exists.</summary>
    public Temperature StageDischargeTemperature(Temperature suctionTemperature) =>
        new(suctionTemperature.Kelvin * DetMath.Pow(StageRatio, Exponent));

    /// <summary>Specific work of one stage, J/kg (SDD-006 §3c).</summary>
    public double StageWorkJoulesPerKg(Temperature suctionTemperature)
    {
        double n = _tier.PolytropicExponent;

        return _compressibility * PhysicalConstants.GasConstantJPerMolK
             * suctionTemperature.Kelvin / _tier.MolarMassKgPerMol
             * (n / (n - 1.0))
             * (DetMath.Pow(StageRatio, Exponent) - 1.0);
    }

    /// <summary>Shaft power for the whole train, W. What stage 4's balance
    /// consumes, exactly as it consumes an ESP's.</summary>
    public Power ShaftPowerFor(MassRate throughput, Temperature suctionTemperature) =>
        new(throughput.KgPerSecond * Stages * StageWorkJoulesPerKg(suctionTemperature)
            / _tier.PolytropicEfficiency);

    /// <summary>
    /// SDD-006 §3c / R9 §2.6. Capacity falls with ambient temperature.
    ///
    /// <para>Seasonal and non-obvious: a desert field loses gas-handling capacity
    /// in exactly the hottest months, and because the flaring cap makes gas
    /// handling limit OIL, its oil rate falls in summer for a reason nowhere
    /// near the reservoir. It is the clearest demonstration in the game that the
    /// setting is a live input rather than a cost multiplier applied once.</para>
    /// </summary>
    public MassRate CapacityAt(Temperature ambient)
    {
        double over = ambient.Kelvin - _tier.DerateReference.Kelvin;
        if (over <= 0.0) return _tier.RatedCapacity;

        double factor = Math.Max(0.0, 1.0 - _tier.DerateFractionPerKelvin * over);
        return new MassRate(_tier.RatedCapacity.KgPerSecond * factor);
    }

    public TransformResult Transform(TransformInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        MaterialStream inlet = input.Inlets.Count > 0
            ? input.Inlets[0]
            : new MaterialStream(Composition.Zero(_materialCount), _suction,
                                 input.Segment.Ambient,
                                 Allocation.FromSingle(new EntityRef(EntityKind.Compartment, 1)));

        var throughput = new MassRate(inlet.MassRates.Total.KgPerSecond);

        // Mass passes through untouched — a compressor raises pressure, it does
        // not consume the gas. Its fuel, if it is gas-driven, is a separate
        // fixed sink at stage 4 (SDD-006 §3), never a silent bite out of this
        // stream.
        return new TransformResult(
            [inlet with { P = _discharge, T = inlet.T }],
            Sourced: Composition.Zero(_materialCount),
            FuelConsumed: Composition.Zero(_materialCount),
            Disposed: NoDisposal(_materialCount),
            PowerDraw: ShaftPowerFor(throughput, inlet.T));
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
                ConstraintKind.TotalCapacity,
                CapacityAt(input.Segment.Ambient).KgPerSecond,
                load),
        ];
    }

    private double Exponent => (_tier.PolytropicExponent - 1.0) / _tier.PolytropicExponent;

    /// <summary>Guards the ceiling against a ratio that lands a hair above an
    /// exact power of r_max and would otherwise buy a whole extra stage.</summary>
    private const double CeilingTolerance = 1e-9;

    private static void Validate(
        CompressorTier tier, Pressure suction, Pressure discharge, double z)
    {
        if (suction.Pascals <= 0.0)
            throw new ModelFault("SDD-006 §3c", null, "suction pressure must be positive");

        if (discharge.Pascals < suction.Pascals)
            throw new ModelFault("SDD-006 §3c", null,
                $"discharge {Format(discharge.Pascals)} Pa is below suction " +
                $"{Format(suction.Pascals)} Pa; a compressor raises pressure");

        if (tier.MaxStageRatio <= 1.0)
            throw new ModelFault("SDD-006 §3c", null,
                $"max stage ratio {Format(tier.MaxStageRatio)} must exceed 1; " +
                "the stage count divides by its logarithm");

        if (tier.PolytropicExponent <= 1.0)
            throw new ModelFault("SDD-006 §3c", null,
                $"polytropic exponent {Format(tier.PolytropicExponent)} must exceed 1");

        if (tier.PolytropicEfficiency is <= 0.0 or > 1.0)
            throw new ModelFault("SDD-006 §3c", null,
                $"polytropic efficiency {Format(tier.PolytropicEfficiency)} is not in (0, 1]");

        if (tier.MolarMassKgPerMol <= 0.0)
            throw new ModelFault("SDD-006 §3c", null, "molar mass must be positive");

        if (z <= 0.0)
            throw new ModelFault("SDD-006 §3c", null, "compressibility must be positive");
    }

    internal static DisposedMass NoDisposal(int materialCount) => new(
        Composition.Zero(materialCount),
        Composition.Zero(materialCount),
        Composition.Zero(materialCount));

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// The flare — where refused mass goes, and the element that makes an
/// environmental rule a PHYSICAL PRODUCTION CONSTRAINT (R9 §2.2, R9-V8).
///
/// <para>Its transform consumes its inlet entirely: mass leaves the network as
/// the <c>Flared</c> conservation term, and R16 prices the emissions. Its
/// constraint is the flaring cap — and when that binds with no other gas outlet,
/// S3 throttles the completions upstream, which limits OIL because the oil
/// carries the gas.</para>
///
/// <para>No special handling anywhere. The flare is an element with a capacity;
/// the solver already knew what to do with one.</para>
/// </summary>
public sealed class Flare : IFlowElement
{
    private readonly MassRate _capacity;
    private readonly double _combustionEfficiency;
    private readonly int _materialCount;

    public Flare(
        EntityId<IFlowElement> id,
        MassRate capacity,
        double combustionEfficiency,
        int materialCount)
    {
        if (capacity.KgPerSecond < 0.0)
            throw new ModelFault("SDD-006 §3", null, "flare capacity cannot be negative");

        if (combustionEfficiency is < 0.0 or > 1.0)
            throw new ModelFault("SDD-006 §3", null,
                $"combustion efficiency " +
                combustionEfficiency.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                " is not a fraction in [0, 1]");

        Id = id;
        _capacity = capacity;
        _combustionEfficiency = combustionEfficiency;
        _materialCount = materialCount;
    }

    public EntityId<IFlowElement> Id { get; }

    /// <summary>The one port, named so a caller wiring the chain never writes a
    /// bare index (see <see cref="Separator.Inlet"/>).</summary>
    public static PortId Inlet { get; } = new(0);

    /// <summary>Inlet only. A flare is a TERMINAL sink: what enters leaves the
    /// network as Disposed, combusted or not, and there is nothing downstream of
    /// it to connect.</summary>
    public IReadOnlyList<PortSpec> Ports { get; } =
        [new PortSpec(new PortId(0), PortDirection.Inlet, PortRole.Main)];

    public TransformResult Transform(TransformInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        Composition arriving = input.Inlets.Count > 0
            ? input.Inlets[0].MassRates
            : Composition.Zero(_materialCount);

        // Combusted and unburnt are BOTH accounted for and split by efficiency:
        // the unburnt fraction is vented methane, which R16 prices very
        // differently from CO2. Reporting it all as Flared would understate the
        // emissions of a poorly-performing flare by an order of magnitude in
        // warming terms.
        (Composition combusted, Composition unburnt) = arriving.Split(_combustionEfficiency);

        return new TransformResult(
            [],
            Sourced: Composition.Zero(_materialCount),
            FuelConsumed: Composition.Zero(_materialCount),
            Disposed: new DisposedMass(
                Flared: combusted,
                Vented: unburnt,
                Discharged: Composition.Zero(_materialCount)),
            PowerDraw: new Power(0.0));
    }

    /// <summary>The flaring cap. R9-V8's whole mechanism.</summary>
    public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        double load = input.Inlets.Count > 0
            ? input.Inlets[0].MassRates.Total.KgPerSecond
            : 0.0;

        return [new ConstraintEvaluation(ConstraintKind.TotalCapacity, _capacity.KgPerSecond, load)];
    }
}

/// <summary>
/// SDD-006 §4's NGL plant — the ONE place components exist (FD2).
///
/// <para>It takes a gas stream and the fluid system's declared component split,
/// applies the tier's per-component recoveries, and emits the recovered liquids
/// as an ordinary black-oil stream. Nothing upstream or downstream gains a
/// component field, which is what keeps the boundary from leaking.</para>
/// </summary>
public sealed class NglExtractionPlant : IFlowElement
{
    private readonly ComponentSplit _feedSplit;
    private readonly NglRecovery _recovery;
    private readonly int _liquidOrdinal;
    private readonly int _gasOrdinal;
    private readonly int _materialCount;

    public NglExtractionPlant(
        EntityId<IFlowElement> id,
        ComponentSplit feedSplit,
        NglRecovery recovery,
        int gasOrdinal,
        int liquidOrdinal,
        int materialCount)
    {
        ArgumentNullException.ThrowIfNull(feedSplit);
        ArgumentNullException.ThrowIfNull(recovery);

        Id = id;
        _feedSplit = feedSplit;
        _recovery = recovery;
        _gasOrdinal = gasOrdinal;
        _liquidOrdinal = liquidOrdinal;
        _materialCount = materialCount;
    }

    public EntityId<IFlowElement> Id { get; }

    /// <summary>Inlet, residue gas, and the NGL product.</summary>
    public IReadOnlyList<PortSpec> Ports { get; } =
    [
        new PortSpec(new PortId(0), PortDirection.Inlet, PortRole.Main),
        new PortSpec(new PortId(1), PortDirection.Outlet, PortRole.Gas),
        new PortSpec(new PortId(2), PortDirection.Outlet, PortRole.Liquid),
    ];

    /// <summary>
    /// The recovered mass fraction of the feed gas — the weighted sum over
    /// components. Exposed because it is the number the plant is judged on and
    /// R9-V5 checks it against the declared efficiencies directly.
    /// </summary>
    public double RecoveredFraction
    {
        get
        {
            double recovered = 0.0;
            for (int i = 0; i < ComponentSplit.ComponentCount; i++)
                recovered += _feedSplit.MassFractionByComponent[i]
                           * _recovery.FractionByComponent[i];
            return recovered;
        }
    }

    public TransformResult Transform(TransformInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Inlets.Count == 0)
        {
            MaterialStream nothing = new(
                Composition.Zero(_materialCount), new Pressure(0.0), input.Segment.Ambient,
                Allocation.FromSingle(new EntityRef(EntityKind.Compartment, 1)));

            return new TransformResult(
                [nothing, nothing],
                Composition.Zero(_materialCount), Composition.Zero(_materialCount),
                Compressor.NoDisposal(_materialCount), new Power(0.0));
        }

        MaterialStream inlet = input.Inlets[0];
        double feedGas = inlet.MassRates[new MaterialId(_gasOrdinal)].KgPerSecond;

        double recovered = feedGas * RecoveredFraction;

        // The residue is the REMAINDER, not an independently computed number, so
        // gas in equals residue plus NGL to the bit. The element-level INV1
        // check has no tolerance for a rounding residue here.
        var residue = new double[_materialCount];
        var product = new double[_materialCount];

        for (int m = 0; m < _materialCount; m++) residue[m] = inlet.MassRates[new MaterialId(m)].KgPerSecond;

        residue[_gasOrdinal] = feedGas - recovered;
        product[_liquidOrdinal] = recovered;

        // Everything that is not gas leaves with the residue stream untouched —
        // an NGL plant takes gas, and a liquid carried into it is not its
        // business to fractionate.
        return new TransformResult(
            [
                inlet with { MassRates = Composition.Validated([.. residue]) },
                inlet with { MassRates = Composition.Validated([.. product]) },
            ],
            Sourced: Composition.Zero(_materialCount),
            FuelConsumed: Composition.Zero(_materialCount),
            Disposed: Compressor.NoDisposal(_materialCount),
            PowerDraw: new Power(0.0));
    }

    public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input) => [];
}

/// <summary>
/// Dehydration and sweetening: a removal step with a declared efficiency,
/// routing what it removes to a named outlet.
///
/// <para>ONE class for both because SDD-006 §4 gives them the same form — a
/// fraction removed, per the tier — and the difference between a glycol
/// contactor and an amine unit is entirely which material it acts on and what
/// the removed stream is worth. Two classes differing only in a field name would
/// be an equipment hierarchy in code (design 02 §4.1).</para>
/// </summary>
public sealed class RemovalUnit : IFlowElement
{
    private readonly int _targetOrdinal;
    private readonly double _removalEfficiency;
    private readonly double _byProductYield;
    private readonly int _byProductOrdinal;
    private readonly int _materialCount;

    public RemovalUnit(
        EntityId<IFlowElement> id,
        ContentId tier,
        int targetOrdinal,
        double removalEfficiency,
        int byProductOrdinal,
        double byProductYield,
        int materialCount)
    {
        if (removalEfficiency is < 0.0 or > 1.0)
            throw new ModelFault("SDD-006 §4", null,
                $"removal efficiency for tier {tier.Value} is " +
                removalEfficiency.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                ", not a fraction in [0, 1]");

        if (byProductYield < 0.0)
            throw new ModelFault("SDD-006 §4", null,
                $"by-product yield for tier {tier.Value} is negative");

        Id = id;
        Tier = tier;
        _targetOrdinal = targetOrdinal;
        _removalEfficiency = removalEfficiency;
        _byProductOrdinal = byProductOrdinal;
        _byProductYield = byProductYield;
        _materialCount = materialCount;
    }

    public EntityId<IFlowElement> Id { get; }

    public ContentId Tier { get; }

    /// <summary>Inlet, treated outlet, and the removed material's own outlet —
    /// water to disposal, acid gas to the sulphur unit or the flare.</summary>
    public IReadOnlyList<PortSpec> Ports { get; } =
    [
        new PortSpec(new PortId(0), PortDirection.Inlet, PortRole.Main),
        new PortSpec(new PortId(1), PortDirection.Outlet, PortRole.Main),
        new PortSpec(new PortId(2), PortDirection.Outlet, PortRole.Reject),
    ];

    public TransformResult Transform(TransformInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Inlets.Count == 0)
        {
            MaterialStream nothing = new(
                Composition.Zero(_materialCount), new Pressure(0.0), input.Segment.Ambient,
                Allocation.FromSingle(new EntityRef(EntityKind.Compartment, 1)));

            return new TransformResult(
                [nothing, nothing],
                Composition.Zero(_materialCount), Composition.Zero(_materialCount),
                Compressor.NoDisposal(_materialCount), new Power(0.0));
        }

        MaterialStream inlet = input.Inlets[0];
        double present = inlet.MassRates[new MaterialId(_targetOrdinal)].KgPerSecond;
        double removed = present * _removalEfficiency;

        var treated = new double[_materialCount];
        var rejected = new double[_materialCount];

        for (int m = 0; m < _materialCount; m++)
            treated[m] = inlet.MassRates[new MaterialId(m)].KgPerSecond;

        // Remainder again: treated + rejected == inlet exactly.
        treated[_targetOrdinal] = present - removed;
        rejected[_targetOrdinal] = removed;

        // R9 §2.4 / R9-V4: sulphur is a BY-PRODUCT, produced in proportion to
        // what was removed. It is a conversion within the reject stream, not new
        // mass — an amine unit that created sulphur out of nothing would break
        // INV1 at its own element, which is where it should break.
        if (_byProductYield > 0.0 && removed > 0.0)
        {
            double sulphur = removed * _byProductYield;
            rejected[_byProductOrdinal] += sulphur;
            rejected[_targetOrdinal] -= sulphur;

            if (rejected[_targetOrdinal] < 0.0)
                throw new ModelFault("SDD-006 §4", null,
                    $"tier {Tier.Value} declares a by-product yield above 1: it would " +
                    "produce more sulphur than there was acid gas to make it from");
        }

        return new TransformResult(
            [
                inlet with { MassRates = Composition.Validated([.. treated]) },
                inlet with { MassRates = Composition.Validated([.. rejected]) },
            ],
            Sourced: Composition.Zero(_materialCount),
            FuelConsumed: Composition.Zero(_materialCount),
            Disposed: Compressor.NoDisposal(_materialCount),
            PowerDraw: new Power(0.0));
    }

    public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input) => [];
}
