# WaterCut

Source: `src\OGSim.Subsurface\WaterCut.cs` · Lines: 56

## File intent

> R10.5 — water cut, by fractional flow (SDD-003 §3.1c, CAL3).
> 
> THE S-CURVE IS NOT A SHAPE THAT IS DRAWN. It is what fractional flow does when
> the relative permeabilities are power laws, which is the standard Corey
> treatment. A fitted sigmoid would have produced the same picture and taught
> nobody anything, because it would not respond to viscosity — and the whole
> reason a viscous oil waters out early is the mobility ratio in the denominator.
> 

## Namespaces

- `OGSim.Subsurface`

## Type declarations

- `L19` `internal static class FractionalFlow`

## Accessible members

- `L28` `public static double WaterCut(`
- `L53` `public static double MobilityRatio(`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

