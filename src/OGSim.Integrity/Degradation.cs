// R18.1 / R18.2 — degradation and hazard (SDD-012 §0–2).
//
// NEITHER MODEL DRAWS. The hazard model returns a PROBABILITY and the engine
// performs the draw at stage 4, consuming only the `Hazard` stream — the same
// separation SDD-008 applies to observation, and for the same reason: a plugin
// drawing its own numbers could consume a different count and shift every later
// draw in that stream.
//
// THE HAZARD LAW IS EXPONENTIAL IN (1 − c), NOT A THRESHOLD. A player sitting
// just above a line is not rewarded for it, and the cost of deferring
// maintenance grows smoothly rather than arriving all at once. A threshold would
// teach the player to game the number; a curve teaches them what the number
// means.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Integrity;

/// <summary>Per-class decay coefficients, from the equipment tier (SDD-012 §1).</summary>
public sealed record DegradationCoefficients(
    double BaseRatePerYear,        // baseRate(tier)
    double WaterCutFactor,         // k_w
    double SourFactor,             // k_s
    double DutyFactor,             // k_d
    double TemperatureFactor,      // k_T
    double ServiceIntervalFactor); // k_i

/// <summary>
/// SDD-012 §1's decay:
/// <c>Δc = −baseRate · (1 + Σ severityTerms) · Δt(years)</c>.
///
/// <para>The severity terms ADD to one rather than multiplying it, so a
/// component in mild service still ages — equipment does not stop wearing out
/// because conditions are pleasant, and a multiplicative form with a zero term
/// would have said it did.</para>
/// </summary>
public sealed class SeverityWeightedDegradation : IDegradationModel
{
    private readonly DegradationCoefficients _coefficients;

    public SeverityWeightedDegradation(ContentId id, DegradationCoefficients coefficients)
    {
        ArgumentNullException.ThrowIfNull(coefficients);

        if (coefficients.BaseRatePerYear < 0.0)
            throw new ModelFault("SDD-012 §1", null,
                $"tier {id.Value} declares a negative base decay rate; equipment does not " +
                "improve by being used");

        Id = id;
        _coefficients = coefficients;
    }

    public ContentId Id { get; }

    public double NextCondition(double condition, ServiceSeverity severity, Duration dt)
    {
        ArgumentNullException.ThrowIfNull(severity);

        if (condition is < 0.0 or > 1.0 || double.IsNaN(condition))
            throw new ModelFault("SDD-012 §1", null,
                $"condition {Format(condition)} is not in [0, 1]");

        if (dt.Days < 0.0)
            throw new ModelFault("SDD-012 §1", null, "negative elapsed time");

        double years = dt.Days / DaysPerYear;

        double severitySum =
            _coefficients.WaterCutFactor * severity.WaterCut
            + _coefficients.SourFactor * severity.SourFraction
            + _coefficients.DutyFactor * severity.DutyFraction
            + _coefficients.TemperatureFactor * severity.OverTemperature
            + _coefficients.ServiceIntervalFactor * (severity.TicksSinceService / TicksPerYear);

        double decay = _coefficients.BaseRatePerYear * (1.0 + severitySum) * years;

        // Clamped at zero and NEVER restored implicitly. Only a completed
        // maintenance operation raises condition — a component that healed by
        // being left alone would make maintenance optional in the one way the
        // player would notice.
        return Math.Clamp(condition - decay, 0.0, 1.0);
    }

    /// <summary>30/360 (SDD-001 §3): every month is thirty days, so a year is
    /// 360 and the /30ths grid stays exact.</summary>
    private const double DaysPerYear = 360.0;
    private const double TicksPerYear = 12.0;

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// SDD-012 §2's pinned law: <c>λ(c) = λ_base · exp(k_h · (1 − c))</c>,
/// <c>p = 1 − exp(−λ·Δt)</c>.
///
/// <para>Design 03 §3.2 names this slot the "off ↔ realistic ↔ punishing"
/// difficulty dial — the whole of difficulty is one content entry, not a
/// parallel code path.</para>
/// </summary>
public sealed class ExponentialHazardModel : IHazardModel
{
    private readonly double _baseRatePerYear;   // λ_base
    private readonly double _conditionExponent; // k_h

    public ExponentialHazardModel(ContentId id, double baseRatePerYear, double conditionExponent)
    {
        if (baseRatePerYear < 0.0)
            throw new ModelFault("SDD-012 §2", null,
                $"tier {id.Value} declares a negative base failure rate");

        if (conditionExponent < 0.0)
            throw new ModelFault("SDD-012 §2", null,
                $"tier {id.Value} declares a negative condition exponent; worse condition " +
                "would make failure LESS likely");

        Id = id;
        _baseRatePerYear = baseRatePerYear;
        _conditionExponent = conditionExponent;
    }

    public ContentId Id { get; }

    /// <summary>λ at a condition, per year. Exposed because it is the number the
    /// audit records and the advisor reasons about.</summary>
    public double RateAt(double condition) =>
        _baseRatePerYear * DetMath.Exp(_conditionExponent * (1.0 - condition));

    public double FailureProbability(double condition, Duration dt)
    {
        if (condition is < 0.0 or > 1.0 || double.IsNaN(condition))
            throw new ModelFault("SDD-012 §2", null,
                $"condition {condition.ToString("R", System.Globalization.CultureInfo.InvariantCulture)} " +
                "is not in [0, 1]");

        if (dt.Days <= 0.0) return 0.0;

        // 1 − exp(−λΔt): the probability of at least one failure in the
        // interval, which saturates toward 1 rather than exceeding it. A linear
        // λ·Δt would have produced probabilities above 1 for a badly-degraded
        // component over a long tick.
        return 1.0 - DetMath.Exp(-RateAt(condition) * dt.Days / 360.0);
    }
}
