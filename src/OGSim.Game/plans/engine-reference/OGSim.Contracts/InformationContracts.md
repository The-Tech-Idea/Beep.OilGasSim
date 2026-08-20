# InformationContracts

Source: `src\OGSim.Contracts\InformationContracts.cs` · Lines: 154

## File intent

> SDD-008 — beliefs. One conjugate update rule, Normal in a declared space;
> POS as Beta-Bernoulli with the play-shared factors AS the correlation.
> Truth is unreachable from here: only observation deliveries cross the wall.
> BeliefSpace and Provenance moved to OGSim.Kernel/Provenance.cs at R2.1:
> IProperty needs them and R2 runs eleven phases before R14, so they cannot
> live in an assembly the material layer is below. They are vocabulary, not
> belief state.
> <summary>Every belief is Normal in its declared space (SDD-008 §2). Quantiles closed-form.</summary>

## Namespaces

- `OGSim.Contracts`

## Type declarations

- `L15` `public readonly record struct Belief(`
- `L23` `public readonly record struct FactorBelief(double Alpha, double Beta);`
- `L26` `public enum PosFactor { Source, Reservoir, Seal, Trap, Timing }`
- `L32` `public sealed record Observation(`
- `L55` `public sealed record InformationValue(`
- `L81` `public interface IInformationValueModel`
- `L91` `public interface IObservationModel`
- `L111` `public readonly record struct HeldBelief(`
- `L116` `public interface IBeliefStore`

## Accessible members

- `L64` `public bool WorthBuying => ExpectedValue > Cost;`

## Imports

- `using OGSim.Kernel;`

