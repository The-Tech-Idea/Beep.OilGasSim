# SubsurfaceContracts

Source: `src\OGSim.Contracts\SubsurfaceContracts.cs` · Lines: 210

## File intent

> SDD-003 — subsurface contracts. Truth types (IReservoirCompartment and the
> accumulation) are INTERNAL to their owning modules and do not appear here:
> this file carries only what other modules may legitimately see.
> <summary>Marker for compartment entity ids (truth object itself is internal to OGSim.Subsurface).</summary>

## Namespaces

- `OGSim.Contracts`

## Type declarations

- `L10` `public interface IReservoirCompartmentEntity { }`
- `L21` `public interface IProspect { }`
- `L24` `public enum DetectClass { D0, D1, D2, D3 }`
- `L26` `public enum DepthClass { Shallow, Standard, Deep, UltraDeep }`
- `L28` `public enum WaterDepthClass { Onshore, Shallow, Deep, UltraDeep }`
- `L34` `public sealed record AccessRequirements(`
- `L41` `public enum FluidForm { BlackOil, ModifiedBlackOil }`
- `L44` `public sealed record PhaseSplit(`
- `L57` `public sealed record ValidityRange(Pressure MinP, Pressure MaxP, Temperature MinT, Temperature MaxT);`
- `L64` `public interface IFluidPropertyModel`
- `L101` `public sealed record MaterialBalanceInput(`
- `L134` `public sealed record AdmittedTerms(bool GasCap, bool AquiferInflux);`
- `L136` `public interface IDriveMechanism`
- `L160` `public sealed record RelativePermeabilityCurve(`
- `L205` `public interface IAquiferModel`

## Accessible members

- `L51` `public bool Equals(PhaseSplit? other) =>`
- `L54` `public override int GetHashCode() => Structural.HashOf(Fractions);`
- `L168` `public static RelativePermeabilityCurve Validated(`
- `L187` `public double NormalisedSaturation(double waterSaturation)`
- `L194` `public double WaterPermeability(double waterSaturation) =>`
- `L197` `public double OilPermeability(double waterSaturation) =>`
- `L200` `private static string Format(double value) =>`

## Imports

- `using OGSim.Kernel;`

