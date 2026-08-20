# GasMaterialBalance

Source: `src\OGSim.Subsurface\GasMaterialBalance.cs` · Lines: 138

## File intent

> R5.7 — the gas material balance and the p/Z line (SDD-003 §3.1b, design 05 §3.2).
> 
> §3.1's oil balance expresses every expansion term per stock-tank m³ of
> original oil, so a dry gas reservoir (N = 0) has no root there at all. This is
> the gas form, and it is a different equation rather than a special case of the
> other one.
> 
> Design 05 §3.2 calls p/Z "the single best information mechanic in the game":

## Namespaces

- `OGSim.Subsurface`

## Type declarations

- `L24` `internal static class GasMaterialBalance`

## Accessible members

- `L26` `private const double BracketFloorPa = 1_000.0;`
- `L27` `private const int BisectionIterations = 80;`
- `L28` `private const double PressureTolerancePa = 100.0;`
- `L39` `internal static double PressureOverZ(`
- `L78` `internal static Pressure Solve(`
- `L136` `private static string Format(double value) =>`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

