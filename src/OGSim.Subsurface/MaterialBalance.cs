// R5.3 — the tank material balance (SDD-003 §3.1, design 05 §3.1).
//
// This is the one place the game's central number is produced. Pressure falls
// because the remaining fluid and rock cannot quite fill the space left by what
// was taken — never because a decline curve said so (R5 G1). Recovery factor is
// then whatever this arithmetic and the drive mechanism produce together, which
// is what makes identifying the drive worth doing (G2).
//
// The Havlena-Odeh grouping, cumulative from initial conditions. See §3.1's
// finding-99 amendment for why cumulative and not per tick.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Subsurface;

/// <summary>
/// SDD-003 §3.1's Φ(P) and its bisection. Shared by every drive mechanism: the
/// mechanisms differ in what they contribute to the balance, not in how the root
/// is found, and duplicating the solver per mechanism would let six copies drift.
/// </summary>
internal static class MaterialBalance
{
    /// <summary>Bracket floor. Not zero: Bg → ∞ as p → 0, and the fluid model's
    /// own validity range stops far above it (SDD-003 §3.1).</summary>
    private static readonly Pressure BracketFloor = new(1_000.0);

    private const int BisectionIterations = 80;      // §3.1, pinned
    private const double PressureTolerancePa = 100.0;

    /// <summary>
    /// Underground withdrawal at trial pressure <paramref name="p"/>, reservoir m³:
    /// <c>F = Np·[Bo + (Rp − Rs)·Bg] + Wp·Bw</c>.
    ///
    /// <para>Rp is the CUMULATIVE producing gas-oil ratio. The term
    /// <c>(Rp − Rs)</c> is the free gas produced — gas that came out of solution
    /// on the way up and occupies reservoir volume the oil no longer holds — so
    /// a compartment that has produced only solution gas contributes nothing
    /// here, and one that has broken through its gas cap contributes a great
    /// deal. That difference is the whole reason a gas cap is worth protecting.</para>
    /// </summary>
    internal static double Withdrawal(MaterialBalanceInput input, IFluidPropertyModel fluid, Pressure p)
    {
        double np = input.CumulativeOilProduced.CubicMetres;
        double wp = input.CumulativeWaterProduced.CubicMetres;

        double water = wp * fluid.Bw(p).RbPerStb;
        if (np <= 0.0) return water;

        double rp = input.CumulativeGasProduced.CubicMetres / np;
        double freeGas = (rp - fluid.Rs(p)) * fluid.Bg(p).Rm3PerSm3;

        return np * (fluid.Bo(p).RbPerStb + freeGas) + water;
    }

    /// <summary>
    /// Total expansion at trial pressure, reservoir m³:
    /// <c>N·(Eo + m·Eg + Efw)</c>.
    /// </summary>
    internal static double Expansion(MaterialBalanceInput input, IFluidPropertyModel fluid, Pressure p)
    {
        double boi = fluid.Bo(input.InitialPressure).RbPerStb;
        double bgi = fluid.Bg(input.InitialPressure).Rm3PerSm3;
        double rsi = fluid.Rs(input.InitialPressure);

        // Eo: the oil shrinks as its gas leaves, and that liberated gas expands.
        double eo = (fluid.Bo(p).RbPerStb - boi)
                  + (rsi - fluid.Rs(p)) * fluid.Bg(p).Rm3PerSm3;

        // Eg: the gas cap, expressed per stock-tank m³ of oil so the gas-cap
        // ratio m can weight it.
        double eg = boi * (fluid.Bg(p).Rm3PerSm3 / bgi - 1.0);

        // Efw: connate water and rock. Small, and the ONLY expansion an
        // undersaturated reservoir above its bubble point has — which is why
        // such a reservoir's pressure falls so steeply for so little production.
        double swc = input.ConnateWaterSaturation;
        double efw = (1.0 + input.GasCapRatio) * boi
                   * ((input.WaterCompressibility * swc + input.RockCompressibility) / (1.0 - swc))
                   * (input.InitialPressure.Pascals - p.Pascals);

        return input.OriginalOilInPlace.CubicMetres * (eo + input.GasCapRatio * eg + efw);
    }

    /// <summary>
    /// Φ(P) = expansion + influx + injection − withdrawal. Zero at the pressure
    /// the compartment has actually reached.
    /// </summary>
    internal static double Residual(MaterialBalanceInput input, IFluidPropertyModel fluid, Pressure p) =>
        Expansion(input, fluid, p)
        + input.CumulativeWaterInflux.CubicMetres
        + input.CumulativeInjected.CubicMetres
        - Withdrawal(input, fluid, p);

    /// <summary>
    /// §3.1's bisection. Not Newton: 80 deterministic iterations cost nothing at
    /// tens of compartments, and bisection cannot diverge or need a derivative.
    ///
    /// <para>No root in the bracket is a MODEL FAULT, never a clamp. A clamped
    /// pressure is a compartment quietly producing volume it does not have, and
    /// the error would surface a hundred ticks later as an inexplicable recovery
    /// factor rather than here, where the cause is still in hand.</para>
    /// </summary>
    internal static Pressure Solve(
        MaterialBalanceInput input,
        IFluidPropertyModel fluid,
        double maxTickPressureDropFraction)
    {
        Validate(input, maxTickPressureDropFraction);

        Pressure end = FindRoot(input, fluid);
        AssertStepIsHonest(input, end, maxTickPressureDropFraction);
        return end;
    }

