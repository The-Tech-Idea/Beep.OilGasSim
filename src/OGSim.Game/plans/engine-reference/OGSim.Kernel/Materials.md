# Materials

Source: `src\OGSim.Kernel\Materials.cs` · Lines: 167

## File intent

> SDD-002 §2b — properties, distributions and the material catalogue.
> 
> The ninth contract pass (finding 82) found this whole surface promised by R2's
> deliverables and declared in no SDD and no code: the eight R1-C passes covered
> the 03 §3.2 replaceable slots and the host surface, and never came back for
> the material layer R2 is built from.
> 
> R2.1 LAYERING CORRECTION. R2 §3's deliverables table puts these contracts in

## Namespaces

- `OGSim.Kernel`

## Type declarations

- `L35` `public abstract record Distribution`
- `L44` `public sealed record PointValue(double Value) : Distribution`
- `L52` `public sealed record NormalDistribution(double Centre, double StandardDeviation) : Distribution`
- `L64` `public sealed record LogNormalDistribution(double LogMean, double LogStandardDeviation) : Distribution`
- `L85` `public sealed record TriangularDistribution(double Minimum, double Mode, double Maximum) : Distribution`
- `L103` `public sealed record UniformDistribution(double Minimum, double Maximum) : Distribution`
- `L119` `public interface IPropertyKind`
- `L133` `public interface IProperty`
- `L143` `public enum PhaseAtStandardConditions { Liquid, Gas, Aqueous, Solid }`
- `L150` `public interface IMaterial`
- `L160` `public interface IMaterialCatalog`

## Accessible members

- `L37` `public abstract double Mean { get; }`
- `L38` `public abstract double P90 { get; }`
- `L39` `public abstract double P50 { get; }`
- `L40` `public abstract double P10 { get; }`
- `L46` `public override double Mean => Value;`
- `L47` `public override double P90 => Value;`
- `L48` `public override double P50 => Value;`
- `L49` `public override double P10 => Value;`
- `L54` `public override double Mean => Centre;`
- `L55` `public override double P90 => Centre - PhysicalConstants.NormalZ10 * StandardDeviation;`
- `L56` `public override double P50 => Centre;`
- `L57` `public override double P10 => Centre + PhysicalConstants.NormalZ10 * StandardDeviation;`
- `L66` `public override double Mean =>`
- `L68` `public override double P90 =>`
- `L70` `public override double P50 => DetMath.Exp(LogMean);`
- `L71` `public override double P10 =>`
- `L79` `public static LogNormalDistribution Product(LogNormalDistribution a, LogNormalDistribution b) =>`
- `L87` `public override double Mean => (Minimum + Mode + Maximum) / 3.0;`
- `L88` `public override double P90 => Quantile(0.10);`
- `L89` `public override double P50 => Quantile(0.50);`
- `L90` `public override double P10 => Quantile(0.90);`
- `L93` `private double Quantile(double probability)`
- `L105` `public override double Mean => (Minimum + Maximum) * 0.5;`
- `L106` `public override double P90 => Minimum + 0.10 * (Maximum - Minimum);`
- `L107` `public override double P50 => Mean;`
- `L108` `public override double P10 => Minimum + 0.90 * (Maximum - Minimum);`

