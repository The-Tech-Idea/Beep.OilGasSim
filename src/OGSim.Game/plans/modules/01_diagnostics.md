> Source read in full: `src/OGSim.Composition/Modules.cs`.
> Part of the module review requested 2026-08-23. Nothing in the engine was
> changed to produce this — it records what is there.


# 01 — diagnostics

`internal sealed class DiagnosticsModule(AuditTrail audit, SimulationClock clock, IRandomSource random)`

## Manifest

| | |
|---|---|
| **provides** | `IAuditTrail`, `AuditTrail`, `SimulationClock`, `IRandomSource` |
| **requires** | *(none)* |
| **ownsState** | *(none)* |
| **stages** | *(none)* |
| **commands** | *(none)* |

## Compose

Four `Provide` calls and nothing else. It constructs nothing — all three
services arrive as constructor arguments from `EngineBuilder`.

```
Provide<IAuditTrail>(audit)   Provide(audit)
Provide(clock)                Provide(random)
```

## Why the concrete type sits beside the interface

`Prune` and `RestoreFrom` are on `AuditTrail`, not on `IAuditTrail`. A module
holding the interface can record and query and **cannot rewrite history**; the
two things entitled to — the tick pipeline and a load — ask for the concrete
type by name.

The same argument holds for the clock: `Advance` is on `SimulationClock` alone,
so a module that reads the date takes `ISimulationClock` and cannot move it.

## Functions and properties it brings

Everything here is a kernel facility; the members belong to
`OGSim.Kernel`. What matters at this level:

- **`IAuditTrail`** — `Record(category, subject, cause, fields) -> AuditId`,
  `Query(AuditQuery) -> IReadOnlyList<AuditEntry>`
- **`IRandomSource`** — `Stream(StreamId) -> IRandomStream`; eight named streams,
  independent, so adding a draw in one cannot shift another
- **`SimulationClock`** — `Date`, `Epoch`, `Advance()`

## Dependencies and conditions it decides for itself

**None.** This is the only module with a genuinely empty `requires` that also
holds no branch. It is the cleanest module in the engine.

## Content and Defaults consumed

None directly. Its three arguments come from `EngineSettings`: `Epoch`,
`Retention`, `WorldSeed`.

## Can a style switch it off?

No, and it should not be able to. Seven manifests require `IAuditTrail`, five
require `IRandomSource`, two require `SimulationClock`. It is the seed of
determinism and the whole "why did that happen" trail.
