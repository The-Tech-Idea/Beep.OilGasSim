// R6.6 — vertical lift performance (SDD-003 §6.2, design 05 §4.3).
//
// Inverted relative to the physics, deliberately. The VLP naturally answers
// "what wellhead pressure results from this rate"; §6.3's operating-point
// bisection searches on Pwf, so the useful direction is the one that answers
// "what bottomhole pressure does this rate DEMAND".
//
// The hydrostatic term is why every well eventually dies: the column has to be
// lifted whatever the rate, so as reservoir pressure falls there comes a point
// where no rate satisfies both curves. R6-V6 is that moment, and R7 exists to
// answer it.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Wells;

/// <summary>The tubing string a rate has to be pushed up.</summary>
public sealed record TubingGeometry(
    Length MeasuredDepth,           // MD, for friction length
    Length TrueVerticalDepth,       // TVD, for hydrostatic head
    Length InsideDiameter,          // D
    double RoughnessMetres);        // ε, for Colebrook-White

/// <summary>
/// SDD-003 §6.2: <c>Pwf_required(q) = Pwh + ΔP_hydro + ΔP_friction</c>.
///
/// <para>The two terms pull opposite ways with tubing size, which is the whole
/// of R6-V7: narrow tubing is friction-limited (ΔP_friction ~ 1/D⁵), wide tubing
/// loads up because the velocity falls too low to lift liquid, so the column
/// stays full and heavy. A model with only one term could not produce that
/// trade, and the trade is the decision.</para>
/// </summary>
public sealed class HydrostaticFrictionOutflowModel : IOutflowModel
{
    /// <summary>SDD-003 §6.2: EXACTLY 20 Newton steps from f₀ = 0.02. Fixed, not
    /// converged-when-close — a loop that stopped on a tolerance would take a
    /// different number of steps on different inputs, and D-1 wants the same
    /// arithmetic every run.</summary>
    private const int ColebrookIterations = 20;
    private const double ColebrookSeed = 0.02;

    private readonly TubingGeometry _tubing;
    private readonly Density _mixtureDensity;

    public HydrostaticFrictionOutflowModel(TubingGeometry tubing, Density mixtureDensity)
    {
        ArgumentNullException.ThrowIfNull(tubing);
        Validate(tubing, mixtureDensity);

        _tubing = tubing;
        _mixtureDensity = mixtureDensity;
    }

    public ContentId Id { get; } = new("hydrostatic-friction-outflow");

    public Pressure RequiredBottomhole(ReservoirRate rate, Pressure wellheadPressure)
    {
        double q = rate.CubicMetresPerSecond;
        if (q < 0.0)
            throw new ModelFault("SDD-003 §6.2", null,
                $"negative rate {Format(q)} m³/s; the VLP is not defined for backflow");

        return new Pressure(
            wellheadPressure.Pascals + HydrostaticPa + FrictionPa(q));
    }

    /// <summary>
    /// <c>ρ·g·TVD</c> — the column's own weight, independent of rate. This is the
    /// floor under the VLP curve and the reason a well dies: it must be paid even
    /// at zero flow.
    /// </summary>
    public double HydrostaticPa =>
        _mixtureDensity.KgPerCubicMetre
        * PhysicalConstants.GravityMPerS2
        * _tubing.TrueVerticalDepth.Metres;

    /// <summary><c>f·(MD/D)·ρ·v²/2</c>, Darcy-Weisbach.</summary>
    public double FrictionPa(double rateM3PerS)
    {
        if (rateM3PerS <= 0.0) return 0.0;

        double d = _tubing.InsideDiameter.Metres;
        double area = PhysicalConstants.Pi * d * d / 4.0;
        double velocity = rateM3PerS / area;

        double f = FrictionFactor(velocity, d);

        return f * (_tubing.MeasuredDepth.Metres / d)
                 * _mixtureDensity.KgPerCubicMetre * velocity * velocity / 2.0;
    }

    /// <summary>
    /// Colebrook-White by exactly 20 Newton steps (§6.2). Converged long before
    /// 20 for any physical input; the count is fixed so the arithmetic is too.
    ///
    /// <para>Solved in <c>x = 1/√f</c>, where Colebrook is
    /// <c>x + 2·log10(ε/3.7D + 2.51x/Re) = 0</c> — linear enough in x that
    /// Newton cannot wander, which the same equation in f is not.</para>
    /// </summary>
    private double FrictionFactor(double velocity, double diameter)
    {
        double reynolds = _mixtureDensity.KgPerCubicMetre * velocity * diameter / ViscosityPaS;

        // Laminar has a closed form and Colebrook does not apply to it. The
        // transition is genuinely discontinuous in nature, not an artefact.
        if (reynolds < LaminarLimit) return 64.0 / Math.Max(reynolds, 1.0);

        double relativeRoughness = _tubing.RoughnessMetres / diameter;
        double x = 1.0 / DetMath.Sqrt(ColebrookSeed);

        for (int i = 0; i < ColebrookIterations; i++)
        {
            double inner = relativeRoughness / 3.7 + 2.51 * x / reynolds;
            double g = x + 2.0 * Log10(inner);
            double dg = 1.0 + 2.0 * (2.51 / reynolds) / (inner * Ln10);

            x -= g / dg;
        }

        return 1.0 / (x * x);
    }

    /// <summary>Base-10 log via DetMath: D-2 forbids System.Math transcendentals
    /// in simulation code, and Colebrook is stated in log10.</summary>
    private static double Log10(double value) => DetMath.Ln(value) / Ln10;

    private static readonly double Ln10 = DetMath.Ln(10.0);

    /// <summary>Reynolds number below which flow is laminar. The conventional
    /// value (SDD-003 §6.2's Colebrook applies to turbulent flow only).</summary>
    private const double LaminarLimit = 2300.0;

    /// <summary>Mixture viscosity for the Reynolds number. Held at the fluid's
    /// own value would be better; §6.2 states the friction term against ρ_mix
    /// and does not pin a viscosity, so this is R6's declared placeholder and
    /// open item S003-5 records it.</summary>
    private const double ViscosityPaS = 1e-3;

    private static void Validate(TubingGeometry tubing, Density density)
    {
        if (tubing.InsideDiameter.Metres <= 0.0)
            throw new ModelFault("SDD-003 §6.2", null, "tubing inside diameter must be positive");

        if (tubing.TrueVerticalDepth.Metres < 0.0)
            throw new ModelFault("SDD-003 §6.2", null, "true vertical depth cannot be negative");

        if (tubing.MeasuredDepth.Metres < tubing.TrueVerticalDepth.Metres)
            throw new ModelFault("SDD-003 §6.2", null,
                $"measured depth {Format(tubing.MeasuredDepth.Metres)} m is less than TVD " +
                $"{Format(tubing.TrueVerticalDepth.Metres)} m; a hole cannot be shorter than it is deep");

        if (density.KgPerCubicMetre <= 0.0)
            throw new ModelFault("SDD-003 §6.2", null, "mixture density must be positive");

        if (tubing.RoughnessMetres < 0.0)
            throw new ModelFault("SDD-003 §6.2", null, "roughness cannot be negative");
    }

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}
