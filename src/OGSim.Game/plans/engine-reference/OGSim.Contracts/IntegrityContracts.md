# IntegrityContracts

Source: `src\OGSim.Contracts\IntegrityContracts.cs` · Lines: 64

## File intent

> SDD-012 — condition, degradation, failure. Both models are replaceable
> (design 03 §3.2) and BLIND to everything but their declared inputs: severity
> in, decay out; condition in, probability out. The hazard draw itself happens
> in the engine at stage 4, consuming ONLY the Hazard stream (D-4).
> <summary>
> What service does to equipment, per tick (SDD-012 §2): each term is a
> dimensionless 0..1 severity the datasheet's decay curve responds to.
> </summary>

## Namespaces

- `OGSim.Contracts`

## Type declarations

- `L14` `public sealed record ServiceSeverity(`
- `L22` `public interface IDegradationModel`
- `L33` `public interface IHazardModel`
- `L56` `public interface ISouringModel`

## Accessible members

_No public/internal/protected/private member lines matched the extractor._

## Imports

- `using OGSim.Kernel;`

