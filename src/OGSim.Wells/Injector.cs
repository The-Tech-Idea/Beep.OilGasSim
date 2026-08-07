// R10.2 — injection and disposal (SDD-003 §3.1d, R10 §2.2).
//
// An injector is §6.1's Darcy form with the pressure difference reversed and
// water's viscosity in place of oil's. It is not a producer with a minus sign
// bolted on: the fluid is different, the skin grows instead of staying put, and
// the constraint it reports is Injectivity rather than a capacity.
//
// INJECTIVITY DECLINES. The formation plugs with solids and fines, so skin grows
// with cumulative volume, so the rate at a given pressure falls. That is what
// makes water disposal an ongoing operational problem rather than a one-time
// build, and it produces the authentic case where a field is throttled by
// disposal capacity and by nothing upstream at all (R10-V3).

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Wells;

/// <summary>What an injector needs to state a rate (SDD-003 §3.1d).</summary>
public sealed record InjectionConditions(
    Permeability Permeability,
    Length InjectionInterval,
    Area DrainageArea,
    Length WellboreRadius,
    Viscosity WaterViscosity,
    double InitialSkin,                 // s0
    double PluggingPerReferenceVolume,  // α
    ReservoirVolume ReferenceVolume);   // V_reference

/// <summary>
/// SDD-003 §3.1d. A disposal or water-injection well.
/// </summary>
public sealed class Injector
{
    private readonly InjectionConditions _conditions;
    private double _cumulativeInjectedM3;

    public Injector(EntityId<ICompletion> id, InjectionConditions conditions)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        Validate(conditions);

        Id = id;
        _conditions = conditions;
    }

    public EntityId<ICompletion> Id { get; }

    public ReservoirVolume CumulativeInjected => new(_cumulativeInjectedM3);

    /// <summary>
    /// <c>s(V) = s0 + α·V/V_ref</c>. Grows without bound in principle; in
    /// practice the rate it produces falls far enough first that the player
    /// either remediates or stops.
    /// </summary>
    public double CurrentSkin =>
        _conditions.InitialSkin
        + _conditions.PluggingPerReferenceVolume
          * (_cumulativeInjectedM3 / _conditions.ReferenceVolume.CubicMetres);

    /// <summary>
    /// The rate this well accepts at a given injection pressure — §6.1's form
    /// with the difference reversed.
    /// </summary>
    public ReservoirRate AcceptanceAt(Pressure injectionPressure, Pressure reservoirPressure)
    {
        double drive = injectionPressure.Pascals - reservoirPressure.Pascals;

        // Injection pressure below reservoir pressure is not production. A
        // disposal well run backwards is a different well with a different
        // completion, and treating it as one here would let a disposal well
        // quietly become a producer of formation water.
        if (drive <= 0.0) return new ReservoirRate(0.0);

        double re = DetMath.Sqrt(_conditions.DrainageArea.SquareMetres / PhysicalConstants.Pi);
        double denominator = _conditions.WaterViscosity.PascalSeconds
                           * (DetMath.Ln(re / _conditions.WellboreRadius.Metres)
                              - SteadyStateOffset + CurrentSkin);

        if (denominator <= 0.0)
            throw new ModelFault("SDD-003 §3.1d", null,
                $"injector {Id.Value}: skin {Format(CurrentSkin)} makes the Darcy " +
                "denominator non-positive");

        return new ReservoirRate(
            PhysicalConstants.TwoPi * _conditions.Permeability.SquareMetres
            * _conditions.InjectionInterval.Metres * drive / denominator);
    }

    /// <summary>
    /// SDD-002 §5's constraint, so the solver throttles against disposal exactly
    /// as it throttles against a separator (R10-V3).
    /// </summary>
    public ConstraintEvaluation ConstraintAt(
        Pressure injectionPressure, Pressure reservoirPressure, double offeredM3PerS) =>
        new(ConstraintKind.Injectivity,
            AcceptanceAt(injectionPressure, reservoirPressure).CubicMetresPerSecond,
            offeredM3PerS);

    /// <summary>Stage 6. What actually went in, which is what plugs the rock.</summary>
    public void Commit(ReservoirVolume injected)
    {
        if (injected.CubicMetres < 0.0)
            throw new InvariantFault("SDD-003 §3.1d", null,
                $"injector {Id.Value} was committed a negative volume");

        _cumulativeInjectedM3 += injected.CubicMetres;
    }

    /// <summary>
    /// R10-V4. An acid job or a filter upgrade clears the accumulated plugging.
    ///
    /// <para>It restores <c>s0</c> and never improves on it: remediation undoes
    /// damage, it does not stimulate. A well that came back better than new
    /// would make the decline free to ignore.</para>
    /// </summary>
    public void Remediate() => _cumulativeInjectedM3 = 0.0;

    private const double SteadyStateOffset = 0.75;

    private static void Validate(InjectionConditions c)
    {
        if (c.Permeability.SquareMetres <= 0.0 || c.InjectionInterval.Metres <= 0.0)
            throw new ModelFault("SDD-003 §3.1d", null,
                "permeability and injection interval must be positive");

        if (c.WaterViscosity.PascalSeconds <= 0.0)
            throw new ModelFault("SDD-003 §3.1d", null, "water viscosity must be positive");

        if (c.ReferenceVolume.CubicMetres <= 0.0)
            throw new ModelFault("SDD-003 §3.1d", null,
                "the plugging reference volume must be positive; the decline divides by it");

        if (c.PluggingPerReferenceVolume < 0.0)
            throw new ModelFault("SDD-003 §3.1d", null,
                "plugging cannot be negative; an injector does not clean itself by running");
    }

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}
