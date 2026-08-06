// SDD-001 §1.3 — deterministic transcendentals, the concrete answer to rule D-2
// (SDD-000 §3): System.Math.Exp/Log/Pow route to the platform's libm and are NOT
// guaranteed bit-identical across OS or architecture, so a save digest computed
// on windows-x64 would not match linux-arm64. Everything below is built from
// IEEE-754 basic operations (+ - * /), comparisons, and bit manipulation — all
// of which ARE exactly specified — so the result is the same bits everywhere.
//
// Math.Sqrt is the one deliberate exception: IEEE-754 requires it to be
// correctly rounded, so it is portable by specification (SDD-001 §1.3).
//
// TWO RULE EXEMPTIONS LIVE HERE, both to be encoded in the R1.12 analyzer:
//   D-2 already names DetMath as the sole legal site for System.Math.
//   F-2 (no numeric literal but 0 and 1) cannot apply to this file: the
//   polynomial coefficients below ARE the definition of the functions, not
//   tunable model parameters. Each carries the term it represents.
//
// Accuracy target: ≤ 2 ulp over the correlation input ranges of design 05,
// pinned by the MX-class tests in OGSim.Kernel.Tests.

namespace OGSim.Kernel;

public static class DetMath
{
    // ---------------------------------------------------------------- constants

    /// <summary>ln 2, upper half: low 21 mantissa bits zero, so e·Ln2Hi is EXACT
    /// for every reachable binary exponent (|e| &lt; 2^11; 32 + 11 ≤ 53 bits).</summary>
    private const double Ln2Hi = 6.93147180369123816490e-01;

    /// <summary>ln 2, remainder — restores the bits Ln2Hi drops.</summary>
    private const double Ln2Lo = 1.90821492927058770002e-10;

    private const double InvLn2 = 1.44269504088896338700e+00;
    private const double Sqrt2 = 1.41421356237309504880e+00;

    /// <summary>exp overflows the double range above this; a correlation reaching
    /// it has out-of-range inputs, which design 09 §5.1 C4 calls a model fault.</summary>
    private const double ExpOverflow = 7.09782712893383973096e+02;

    /// <summary>Below this exp is zero to every representable bit — a true value,
    /// not a failure, so it is returned rather than raised.</summary>
    private const double ExpUnderflow = -7.45133219101941108420e+02;

    /// <summary>2^27 + 1 — Veltkamp's splitting constant.</summary>
    private const double SplitConstant = 134217729.0;

    /// <summary>Above this a Veltkamp split would overflow, so the double-double
    /// path in <see cref="Pow"/> is not available. No correlation has an exponent
    /// remotely this large; reaching it is out-of-range input.</summary>
    private const double SplitLimit = 1.3e292;

    /// <summary>2^54 — renormalises a subnormal before the exponent is read.</summary>
    private const double Pow2_54 = 18014398509481984.0;

    /// <summary>2^-1022 — the smallest normal, for the two-step scale below.</summary>
    private const double Pow2Minus1022 = 2.2250738585072014e-308;

    /// <summary>2^53 — at and above this every double is an even integer.</summary>
    private const double Pow2_53 = 9007199254740992.0;

    /// <summary>Largest |n| taken through exact repeated squaring in Pow. Kept
    /// small so the accumulated roundings stay inside the §1.3 ulp budget.</summary>
    private const double IntegralPowLimit = 64.0;

    private const long MantissaMask = 0x000FFFFFFFFFFFFFL;
    private const long ExponentOne = 0x3FF0000000000000L;
    private const int ExponentBias = 1023;

    // ---------------------------------------------------------------- public

    /// <summary>IEEE-754 requires correct rounding, so this is portable as-is.</summary>
    public static double Sqrt(double x)
    {
        if (double.IsNaN(x) || x < 0.0)
            throw new ModelFault("SDD-001 §1.3", null, $"Sqrt domain: x = {Describe(x)}, requires x >= 0.");
        return Math.Sqrt(x);
    }

