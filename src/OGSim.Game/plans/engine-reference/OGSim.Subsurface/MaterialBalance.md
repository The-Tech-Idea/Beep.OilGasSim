# MaterialBalance

Source: `src\OGSim.Subsurface\MaterialBalance.cs` · Lines: 237

## File intent

> R5.3 — the tank material balance (SDD-003 §3.1, design 05 §3.1).
> 
> This is the one place the game's central number is produced. Pressure falls
> because the remaining fluid and rock cannot quite fill the space left by what
> was taken — never because a decline curve said so (R5 G1). Recovery factor is
> then whatever this arithmetic and the drive mechanism produce together, which
> is what makes identifying the drive worth doing (G2).
> 

## Namespaces

- `OGSim.Subsurface`

## Type declarations

- `L22` `internal static class MaterialBalance`

## Accessible members

- `L26` `private static readonly Pressure BracketFloor = new(1_000.0);`
- `L28` `private const int BisectionIterations = 80;      // §3.1, pinned`
- `L29` `private const double PressureTolerancePa = 100.0;`
- `L42` `internal static double Withdrawal(MaterialBalanceInput input, IFluidPropertyModel fluid, Pressure p)`
- `L60` `internal static double Expansion(MaterialBalanceInput input, IFluidPropertyModel fluid, Pressure p)`
- `L89` `internal static double Residual(MaterialBalanceInput input, IFluidPropertyModel fluid, Pressure p) =>`
- `L104` `internal static Pressure Solve(`
- `L116` `private static Pressure FindRoot(MaterialBalanceInput input, IFluidPropertyModel fluid)`
- `L195` `private static void AssertStepIsHonest(`
- `L211` `private static void Validate(`
- `L235` `private static string Format(double value) =>`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

