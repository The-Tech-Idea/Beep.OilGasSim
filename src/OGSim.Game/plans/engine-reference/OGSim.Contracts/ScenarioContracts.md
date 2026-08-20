# ScenarioContracts

Source: `src\OGSim.Contracts\ScenarioContracts.cs` · Lines: 254

## File intent

> SDD-014 §5 — scenarios, challenges and campaigns.
> 
> A SCENARIO IS CONTENT, NOT CODE (design 03 §3.3, 18 §3). Every mission,
> challenge and campaign chapter the game ships is one of these records loaded
> from JSON: a world to open, a position to open it from, things to achieve,
> things that must never happen, what the run is scored on, and what the
> scenario itself does to the world while it runs.
> 

## Namespaces

- `OGSim.Contracts`

## Type declarations

- `L38` `public abstract record WorldSource;`
- `L42` `public sealed record GeneratedWorld(ulong Seed) : WorldSource;`
- `L44` `public sealed record AuthoredWorld(ContentId World) : WorldSource;`
- `L55` `public abstract record ScriptedEntry(Tick At);`
- `L57` `public sealed record ScriptedCommand(Tick At, Command Command) : ScriptedEntry(At);`
- `L59` `public sealed record ScriptedParameter(`
- `L64` `public enum ScoreDimension`
- `L83` `public sealed record ScoreWeight(ScoreDimension Dimension, double Weight);`
- `L90` `public enum ObjectiveState`
- `L111` `public sealed record Scenario(`
- `L164` `public sealed record ChapterLink(ObjectiveState Outcome, ContentId NextChapter);`
- `L169` `public sealed record Campaign(`
- `L209` `public sealed record ScenarioProgress(`
- `L232` `public interface IScenarioRunner`

## Accessible members

- `L148` `public bool Equals(Scenario? other) =>`
- `L157` `public override int GetHashCode() =>`
- `L191` `public bool Equals(Campaign? other) =>`
- `L197` `public override int GetHashCode() =>`
- `L215` `public bool Equals(ScenarioProgress? other) =>`
- `L220` `public override int GetHashCode() =>`

## Imports

- `using OGSim.Kernel;`