    /// <summary>e^x. Overflow is a model fault; underflow is zero, which is true.</summary>
    public static double Exp(double x)
    {
        if (double.IsNaN(x))
            throw new ModelFault("SDD-001 §1.3", null, "Exp domain: x is NaN.");
        if (x > ExpOverflow)
            throw new ModelFault("SDD-001 §1.3", null,
                $"Exp overflow: x = {Describe(x)} exceeds {Describe(ExpOverflow)}.");
        if (x < ExpUnderflow)
            return 0.0;
        if (x == 0.0)
            return 1.0;

        // Range reduction: x = k·ln2 + r with |r| ≤ ln2/2, so the series below
        // only ever sees a small argument and 2^k carries all the magnitude.
        double scaled = x * InvLn2;
        int k = (int)(scaled >= 0.0 ? scaled + 0.5 : scaled - 0.5);
        double r = (x - k * Ln2Hi) - k * Ln2Lo;

        return Scale2(ExpSeries(r), k);
    }

    /// <summary>
    /// Natural logarithm. x ≤ 0 is a domain fault, never NaN (SDD-001 §1.3) —
    /// Ln(-1) inside a correlation means the inputs are wrong, and a NaN would
    /// travel silently until it poisoned a commit.
    /// </summary>
    public static double Ln(double x)
    {
        LnExtended(x, out double hi, out double lo);
        return hi + lo;
    }

    /// <summary>
    /// x^y as exp(y·ln x), with ln x and the product carried in double-double —
    /// the naive single-double form loses ~|y·ln x| ulp, which is exactly the
    /// range where correlation exponents live.
    /// </summary>
    public static double Pow(double x, double y)
    {
        if (double.IsNaN(x) || double.IsNaN(y))
            throw new ModelFault("SDD-001 §1.3", null, "Pow domain: NaN operand.");

        // Exact cases first — both for speed and because y = 2 through the log
        // path would return x·x only to within an ulp.
        if (y == 0.0) return 1.0;
        if (y == 1.0) return x;
        if (x == 1.0) return 1.0;
        if (y == 2.0) return x * x;

        if (double.IsInfinity(x) || double.IsInfinity(y))
            throw new ModelFault("SDD-001 §1.3", null,
                $"Pow domain: infinite operand (x = {Describe(x)}, y = {Describe(y)}).");

        if (x == 0.0)
        {
            if (y > 0.0) return 0.0;
            throw new ModelFault("SDD-001 §1.3", null,
                $"Pow domain: 0 raised to {Describe(y)} is unbounded.");
        }

        // Integral exponents go through binary exponentiation rather than the
        // log path. It is EXACT whenever the result and every intermediate are
        // representable — (-2)^4 is 16, not 15.999999999999998 — which exp(y·ln x)
        // cannot promise at any working precision. Correlations and the p/Z
        // deduction use small integer exponents constantly, and a result that is
        // exact on one path and an ulp short on another is a reproducibility trap.
        if (y == Math.Truncate(y) && y >= -IntegralPowLimit && y <= IntegralPowLimit)
            return IntegralPow(x, (int)y);

        double sign = 1.0;
        if (x < 0.0)
        {
            // A negative base is real only at integral exponents; anything else
            // is a complex result the engine has no type for.
            if (y != Math.Truncate(y))
                throw new ModelFault("SDD-001 §1.3", null,
                    $"Pow domain: negative base {Describe(x)} with non-integral exponent {Describe(y)}.");
            if (IsOddInteger(y)) sign = -1.0;
            x = -x;
        }

        if (y > SplitLimit || y < -SplitLimit)
            throw new ModelFault("SDD-001 §1.3", null,
                $"Pow exponent {Describe(y)} is beyond the exact-splitting range.");

        LnExtended(x, out double lnHi, out double lnLo);

        // y·ln(x) in double-double: the whole point of the extended log.
        TwoProduct(y, lnHi, out double productHi, out double productErr);
        TwoSum(productHi, productErr + y * lnLo, out double wHi, out double wLo);

        if (wHi > ExpOverflow)
            throw new ModelFault("SDD-001 §1.3", null,
                $"Pow overflow: {Describe(x)}^{Describe(y)} exceeds the double range.");
        if (wHi < ExpUnderflow)
            return 0.0;

        // exp(wHi + wLo) = exp(wHi)·(1 + wLo + …); wLo is below an ulp of wHi,
        // so the dropped wLo²/2 term is under 1e-32 relative.
        return sign * Exp(wHi) * (1.0 + wLo);
    }

    // ---------------------------------------------------------------- series

