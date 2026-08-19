# InflowModels

Source: `src\OGSim.Wells\InflowModels.cs` · Lines: 213

## File intent

> R6.5 — inflow performance (SDD-003 §6.1, design 05 §4.1–4.2).
> 
> PER PERFORATION, not per completion. A well with a damaged lower zone and a
> clean upper zone is expressible, isolating the damaged zone is a real
> intervention with a computable benefit, and multi-perforation commingling
> apportions by each perf's own kh share (R6 §2.4, FV10). A completion-level
> signature could not express the case the model exists for.
> 

## Namespaces

- `OGSim.Wells`

## Type declarations

- `L24` `public sealed record InflowConditions(`
- `L40` `public sealed class DarcyInflowModel : IInflowModel`
- `L154` `public sealed class CompositeInflowModel : IInflowModel`

## Accessible members

- `L42` `private readonly InflowConditions _conditions;`
- `L44` `public DarcyInflowModel(InflowConditions conditions)`
- `L52` `public ContentId Id { get; } = new("darcy-inflow");`
- `L54` `public ReservoirRate InflowAt(`
- `L78` `public double ProductivityIndex(Perforation perforation)`
- `L102` `private double HeightOf(Perforation perforation) =>`
- `L111` `private const double SteadyStateOffset = 0.75;`
- `L113` `private static void Validate(InflowConditions c)`
- `L134` `private static string Format(double value) =>`
- `L158` `private const double VogelLinear = 0.2;`
- `L159` `private const double VogelQuadratic = 0.8;`
- `L161` `private readonly DarcyInflowModel _darcy;`
- `L162` `private readonly Pressure _bubblePoint;`
- `L164` `public CompositeInflowModel(InflowConditions conditions)`
- `L172` `public ContentId Id { get; } = new("composite-inflow");`
- `L174` `public ReservoirRate InflowAt(`
- `L211` `private static double VogelFraction(double ratio) =>`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