    private static Pressure FindRoot(MaterialBalanceInput input, IFluidPropertyModel fluid)
    {
        double lowPa = BracketFloor.Pascals;
        double highPa = input.InitialPressure.Pascals;

        if (highPa <= lowPa)
            throw new ModelFault("SDD-003 §3.1", null,
                $"initial pressure {Format(highPa)} Pa is at or below the bracket floor " +
                $"{Format(lowPa)} Pa — a compartment cannot start below the pressure " +
                "the solve is allowed to search down to");

        double atLow = Residual(input, fluid, new Pressure(lowPa));
        double atHigh = Residual(input, fluid, new Pressure(highPa));

        // Φ(Pi) is ≤ 0 once anything has been produced and exactly 0 before.
        if (atHigh == 0.0) return new Pressure(highPa);

        if (atLow * atHigh > 0.0)
            throw new ModelFault("SDD-003 §3.1", null,
                $"no root in [{Format(lowPa)}, {Format(highPa)}] Pa: Φ = {Format(atLow)} and " +
                $"{Format(atHigh)} reservoir m³ at the ends. The compartment has been asked " +
                "for more volume than it can supply at any pressure in range");

        for (int i = 0; i < BisectionIterations; i++)
        {
            double midPa = lowPa + (highPa - lowPa) * 0.5;
            if (highPa - lowPa < PressureTolerancePa) return new Pressure(midPa);

            double atMid = Residual(input, fluid, new Pressure(midPa));
            if (atMid == 0.0) return new Pressure(midPa);

            if (atLow * atMid < 0.0)
            {
                highPa = midPa;
                atHigh = atMid;
            }
            else
            {
                lowPa = midPa;
                atLow = atMid;
            }
        }

        // 80 halvings of any physical bracket is far below 100 Pa, so arriving
        // here means the bracket was not what it claimed.
        throw new ModelFault("SDD-003 §3.1", null,
            $"bisection did not reach {Format(PressureTolerancePa)} Pa in " +
            $"{BisectionIterations} iterations; bracket is still " +
            $"[{Format(lowPa)}, {Format(highPa)}] Pa");
    }

    /// <summary>
    /// Design 05 §3.1's validity limit. The tick's withdrawal is checked against
    /// the expansion the compartment has left, because explicit first-order
    /// integration is only honest while one step is small against the capacity
    /// it is stepping through.
    ///
    /// <para>The fault is the point. A compartment asked for half its remaining
    /// expansion in one month gives an answer that is wrong by an amount nobody
    /// can bound, and the design's decision (05 §3.1) is to refuse rather than to
    /// return it. In practice this binds only on absurdly small compartments with
    /// absurdly large offtake — a content error worth catching anyway.</para>
    /// </summary>
    /// <summary>
    /// 05 §3.1's validity limit, on the PRESSURE STEP — the quantity that
    /// actually bounds explicit first-order error.
    ///
    /// <para>SDD-003 §3.1's finding-106 amendment records why it is not stated
    /// against "a fraction of expansion capacity" as the design first put it:
    /// <c>Bg → ∞</c> at the bracket floor, so the liberated-gas term makes any
    /// compartment's remaining capacity effectively infinite and the limit could
    /// never fire. A guard that reads as present and is not is worse than no
    /// guard, because the phase ships believing the refusal is in place.</para>
    ///
    /// <para>Checked after the solve because the drop is what the solve
    /// computes. The fault is the point: a compartment that moved a quarter of
    /// its pressure in one month gives an answer wrong by an amount nobody can
    /// bound, and 05 §3.1's decision is to refuse rather than return it.</para>
    /// </summary>
    private static void AssertStepIsHonest(
        MaterialBalanceInput input, Pressure end, double maxTickPressureDropFraction)
    {
        double startPa = input.StartPressure.Pascals;
        if (startPa <= 0.0) return;

        double drop = (startPa - end.Pascals) / startPa;
        if (drop <= maxTickPressureDropFraction) return;

        throw new ModelFault("SDD-003 §3.1", null,
            $"this tick drops the pressure from {Format(startPa)} Pa to {Format(end.Pascals)} Pa, " +
            $"a fraction of {Format(drop)} — above the {Format(maxTickPressureDropFraction)} limit. " +
            "One-step monthly integration is not honest at this step size, and 05 §3.1 refuses " +
            "rather than returning a number whose error nobody can bound");
    }

    private static void Validate(
        MaterialBalanceInput input, double maxTickPressureDropFraction)
    {
        if (maxTickPressureDropFraction <= 0.0 || maxTickPressureDropFraction > 1.0)
            throw new ModelFault("SDD-003 §3.1", null,
                $"maxTickPressureDropFraction {Format(maxTickPressureDropFraction)} is not in (0, 1]");

        double swc = input.ConnateWaterSaturation;
        if (swc < 0.0 || swc >= 1.0)
            throw new ModelFault("SDD-003 §3.1", null,
                $"connate water saturation {Format(swc)} is not in [0, 1) — " +
                "Efw divides by (1 − Swc)");

        if (input.OriginalOilInPlace.CubicMetres <= 0.0)
            throw new ModelFault("SDD-003 §3.1", null,
                "original oil in place is zero or negative; the expansion terms are " +
                "expressed per stock-tank m³ of it");

        if (input.WithdrawnThisTick.CubicMetres < 0.0)
            throw new ModelFault("SDD-003 §3.1", null,
                $"withdrawal this tick is {Format(input.WithdrawnThisTick.CubicMetres)} " +
                "reservoir m³; negative withdrawal is injection and belongs in CumulativeInjected");
    }

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}
