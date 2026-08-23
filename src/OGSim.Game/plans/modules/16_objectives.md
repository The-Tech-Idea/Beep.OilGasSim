> Source read in full: `src/OGSim.Composition/Modules.cs`, plus the types this
> module composes. Part of the module review requested 2026-08-23. Nothing in the
> engine was changed to produce this.


# 16 — objectives

`internal sealed class ObjectivesModule()`

## Manifest

| | |
|---|---|
| **provides** | *(empty)* |
| **requires** | *(empty)* |
| **ownsState** | *(none)* |
| **stages** | *(none)* |

## Compose

```csharp
public override void Compose(IModuleComposition composition) =>
    ArgumentNullException.ThrowIfNull(composition);
```

## The empty `requires` is the architectural statement

From the source: *"an objective module that required a command bus could act, and
a scenario that could act would be a second player."*

## Functions and properties

**None.** Every objective mechanic belongs to **field**, which composes
`ScenarioRunner` and `ObjectiveStage` and owns both `objectives.evaluation` and
`objectives.reporting`. The `OGSim.Objectives` assembly is consumed from
`Scenario.cs`, not from here.

## Dependencies and conditions it decides for itself

**None.**

## Can it be omitted?

Yes, with no observable effect — the same as **operations**. It is a placeholder
for the module that will eventually own the objective machinery.

To switch objectives off in practice you would give `Defaults.FirstField` empty
`Objectives` and `Failures`, which is a **field** and content concern.
