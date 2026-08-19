# Provenance

Source: `src\OGSim.Kernel\Provenance.cs` · Lines: 30

## File intent

> SDD-008 §2 — how a value is known, and in what space it is believed.
> 
> These two live in the KERNEL rather than with the belief machinery that made
> them necessary, because R2's IProperty needs them and R2 runs eleven phases
> before R14. They are vocabulary, not belief state: nothing here knows what a
> belief is (R2.0 layering correction — see Materials.cs).
> <summary>
> Declared per property kind in content. Additive kinds (depth, net pay,

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L16` `public enum BeliefSpace { Linear, Log }`
- `L27` `public enum Provenance`

## Accessible members

_No public/internal/protected/private member lines matched the extractor._

