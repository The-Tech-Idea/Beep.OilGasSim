# Completion

Source: `src\OGSim.Wells\Completion.cs` · Lines: 346

## File intent

> R6.3 / R6.9 / R6.10 — the completion: the network's source element
> (SDD-003 §6, design 02 §3.1).
> 
> This is where the "one engine" claim is genuinely tested (R6 §2.3). The
> completion takes wellhead pressure as a BOUNDARY CONDITION from the flow
> solve, so a full tank raises manifold pressure, raises wellhead pressure,
> raises Pwf, reduces drawdown, and reduces withdrawal — with no rule anywhere
> saying tanks affect reservoirs. If R4 needed changing to accommodate this, the

## Namespaces

- `OGSim.Wells`

## Type declarations

- `L22` `public sealed record ChokeSetting(double CriticalPressureRatio, ReservoirRate CriticalRate)`
- `L43` `public sealed record CompletionFluid(`
- `L91` `public sealed class Completion : ICompletion`

## Accessible members

- `L26` `public static ChokeSetting Open { get; } =`
- `L38` `public static ChokeSetting Closed { get; } =`
- `L93` `private readonly IInflowModel _inflow;`
- `L94` `private readonly IOutflowModel _outflow;`
- `L102` `private CompletionFluid _fluid;`
- `L103` `private ChokeSetting _choke;`
- `L104` `private readonly int _oilOrdinal;`
- `L105` `private readonly int _gasOrdinal;`
- `L106` `private readonly int _waterOrdinal;`
- `L107` `private readonly int _materialCount;`
- `L109` `private bool _pressureDecoupled;`
- `L111` `public Completion(`
- `L152` `public EntityId<ICompletion> CompletionId { get; }`
- `L154` `public EntityId<IFlowElement> Id { get; }`
- `L156` `public EntityId<IWellbore> Wellbore { get; }`
- `L158` `public IReadOnlyList<Perforation> Perforations { get; }`
- `L160` `public ILiftMethod? Lift { get; }`
- `L162` `public bool IsPressureDecoupled => _pressureDecoupled;`
- `L165` `public IReadOnlyList<PortSpec> Ports { get; } =`
- `L188` `public void SetReservoirConditions(`
- `L216` `public Pressure ReservoirPressure => _fluid.ReservoirPressure;`
- `L219` `public ChokeSetting Choke => _choke;`
- `L228` `public bool IsShutIn => _choke.CriticalRate.CubicMetresPerSecond <= 0.0;`
- `L231` `public void SetChoke(ChokeSetting choke)`
- `L237` `public OperatingPoint SolveOperatingPoint(Pressure wellheadBackpressure)`
- `L286` `public TransformResult Transform(TransformInput input)`
- `L345` `public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input) => [];`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

