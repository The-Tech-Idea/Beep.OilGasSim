# GasProcessing

Source: `src\OGSim.Facilities\GasProcessing.cs` · Lines: 541

## File intent

> R9 — gas processing (SDD-006 §3c, §4, design 04, R9).
> 
> EACH TREATING STEP IS AN INDEPENDENT UNIT. There is no "gas plant": sweet dry
> gas needs compression only, sour wet gas needs everything, and the player pays
> for exactly the chain their gas requires (R9 §2.1). That difference is what
> makes gas quality a property of an asset rather than a label.
> 
> The flare is the most important element in this file, and not for its own

## Namespaces

- `OGSim.Facilities`

## Type declarations

- `L21` `public sealed record CompressorTier(`
- `L40` `public sealed class Compressor : IFlowElement`
- `L235` `public sealed class Flare : IFlowElement`
- `L321` `public sealed class NglExtractionPlant : IFlowElement`
- `L434` `public sealed class RemovalUnit : IFlowElement`

## Accessible members

- `L42` `private readonly CompressorTier _tier;`
- `L43` `private readonly Pressure _suction;`
- `L44` `private readonly Pressure _discharge;`
- `L45` `private readonly double _compressibility;   // Z̄`
- `L46` `private readonly int _materialCount;`
- `L48` `public Compressor(`
- `L67` `public EntityId<IFlowElement> Id { get; }`
- `L69` `public IReadOnlyList<PortSpec> Ports { get; } =`
- `L81` `public int Stages`
- `L94` `public double StageRatio => DetMath.Pow(_discharge.Pascals / _suction.Pascals, 1.0 / Stages);`
- `L97` `public Temperature StageDischargeTemperature(Temperature suctionTemperature) =>`
- `L101` `public double StageWorkJoulesPerKg(Temperature suctionTemperature)`
- `L113` `public Power ShaftPowerFor(MassRate throughput, Temperature suctionTemperature) =>`
- `L126` `public MassRate CapacityAt(Temperature ambient)`
- `L135` `public TransformResult Transform(TransformInput input)`
- `L159` `public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input)`
- `L176` `private double Exponent => (_tier.PolytropicExponent - 1.0) / _tier.PolytropicExponent;`
- `L180` `private const double CeilingTolerance = 1e-9;`
- `L182` `private static void Validate(`
- `L213` `internal static DisposedMass NoDisposal(int materialCount) => new(`
- `L218` `private static string Format(double value) =>`
- `L237` `private readonly MassRate _capacity;`
- `L238` `private readonly double _combustionEfficiency;`
- `L239` `private readonly int _materialCount;`
- `L241` `public Flare(`
- `L262` `public EntityId<IFlowElement> Id { get; }`
- `L266` `public static PortId Inlet { get; } = new(0);`
- `L271` `public IReadOnlyList<PortSpec> Ports { get; } =`
- `L274` `public TransformResult Transform(TransformInput input)`
- `L301` `public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input)`
- `L323` `private readonly ComponentSplit _feedSplit;`
- `L324` `private readonly NglRecovery _recovery;`
- `L325` `private readonly int _liquidOrdinal;`
- `L326` `private readonly int _gasOrdinal;`
- `L327` `private readonly int _materialCount;`
- `L329` `public NglExtractionPlant(`
- `L348` `public EntityId<IFlowElement> Id { get; }`
- `L351` `public IReadOnlyList<PortSpec> Ports { get; } =`
- `L363` `public double RecoveredFraction`
- `L375` `public TransformResult Transform(TransformInput input)`
- `L421` `public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input) => [];`
- `L436` `private readonly int _targetOrdinal;`
- `L437` `private readonly double _removalEfficiency;`
- `L438` `private readonly double _byProductYield;`
- `L439` `private readonly int _byProductOrdinal;`
- `L440` `private readonly int _materialCount;`
- `L442` `public RemovalUnit(`
- `L470` `public EntityId<IFlowElement> Id { get; }`
- `L472` `public ContentId Tier { get; }`
- `L476` `public IReadOnlyList<PortSpec> Ports { get; } =`
- `L483` `public TransformResult Transform(TransformInput input)`
- `L540` `public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input) => [];`

## Imports

- `using System.Collections.Immutable;`
- `using OGSim.Contracts;`
- `using OGSim.Kernel;`