    /// <summary>
    /// e^r for |r| ≤ ln2/2 by Maclaurin series, Horner from the tail. The
    /// truncated term is r^17/17! ≈ 3e-23 at the worst argument — far below the
    /// double's own resolution, so the series is not the limiting error.
    /// </summary>
    private static double ExpSeries(double r)
    {
        double p = 4.779477332387385e-14;      // 1/16!
        p = p * r + 7.647163731819816e-13;     // 1/15!
        p = p * r + 1.1470745597729725e-11;    // 1/14!
        p = p * r + 1.6059043836821613e-10;    // 1/13!
        p = p * r + 2.08767569878681e-09;      // 1/12!
        p = p * r + 2.505210838544172e-08;     // 1/11!
        p = p * r + 2.755731922398589e-07;     // 1/10!
        p = p * r + 2.7557319223985893e-06;    // 1/9!
        p = p * r + 2.48015873015873e-05;      // 1/8!
        p = p * r + 1.984126984126984e-04;     // 1/7!
        p = p * r + 1.3888888888888889e-03;    // 1/6!
        p = p * r + 8.333333333333333e-03;     // 1/5!
        p = p * r + 4.1666666666666664e-02;    // 1/4!
        p = p * r + 1.6666666666666666e-01;    // 1/3!
        p = p * r + 5.0e-01;                   // 1/2!
        p = p * r + 1.0;                       // 1/1!
        p = p * r + 1.0;                       // 1/0!
        return p;
    }

    /// <summary>
    /// The atanh series tail: (P(z) − 1)/z where ln m = 2s·P(s²). Returned
    /// without its leading 1 so the caller can keep the dominant 2s term exact.
    /// </summary>
    private static double LnSeriesTail(double z)
    {
        double q = 4.0e-02;                    // 1/25
        q = q * z + 4.3478260869565216e-02;    // 1/23
        q = q * z + 4.7619047619047616e-02;    // 1/21
        q = q * z + 5.2631578947368418e-02;    // 1/19
        q = q * z + 5.8823529411764705e-02;    // 1/17
        q = q * z + 6.6666666666666666e-02;    // 1/15
        q = q * z + 7.6923076923076927e-02;    // 1/13
        q = q * z + 9.0909090909090912e-02;    // 1/11
        q = q * z + 1.1111111111111110e-01;    // 1/9
        q = q * z + 1.4285714285714285e-01;    // 1/7
        q = q * z + 2.0e-01;                   // 1/5
        q = q * z + 3.3333333333333331e-01;    // 1/3
        return q;
    }

    /// <summary>
    /// ln x as an unevaluated hi + lo pair (~106 bits). Pow needs this: the
    /// error in a single-double ln is multiplied by y before exp sees it.
    /// </summary>
    private static void LnExtended(double x, out double hi, out double lo)
    {
        if (double.IsNaN(x))
            throw new ModelFault("SDD-001 §1.3", null, "Ln domain: x is NaN.");
        if (x <= 0.0)
            throw new ModelFault("SDD-001 §1.3", null,
                $"Ln domain: x = {Describe(x)}, requires x > 0.");
        if (double.IsInfinity(x))
            throw new ModelFault("SDD-001 §1.3", null, "Ln domain: x is infinite.");

        long bits = BitConverter.DoubleToInt64Bits(x);
        int rawExponent = (int)((bits >> 52) & 0x7FFL);
        int exponentAdjust = 0;

        if (rawExponent == 0)
        {
            // Subnormal: renormalise so the exponent field is meaningful again.
            bits = BitConverter.DoubleToInt64Bits(x * Pow2_54);
            rawExponent = (int)((bits >> 52) & 0x7FFL);
            exponentAdjust = -54;
        }

        int e = rawExponent - ExponentBias + exponentAdjust;
        double m = BitConverter.Int64BitsToDouble((bits & MantissaMask) | ExponentOne);

        // Centre the mantissa on 1 so the series argument stays small: without
        // this, m ∈ [1,2) gives |s| up to 0.333 instead of 0.172.
        if (m > Sqrt2)
        {
            m *= 0.5;
            e += 1;
        }

        // m ∈ (√½, √2], so 1 lies within [m/2, 2m] and Sterbenz makes m − 1 EXACT.
        double f = m - 1.0;

        // s = f/(2+f) in double-double. 2+f is not exact, so it is carried as a
        // pair and the quotient is refined by its own residual.
        TwoSum(2.0, f, out double dHi, out double dLo);
        double sHi = f / dHi;
        TwoProduct(sHi, dHi, out double sProduct, out double sProductErr);
        double residual = ((f - sProduct) - sProductErr) - sHi * dLo;
        double sLo = residual / dHi;

        double z = sHi * sHi;
        // ln m = 2s + 2s·z·tail(z); the first term is exact (a scale by 2), the
        // second is a correction under 0.4% of it, so its rounding is harmless.
        double correction = 2.0 * sHi * z * LnSeriesTail(z);
        TwoSum(2.0 * sHi, correction, out double lnmHi, out double lnmErr);
        double lnmLo = lnmErr + 2.0 * sLo;

        // ln x = e·ln2 + ln m, keeping e·Ln2Hi (exact) apart from every rounding.
        TwoSum(e * Ln2Hi, lnmHi, out double sum, out double sumErr);
        TwoSum(sum, sumErr + lnmLo + e * Ln2Lo, out hi, out lo);
    }

