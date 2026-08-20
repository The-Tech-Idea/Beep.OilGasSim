# FlowContracts

Source: `src\OGSim.Contracts\FlowContracts.cs` · Lines: 242

## File intent

> SDD-002 §5–8 — elements, transforms, the solver surface. The solver knows
> only IFlowElement (design 04 §1): adding equipment never touches it.

## Namespaces

- `OGSim.Contracts`

## Type declarations

- `L8` `public readonly record struct PortId(int Index);`
- `L10` `public enum PortDirection { Inlet, Outlet }`
- `L14` `public enum PortRole { Main, Gas, Liquid, Water, Reject }`
- `L16` `public sealed record PortSpec(PortId Id, PortDirection Direction, PortRole Role);`
- `L19` `public enum ConstraintKind`
- `L25` `public sealed record ConstraintEvaluation(`
- `L31` `public sealed record SegmentContext(`
- `L49` `public sealed record TransformInput(`
- `L66` `public sealed record DisposedMass(`
- `L77` `public sealed record TransformResult(`
- `L102` `public interface IFlowElement`
- `L127` `public interface IPressureController : IFlowElement`
- `L140` `public sealed record ForcedShutIn(`
- `L145` `public sealed record ElementSolution(`
- `L150` `public sealed record CompletionState(`
- `L160` `public sealed record SolveReport(`
- `L187` `public interface ICommitTarget`
- `L193` `public interface IWithdrawalTarget : ICommitTarget`
- `L199` `public interface IReceiptTarget : ICommitTarget`
- `L205` `public interface ICustodyRecorder : ICommitTarget`
- `L213` `public sealed record FlowConnection(`
- `L219` `public sealed record FlowTopology(`
- `L239` `public interface IFlowSolver`

## Accessible members

- `L56` `public bool Equals(TransformInput? other) =>`
- `L60` `public override int GetHashCode() =>`
- `L87` `public bool Equals(TransformResult? other) =>`
- `L92` `public override int GetHashCode() =>`
- `L168` `public bool Equals(SolveReport? other) =>`
- `L175` `public override int GetHashCode() =>`
- `L225` `public bool Equals(FlowTopology? other) =>`
- `L230` `public override int GetHashCode() =>`

## Imports

- `using OGSim.Kernel;`

