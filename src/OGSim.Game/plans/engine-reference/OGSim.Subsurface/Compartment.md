# Compartment

Source: `src\OGSim.Subsurface\Compartment.cs` · Lines: 136

## File intent

> R5.1 — the compartment: the simulated unit and the whole of subsurface truth
> (SDD-003 §3, design 02 §2.1).
> 
> EVERYTHING IN THIS FILE IS INTERNAL, and that is the phase's most important
> deliverable after the material balance itself. The player's belief about a
> reservoir is the game; if any consumer could read Pr directly, every
> exploration and appraisal decision downstream would be theatre. The assembly
> boundary is what makes that impossible rather than merely discouraged

## Namespaces

- `OGSim.Subsurface`

## Type declarations

- `L31` `internal readonly record struct ContactSet(`
- `L35` `internal readonly record struct RockTruth(`
- `L58` `internal readonly record struct CompartmentLink(`
- `L67` `internal sealed record InitialConditions(`
- `L96` `internal interface IReservoirCompartment`
- `L117` `internal readonly record struct CumulativeProduction(`

## Accessible members

- `L124` `public static CumulativeProduction None { get; } = new(`
- `L128` `public CumulativeProduction Plus(`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`
- `using InPlace = OGSim.Kernel.MaterialInventory;`

