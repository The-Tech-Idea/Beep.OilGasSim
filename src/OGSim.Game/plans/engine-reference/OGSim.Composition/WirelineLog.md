# WirelineLog

Source: `src\OGSim.Composition\WirelineLog.cs` · Lines: 67

## File intent

> R12b — run logs in a wellbore (SDD-008 §3, design 05 §4).
> 
> Cheap, quick, and the first thing done in any new hole. It reads the rock at
> the wellbore: porosity sharply, permeability only through a transform and
> therefore badly, and the size of the accumulation not at all — a log sees one
> point, and no number of points tells you how far the oil extends.
> 
> That last absence is the whole reason seismic exists as a separate activity.

## Namespaces

- `OGSim.Composition`

## Type declarations

- `L16` `public sealed record WirelineLogCommand(`
- `L19` `internal sealed class WirelineLogActivity(`

## Accessible members

- `L29` `public override bool LeavesAnAsset => false;`
- `L31` `public override bool OnePerTarget => true;`
- `L33` `public override (EntityRef Target, Length Depth) Aim(WirelineLogCommand command)`
- `L40` `public override IReadOnlyList<RejectionReason> OwnRefusals(WirelineLogCommand command)`
- `L53` `public override void Complete(CompletedActivity done, Tick tick)`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