    // ---------------------------------------------------------------- exact arithmetic

    /// <summary>Veltkamp split: a = hi + lo exactly, each with ≤ 26 significant bits.</summary>
    private static void Split(double a, out double hi, out double lo)
    {
        double c = SplitConstant * a;
        hi = c - (c - a);
        lo = a - hi;
    }

    /// <summary>Dekker product: a·b = product + error, exactly.</summary>
    private static void TwoProduct(double a, double b, out double product, out double error)
    {
        product = a * b;
        Split(a, out double aHi, out double aLo);
        Split(b, out double bHi, out double bLo);
        error = ((aHi * bHi - product) + aHi * bLo + aLo * bHi) + aLo * bLo;
    }

    /// <summary>Knuth's two-sum: a + b = sum + error, exactly, for any magnitudes.</summary>
    private static void TwoSum(double a, double b, out double sum, out double error)
    {
        sum = a + b;
        double bVirtual = sum - a;
        error = (a - (sum - bVirtual)) + (b - bVirtual);
    }

    /// <summary>value·2^k by exponent construction — exact, and portable in a way
    /// that a Math.Pow(2, k) multiply would not be.</summary>
    private static double Scale2(double value, int k)
    {
        if (k >= -ExponentBias + 1 && k <= ExponentBias)
            return value * BitConverter.Int64BitsToDouble((long)(k + ExponentBias) << 52);

        if (k > ExponentBias)
            throw new ModelFault("SDD-001 §1.3", null, $"Exp overflow at scale 2^{k}.");

        // Subnormal reach: two steps, because 2^k is not representable alone.
        int remainder = k + (ExponentBias - 1);
        if (remainder < -(ExponentBias - 1))
            return 0.0;
        return value * Pow2Minus1022
            * BitConverter.Int64BitsToDouble((long)(remainder + ExponentBias) << 52);
    }

    /// <summary>
    /// x^n by repeated squaring, for integral n. Exact where the arithmetic is
    /// exact; at most ⌈log₂ n⌉ roundings otherwise, which is why the limit is
    /// small — beyond it the log path's uniform accuracy is the better trade.
    /// </summary>
    private static double IntegralPow(double x, int exponent)
    {
        bool inverted = exponent < 0;
        int remaining = inverted ? -exponent : exponent;

        double result = 1.0;
        double square = x;
        while (remaining > 0)
        {
            if ((remaining & 1) != 0) result *= square;
            remaining >>= 1;
            if (remaining > 0) square *= square;
        }

        if (double.IsInfinity(result))
            throw new ModelFault("SDD-001 §1.3", null,
                $"Pow overflow: {Describe(x)}^{exponent} exceeds the double range.");

        return inverted ? 1.0 / result : result;
    }

    /// <summary>True when y is an odd integer. At 2^53 and above every double is
    /// even, which is what keeps this total without a big-integer path.</summary>
    private static bool IsOddInteger(double y)
    {
        if (y >= Pow2_53 || y <= -Pow2_53) return false;
        return ((long)y & 1L) != 0L;
    }

    /// <summary>Round-trip formatting for fault detail — invariant, per rule D-8.</summary>
    private static string Describe(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}
