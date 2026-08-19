# ExpandExport

Source: `src\OGSim.Composition\ExpandExport.cs` · Lines: 81

## File intent

> R20d.8 — expanding export (SDD-006 §7b, SDD-007 §5).
> 
> THE VERB THAT MAKES A BIG FIELD WORTH MORE THAN A SMALL ONE. Until this, the
> export line took what it took and the accumulation underneath was irrelevant:
> ten times the oil earned the same money over twenty years, because both fields
> spent every month against one constant.
> 
> A JUDGEMENT MADE ON BELIEFS, which is the whole reason this is a command and

## Namespaces

- `OGSim.Composition`

## Type declarations

- `L30` `public sealed record ExpandExportCommand() : Command(Subject: null);`
- `L32` `internal sealed class ExpandExportActivity(`

## Accessible members

- `L38` `public override bool LeavesAnAsset => true;`
- `L40` `public override bool OnePerTarget => true;`
- `L42` `public override (EntityRef Target, Length Depth) Aim(ExpandExportCommand command) =>`
- `L45` `public override IReadOnlyList<RejectionReason> OwnRefusals(ExpandExportCommand command)`
- `L58` `public override void Complete(CompletedActivity done, Tick tick)`
- `L74` `private OGSim.Facilities.ExportTier? NextRung()`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

