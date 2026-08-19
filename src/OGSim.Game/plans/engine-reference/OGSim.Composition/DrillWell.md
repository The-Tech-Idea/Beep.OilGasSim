# DrillWell

Source: `src\OGSim.Composition\DrillWell.cs` · Lines: 167

## File intent

> R12b — drill and complete a well (SDD-007, design 20's D-catalogue).
> 
> The first decision a player makes and the one every other decision waits on:
> it is the only activity that leaves an asset behind, the only one that can
> come back dry, and the only one whose failure costs a fortune.
> <summary>Drill and complete a well on a known compartment.</summary>
> <summary>
> Drill a PROSPECT — a closed structure, which may or may not hold anything

## Namespaces

- `OGSim.Composition`

## Type declarations

- `L22` `public sealed record DrillWellCommand(`
- `L31` `internal delegate OGSim.Wells.Completion WellDesign(`
- `L34` `internal sealed class DrillWellActivity(`

## Accessible members

- `L45` `public override bool LeavesAnAsset => true;`
- `L53` `public override bool OnePerTarget => false;`
- `L55` `public override (EntityRef Target, Length Depth) Aim(DrillWellCommand command)`
- `L62` `public override IReadOnlyList<RejectionReason> OwnRefusals(DrillWellCommand command)`
- `L91` `public override void Complete(CompletedActivity done, Tick tick)`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

