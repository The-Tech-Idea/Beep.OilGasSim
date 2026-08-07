// R11.1 / R11.3 — pipelines (SDD-006 §6, R11 §2.1–2.3).
//
// CAPACITY IS NEVER CONFIGURED. A pipeline declares geometry and a rating; its
// throughput is whatever the hydraulics permit for the fluid actually flowing at
// the actual inlet pressure. A configured maxRate would make pipelines inert
// numbers and would have made the emergent behaviour below impossible to express
// — which is why SDD-006 §6 says the field is deliberately absent.
//
// Two consequences the design wants and gets for free:
//
//   A line comfortable on dry oil throttles on its own as the water cut climbs,
//   because water is denser and the friction term goes as ρv².
//
//   A GAS line's capacity collapses as inlet pressure declines, because the
//   pressure-squared form makes throughput depend on P1² − P2² rather than on
//   P1 − P2. An export line sized at first gas is inadequate years later even at
//   a lower rate, and the remedy — compression — has to be anticipated.
//
// Linefill is real, owned inventory. It appears in the conservation check and it
// is what produces the authentic delay between a production change and its
// arrival at the terminal.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Facilities;

/// <summary>SDD-006 §6's liquid form: Darcy-Weisbach plus static head.</summary>
public sealed class LiquidHydraulicModel : IHydraulicModel
{
    private readonly Density _density;
    private readonly Viscosity _viscosity;
    private readonly Length _elevationRise;

    public LiquidHydraulicModel(Density density, Viscosity viscosity, Length elevationRise)
    {
        if (density.KgPerCubicMetre <= 0.0 || viscosity.PascalSeconds <= 0.0)
            throw new ModelFault("SDD-006 §6", null,
                "density and viscosity must be positive");

        _density = density;
        _viscosity = viscosity;
        _elevationRise = elevationRise;
    }

    /// <summary><c>ΔP = f·(L/D)·ρv²/2 + ρgΔz</c>.</summary>
    public Pressure DropAlong(
        MaterialStream stream, PipeGeometry geometry, IFluidPropertyModel fluid)
    {
        ArgumentNullException.ThrowIfNull(fluid);
        Validate(geometry);

        double massRate = stream.MassRates.Total.KgPerSecond;
        double staticHead = _density.KgPerCubicMetre * PhysicalConstants.GravityMPerS2
                          * _elevationRise.Metres;

        if (massRate <= 0.0) return new Pressure(staticHead);

        double d = geometry.InnerDiameter.Metres;
        double area = PhysicalConstants.Pi * d * d / 4.0;
        double velocity = massRate / (_density.KgPerCubicMetre * area);

        double reynolds = _density.KgPerCubicMetre * velocity * d / _viscosity.PascalSeconds;
        double f = Friction.Factor(Math.Max(reynolds, 1.0), geometry.Roughness / d);

        double friction = f * (geometry.PipeLength.Metres / d)
                        * _density.KgPerCubicMetre * velocity * velocity / 2.0;

        return new Pressure(friction + staticHead);
    }

    /// <summary>
    /// The rate this line will pass for a declared pressure difference — the
    /// inverse of <see cref="DropAlong"/>, by bisection because the friction
    /// factor has no closed form.
    ///
    /// <para>This is "capacity" in the only sense the design allows: an OUTCOME
    /// of geometry and fluid, computed on demand, never a stored field.</para>
    /// </summary>
    public MassRate CapacityFor(PipeGeometry geometry, Pressure available)
    {
        Validate(geometry);

        double staticHead = _density.KgPerCubicMetre * PhysicalConstants.GravityMPerS2
                          * _elevationRise.Metres;

        double forFriction = available.Pascals - staticHead;
        if (forFriction <= 0.0) return new MassRate(0.0);

        double low = 0.0, high = 1.0;

        // Grow until the drop exceeds what is available, so the answer is inside.
        for (int i = 0; i < BracketGrowths; i++)
        {
            if (DropFor(geometry, high) > forFriction) break;
            high *= 4.0;
        }

        for (int i = 0; i < BisectionIterations; i++)
        {
            double mid = low + (high - low) * 0.5;
            if (DropFor(geometry, mid) > forFriction) high = mid;
            else low = mid;
        }

        return new MassRate(low + (high - low) * 0.5);
    }

