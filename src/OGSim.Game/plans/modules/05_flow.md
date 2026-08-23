> Source read in full: `src/OGSim.Composition/Modules.cs`.
> Part of the module review requested 2026-08-23. Nothing in the engine was
> changed to produce this — it records what is there.


# 05 — flow

`internal sealed class FlowModule()`

## Manifest

| | |
|---|---|
| **provides** | `IFlowSolver`, `IFlowElementRegistry`, `TickProduction` |
| **requires** | `IAuditTrail` |
| **ownsState** | *(none)* |
| **stages** | *(none)* |

The registry is provided **here** because it is the solver's input and the
solver is what gives it meaning. Wells and facilities *require* it — a contract
dependency, never an assembly one.

## Compose

```
Provide<IFlowSolver>(new FlowSolver(SolverSettings.Pinned, Require<IAuditTrail>()))
Provide<IFlowElementRegistry>(new FlowElementRegistry())
Provide(new TickProduction())
```

## Stages

**None.** The stage that runs the solver is `SolveFlowStage`, contributed by
**field** at stage 5.

## Functions and properties

- **`IFlowSolver.Solve(SegmentContext, FlowTopology)`** — knows only
  `IFlowElement`, which is why adding equipment never touches the solver
- **`IFlowElementRegistry`** — `Add`, `Connect`, `All`, `ViewFor(available)`.
  `Connect` does **not** dedupe; one edge per port is the caller's rule
- **`TickProduction`** — the solve/commit seam. Stage 5 fills it, stage 6 drains
  it. "Replaces, never appends"

## The forced shut-in ladder

When the network will not converge the solver shuts in the completion with the
largest residual, one per outer round, bounded by the completion count, and
reports each as a `ForcedShutIn`. A player watches wells go off **because the
chain cannot take them**.

## Dependencies and conditions it decides for itself

**None.**

## Static numbers found

`SolverSettings.Pinned` — damping 0.5, rate tolerance 1e-4, pressure tolerance
1000 Pa, rate floor 1e-8, 200 outer iterations, sink boundary 101 325 Pa. The
record's own doc says "content-supplied, defaults as pinned in SDD-002 §7", but
**no content kind for solver settings exists**; composition passes `Pinned`
literally.

## Content and Defaults consumed

No `Defaults.*` at all, and no content.
