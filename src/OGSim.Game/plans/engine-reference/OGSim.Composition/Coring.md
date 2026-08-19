# Coring

Source: `src\OGSim.Composition\Coring.cs` · Lines: 68

## File intent

> R12b — cut a core and measure the rock itself (SDD-008 §3, design 05 §4).
> 
> The same two properties a log reads, an order of magnitude sharper and several
> times the price, because the laboratory has the rock in its hands instead of a
> tool's inference about it.
> 
> Log against core is the cheap-and-fuzzy against dear-and-sharp decision in its
> purest form, and it is a real one: the sigma floor (INV8) means a core cannot

## Namespaces

- `OGSim.Composition`

## Type declarations

- `L17` `public sealed record CutCoreCommand(`
- `L20` `internal sealed class CoringActivity(`

## Accessible members

- `L31` `public override bool LeavesAnAsset => false;`
- `L33` `public override bool OnePerTarget => true;`
- `L35` `public override (EntityRef Target, Length Depth) Aim(CutCoreCommand command)`
- `L42` `public override IReadOnlyList<RejectionReason> OwnRefusals(CutCoreCommand command)`
- `L54` `public override void Complete(CompletedActivity done, Tick tick)`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