    private double DropFor(PipeGeometry geometry, double massRate)
    {
        if (massRate <= 0.0) return 0.0;

        double d = geometry.InnerDiameter.Metres;
        double area = PhysicalConstants.Pi * d * d / 4.0;
        double velocity = massRate / (_density.KgPerCubicMetre * area);

        double reynolds = _density.KgPerCubicMetre * velocity * d / _viscosity.PascalSeconds;
        double f = Friction.Factor(Math.Max(reynolds, 1.0), geometry.Roughness / d);

        return f * (geometry.PipeLength.Metres / d)
                 * _density.KgPerCubicMetre * velocity * velocity / 2.0;
    }

    private const int BisectionIterations = 80;
    private const int BracketGrowths = 20;

    internal static void Validate(PipeGeometry geometry)
    {
        if (geometry.InnerDiameter.Metres <= 0.0)
            throw new ModelFault("SDD-006 §6", null, "pipe inner diameter must be positive");

        if (geometry.PipeLength.Metres <= 0.0)
            throw new ModelFault("SDD-006 §6", null, "pipe length must be positive");

        if (geometry.Roughness < 0.0)
            throw new ModelFault("SDD-006 §6", null, "roughness cannot be negative");
    }
}

/// <summary>
/// SDD-006 §6's gas form, the pressure-squared relation:
/// <c>ṁ = sqrt[ (P1² − P2²)·π²·D⁵·MW / (16·f·Z̄·R·T̄·L) ]</c>.
///
/// <para><b>The squares are the point</b> (R11 §2.2). Throughput depends on
/// <c>P1² − P2²</c>, so halving the inlet pressure at a fixed outlet costs far
/// more than half the capacity. A line sized at first gas is inadequate years
/// later at a lower rate, and no amount of the line being "big enough" fixes it
/// — the remedy is compression, and it has to be anticipated.</para>
/// </summary>
public sealed class GasHydraulicModel
{
    private readonly double _molarMassKgPerMol;
    private readonly double _compressibility;    // Z̄
    private readonly Temperature _average;       // T̄
    private readonly double _frictionFactor;     // f, from Colebrook at line conditions

    public GasHydraulicModel(
        double molarMassKgPerMol,
        double averageCompressibility,
        Temperature averageTemperature,
        double frictionFactor)
    {
        if (molarMassKgPerMol <= 0.0 || averageCompressibility <= 0.0)
            throw new ModelFault("SDD-006 §6", null,
                "molar mass and compressibility must be positive");

        if (frictionFactor <= 0.0)
            throw new ModelFault("SDD-006 §6", null, "friction factor must be positive");

        _molarMassKgPerMol = molarMassKgPerMol;
        _compressibility = averageCompressibility;
        _average = averageTemperature;
        _frictionFactor = frictionFactor;
    }

    public MassRate CapacityBetween(PipeGeometry geometry, Pressure inlet, Pressure outlet)
    {
        LiquidHydraulicModel.Validate(geometry);

        double squares = inlet.Pascals * inlet.Pascals - outlet.Pascals * outlet.Pascals;
        if (squares <= 0.0) return new MassRate(0.0);

        double d = geometry.InnerDiameter.Metres;
        double numerator = squares * PhysicalConstants.Pi * PhysicalConstants.Pi
                         * DetMath.Pow(d, 5.0) * _molarMassKgPerMol;

        double denominator = 16.0 * _frictionFactor * _compressibility
                           * PhysicalConstants.GasConstantJPerMolK
                           * _average.Kelvin * geometry.PipeLength.Metres;

        return new MassRate(DetMath.Sqrt(numerator / denominator));
    }
}

/// <summary>
/// SDD-006 §6's pipeline element, with linefill.
///
/// <para>Linefill is REAL, OWNED MASS. It appears in the conservation check
/// (R11-V7) and it is what produces the authentic delay between a production
/// change and its arrival at the terminal — a line does not deliver what entered
/// it this tick, it delivers what entered it however many ticks ago the transit
/// takes.</para>
/// </summary>
public sealed class Pipeline : IPipeline
{
    private readonly IHydraulicModel _hydraulics;
    private readonly IFluidPropertyModel _fluid;
    private readonly Density _density;
    private readonly int _materialCount;

    private MaterialInventory _linefill;

