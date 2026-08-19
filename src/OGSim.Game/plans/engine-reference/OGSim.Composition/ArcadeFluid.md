# ArcadeFluid

Source: `src\OGSim.Composition\ArcadeFluid.cs` · Lines: 170

## File intent

> R25.1 — the arcade fluid model (design 18 §5b.1, SDD-005 §7b).
> 
> THE SAME GAME, COMPUTED MORE SIMPLY. Design 18 §5b's fidelity axis is
> per-model plugin selection: arcade, standard and simulation implementations of
> the same slot, chosen by a reality profile. This is the arcade one for fluid
> properties — the "simple flight model" beside the full one.
> 
> IT IS NOT A STUB AND NOT A LESSER GAME. Every number it returns is a real

## Namespaces

- `OGSim.Composition`

## Type declarations

- `L40` `public sealed record RealityProfile(`
- `L68` `internal sealed class ArcadeFluidModel : IFluidPropertyModel`

## Accessible members

- `L45` `public bool Equals(RealityProfile? other) =>`
- `L48` `public override int GetHashCode() => HashCode.Combine(Id, Structural.HashOf(Fidelity));`
- `L52` `public ContentId? Selected(ModelSlot slot)`
- `L70` `private readonly BlackOilInputs _inputs;`
- `L71` `private readonly FormationVolumeFactor _bo;`
- `L72` `private readonly IMaterialCatalog _materials;`
- `L74` `public ArcadeFluidModel(`
- `L90` `public ContentId Id { get; } = new("arcade-fluid");`
- `L92` `public FluidForm Form => _inputs.Form;`
- `L94` `public ValidityRange Validity { get; }`
- `L101` `public Pressure Pb { get; } = Pressure.FromBar(50.0);`
- `L109` `public FormationVolumeFactor Bo(Pressure p) => _bo;`
- `L113` `public double Rs(Pressure p) => _inputs.SolutionGorAtBubblePoint;`
- `L116` `public double Rv(Pressure p) => 0.0;`
- `L118` `public GasFormationVolumeFactor Bg(Pressure p) => new(ArcadeGasFormationVolumeFactor);`
- `L120` `public FormationVolumeFactor Bw(Pressure p) => new(1.0);`
- `L122` `public Viscosity MuOil(Pressure p) => new(ArcadeOilViscosityPaS);`
- `L124` `public Viscosity MuGas(Pressure p) => new(ArcadeGasViscosityPaS);`
- `L128` `public double Z(Pressure p, Temperature t) => 1.0;`
- `L136` `public PhaseSplit SplitAt(`
- `L163` `private const double ArcadeGasFormationVolumeFactor = 0.005;`
- `L166` `private const double ArcadeOilViscosityPaS = 2.0e-3;`
- `L169` `private const double ArcadeGasViscosityPaS = 1.5e-5;`

## Imports

- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

