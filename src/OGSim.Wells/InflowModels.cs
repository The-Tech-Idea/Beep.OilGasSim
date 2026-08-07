// R6.5 — inflow performance (SDD-003 §6.1, design 05 §4.1–4.2).
//
// PER PERFORATION, not per completion. A well with a damaged lower zone and a
// clean upper zone is expressible, isolating the damaged zone is a real
// intervention with a computable benefit, and multi-perforation commingling
// apportions by each perf's own kh share (R6 §2.4, FV10). A completion-level
// signature could not express the case the model exists for.
//
// SI Darcy throughout. The field-unit constants 7758 and 141.2 are unit
// conversions, not regression coefficients, so an SI form exists and is exact —
// SDD-003 §2's rule applies in full here, unlike the black-oil correlations
// where it does not.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Wells;

/// <summary>
/// What a perforation needs from its compartment and fluid to state an inflow.
/// Passed as a value: <see cref="IInflowModel"/> is a public plugin slot and the
/// compartment is truth internal to OGSim.Subsurface (SDD-003 §3).
/// </summary>
public sealed record InflowConditions(
    Permeability Permeability,          // k
    Length PerforatedInterval,          // h_perf
    Area DrainageArea,                  // A, for re
    Length WellboreRadius,              // rw
    Viscosity OilViscosity,             // μo
    Pressure BubblePoint);              // Pb — where Vogel takes over

/// <summary>
/// SDD-003 §6.1's SI Darcy form, above the bubble point:
/// <c>q = 2π·k·h·(Pr − Pwf) / [μ·(ln(re/rw) − 0.75 + s)]</c>.
///
/// <para>Straight-line IPR. Correct while the oil is undersaturated, because
/// nothing comes out of solution to occupy pore space and the mobility is
/// constant — which is exactly the condition that ends at Pb.</para>
/// </summary>
public sealed class DarcyInflowModel : IInflowModel
{
    private readonly InflowConditions _conditions;

    public DarcyInflowModel(InflowConditions conditions)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        Validate(conditions);

        _conditions = conditions;
    }

    public ContentId Id { get; } = new("darcy-inflow");

    public ReservoirRate InflowAt(
        Pressure reservoirPressure, Pressure bottomholePressure, Perforation perforation)
    {
        ArgumentNullException.ThrowIfNull(perforation);

        double drawdown = reservoirPressure.Pascals - bottomholePressure.Pascals;

        // No drawdown, no flow. Negative drawdown is not injection — an injector
        // is a different completion with a different model (R10), and treating a
        // backwards pressure difference as inflow here would let a producer
        // quietly refill its own compartment.
        if (drawdown <= 0.0) return new ReservoirRate(0.0);

        return new ReservoirRate(
            ProductivityIndex(perforation) * drawdown);
    }

    /// <summary>
    /// <c>J = 2π·k·h / [μ·(ln(re/rw) − 0.75 + s)]</c>, m³/s per Pa.
    ///
    /// <para>Isolated on its own because the productivity index is the number the
    /// player reasons about — a workover that halves skin doubles nothing else —
    /// and because MX2 pins the skin sensitivity against it directly.</para>
    /// </summary>
    public double ProductivityIndex(Perforation perforation)
    {
        double h = HeightOf(perforation);
        if (h <= 0.0) return 0.0;

        double re = DetMath.Sqrt(_conditions.DrainageArea.SquareMetres / PhysicalConstants.Pi);
        double denominator = _conditions.OilViscosity.PascalSeconds
                           * (DetMath.Ln(re / _conditions.WellboreRadius.Metres)
                              - SteadyStateOffset + perforation.Skin);

        // Skin so negative it inverts the denominator would give NEGATIVE
        // productivity — a perforation producing against its own drawdown. A
        // fracture's benefit is bounded by the geometry, not unbounded.
        if (denominator <= 0.0)
            throw new ModelFault("SDD-003 §6.1", null,
                $"skin {Format(perforation.Skin)} makes the Darcy denominator " +
                $"{Format(denominator)}; the perforation would produce against its own drawdown");

        return PhysicalConstants.TwoPi * _conditions.Permeability.SquareMetres * h / denominator;
    }

    /// <summary>The perforated interval this perf actually contributes, m. A
    /// perforation isolated by a plug contributes nothing — which is what makes
    /// isolating a watered-out zone a real intervention (R6 §2.4).</summary>
    private double HeightOf(Perforation perforation) =>
        perforation.Isolated
            ? 0.0
            : Math.Min(
                perforation.BottomMd.Metres - perforation.TopMd.Metres,
                _conditions.PerforatedInterval.Metres);

    /// <summary>SDD-003 §6.1's −0.75: the pseudo-steady-state shape factor for
    /// radial flow into a circular drainage area.</summary>
    private const double SteadyStateOffset = 0.75;

    private static void Validate(InflowConditions c)
    {
        if (c.Permeability.SquareMetres <= 0.0)
            throw new ModelFault("SDD-003 §6.1", null, "permeability must be positive");

        if (c.WellboreRadius.Metres <= 0.0)
            throw new ModelFault("SDD-003 §6.1", null, "wellbore radius must be positive");

        if (c.DrainageArea.SquareMetres <= 0.0)
            throw new ModelFault("SDD-003 §6.1", null, "drainage area must be positive");

        if (c.OilViscosity.PascalSeconds <= 0.0)
            throw new ModelFault("SDD-003 §6.1", null, "viscosity must be positive");

        double re = DetMath.Sqrt(c.DrainageArea.SquareMetres / PhysicalConstants.Pi);
        if (re <= c.WellboreRadius.Metres)
            throw new ModelFault("SDD-003 §6.1", null,
                $"drainage radius {Format(re)} m is not larger than the wellbore radius " +
                $"{Format(c.WellboreRadius.Metres)} m; ln(re/rw) would be non-positive");
    }

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// SDD-003 §6.1's composite IPR: Darcy above the bubble point, Vogel below, on
/// the same q_max basis (design 05 §4.2).
///
/// <para>Vogel's curve <c>q/q_max = 1 − 0.2(Pwf/Pr) − 0.8(Pwf/Pr)²</c> bends the
/// straight line over because below Pb, gas comes out of solution in the pores
/// and takes mobility away from the oil. That bend is the reason a well's
/// productivity falls off faster than the pressure does — the same
/// bubble-point cliff R5's material balance shows from the other side.</para>
///
/// <para>The two branches are joined at Pb by CONSTRUCTION rather than by
/// blending: the Vogel branch is anchored to the Darcy rate AT the bubble point,
/// so the composite is continuous there whatever the parameters. A discontinuity
/// at Pb would mean crossing it created or destroyed rate, in the one place the
/// design most wants to be trustworthy.</para>
/// </summary>
public sealed class CompositeInflowModel : IInflowModel
{
    // Vogel (1968), dimensionless. Not physical constants — the two coefficients
    // of the published quadratic (SDD-003 §6.1, design 05 §4.2).
    private const double VogelLinear = 0.2;
    private const double VogelQuadratic = 0.8;

