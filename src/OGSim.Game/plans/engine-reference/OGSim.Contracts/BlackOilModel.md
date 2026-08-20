# BlackOilModel

Source: `src\OGSim.Contracts\BlackOilModel.cs` · Lines: 360

## File intent

> R2.7 — the black-oil fluid property model (SDD-003 §4.1, design 05 §2).
> 
> FIELD UNITS INSIDE, SI AT THE BOUNDARY — SDD-003 §2's R2.7 amendment. These
> five correlations are EMPIRICAL: their constants are regression coefficients
> fitted to field-unit data, not unit conversions, so there is no SI form of
> them. Absorbing the conversions into new coefficients would produce numbers
> appearing in no paper and checkable against nothing. Each body below is
> transcribed so a reader holding the paper can follow it line by line.

## Namespaces

- `OGSim.Contracts`

## Type declarations

- `L21` `public sealed record BlackOilInputs(`
- `L35` `public sealed class BlackOilModel : IFluidPropertyModel`

## Accessible members

- `L38` `private const double PascalsPerPsi = 6894.757293168;`
- `L39` `private const double RankinePerKelvin = 1.8;`
- `L40` `private const double FahrenheitOffset = 459.67;`
- `L41` `private const double ScfPerStbPerSm3PerSm3 = 5.614583;   // sm³/sm³ → scf/STB`
- `L42` `private const double CentipoisePerPascalSecond = 1000.0;`
- `L45` `private const double StandardPressurePa = 101_325.0;`
- `L46` `private const double StandardTemperatureK = 288.706;`
- `L49` `private const double A1 = 0.3265, A2 = -1.0700, A3 = -0.5339, A4 = 0.01569;`
- `L50` `private const double A5 = -0.05165, A6 = 0.5475, A7 = -0.7361, A8 = 0.1844;`
- `L51` `private const double A9 = 0.1056, A10 = 0.6134, A11 = 0.7210;`
- `L53` `private readonly BlackOilInputs _inputs;`
- `L54` `private readonly double _api;              // °API`
- `L55` `private readonly double _gammaG;`
- `L56` `private readonly double _reservoirF;       // °F`
- `L57` `private readonly double _reservoirR;       // °R`
- `L58` `private readonly double _rsbScf;           // scf/STB`
- `L59` `private readonly double _bubblePointPsia;`
- `L60` `private readonly double _plateauRs;        // sm³/sm³ — Rs at Pb by the forward form`
- `L61` `private readonly double _bobAtBubblePoint;`
- `L66` `public ContentId Id { get; } = new("black-oil-correlations");`
- `L68` `public BlackOilModel(BlackOilInputs inputs, ValidityRange validity)`
- `L93` `public FluidForm Form { get; }`
- `L94` `public Pressure Pb { get; }`
- `L95` `public ValidityRange Validity { get; }`
- `L103` `public double Rs(Pressure p)`
- `L120` `private double ForwardRs(double psia)`
- `L129` `public double Rv(Pressure p) =>`
- `L143` `public FormationVolumeFactor Bo(Pressure p)`
- `L166` `public FormationVolumeFactor Bw(Pressure p)`
- `L181` `private double SaturatedBo(double rsScf)`
- `L193` `private double StandingBubblePointPsia()`
- `L212` `public Viscosity MuOil(Pressure p)`
- `L236` `public Viscosity MuGas(Pressure p)`
- `L261` `public double Z(Pressure p, Temperature t)`
- `L284` `private static double DakResidual(double z, double tpr, double ppr)`
- `L308` `public GasFormationVolumeFactor Bg(Pressure p)`
- `L323` `public PhaseSplit SplitAt(Composition composition, Pressure p, Temperature t)`
- `L343` `private IMaterialCatalog? _materials;`
- `L351` `public void BindMaterials(IMaterialCatalog materials)`
- `L359` `private static double Psia(Pressure p) => p.Pascals / PascalsPerPsi;`

## Imports

- `using OGSim.Kernel;`

