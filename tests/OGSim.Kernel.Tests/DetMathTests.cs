// R1.1 — DetMath. Two things are being proved here and they are not the same:
//
//   ACCURACY  — DetMath agrees with the platform libm to within the SDD-001 §1.3
//               budget of 2 ulp. System.Math is a legitimate ORACLE for this;
//               it is banned as an implementation, not as a reference.
//   DOMAIN    — out-of-range input raises a classified ModelFault and never
//               returns NaN, so a bad correlation input cannot travel silently.
//
// Cross-platform BIT-identity is the third property and is not testable in one
// process. It follows by construction (only IEEE-754 basic operations and
// Math.Sqrt, all exactly specified) and is pinned mechanically by the R19
// determinism digest across the CI matrix, per SDD-000 §6 gate 5.

using OGSim.Kernel;

namespace OGSim.Kernel.Tests;

public class DetMathTests
{
    /// <summary>Distance in representable doubles — the only honest way to state
    /// a floating-point accuracy budget.</summary>
    private static long UlpDifference(double a, double b)
    {
        if (a == b) return 0;
        long left = BitConverter.DoubleToInt64Bits(a);
        long right = BitConverter.DoubleToInt64Bits(b);
        if (left < 0) left = long.MinValue - left;
        if (right < 0) right = long.MinValue - right;
        return Math.Abs(left - right);
    }

    private const long UlpBudget = 2; // SDD-001 §1.3

    // ------------------------------------------------------------- exact identities

    [Fact] // SDD-001 §1.3: the anchors are exact, not merely close
    public void MX7_DetMath_anchors_are_exact()
    {
        Assert.Equal(1.0, DetMath.Exp(0.0));
        Assert.Equal(0.0, DetMath.Ln(1.0));
        Assert.Equal(2.0, DetMath.Sqrt(4.0));
        Assert.Equal(1.0, DetMath.Pow(7.3, 0.0));
        Assert.Equal(7.3, DetMath.Pow(7.3, 1.0));
        Assert.Equal(1.0, DetMath.Pow(1.0, 12345.0));
    }

    [Fact] // Integral powers route around the log entirely, so they stay exact
    public void MX7_DetMath_integral_powers_are_exact()
    {
        Assert.Equal(1024.0, DetMath.Pow(2.0, 10.0));
        Assert.Equal(9.0, DetMath.Pow(3.0, 2.0));
        Assert.Equal(0.25, DetMath.Pow(2.0, -2.0));
        Assert.Equal(-8.0, DetMath.Pow(-2.0, 3.0));   // odd exponent keeps the sign
        Assert.Equal(16.0, DetMath.Pow(-2.0, 4.0));   // even exponent drops it
    }

    // ------------------------------------------------------------- accuracy

    [Fact] // Exp across the full representable range, not just the comfortable part
    public void MX7_Exp_matches_the_oracle_within_budget()
    {
        double worst = 0;
        for (double x = -700.0; x <= 700.0; x += 0.37)
        {
            long ulps = UlpDifference(DetMath.Exp(x), Math.Exp(x));
            if (ulps > worst) worst = ulps;
            Assert.True(ulps <= UlpBudget, $"Exp({x}) differs by {ulps} ulp");
        }
        Assert.True(worst <= UlpBudget);
    }

    [Fact] // Ln over ten decades either side of 1 — the reservoir/PVT working span
    public void MX7_Ln_matches_the_oracle_within_budget()
    {
        for (double exponent = -10.0; exponent <= 10.0; exponent += 0.13)
        {
            double x = Math.Pow(10.0, exponent);
            long ulps = UlpDifference(DetMath.Ln(x), Math.Log(x));
            Assert.True(ulps <= UlpBudget, $"Ln({x}) differs by {ulps} ulp");
        }
    }

    [Fact] // Ln is most delicate next to 1, where the leading term cancels
    public void MX7_Ln_is_accurate_near_one()
    {
        double[] nearOne = [0.5, 0.9, 0.99, 0.999, 1.001, 1.01, 1.1, 1.5, 1.9999, 2.0];
        foreach (double x in nearOne)
        {
            long ulps = UlpDifference(DetMath.Ln(x), Math.Log(x));
            Assert.True(ulps <= UlpBudget, $"Ln({x}) differs by {ulps} ulp");
        }
    }

    [Fact] // Pow is why Ln is carried in double-double: the error is scaled by y
    public void MX7_Pow_matches_the_oracle_within_budget()
    {
        double[] bases = [0.001, 0.1, 0.5, 1.5, 2.0, 7.0, 60.0, 1000.0, 1.0e6];
        double[] exponents = [-5.0, -2.5, -1.0, -0.25, 0.25, 0.5, 1.5, 2.5, 5.0];
        foreach (double b in bases)
            foreach (double y in exponents)
            {
                long ulps = UlpDifference(DetMath.Pow(b, y), Math.Pow(b, y));
                Assert.True(ulps <= UlpBudget, $"Pow({b}, {y}) differs by {ulps} ulp");
            }
    }

    [Fact] // Subnormal input exercises the renormalisation branch in LnExtended
    public void MX7_Ln_handles_subnormal_input()
    {
        double subnormal = 5.0e-320;
        Assert.True(subnormal < 2.2250738585072014e-308);   // genuinely subnormal
        Assert.True(UlpDifference(DetMath.Ln(subnormal), Math.Log(subnormal)) <= UlpBudget);
    }

