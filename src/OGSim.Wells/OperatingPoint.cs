// R6.7 — the operating point (SDD-003 §6.3). The phase's centre.
//
// IPR and VLP are both functions of bottomhole flowing pressure, and the well
// produces where they cross. NO INTERSECTION MEANS THE WELL DOES NOT FLOW, and
// that is reported as DEAD — a distinct outcome, never a rate of zero.
//
// The distinction is not pedantry. "This well produced nothing this tick" and
// "this well cannot flow at any rate" have different remedies: the first is a
// choke setting or a shut-in, the second is artificial lift or abandonment. A
// player told only "0" cannot tell which they have, and R7 exists entirely to
// answer the second (R6 §2.2, R6-V6).

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Wells;

/// <summary>
/// SDD-003 §6.3's bisection on Pwf. Shared by every completion, because a
/// completion differs in its IPR and its VLP, never in how the two are crossed.
/// </summary>
internal static class OperatingPointSolver
{
    private const int Iterations = 64;              // §6.3, pinned
    private const double TolerancePa = 500.0;

    /// <summary>
    /// Crosses the two curves. The residual is
    /// <c>Φ(Pwf) = q_IPR(Pwf) − q_VLP(Pwf)</c>, where <c>q_VLP</c> is the rate at
    /// which the tubing demands exactly this Pwf.
    ///
    /// <para>Bracketed on <c>[Pwh + ρ_min·g·TVD, Pr]</c>: below the bracket the
    /// tubing cannot deliver to the wellhead at all, above it there is no
    /// drawdown. A sign change inside means a crossing; no sign change means the
    /// reservoir cannot push the column, which is the well dying.</para>
    /// </summary>
    internal static OperatingPoint Solve(
        Pressure reservoirPressure,
        Pressure wellheadPressure,
        IInflowModel inflow,
        IOutflowModel outflow,
        IReadOnlyList<Perforation> perforations,
        double hydrostaticFloorPa)
    {
        double highPa = reservoirPressure.Pascals;
        double lowPa = wellheadPressure.Pascals + hydrostaticFloorPa;

        // The column already weighs more than the reservoir can push. No rate
        // is possible, and no amount of searching will find one.
        if (lowPa >= highPa) return new Dead();

        double Residual(double pwfPa)
        {
            var pwf = new Pressure(pwfPa);

            double supplied = 0.0;
            for (int i = 0; i < perforations.Count; i++)
                supplied += inflow.InflowAt(reservoirPressure, pwf, perforations[i])
                                  .CubicMetresPerSecond;

            return supplied - RateDemanding(outflow, wellheadPressure, pwfPa, supplied);
        }

        double atLow = Residual(lowPa);
        double atHigh = Residual(highPa);

        // At Pr the IPR supplies nothing, so Φ(Pr) ≤ 0 always. A well that flows
        // has Φ(low) > 0: the reservoir can supply more than the tubing needs.
        if (atLow <= 0.0) return new Dead();
        if (atHigh > 0.0)
            throw new ModelFault("SDD-003 §6.3", null,
                $"the IPR supplies {Format(atHigh)} m³/s at zero drawdown; " +
                "an inflow model must give zero rate at Pwf = Pr");

        for (int i = 0; i < Iterations; i++)
        {
            double midPa = lowPa + (highPa - lowPa) * 0.5;
            if (highPa - lowPa < TolerancePa) return At(midPa);

            if (Residual(midPa) > 0.0) lowPa = midPa;
            else highPa = midPa;
        }

        return At(lowPa + (highPa - lowPa) * 0.5);

        OperatingPoint At(double pwfPa)
        {
            var pwf = new Pressure(pwfPa);

            double rate = 0.0;
            for (int i = 0; i < perforations.Count; i++)
                rate += inflow.InflowAt(reservoirPressure, pwf, perforations[i])
                              .CubicMetresPerSecond;

            // A crossing at a rate indistinguishable from zero is a dead well
            // that happened to bracket. Reporting it as Flowing(0) would put back
            // exactly the ambiguity this whole method exists to remove.
            return rate <= RateFloor
                ? new Dead()
                : new Flowing(new ReservoirRate(rate), pwf);
        }
    }

    /// <summary>
    /// The rate at which the VLP demands <paramref name="pwfPa"/>, found by
    /// inverting <see cref="IOutflowModel.RequiredBottomhole"/>.
    ///
    /// <para>Inverted numerically rather than algebraically because the friction
    /// term goes through Colebrook-White, which has no closed form — and because
    /// the contract is deliberately stated in the direction §6.3 searches, so
    /// this is the one place that needs the other direction.</para>
    /// </summary>
    private static double RateDemanding(
        IOutflowModel outflow, Pressure wellhead, double pwfPa, double upperBound)
    {
        // Monotone in rate: more flow needs more pressure. So the demanded Pwf at
        // rate 0 is the floor, and bisection on rate is safe.
        double atZero = outflow.RequiredBottomhole(new ReservoirRate(0.0), wellhead).Pascals;
        if (pwfPa <= atZero) return 0.0;

        double low = 0.0;
        double high = Math.Max(upperBound, RateFloor) * RateBracketSlack;

        // Grow the bracket until the tubing demands more than pwf, so the answer
        // is inside it. Bounded: eight doublings of the IPR's own rate is far
        // past anything the reservoir can deliver.
        for (int i = 0; i < RateBracketGrowths; i++)
        {
            if (outflow.RequiredBottomhole(new ReservoirRate(high), wellhead).Pascals > pwfPa) break;
            high *= 2.0;
        }

        for (int i = 0; i < Iterations; i++)
        {
            double mid = low + (high - low) * 0.5;
            if (outflow.RequiredBottomhole(new ReservoirRate(mid), wellhead).Pascals > pwfPa)
                high = mid;
            else
                low = mid;
        }

        return low + (high - low) * 0.5;
    }

    private const double RateFloor = 1e-8;           // SDD-002 §7 S5's q_floor
    private const double RateBracketSlack = 4.0;
    private const int RateBracketGrowths = 8;

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}
