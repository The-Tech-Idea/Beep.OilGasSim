# OperatingPoint

Source: `src\OGSim.Wells\OperatingPoint.cs` · Lines: 158

## File intent

> R6.7 — the operating point (SDD-003 §6.3). The phase's centre.
> 
> IPR and VLP are both functions of bottomhole flowing pressure, and the well
> produces where they cross. NO INTERSECTION MEANS THE WELL DOES NOT FLOW, and
> that is reported as DEAD — a distinct outcome, never a rate of zero.
> 
> The distinction is not pedantry. "This well produced nothing this tick" and
> "this well cannot flow at any rate" have different remedies: the first is a

## Namespaces

- `OGSim.Wells`

## Type declarations

- `L22` `internal static class OperatingPointSolver`

## Accessible members

- `L24` `private const int Iterations = 64;              // §6.3, pinned`
- `L25` `private const double TolerancePa = 500.0;`
- `L37` `internal static OperatingPoint Solve(`
- `L120` `private static double RateDemanding(`
- `L152` `private const double RateFloor = 1e-8;           // SDD-002 §7 S5's q_floor`
- `L153` `private const double RateBracketSlack = 4.0;`
- `L154` `private const int RateBracketGrowths = 8;`
- `L156` `private static string Format(double value) =>`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

