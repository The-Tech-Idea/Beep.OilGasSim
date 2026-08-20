# SolveState

Source: `src\OGSim.Flow\SolveState.cs` · Lines: 465

## File intent

> R4.3–R4.5 — everything one solve knows (SDD-002 §7).
> 
> All per-solve state lives here rather than on the solver, so a solver instance
> carries nothing between segments and two solves cannot interfere. Every
> collection walked during a pass is a List or is sorted before walking: rule
> D-5 in the one place where iteration order would silently change results.

## Namespaces

- `OGSim.Flow`

## Type declarations

- `L13` `internal sealed class SolveState`
- `L457` `private struct CompletionSolveState`

## Accessible members

- `L15` `private readonly SolverSettings _settings;`
- `L16` `private readonly IReadOnlyList<ICompletion> _completions;`
- `L17` `private readonly CompletionSolveState[] _byCompletion;`
- `L19` `private readonly Dictionary<EntityId<IFlowElement>, TransformResult> _results = [];`
- `L20` `private readonly Dictionary<EntityId<IFlowElement>, IReadOnlyList<ConstraintEvaluation>> _constraints = [];`
- `L22` `public SolveState(IReadOnlyList<ICompletion> completions, SolverSettings settings)`
- `L47` `public List<ElementSolution> Solutions { get; }`
- `L49` `public List<(EntityId<IFlowElement> Element, ConstraintKind Kind, Mass Deferred)> Deferrals { get; }`
- `L51` `public double WorstResidual { get; private set; }`
- `L59` `public double AdvanceRates()`
- `L104` `public void ClearPass()`
- `L116` `public IReadOnlyList<MaterialStream> InletsOf(IFlowElement element, FlowNetwork network) =>`
- `L119` `private static IReadOnlyList<MaterialStream> InletsFrom(`
- `L146` `private static int OutletIndexOf(IFlowElement element, PortId port)`
- `L159` `public void RecordSolution(`
- `L173` `public void RecordConstraints(`
- `L177` `public IReadOnlyList<ConstraintEvaluation> ConstraintsOf(EntityId<IFlowElement> id) =>`
- `L191` `public ReservoirRate? SolvedRateOf(EntityId<IFlowElement> id)`
- `L203` `public bool HasThrottleableCompletion()`
- `L210` `public void ApplyProRataCap(double factor)`
- `L244` `public void AttributeDeferrals(FlowNetwork network, SegmentContext segment)`
- `L283` `private void RecordDeferral(EntityId<IFlowElement> element, ConstraintKind kind, Mass deferred)`
- `L310` `public void BackwardPass(FlowNetwork network, double sinkBoundaryPa)`
- `L340` `private double PressureDropOf(EntityId<IFlowElement> id)`
- `L356` `public double UpdateBackpressures(FlowNetwork network)`
- `L374` `private double UpdateBackpressure(ICompletion completion, FlowNetwork network)`
- `L389` `private readonly Dictionary<EntityId<IFlowElement>, double> _inletPressurePa = [];`
- `L390` `private readonly Dictionary<EntityId<IFlowElement>, double> _inletSeenPa = [];`
- `L396` `public ICompletion LargestResidual()`
- `L416` `public void ForceShutIn(EntityId<IFlowElement> id)`
- `L429` `public void ResetForLadderStep()`
- `L439` `public List<CompletionState> CompletionStates()`
- `L450` `private int IndexOf(EntityId<IFlowElement> id)`
- `L459` `public double Rate;`
- `L460` `public double UncappedTarget;`
- `L461` `public Pressure Backpressure;`
- `L462` `public double Cap;`
- `L463` `public bool ShutIn;`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

