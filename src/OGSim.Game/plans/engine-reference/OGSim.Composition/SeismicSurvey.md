# SeismicSurvey

Source: `src\OGSim.Composition\SeismicSurvey.cs` · Lines: 102

## File intent

> R12b — shoot 3-D seismic over a compartment (SDD-008 §3, design 05 §2).
> 
> THE ONLY ACTIVITY A COMPANY WITH NOTHING DRILLED CAN ORDER, and the reason the
> exploration game has a first move at all. It needs no wellbore and no rig, so
> it can be shot while the rig is turning elsewhere; what it buys is the one
> thing no downhole measurement can reach — how big the accumulation is.
> 
> It is also blunt. σ is wide, and the sigma floor (INV8) means it stays wide no

## Namespaces

- `OGSim.Composition`

## Type declarations

- `L26` `public sealed record SeismicSurveyCommand(`
- `L29` `internal sealed class SeismicSurveyActivity(`

## Accessible members

- `L42` `public override bool LeavesAnAsset => false;`
- `L44` `public override bool OnePerTarget => true;`
- `L46` `public override (EntityRef Target, Length Depth) Aim(SeismicSurveyCommand command)`
- `L59` `public override IReadOnlyList<RejectionReason> OwnRefusals(SeismicSurveyCommand command) => [];`
- `L61` `public override void Complete(CompletedActivity done, Tick tick)`
- `L98` `private const double HardEvidence = 2.0;`
- `L101` `private const double SoftEvidence = 0.5;`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

