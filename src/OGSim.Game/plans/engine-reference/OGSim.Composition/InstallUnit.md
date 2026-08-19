# InstallUnit

Source: `src\OGSim.Composition\InstallUnit.cs` · Lines: 98

## File intent

> R12b.8 — install / construct (SDD-006 §0c, SDD-007 §5, catalogue C06/C07).
> 
> THE VERB THAT ANSWERS A BOTTLENECK. Every other activity either learns
> something or makes a hole; this is the one that changes what the field can
> carry. A player watches the separator refuse production on the read model,
> reads how much it is costing them a month, and decides whether a bigger vessel
> is worth its price and the months it takes.
> 

## Namespaces

- `OGSim.Composition`

## Type declarations

- `L34` `public sealed record InstallSeparatorCommand() : Command(Subject: null);`
- `L36` `internal sealed class InstallSeparatorActivity(`

## Accessible members

- `L43` `public override bool LeavesAnAsset => true;`
- `L49` `public override bool OnePerTarget => true;`
- `L51` `public override (EntityRef Target, Length Depth) Aim(InstallSeparatorCommand command) =>`
- `L54` `public override IReadOnlyList<RejectionReason> OwnRefusals(InstallSeparatorCommand command)`
- `L67` `public override void Complete(CompletedActivity done, Tick tick)`
- `L91` `private OGSim.Facilities.SeparatorTier? NextRung()`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

