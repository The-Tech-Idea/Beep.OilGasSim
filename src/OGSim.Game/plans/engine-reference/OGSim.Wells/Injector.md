# Injector

Source: `src\OGSim.Wells\Injector.cs` · Lines: 235

## File intent

> R10.2 — injection and disposal (SDD-003 §3.1d, R10 §2.2).
> 
> An injector is §6.1's Darcy form with the pressure difference reversed and
> water's viscosity in place of oil's. It is not a producer with a minus sign
> bolted on: the fluid is different, the skin grows instead of staying put, and
> the constraint it reports is Injectivity rather than a capacity.
> 
> INJECTIVITY DECLINES. The formation plugs with solids and fines, so skin grows

## Namespaces

- `OGSim.Wells`

## Type declarations

- `L20` `public sealed record InjectionConditions(`
- `L33` `public sealed class Injector : IFlowElement`

## Accessible members

- `L35` `private readonly InjectionConditions _conditions;`
- `L36` `private readonly int _materialCount;`
- `L37` `private readonly int _waterOrdinal;`
- `L38` `private double _cumulativeInjectedM3;`
- `L43` `private Pressure _reservoirPressure;`
- `L44` `private Pressure _injectionPressure;`
- `L46` `public Injector(`
- `L62` `public EntityId<ICompletion> InjectorId { get; }`
- `L67` `public EntityId<IFlowElement> Id { get; }`
- `L71` `public IReadOnlyList<PortSpec> Ports { get; } =`
- `L76` `public static PortId Inlet { get; } = new(0);`
- `L86` `public void SetInjectionConditions(Pressure reservoir, Pressure injection)`
- `L102` `public TransformResult Transform(TransformInput input)`
- `L130` `public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input)`
- `L143` `public ReservoirVolume CumulativeInjected => new(_cumulativeInjectedM3);`
- `L150` `public double CurrentSkin =>`
- `L159` `public ReservoirRate AcceptanceAt(Pressure injectionPressure, Pressure reservoirPressure)`
- `L188` `public ConstraintEvaluation ConstraintAt(`
- `L195` `public void Commit(ReservoirVolume injected)`
- `L211` `public void Remediate() => _cumulativeInjectedM3 = 0.0;`
- `L213` `private const double SteadyStateOffset = 0.75;`
- `L215` `private static void Validate(InjectionConditions c)`
- `L233` `private static string Format(double value) =>`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