    /// <summary>
    /// The two functions must agree with each other, not only with the oracle.
    ///
    /// An ulp budget is the WRONG instrument for a round trip and asserting one
    /// here would be a test that lies: exp then ln is an ill-conditioned pair for
    /// small x. At x = 0.05, exp's final rounding is ~1e-16 absolute, while one
    /// ulp of 0.05 is ~7e-18 — so a perfect implementation still lands ~14 ulp
    /// out, and no correct code could pass. The bound below is the conditioning
    /// of the composition, which is what is actually being claimed.
    /// </summary>
    [Fact]
    public void MX7_Exp_and_Ln_round_trip_within_their_conditioning()
    {
        const double Eps = 2.220446049250313e-16;   // 2^-52
        const double Slack = 4.0;                   // a few roundings, not an order of magnitude

        for (double x = 0.05; x < 50.0; x += 0.37)
        {
            // ln(exp(x)): exp's relative error arrives as an ABSOLUTE error here.
            double back = DetMath.Ln(DetMath.Exp(x));
            Assert.True(Math.Abs(back - x) <= Slack * Eps * (1.0 + x),
                $"Ln(Exp({x})) = {back}");

            // exp(ln(x)): ln's absolute error is amplified by exp in proportion
            // to |ln x|, so the tolerance carries that factor explicitly.
            double forward = DetMath.Exp(DetMath.Ln(x));
            Assert.True(Math.Abs(forward - x) <= Slack * Eps * x * (1.0 + Math.Abs(Math.Log(x))),
                $"Exp(Ln({x})) = {forward}");
        }
    }

    // ------------------------------------------------------------- domain

    [Fact] // SDD-001 §1.3 / §11: x <= 0 is a MODEL fault, never a NaN
    public void R1V10_Ln_of_a_non_positive_argument_is_a_model_fault()
    {
        var zero = Assert.Throws<ModelFault>(() => DetMath.Ln(0.0));
        Assert.Equal(FaultClass.Model, zero.Fault.Class);

        var negative = Assert.Throws<ModelFault>(() => DetMath.Ln(-1.0));
        Assert.Equal(FaultClass.Model, negative.Fault.Class);
        Assert.Contains("SDD-001", negative.Fault.Rule);

        Assert.Throws<ModelFault>(() => DetMath.Ln(double.NaN));
        Assert.Throws<ModelFault>(() => DetMath.Ln(double.PositiveInfinity));
    }

    [Fact] // Overflow is a fault; underflow is zero, which is a true answer
    public void R1V10_Exp_overflow_faults_and_underflow_is_zero()
    {
        var overflow = Assert.Throws<ModelFault>(() => DetMath.Exp(710.0));
        Assert.Equal(FaultClass.Model, overflow.Fault.Class);

        Assert.Equal(0.0, DetMath.Exp(-800.0));
        Assert.Throws<ModelFault>(() => DetMath.Exp(double.NaN));
    }

    [Fact] // A negative base is real only at integral exponents
    public void R1V10_Pow_rejects_a_negative_base_with_a_fractional_exponent()
    {
        var fault = Assert.Throws<ModelFault>(() => DetMath.Pow(-8.0, 1.0 / 3.0));
        Assert.Equal(FaultClass.Model, fault.Fault.Class);

        Assert.Throws<ModelFault>(() => DetMath.Pow(0.0, -1.0));   // unbounded
        Assert.Equal(0.0, DetMath.Pow(0.0, 2.0));                  // bounded, and true
    }

    [Fact] // Sqrt is the one delegated function and still owns its domain
    public void R1V10_Sqrt_rejects_a_negative_argument()
    {
        Assert.Throws<ModelFault>(() => DetMath.Sqrt(-1.0));
        Assert.Throws<ModelFault>(() => DetMath.Sqrt(double.NaN));
        Assert.Equal(0.0, DetMath.Sqrt(0.0));
    }

    [Fact] // Nothing DetMath returns may be NaN — INV6 depends on it
    public void R1V10_no_result_is_ever_NaN()
    {
        for (double x = -600.0; x <= 600.0; x += 7.3)
            Assert.False(double.IsNaN(DetMath.Exp(x)));

        for (double exponent = -8.0; exponent <= 8.0; exponent += 0.7)
        {
            double x = Math.Pow(10.0, exponent);
            Assert.False(double.IsNaN(DetMath.Ln(x)));
            Assert.False(double.IsNaN(DetMath.Pow(x, 1.7)));
        }
    }

    // ------------------------------------------------------------- purity

    [Fact] // No hidden state: the same argument yields the identical BITS, always
    public void R1V6_repeated_calls_return_identical_bits()
    {
        // Probes are per-function: exp's domain stops at ~709, so a value that is
        // perfectly ordinary for Ln or Pow is an overflow fault for Exp.
        double[] expProbes = [-708.0, -1.0, 0.017, 1.0, 88.7, 709.0];
        double[] positiveProbes = [1.0e-300, 0.017, 1.0, 2.718281828459045, 137.035, 6.02e5];

        foreach (double x in expProbes)
        {
            long bits = BitConverter.DoubleToInt64Bits(DetMath.Exp(x));
            for (int repeat = 0; repeat < 4; repeat++)
                Assert.Equal(bits, BitConverter.DoubleToInt64Bits(DetMath.Exp(x)));
        }

        foreach (double x in positiveProbes)
        {
            long lnBits = BitConverter.DoubleToInt64Bits(DetMath.Ln(x));
            long powBits = BitConverter.DoubleToInt64Bits(DetMath.Pow(x, 2.5));
            long sqrtBits = BitConverter.DoubleToInt64Bits(DetMath.Sqrt(x));
            for (int repeat = 0; repeat < 4; repeat++)
            {
                Assert.Equal(lnBits, BitConverter.DoubleToInt64Bits(DetMath.Ln(x)));
                Assert.Equal(powBits, BitConverter.DoubleToInt64Bits(DetMath.Pow(x, 2.5)));
                Assert.Equal(sqrtBits, BitConverter.DoubleToInt64Bits(DetMath.Sqrt(x)));
            }
        }
    }
}
