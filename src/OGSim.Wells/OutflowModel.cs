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
    private readonly TubingGeometry _tubing;
    private readonly Density _mixtureDensity;
    private readonly ILiftMethod? _lift;

    /// <summary>
    /// <paramref name="lift"/> is nullable but NOT optional (law L2): a well with
    /// no lift installed must say so, because the difference between "natural
    /// flow" and "someone forgot to pass the pump" is not one a default can tell.
    /// </summary>
    public HydrostaticFrictionOutflowModel(
        TubingGeometry tubing, Density mixtureDensity, ILiftMethod? lift)
    {
        ArgumentNullException.ThrowIfNull(tubing);
        Validate(tubing, mixtureDensity);

        _tubing = tubing;
        _mixtureDensity = mixtureDensity;
        _lift = lift;
    }

    public ContentId Id { get; } = new("hydrostatic-friction-outflow");

    /// <summary>
    /// SDD-003 §6.2, with lift applied. All three of §6.2's hooks land here and
    /// none of them asks which method is installed:
    ///
    /// <list type="bullet">
    /// <item>the density factor lightens BOTH the hydrostatic and friction terms,
    /// because both are proportional to ρ_mix — which is why gas lift's benefit
    /// is partly self-limiting;</item>
    /// <item>the pressure boost is subtracted from what the reservoir must
    /// supply, since the pump provides it instead;</item>
    /// <item>the displacement cap is not applied here at all — it is a bound on
    /// RATE, not a pressure, and belongs to the operating-point solve.</item>
    /// </list>
    /// </summary>
    public Pressure RequiredBottomhole(ReservoirRate rate, Pressure wellheadPressure)
    {
        double q = rate.CubicMetresPerSecond;
        if (q < 0.0)
            throw new ModelFault("SDD-003 §6.2", null,
                $"negative rate {Format(q)} m³/s; the VLP is not defined for backflow");

        LiftEffect effect = EffectAt(rate);

        double required = wellheadPressure.Pascals
                        + HydrostaticAt(effect.DensityFactor)
                        + FrictionPa(q, effect.DensityFactor)
                        - effect.PressureBoost.Pascals;

        // A pump that more than covers the column means the reservoir need
        // supply nothing — but not less than nothing, which would be the well
        // being pushed backwards into the formation.
        return new Pressure(Math.Max(0.0, required));
    }

    /// <summary>The installed lift's effect at a rate, or none.</summary>
    public LiftEffect EffectAt(ReservoirRate rate) =>
        _lift?.EffectAt(rate, _mixtureDensity) ?? LiftEffect.None;

    /// <summary>
    /// <c>ρ·g·TVD</c> — the column's own weight, independent of rate. This is the
    /// floor under the VLP curve and the reason a well dies: it must be paid even
    /// at zero flow, and lift is what pays it instead.
    /// </summary>
    public double HydrostaticPa => HydrostaticAt(EffectAt(new ReservoirRate(0.0)).DensityFactor);

    private double HydrostaticAt(double densityFactor) =>
        _mixtureDensity.KgPerCubicMetre * densityFactor
        * PhysicalConstants.GravityMPerS2
        * _tubing.TrueVerticalDepth.Metres;

    /// <summary><c>f·(MD/D)·ρ·v²/2</c>, Darcy-Weisbach.</summary>
    public double FrictionPa(double rateM3PerS) => FrictionPa(rateM3PerS, densityFactor: 1.0);

    private double FrictionPa(double rateM3PerS, double densityFactor)
    {
        if (rateM3PerS <= 0.0) return 0.0;

        double d = _tubing.InsideDiameter.Metres;
        double area = PhysicalConstants.Pi * d * d / 4.0;

        // Gas lift adds VOLUME to the tubing, and that volume has to be pushed
        // up the same pipe. Velocity therefore rises even as density falls,
        // and since friction goes as v² this is what puts a turning point in
        // the gas-lift optimum (R7-V4) rather than an ever-improving curve.
        double liftedVolume = rateM3PerS / Math.Max(densityFactor, DensityFactorFloor);
        double velocity = liftedVolume / area;

        double f = FrictionFactor(velocity, d);

        return f * (_tubing.MeasuredDepth.Metres / d)
                 * _mixtureDensity.KgPerCubicMetre * densityFactor
                 * velocity * velocity / 2.0;
    }

    /// <summary>Guards the division when a method claims to remove essentially
    /// all the column's weight, which no real one does.</summary>
    private const double DensityFactorFloor = 1e-3;

    /// <summary>
    /// Colebrook-White — the KERNEL's implementation, shared with the pipeline
    /// (SDD-006 §6 says one implementation in those words). It lived here
    /// privately until R11.0's finding 122, when the pipeline needed the
    /// identical procedure and a second copy would have been two chances to get
    /// the same equation wrong.
    /// </summary>
    private double FrictionFactor(double velocity, double diameter)
    {
        double reynolds = _mixtureDensity.KgPerCubicMetre * velocity * diameter / ViscosityPaS;
        return Friction.Factor(Math.Max(reynolds, 1.0), _tubing.RoughnessMetres / diameter);
    }

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
