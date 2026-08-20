# FacilityContracts

Source: `src\OGSim.Contracts\FacilityContracts.cs` · Lines: 208

## File intent

> Design 02 §4 — a facility is a container and a cost centre, NEVER a process.
> All physics lives in units, each an IFlowElement. There is no facility-type
> hierarchy in code at all (02 §4.1): "gas plant" is a template id in content.

## Namespaces

- `OGSim.Contracts`

## Type declarations

- `L10` `public interface IFacility`
- `L19` `public enum SpecProperty`
- `L25` `public sealed record SpecLimit(SpecProperty Property, double Limit);`
- `L28` `public sealed record Specification(IReadOnlyList<SpecLimit> Limits)`
- `L44` `public interface ICustodyTransferPoint : IFlowElement`
- `L51` `public interface IPipeline : IFlowElement`
- `L60` `public interface IPowerSource`
- `L71` `public readonly record struct SeparationEfficiency(`
- `L86` `public interface ISeparationModel`
- `L95` `public readonly record struct PipeGeometry(`
- `L106` `public interface IHydraulicModel`
- `L121` `public enum GasComponent { C1, C2, C3, C4, C5Plus }`
- `L131` `public sealed record ComponentSplit(ImmutableArray<double> MassFractionByComponent)`
- `L180` `public sealed record NglRecovery(ImmutableArray<double> FractionByComponent)`

## Accessible members

- `L34` `public bool Equals(Specification? other) =>`
- `L37` `public override int GetHashCode() => Structural.HashOf(Limits);`
- `L133` `public const int ComponentCount = 5;`
- `L135` `public static ComponentSplit Validated(params double[] massFractions)`
- `L166` `public double this[GasComponent component] => MassFractionByComponent[(int)component];`
- `L172` `public bool Equals(ComponentSplit? other) =>`
- `L176` `public override int GetHashCode() => Structural.HashOf(MassFractionByComponent);`
- `L182` `public static NglRecovery Validated(params double[] fractions)`
- `L200` `public double this[GasComponent component] => FractionByComponent[(int)component];`
- `L204` `public bool Equals(NglRecovery? other) =>`
- `L207` `public override int GetHashCode() => Structural.HashOf(FractionByComponent);`

## Imports

- `using System.Collections.Immutable;`
- `using OGSim.Kernel;`

