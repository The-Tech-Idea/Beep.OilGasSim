// SDD-002 §2b — properties, distributions and the material catalogue.
//
// The ninth contract pass (finding 82) found this whole surface promised by R2's
// deliverables and declared in no SDD and no code: the eight R1-C passes covered
// the 03 §3.2 replaceable slots and the host surface, and never came back for
// the material layer R2 is built from.
//
// Data shapes and contracts only — the catalogue implementations, the
// distribution arithmetic and the black-oil model are R2 proper.

using OGSim.Kernel;

namespace OGSim.Contracts;

// ---------------------------------------------------------------- distributions

/// <summary>
/// R2 §2.1 — a property holds a DISTRIBUTION, never a bare value. A measured
/// property is a distribution with very small spread, not a special case: the
/// alternative invites every consumer to read the scalar and ignore the
/// uncertainty, and within months the uncertainty is decorative and the
/// exploration game has quietly stopped working.
///
/// P90/P50/P10 follow PETROLEUM practice, which is the reverse of the
/// statistical reading: <see cref="P90"/> is the LOW, conservative, proved case
/// (90% probability of being exceeded) and <see cref="P10"/> is the HIGH one, so
/// numerically P90 &lt; P50 &lt; P10. Reading P10 as "the 10th percentile" books
/// possible reserves as proved and no type objects — which is why the ordering
/// is stated on the contract rather than left to the reader (SDD-002 §2b).
/// </summary>
public abstract record Distribution
{
    public abstract double Mean { get; }
    public abstract double P90 { get; }
    public abstract double P50 { get; }
    public abstract double P10 { get; }
}

/// <summary>Zero spread — a measurement, not a special case.</summary>
public sealed record PointValue(double Value) : Distribution
{
    public override double Mean => Value;
    public override double P90 => Value;
    public override double P50 => Value;
    public override double P10 => Value;
}

public sealed record NormalDistribution(double Centre, double StandardDeviation) : Distribution
{
    public override double Mean => Centre;
    public override double P90 => Centre - PhysicalConstants.NormalZ10 * StandardDeviation;
    public override double P50 => Centre;
    public override double P10 => Centre + PhysicalConstants.NormalZ10 * StandardDeviation;
}

/// <summary>
/// Essential rather than optional: hydrocarbon volumes are products of uncertain
/// terms and come out strongly right-skewed (design 05 §1.4).
/// </summary>
public sealed record LogNormalDistribution(double LogMean, double LogStandardDeviation) : Distribution
{
    public override double Mean =>
        DetMath.Exp(LogMean + 0.5 * LogStandardDeviation * LogStandardDeviation);
    public override double P90 =>
        DetMath.Exp(LogMean - PhysicalConstants.NormalZ10 * LogStandardDeviation);
    public override double P50 => DetMath.Exp(LogMean);
    public override double P10 =>
        DetMath.Exp(LogMean + PhysicalConstants.NormalZ10 * LogStandardDeviation);

    /// <summary>
    /// R2-V5 — a product of log-normals is log-normal, analytically: the log
    /// parameters add and the variances add. This is why volumetrics can be
    /// propagated in closed form instead of sampled (design 05 §1.4).
    /// </summary>
    public static LogNormalDistribution Product(LogNormalDistribution a, LogNormalDistribution b) =>
        new(a.LogMean + b.LogMean,
            DetMath.Sqrt(a.LogStandardDeviation * a.LogStandardDeviation
                       + b.LogStandardDeviation * b.LogStandardDeviation));
}

public sealed record TriangularDistribution(double Minimum, double Mode, double Maximum) : Distribution
{
    public override double Mean => (Minimum + Mode + Maximum) / 3.0;
    public override double P90 => Quantile(0.10);
    public override double P50 => Quantile(0.50);
    public override double P10 => Quantile(0.90);

    /// <summary>Analytic inverse CDF — no sampling, so it is D-1 safe by construction.</summary>
    private double Quantile(double probability)
    {
        double span = Maximum - Minimum;
        double lower = (Mode - Minimum) / span;
        return probability < lower
            ? Minimum + DetMath.Sqrt(probability * span * (Mode - Minimum))
            : Maximum - DetMath.Sqrt((1.0 - probability) * span * (Maximum - Mode));
    }
}

public sealed record UniformDistribution(double Minimum, double Maximum) : Distribution
{
    public override double Mean => (Minimum + Maximum) * 0.5;
    public override double P90 => Minimum + 0.10 * (Maximum - Minimum);
    public override double P50 => Mean;
    public override double P10 => Minimum + 0.90 * (Maximum - Minimum);
}

// ---------------------------------------------------------------- property kinds

/// <summary>
/// R2.1 — what a property measures, what units it accepts, and where its values
/// stop being physical. The validity range lives on the KIND rather than on each
/// correlation, so R2-V10's "out of range is a model fault, never a silent
/// extrapolation" is checked once at the boundary every value crosses.
/// </summary>
public interface IPropertyKind
{
    ContentId Id { get; }
    Dimension Dimension { get; }
    double MinimumValid { get; }        // canonical SI, inclusive
    double MaximumValid { get; }
    BeliefSpace Space { get; }          // Log for volumes and permeability
}

/// <summary>
/// R2.2 — every property states how it is known. Provenance drives the default
/// uncertainty, the R14 belief weighting, and the player-facing "how do we know
/// this?"; AsOf drives staleness. None of the four is optional.
/// </summary>
public interface IProperty
{
    ContentId Kind { get; }
    Distribution Value { get; }
    Provenance Source { get; }
    GameDate AsOf { get; }
}

// ---------------------------------------------------------------- materials

public enum PhaseAtStandardConditions { Liquid, Gas, Aqueous, Solid }

/// <summary>
/// R2.4 — a material is a catalogue entry, not a code type. R2-V1 requires a
/// synthetic oil-like material to behave identically to crude through every
/// stream operation, which is only true while nothing branches on identity.
/// </summary>
public interface IMaterial
{
    ContentId Id { get; }
    /// <summary>Catalogue position, and the index into a Composition. NEVER
    /// persisted — ordinals shift when content changes (SDD-004 §6).</summary>
    MaterialId Ordinal { get; }
    PhaseAtStandardConditions Phase { get; }
    IReadOnlyList<IProperty> Properties { get; }
}

public interface IMaterialCatalog
{
    int Count { get; }
    IMaterial this[MaterialId ordinal] { get; }
    /// <summary>Unknown id is a content fault, never null (SDD-004 §3).</summary>
    IMaterial Resolve(ContentId id);
    bool TryResolve(ContentId id, out IMaterial? material);
}
