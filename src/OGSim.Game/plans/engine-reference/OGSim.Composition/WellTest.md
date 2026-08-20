# WellTest

Source: `src\OGSim.Composition\WellTest.cs` · Lines: 75

## File intent

> R12b — flow a well and watch the pressure build back up (SDD-008 §3, design 06 §3).
> 
> The sharpest measurement of a compartment there is: it watches the reservoir
> answer for itself over days, which is why it beats a log on permeability and
> is the only source that can see pressure at all. It is also the reason a player
> would ever stop producing on purpose — the well is shut in for the build-up, so
> the test costs the month's oil as well as its own price.
> <summary>Shut a well in and measure the compartment behind it.</summary>

## Namespaces

- `OGSim.Composition`

## Type declarations

- `L15` `public sealed record WellTestCommand(`
- `L18` `internal sealed class WellTestActivity(`

## Accessible members

- `L28` `public override bool LeavesAnAsset => false;`
- `L30` `public override bool OnePerTarget => true;`
- `L32` `public override (EntityRef Target, Length Depth) Aim(WellTestCommand command)`
- `L39` `public override IReadOnlyList<RejectionReason> OwnRefusals(WellTestCommand command)`
- `L55` `public override void Complete(CompletedActivity done, Tick tick)`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