    private readonly DarcyInflowModel _darcy;
    private readonly Pressure _bubblePoint;

    public CompositeInflowModel(InflowConditions conditions)
    {
        ArgumentNullException.ThrowIfNull(conditions);

        _darcy = new DarcyInflowModel(conditions);
        _bubblePoint = conditions.BubblePoint;
    }

    public ContentId Id { get; } = new("composite-inflow");

    public ReservoirRate InflowAt(
        Pressure reservoirPressure, Pressure bottomholePressure, Perforation perforation)
    {
        ArgumentNullException.ThrowIfNull(perforation);

        double pr = reservoirPressure.Pascals;
        double pwf = bottomholePressure.Pascals;
        double pb = _bubblePoint.Pascals;

        if (pwf >= pr) return new ReservoirRate(0.0);

        // Wholly undersaturated: the reservoir never reaches Pb, so the straight
        // line holds all the way down.
        if (pwf >= pb && pr > pb)
            return _darcy.InflowAt(reservoirPressure, bottomholePressure, perforation);

        double j = _darcy.ProductivityIndex(perforation);

        if (pr > pb)
        {
            // Saturated below Pb, undersaturated above: the Darcy segment runs
            // down to Pb, and Vogel continues from there. Anchoring the Vogel
            // branch to the Darcy rate AT Pb is what makes the join continuous.
            double atBubblePoint = j * (pr - pb);
            double vogelMax = atBubblePoint + j * pb / (VogelLinear + 2.0 * VogelQuadratic);

            return new ReservoirRate(
                atBubblePoint + (vogelMax - atBubblePoint) * VogelFraction(pwf / pb));
        }

        // Wholly saturated: the reservoir is already below its bubble point, so
        // Vogel applies over the whole range with q_max at Pwf = 0.
        double qMax = j * pr / (VogelLinear + 2.0 * VogelQuadratic);
        return new ReservoirRate(qMax * VogelFraction(pwf / pr));
    }

    /// <summary>Vogel's dimensionless curve. 1 at Pwf = 0, 0 at Pwf = Pr.</summary>
    private static double VogelFraction(double ratio) =>
        1.0 - VogelLinear * ratio - VogelQuadratic * ratio * ratio;
}