    public Pipeline(
        EntityId<IFlowElement> id,
        PipeGeometry geometry,
        Pressure rating,
        ContentId pipeSpec,
        IHydraulicModel hydraulics,
        IFluidPropertyModel fluid,
        Density density,
        int materialCount)
    {
        ArgumentNullException.ThrowIfNull(hydraulics);
        ArgumentNullException.ThrowIfNull(fluid);
        LiquidHydraulicModel.Validate(geometry);

        Id = id;
        Geometry = geometry;
        PipeLength = geometry.PipeLength;
        InnerDiameter = geometry.InnerDiameter;
        Rating = rating;
        PipeSpec = pipeSpec;

        _hydraulics = hydraulics;
        _fluid = fluid;
        _density = density;
        _materialCount = materialCount;
        _linefill = MaterialInventory.Empty(materialCount);
    }

    public EntityId<IFlowElement> Id { get; }
    public PipeGeometry Geometry { get; }
    public Length PipeLength { get; }
    public Length InnerDiameter { get; }
    public Pressure Rating { get; }
    public ContentId PipeSpec { get; }

    /// <summary>The mass in transit. Owned, and on the balance sheet.</summary>
    public MaterialInventory Linefill => _linefill;

    /// <summary>
    /// <c>ρ̄·A·L</c> — the mass a full line holds. Not a capacity on throughput;
    /// it is how much is in the pipe at any moment, which is what makes the
    /// transit delay a length rather than a declared lag.
    /// </summary>
    public Mass FullLinefill => new(
        _density.KgPerCubicMetre
        * PhysicalConstants.Pi * InnerDiameter.Metres * InnerDiameter.Metres / 4.0
        * PipeLength.Metres);

    public IReadOnlyList<PortSpec> Ports { get; } =
    [
        new PortSpec(new PortId(0), PortDirection.Inlet, PortRole.Main),
        new PortSpec(new PortId(1), PortDirection.Outlet, PortRole.Main),
    ];

    public TransformResult Transform(TransformInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        MaterialStream inlet = input.Inlets.Count > 0
            ? input.Inlets[0]
            : new MaterialStream(Composition.Zero(_materialCount), Rating,
                                 input.Segment.Ambient,
                                 Allocation.FromSingle(new EntityRef(EntityKind.Compartment, 1)));

        Pressure drop = _hydraulics.DropAlong(inlet, Geometry, _fluid);

        // Mass passes; pressure falls. The line does not consume anything, and
        // the linefill it carries is committed at stage 6 rather than taken out
        // of the stream here — Transform is pure.
        return new TransformResult(
            [inlet with { P = new Pressure(Math.Max(0.0, inlet.P.Pascals - drop.Pascals)) }],
            Sourced: Composition.Zero(_materialCount),
            FuelConsumed: Composition.Zero(_materialCount),
            Disposed: Compressor.NoDisposal(_materialCount),
            PowerDraw: new Power(0.0));
    }

    /// <summary>
    /// SDD-006 §6's erosional-velocity limit: <c>v_max = C_e / sqrt(ρ_mix)</c>,
    /// API 14E.
    ///
    /// <para>Exceeding it is a CONSTRAINT feeding the hazard severity, not a hard
    /// block (design 05 §6.3) — a line run hot erodes, it does not refuse. The
    /// player gets a rising failure rate rather than a wall.</para>
    /// </summary>
    public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.Inlets.Count == 0) return [];

        double massRate = input.Inlets[0].MassRates.Total.KgPerSecond;
        double area = PhysicalConstants.Pi * InnerDiameter.Metres * InnerDiameter.Metres / 4.0;
        double velocity = massRate / (_density.KgPerCubicMetre * area);

        double maximum = ErosionalConstant / DetMath.Sqrt(_density.KgPerCubicMetre);

        return [new ConstraintEvaluation(ConstraintKind.ErosionalVelocity, maximum, velocity)];
    }

    /// <summary>Stage 6. What is in the line now.</summary>
    public void CommitLinefill(MaterialInventory contents)
    {
        if (contents.Total.Kilograms > FullLinefill.Kilograms + LinefillTolerance)
            throw new InvariantFault("SDD-006 §6", null,
                $"pipeline {Id.Value} was committed {Format(contents.Total.Kilograms)} kg of " +
                $"linefill against a volume holding {Format(FullLinefill.Kilograms)} kg");

        _linefill = contents;
    }

    /// <summary>API 14E's C, SI-converted. SDD-006 §6 cites it to
    /// PhysicalConstants; it is here until a second consumer needs it.</summary>
    private const double ErosionalConstant = 122.0;

    private const double LinefillTolerance = 1e-6;

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}
