# DetMath

Source: `src\OGSim.Kernel\DetMath.cs` · Lines: 432

## File intent

> SDD-001 §1.3 — deterministic transcendentals, the concrete answer to rule D-2
> (SDD-000 §3): System.Math.Exp/Log/Pow route to the platform's libm and are NOT
> guaranteed bit-identical across OS or architecture, so a save digest computed
> on windows-x64 would not match linux-arm64. Everything below is built from
> IEEE-754 basic operations (+ - * /), comparisons, and bit manipulation — all
> of which ARE exactly specified — so the result is the same bits everywhere.
> 
> Math.Sqrt is the one deliberate exception: IEEE-754 requires it to be

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L22` `public static class DetMath`
- `L392` `public static class Friction`

## Accessible members

- `L28` `private const double Ln2Hi = 6.93147180369123816490e-01;`
- `L31` `private const double Ln2Lo = 1.90821492927058770002e-10;`
- `L33` `private const double InvLn2 = 1.44269504088896338700e+00;`
- `L34` `private const double Sqrt2 = 1.41421356237309504880e+00;`
- `L38` `private const double ExpOverflow = 7.09782712893383973096e+02;`
- `L42` `private const double ExpUnderflow = -7.45133219101941108420e+02;`
- `L45` `private const double SplitConstant = 134217729.0;`
- `L50` `private const double SplitLimit = 1.3e292;`
- `L53` `private const double Pow2_54 = 18014398509481984.0;`
- `L56` `private const double Pow2Minus1022 = 2.2250738585072014e-308;`
- `L59` `private const double Pow2_53 = 9007199254740992.0;`
- `L63` `private const double IntegralPowLimit = 64.0;`
- `L65` `private const long MantissaMask = 0x000FFFFFFFFFFFFFL;`
- `L66` `private const long ExponentOne = 0x3FF0000000000000L;`
- `L67` `private const int ExponentBias = 1023;`
- `L72` `public static double Sqrt(double x)`
- `L80` `public static double Exp(double x)`
- `L106` `public static double Ln(double x)`
- `L117` `public static double Pow(double x, double y)`
- `L189` `private static double ExpSeries(double r)`
- `L215` `private static double LnSeriesTail(double z)`
- `L236` `private static void LnExtended(double x, out double hi, out double lo)`
- `L295` `private static void Split(double a, out double hi, out double lo)`
- `L303` `private static void TwoProduct(double a, double b, out double product, out double error)`
- `L312` `private static void TwoSum(double a, double b, out double sum, out double error)`
- `L321` `private static double Scale2(double value, int k)`
- `L342` `private static double IntegralPow(double x, int exponent)`
- `L365` `private static bool IsOddInteger(double y)`
- `L372` `private static string Describe(double value) =>`
- `L394` `private const int NewtonSteps = 20;`
- `L395` `private const double Seed = 0.02;`
- `L400` `public const double LaminarLimit = 2300.0;`
- `L402` `public static double Factor(double reynolds, double relativeRoughness)`

