// R10.5 — water cut, by fractional flow (SDD-003 §3.1c, CAL3).
//
// THE S-CURVE IS NOT A SHAPE THAT IS DRAWN. It is what fractional flow does when
// the relative permeabilities are power laws, which is the standard Corey
// treatment. A fitted sigmoid would have produced the same picture and taught
// nobody anything, because it would not respond to viscosity — and the whole
// reason a viscous oil waters out early is the mobility ratio in the denominator.
//
// Breakthrough is likewise not scheduled. It is the first tick at which water
// saturation at the producer exceeds the connate value, and before that krw is
// exactly zero, so no water flows at all.

using OGSim.Kernel;

namespace OGSim.Subsurface;

/// <summary>Corey endpoints and exponents, from the rock type.</summary>
internal sealed record RelativePermeabilityCurve(
    double ConnateWaterSaturation,     // Swc
    double ResidualOilSaturation,      // Sor
    double WaterEndpoint,              // krw_max
    double OilEndpoint,                // kro_max
    double WaterExponent,              // nw
    double OilExponent)                // no
{
    public static RelativePermeabilityCurve Validated(
        double swc, double sor, double krwMax, double kroMax, double nw, double no)
    {
        if (swc < 0.0 || sor < 0.0 || swc + sor >= 1.0)
            throw new ModelFault("SDD-003 §3.1c", null,
                $"Swc {Format(swc)} and Sor {Format(sor)} leave no movable saturation");

        if (krwMax is <= 0.0 or > 1.0 || kroMax is <= 0.0 or > 1.0)
            throw new ModelFault("SDD-003 §3.1c", null,
                "relative permeability endpoints must be in (0, 1]");

        if (nw <= 0.0 || no <= 0.0)
            throw new ModelFault("SDD-003 §3.1c", null,
                "Corey exponents must be positive");

        return new RelativePermeabilityCurve(swc, sor, krwMax, kroMax, nw, no);
    }

    /// <summary>S*, clamped to [0, 1].</summary>
    public double NormalisedSaturation(double waterSaturation)
    {
        double span = 1.0 - ConnateWaterSaturation - ResidualOilSaturation;
        double s = (waterSaturation - ConnateWaterSaturation) / span;
        return Math.Clamp(s, 0.0, 1.0);
    }

    public double WaterPermeability(double waterSaturation) =>
        WaterEndpoint * DetMath.Pow(NormalisedSaturation(waterSaturation), WaterExponent);

    public double OilPermeability(double waterSaturation) =>
        OilEndpoint * DetMath.Pow(1.0 - NormalisedSaturation(waterSaturation), OilExponent);

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>SDD-003 §3.1c's fractional flow.</summary>
internal static class FractionalFlow
{
    /// <summary>
    /// <c>fw = 1 / (1 + (kro·μw)/(krw·μo))</c> — the water cut at the sandface.
    ///
    /// <para>At or below connate saturation <c>krw</c> is exactly zero and the
    /// expression is <c>1/∞</c>: no water flows. That is breakthrough's
    /// definition rather than a guard bolted onto one.</para>
    /// </summary>
    public static double WaterCut(
        RelativePermeabilityCurve curve,
        double waterSaturation,
        Viscosity water,
        Viscosity oil)
    {
        ArgumentNullException.ThrowIfNull(curve);

        double krw = curve.WaterPermeability(waterSaturation);
        if (krw <= 0.0) return 0.0;

        double kro = curve.OilPermeability(waterSaturation);
        if (kro <= 0.0) return 1.0;      // no mobile oil left: pure water

        return 1.0 / (1.0 + (kro * water.PascalSeconds) / (krw * oil.PascalSeconds));
    }

    /// <summary>
    /// The endpoint mobility ratio. <c>M &gt; 1</c> is an unfavourable flood —
    /// the water outruns the oil, breakthrough is early and the S-curve's rise
    /// is sharp.
    ///
    /// <para>Exposed because it is the number that explains the curve, and a
    /// player looking at an early breakthrough is looking at this.</para>
    /// </summary>
    public static double MobilityRatio(
        RelativePermeabilityCurve curve, Viscosity water, Viscosity oil) =>
        (curve.WaterEndpoint / water.PascalSeconds) / (curve.OilEndpoint / oil.PascalSeconds);
}
