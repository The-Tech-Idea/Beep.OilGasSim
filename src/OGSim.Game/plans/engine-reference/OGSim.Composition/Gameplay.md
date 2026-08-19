# Gameplay

Source: `src\OGSim.Composition\Gameplay.cs` · Lines: 350

## File intent

> R21 (first slice) — what makes this a game rather than a simulation that runs:
> the player can act, can see, and can lose.
> 
> AGENCY. Drilling a well is a command, validated then applied, and the
> validation is where the decision has weight: a company that cannot afford the
> well is told so in a reason it can render, not silently allowed to go
> bankrupt. Every rejection is domain-typed (SDD-001 §7) so the host never
> invents an explanation.

## Namespaces

- `OGSim.Composition`

## Type declarations

- `L39` `public sealed record BeliefEntryView(`
- `L58` `public sealed record WellStatusView(`
- `L78` `public sealed record ProspectView(`
- `L109` `public sealed record FieldPosition(`
- `L133` `public sealed record FieldReadModel(`
- `L219` `internal sealed class FieldProjection(`
- `L324` `internal sealed class CloseStage(`

## Accessible members

- `L176` `public IReadOnlyList<ChainElementView> Bottlenecks`
- `L191` `public ObjectiveState Outcome => Progress.Overall;`
- `L194` `public bool Equals(FieldReadModel? other) =>`
- `L204` `public override int GetHashCode() =>`
- `L228` `public FieldPosition Take(Tick tick, GameDate date, bool insolvent) =>`
- `L232` `public FieldReadModel Publish(FieldPosition position, ScenarioProgress progress) =>`
- `L245` `private IReadOnlyList<ProspectView> Prospects()`
- `L289` `private static IReadOnlyList<BeliefEntryView> Project(IBeliefStore beliefs)`
- `L328` `public StageId Id => StageId.Close;`
- `L331` `public FieldReadModel? Published { get; private set; }`
- `L333` `public bool Insolvent => objectives.Insolvent;`
- `L335` `public void Execute(TickContext context)`

## Imports

- `using OGSim.Company;`
- `using OGSim.Contracts;`
- `using OGSim.Kernel;`
- `using OGSim.Wells;`

