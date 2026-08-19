# DriveMechanisms

Source: `src\OGSim.Subsurface\DriveMechanisms.cs` · Lines: 195

## File intent

> R5.4 — the six shipped drive mechanisms (design 02 §2.2, SDD-003 §4.2/§4.2b).
> 
> A plugin deliberately. Recovery factor is never stored anywhere: it is
> whatever the material balance and the mechanism produce together (R5 G2),
> which is what makes identifying your drive worth doing, and what makes
> waterflood (R10) and gas injection (R9) ADDITIONS rather than edits to these.
> 
> What distinguishes them is WHICH TERMS OF §3.1's BALANCE THEY ADMIT (§4.2b).

## Namespaces

- `OGSim.Subsurface`

## Type declarations

- `L20` `internal abstract class DriveMechanism : IDriveMechanism`
- `L103` `internal sealed class SolutionGasDrive()`
- `L110` `internal sealed class GasCapExpansionDrive()`
- `L121` `internal sealed class WaterDrive()`
- `L137` `internal sealed class CompactionDrive()`
- `L149` `internal sealed class GravityDrainageDrive()`
- `L164` `internal sealed class CombinationDrive()`
- `L185` `internal sealed class WaterfloodDrive()`

## Accessible members

- `L23` `protected const double DefaultMaxTickVoidageFraction = 0.25;`
- `L35` `protected DriveMechanism(`
- `L46` `public ContentId Id { get; }`
- `L48` `public AdmittedTerms Admits { get; }`
- `L50` `public IReadOnlyList<ContentId> AcceptedInjectants { get; }`
- `L52` `protected virtual double MaxTickVoidageFraction => DefaultMaxTickVoidageFraction;`
- `L54` `public Pressure SolveEndPressure(MaterialBalanceInput input, IFluidPropertyModel fluid)`
- `L72` `private void AssertCoherent(MaterialBalanceInput input)`
- `L90` `private static string Format(double value) =>`
- `L126` `protected override double MaxTickVoidageFraction => 0.4;`
- `L152` `protected override double MaxTickVoidageFraction => 0.1;`
- `L193` `protected override double MaxTickVoidageFraction => 0.4;`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

