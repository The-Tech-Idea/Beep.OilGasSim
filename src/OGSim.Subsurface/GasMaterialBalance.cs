// R5.7 — the gas material balance and the p/Z line (SDD-003 §3.1b, design 05 §3.2).
//
// §3.1's oil balance expresses every expansion term per stock-tank m³ of
// original oil, so a dry gas reservoir (N = 0) has no root there at all. This is
// the gas form, and it is a different equation rather than a special case of the
// other one.
//
// Design 05 §3.2 calls p/Z "the single best information mechanic in the game":
// the player is never told how big their gas reservoir is. They produce, plot
// p/Z against cumulative gas, extrapolate, and read G off the x-intercept — and
// every point on that plot cost them a shut-in survey, so the knowledge is paid
// for in deferred production. If the line bends upward they have water drive and
// a completely different future.
//
// Which is why exactness matters here more than almost anywhere: the player is
// invited to trust their own arithmetic on this line. A percent of drift would
// make a correct deduction give a wrong answer.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Subsurface;

internal static class GasMaterialBalance
{
    private const double BracketFloorPa = 1_000.0;
    private const int BisectionIterations = 80;
    private const double PressureTolerancePa = 100.0;

    /// <summary>
    /// <c>p/Z</c> at the current cumulative production, Pa. THE line: linear in
    /// <c>Gp</c> for a volumetric reservoir, exactly, by construction.
    ///
    /// <para>Water influx divides it by the fraction of pore volume the water has
    /// taken, which is what bends the plot upward — the reservoir holds pressure
    /// better than depletion alone would explain, and the bend is the only
    /// evidence the player gets.</para>
    /// </summary>
    internal static double PressureOverZ(
        Pressure initialPressure,
        double initialZ,
        StandardGasVolume gasInPlace,
        StandardGasVolume cumulativeGasProduced,
        ReservoirVolume netWaterInflux,
        GasFormationVolumeFactor initialBg)
    {
        double g = gasInPlace.CubicMetres;
        double gp = cumulativeGasProduced.CubicMetres;

        if (g <= 0.0)
            throw new ModelFault("SDD-003 §3.1b", null,
                $"gas in place is {Format(g)} sm³; the p/Z line is expressed as a fraction of it");

        if (gp > g)
            throw new ModelFault("SDD-003 §3.1b", null,
                $"cumulative gas produced {Format(gp)} sm³ exceeds gas in place {Format(g)} sm³ — " +
                "the compartment has produced volume it never had");

        double volumetric = (initialPressure.Pascals / initialZ) * (1.0 - gp / g);

        double waterOccupied = netWaterInflux.CubicMetres / (g * initialBg.Rm3PerSm3);
        if (waterOccupied >= 1.0)
            throw new ModelFault("SDD-003 §3.1b", null,
                $"water influx has taken {Format(waterOccupied)} of the gas pore volume; " +
                "the compartment is fully watered out and the p/Z relation no longer holds");

        return volumetric / (1.0 - waterOccupied);
    }

    /// <summary>
    /// The pressure at which <c>P/Z(P, T)</c> equals the line's value.
    ///
    /// <para>One bisection, not two. The line gives <c>p/Z</c> in closed form;
    /// only recovering P from it needs a root-find, because Z is implicit in P
    /// through Dranchuk-Abou-Kassem (§4.1). Solving the balance iteratively as
    /// well would nest a root-find inside a root-find for no gain.</para>
    /// </summary>
    internal static Pressure Solve(
        Pressure initialPressure,
        double initialZ,
        StandardGasVolume gasInPlace,
        StandardGasVolume cumulativeGasProduced,
        ReservoirVolume netWaterInflux,
        GasFormationVolumeFactor initialBg,
        IFluidPropertyModel fluid,
        Temperature reservoirTemperature)
    {
        ArgumentNullException.ThrowIfNull(fluid);

        double target = PressureOverZ(
            initialPressure, initialZ, gasInPlace,
            cumulativeGasProduced, netWaterInflux, initialBg);

        double lowPa = BracketFloorPa;
        double highPa = initialPressure.Pascals;

        double Residual(double pa) =>
            pa / fluid.Z(new Pressure(pa), reservoirTemperature) - target;

        double atLow = Residual(lowPa);
        double atHigh = Residual(highPa);

        if (atHigh == 0.0) return new Pressure(highPa);

        if (atLow * atHigh > 0.0)
            throw new ModelFault("SDD-003 §3.1b", null,
                $"no pressure in [{Format(lowPa)}, {Format(highPa)}] Pa gives p/Z = " +
                $"{Format(target)} Pa; the residuals at the ends are {Format(atLow)} and " +
                $"{Format(atHigh)}");

        for (int i = 0; i < BisectionIterations; i++)
        {
            double midPa = lowPa + (highPa - lowPa) * 0.5;
            if (highPa - lowPa < PressureTolerancePa) return new Pressure(midPa);

            double atMid = Residual(midPa);
            if (atMid == 0.0) return new Pressure(midPa);

            if (atLow * atMid < 0.0)
            {
                highPa = midPa;
            }
            else
            {
                lowPa = midPa;
                atLow = atMid;
            }
        }

        throw new ModelFault("SDD-003 §3.1b", null,
            $"bisection did not reach {Format(PressureTolerancePa)} Pa in " +
            $"{BisectionIterations} iterations; bracket is still " +
            $"[{Format(lowPa)}, {Format(highPa)}] Pa");
    }

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}
