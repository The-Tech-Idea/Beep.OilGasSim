# Operator

Source: `src\OGSim.ReferenceClient\Operator.cs` · Lines: 218

## File intent

> R21.5 — the reference client (R21 §2.5).
> 
> IT PLAYS A FULL GAME THROUGH THE PUBLISHED SURFACE AND NOTHING ELSE: a read
> model to look at, a command bus to act on. It holds no reference to a domain
> module, cannot see a compartment, a completion or a separator, and knows only
> what a host would know.
> 
> THAT CONSTRAINT IS THE WHOLE POINT. "If it needs anything the surface does not

## Namespaces

- `OGSim.ReferenceClient`

## Type declarations

- `L27` `public sealed record Session(`
- `L38` `public sealed class Operator`

## Accessible members

- `L40` `private readonly Engine _engine;`
- `L41` `private readonly EntityId<IProspect> _prospect;`
- `L42` `private readonly int _wellTarget;`
- `L53` `private readonly Money _hurdle;`
- `L55` `public Operator(`
- `L76` `public Session Play(int months)`
- `L113` `private bool Develop(FieldReadModel seen)`
- `L145` `private static readonly Money ExportLineWorthBuildingAt = Money.FromMillions(100.0);`
- `L162` `private int CloseWhatIsFinished(FieldReadModel seen)`
- `L195` `private static int Producing(FieldReadModel seen)`
- `L206` `private static Length WellDepth { get; } = new(2000.0);`
- `L213` `private const int LosingMonthsBeforeClosing = 3;`
- `L215` `private Money _lastSeenCash = Money.Zero;`
- `L217` `private int _losingMonths;`

## Imports

- `using OGSim.Composition;`
- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

