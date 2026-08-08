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

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Subsurface;

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
