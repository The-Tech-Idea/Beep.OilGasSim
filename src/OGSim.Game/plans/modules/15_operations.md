> Source read in full: `src/OGSim.Composition/Modules.cs`, plus the types this
> module composes. Part of the module review requested 2026-08-23. Nothing in the
> engine was changed to produce this.


# 15 — operations

`internal sealed class OperationsModule()`

## Manifest

| | |
|---|---|
| **provides** | *(empty)* |
| **requires** | `IAuditTrail` |
| **ownsState** | *(none)* |
| **stages** | *(none)* |
| **commands** | *(none)* |

## Compose

```csharp
public override void Compose(IModuleComposition composition) =>
    ArgumentNullException.ThrowIfNull(composition);
```

That is the entire body.

## Functions and properties

**None.** This module is a name-holder.

The real `OGSim.Operations` types are built by **field**, not here:
`ObligationRegistry` and `OperationScheduler` are the only references to that
namespace anywhere in the composition layer, and both are in `FieldModule.Compose`.

## Dependencies and conditions it decides for itself

**None** — there is nothing to decide.

## Can it be omitted?

Yes, with no observable effect. It provides `[]`, so no requirement edge can
point at it; it owns no key and claims no slot. Removing it composes an identical
engine.

It stays because it reserves the name for work that is coming. Recorded here so
the next reader knows it is **inert rather than mysterious**.
