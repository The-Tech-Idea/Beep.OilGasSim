# Explorer

Source: `src\OGSim.ReferenceClient\Explorer.cs` · Lines: 211

## File intent

> R21.5 — a client that explores (R21 §2.5, SDD-008 §4).
> 
> THE OTHER HALF OF THE REFERENCE CLIENT. `Operator` is handed a field and
> develops it, which proves the production surface. Nothing had ever tried to
> play the part BEFORE the field: read a basin's prospects, decide which is
> worth a survey, decide which is worth a hole, be wrong about it, and pay for
> being wrong.
> 

## Namespaces

- `OGSim.ReferenceClient`

## Type declarations

- `L28` `public sealed record Campaign(`
- `L41` `public sealed class Explorer`

## Accessible members

- `L43` `private readonly Engine _engine;`
- `L44` `private readonly double _drillAbove;`
- `L45` `private readonly int _wellTarget;`
- `L50` `private readonly HashSet<ulong> _drilled = [];`
- `L52` `private int _surveyed;`
- `L53` `private int _discoveries;`
- `L54` `private int _dryHoles;`
- `L61` `public Explorer(Engine engine, double drillAbove, int wellTarget)`
- `L70` `public Campaign Play(int months)`
- `L106` `private void Explore(FieldReadModel seen)`
- `L142` `private ProspectView? Best(FieldReadModel seen)`
- `L160` `private void Develop(FieldReadModel seen)`
- `L186` `private ProspectView? Drilled(FieldReadModel seen)`
- `L201` `private void Account(int wellsBefore)`
- `L208` `private static Length WellDepth { get; } = new(2000.0);`
- `L210` `private static Money ExportLineWorthBuildingAt { get; } = Money.FromMillions(100.0);`

## Imports

- `using OGSim.Composition;`
- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

