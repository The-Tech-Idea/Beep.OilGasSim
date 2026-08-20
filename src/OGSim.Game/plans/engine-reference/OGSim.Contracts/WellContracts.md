# WellContracts

Source: `src\OGSim.Contracts\WellContracts.cs` · Lines: 206

## File intent

> SDD-003 §5–6 — the PPDM four-level hierarchy (design 02 §3): a well is not a
> hole. IWell owns identity and status; IWellbore geometry; ICompletion the
> physics; Perforation the reservoir connection.
> <summary>Design 02 §3.4 — every transition is a command; none skips abandonment.</summary>

## Namespaces

- `OGSim.Contracts`

## Type declarations

- `L10` `public enum WellStatus`
- `L16` `public enum WellClassification { Exploration, Appraisal, Development, Injector, Observation }`
- `L19` `public interface IWell`
- `L30` `public readonly record struct TrajectoryStation(Length Md, Length Tvd, Coordinate Position);`
- `L32` `public sealed record Trajectory(IReadOnlyList<TrajectoryStation> Stations)`
- `L44` `public interface IWellbore`
- `L58` `public sealed record Perforation(`
- `L66` `public interface IInflowModel`
- `L73` `public interface IOutflowModel`
- `L90` `public sealed record LiftEffect(`
- `L103` `public sealed record LiftEnvelope(`
- `L113` `public sealed record LiftConditions(`
- `L130` `public sealed record EnvelopeAssessment(`
- `L149` `public interface ILiftMethod : IWellComponent`
- `L162` `public abstract record OperatingPoint;`
- `L163` `public sealed record Flowing(ReservoirRate Rate, Pressure Bottomhole) : OperatingPoint;`
- `L164` `public sealed record Dead : OperatingPoint;`
- `L170` `public interface ICompletion : IFlowElement`
- `L200` `public interface IWellComponent`

## Accessible members

- `L37` `public bool Equals(Trajectory? other) =>`
- `L40` `public override int GetHashCode() => Structural.HashOf(Stations);`
- `L98` `public static LiftEffect None { get; } =`
- `L137` `public bool Equals(EnvelopeAssessment? other) =>`
- `L143` `public override int GetHashCode() =>`

## Imports

- `using OGSim.Kernel;`

