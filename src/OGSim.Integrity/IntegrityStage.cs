// R18.2 / R18.5 — the stage-4 integrity pass (SDD-012 §2–3).
//
// THE ENGINE DRAWS, NOT THE MODEL. The hazard model maps condition to
// probability and stops there; this is where the `hazard` stream is consumed,
// in a FIXED component order (ascending id), so the sequence is reproducible
// and adding a component cannot re-roll an existing one's fate.
//
// A FAILED COMPONENT IS SIMPLY ABSENT from the segment's network (design 04 §4)
// — there is no "broken" flag for the solver to check, and no code path where a
// failed element participates and is then ignored. R18-V3's attribution falls
// out of that: production lost is production the network never carried.
//
// The failure DAY is drawn as an integer in {0..29}, so the segment boundary
// lands on the /30ths grid exactly. A fractional day would put a boundary
// between grid points and every downstream duration would inherit the error.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Integrity;

/// <summary>A component instance under the integrity pass.</summary>
public sealed record ComponentState(
    EntityId<IWellComponent> Id,
    ContentId Tier,
    double Condition,
    bool Failed);

/// <summary>What stage 4 decided about one component.</summary>
public sealed record FailureOutcome(
    EntityId<IWellComponent> Component,
    double Condition,
    double RatePerYear,
    double Probability,
    double Draw,
    int FailureDay);

/// <summary>
/// SDD-012 §2's stage-4 pass.
/// </summary>
public sealed class IntegrityPass
{
    private readonly IDegradationModel _degradation;
    private readonly ExponentialHazardModel _hazard;
    private readonly IRandomStream _hazardStream;
    private readonly IAuditTrail _audit;

    public IntegrityPass(
        IDegradationModel degradation,
        ExponentialHazardModel hazard,
        IRandomStream hazardStream,
        IAuditTrail audit)
    {
        ArgumentNullException.ThrowIfNull(degradation);
        ArgumentNullException.ThrowIfNull(hazard);
        ArgumentNullException.ThrowIfNull(hazardStream);
        ArgumentNullException.ThrowIfNull(audit);

        _degradation = degradation;
        _hazard = hazard;
        _hazardStream = hazardStream;
        _audit = audit;
    }

    /// <summary>
    /// Ages every component, then rolls for each.
    ///
    /// <para>Components are processed in ASCENDING ID (SDD-012 §2). Two draws
    /// per failing component — the failure test and then the day — so a run's
    /// sequence depends on the component set and nothing else. A dictionary
    /// walk here would make the whole campaign's failure history depend on hash
    /// order (D-5).</para>
    /// </summary>
    public IReadOnlyList<FailureOutcome> Advance(
        IReadOnlyList<ComponentState> components,
        ServiceSeverity severity,
        Duration dt,
        out IReadOnlyList<ComponentState> aged)
    {
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(severity);

        var ordered = new List<ComponentState>(components);
        ordered.Sort((a, b) => a.Id.Value.CompareTo(b.Id.Value));

        var outcomes = new List<FailureOutcome>();
        var next = new List<ComponentState>(ordered.Count);

        for (int i = 0; i < ordered.Count; i++)
        {
            ComponentState component = ordered[i];

            // A failed component does not keep degrading — it is already out of
            // service, and continuing to age it would make the repair that
            // follows arbitrarily worse the longer it was left, which is a
            // different mechanic nobody specified.
            if (component.Failed)
            {
                next.Add(component);
                continue;
            }

            double condition = _degradation.NextCondition(component.Condition, severity, dt);
            double rate = _hazard.RateAt(condition);
            double probability = _hazard.FailureProbability(condition, dt);

            double draw = _hazardStream.NextUnit();
            bool failed = draw < probability;

            // The day is drawn ONLY on failure, so a campaign with no failures
            // consumes exactly one value per component per tick and its stream
            // position is predictable.
            int day = failed ? _hazardStream.NextInt(30) : -1;

            next.Add(component with { Condition = condition, Failed = failed });

            if (!failed) continue;

            outcomes.Add(new FailureOutcome(
                component.Id, condition, rate, probability, draw, day));

            // The fairness record (design 09 §4.2): condition, rate, the
            // probability it produced, and the draw that was compared against
            // it. A player who suspects the dice can check that the number which
            // came up actually falls under the threshold.
            _audit.Record(AuditCategory.StochasticOutcome, null, null,
                new Dictionary<string, AuditValue>
                {
                    ["component"] = new(Format(component.Id.Value)),
                    ["tier"] = new(component.Tier.Value),
                    ["stream"] = new("hazard"),
                    ["condition"] = new(Format(condition)),
                    ["ratePerYear"] = new(Format(rate)),
                    ["threshold"] = new(Format(probability)),
                    ["draw"] = new(Format(draw)),
                    ["failureDay"] = new(Format(day)),
                });
        }

        aged = next;
        return outcomes;
    }

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

    private static string Format(int value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Format(ulong value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// R18.4's three strategies (SDD-012 §3).
///
/// <para>All three produce ORDINARY operations — no special execution path.
/// That is what makes R18-V5's comparison meaningful: each strategy wins in its
/// designed circumstance because of what it costs and when, not because the
/// engine treats one of them specially.</para>
/// </summary>
public enum MaintenanceStrategy
{
    /// <summary>Nothing scheduled. Cheapest until it is not.</summary>
    RunToFailure,

    /// <summary>A maintenance operation every N ticks, whatever the condition.</summary>
    Scheduled,

    /// <summary>Triggered by condition — needs a monitoring tier installed.</summary>
    ConditionBased,
}

/// <summary>SDD-012 §3's policy, per asset class.</summary>
public sealed record MaintenancePolicy(
    MaintenanceStrategy Strategy,
    int IntervalTicks,             // Scheduled
    double ConditionTrigger,       // ConditionBased
    bool HasMonitoring)            // ConditionBased requires it (C14)
{
    /// <summary>
    /// Whether this component is due for maintenance now.
    ///
    /// <para>Condition-based WITHOUT monitoring never triggers — and that is the
    /// point of the monitoring tier. A policy that fell back to scheduled would
    /// make the monitoring purchase free, and a player would get
    /// condition-based behaviour without paying for the instrument that makes
    /// it possible.</para>
    /// </summary>
    public bool IsDue(double condition, int ticksSinceService) => Strategy switch
    {
        MaintenanceStrategy.RunToFailure => false,
        MaintenanceStrategy.Scheduled => ticksSinceService >= IntervalTicks,
        MaintenanceStrategy.ConditionBased => HasMonitoring && condition < ConditionTrigger,
        _ => false,
    };
}
