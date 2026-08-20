# AbandonWell

Source: `src\OGSim.Composition\AbandonWell.cs` · Lines: 101

## File intent

> R12b.10 — abandonment (SDD-007 §6, design 02 §3.4).
> 
> THE ENDING. A field's arc is build-up, plateau, decline and then a long tail
> producing almost nothing while the standing charge eats the cash it made — and
> until this a player could watch that happen for thirty years and had no way to
> stop paying for it. Shutting a well in stops what it costs to lift; only
> abandoning it ends the liability and lets the field close.
> 

## Namespaces

- `OGSim.Composition`

## Type declarations

- `L24` `public sealed record AbandonWellCommand(`
- `L27` `internal sealed class AbandonWellActivity(`

## Accessible members

- `L38` `public override bool LeavesAnAsset => false;`
- `L40` `public override bool OnePerTarget => true;`
- `L42` `public override (EntityRef Target, Length Depth) Aim(AbandonWellCommand command)`
- `L49` `public override IReadOnlyList<RejectionReason> OwnRefusals(AbandonWellCommand command)`
- `L81` `public override void Complete(CompletedActivity done, Tick tick)`
- `L99` `private static EntityRef Asset(EntityId<ICompletion> well) =>`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

