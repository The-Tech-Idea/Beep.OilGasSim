# FlowSolver

Source: `src\OGSim.Flow\FlowSolver.cs` · Lines: 297

## File intent

> R4.3–R4.7 — the one flow engine (SDD-002 §7, design 04).
> 
> The algorithm is pinned step by step in SDD-002 §7 and this file implements
> exactly that: S0 seed, S1 wells, S2 forward, S3 throttle, S4 backward,
> S5 converged?, S6 budget → shut-in ladder. Every tolerance, the damping
> factor and the ladder's tie-break come from the SDD rather than from taste.
> 
> The solver knows only IFlowElement. It never asks what an element IS, only

## Namespaces

- `OGSim.Flow`

## Type declarations

- `L18` `public sealed record SolverSettings(`
- `L38` `public sealed class FlowSolver : IFlowSolver`
- `L296` `private readonly record struct SolveOutcome(bool Converged, double WorstResidual, int Iterations);`

## Accessible members

- `L27` `public static SolverSettings Pinned { get; } = new(`
- `L40` `private readonly SolverSettings _settings;`
- `L41` `private readonly IAuditTrail _audit;`
- `L43` `public FlowSolver(SolverSettings settings, IAuditTrail audit)`
- `L52` `public SolveReport Solve(SegmentContext segment, FlowTopology topology)`
- `L113` `private SolveOutcome RunToConvergence(`
- `L139` `private void ForwardPass(FlowNetwork network, SegmentContext segment, SolveState state)`
- `L167` `private static void AssertElementConservation(`
- `L202` `private bool Throttle(FlowNetwork network, SolveState state, SegmentContext segment)`
- `L248` `private double BackwardPass(FlowNetwork network, SolveState state)`
- `L261` `private static IReadOnlyList<ICompletion> CompletionsIn(FlowNetwork network)`
- `L280` `private static SolveReport Report(`
- `L290` `private static string Format(double value) =>`
- `L293` `private static string Format(ulong value) =>`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

