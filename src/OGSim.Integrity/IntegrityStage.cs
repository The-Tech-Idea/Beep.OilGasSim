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

/// <summary>
/// One piece of equipment under the integrity pass.
///
/// <para>A FLOW ELEMENT, per SDD-012 §2's R20d.22 amendment: the things that can
/// be absent from a network are exactly the things whose absence the segment
/// plan can express, and a failure this pass could not express as absence would
/// have nowhere to go. §1's severity terms were always stated per equipment
/// class rather than per downhole component — a separator in marine air
/// corrodes — and a completion is a flow element too, so nothing narrows.</para>
/// </summary>
public sealed record ComponentState(
    EntityId<IFlowElement> Id,
    ContentId Tier,
    double Condition,
    bool Failed,

    // ON THE STATE because it IS state: §1's k_i term reads how long this
    // particular item has gone unserviced, which is a fact about the item and
    // not about the field it sits in. Severity is computed FROM this rather
    // than handed in beside it, which is what stops two callers disagreeing
    // about a component's own history (law L5).
    int TicksSinceService,

    // WHETHER ANYONE CAN SEE ITS CONDITION (C14, §3's R20d.26.4 amendment).
    // A property of the item, because the kit is bolted to the item — and the
    // one owner of it, so the read model asks here rather than keeping a second
    // list of what is instrumented (L5).
    bool Monitored);

/// <summary>What stage 4 decided about one component.</summary>
public sealed record FailureOutcome(
    EntityId<IFlowElement> Component,
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
        Func<ComponentState, ServiceSeverity> severityOf,
        Duration dt,
        out IReadOnlyList<ComponentState> aged)
    {
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(severityOf);

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
                next.Add(component with { TicksSinceService = component.TicksSinceService + 1 });
                continue;
            }

            double condition = _degradation.NextCondition(
                component.Condition, severityOf(component), dt);
            double rate = _hazard.RateAt(condition);
            double probability = _hazard.FailureProbability(condition, dt);

            double draw = _hazardStream.NextUnit();
            bool failed = draw < probability;

            // The day is drawn ONLY on failure, so a campaign with no failures
            // consumes exactly one value per component per tick and its stream
            // position is predictable.
            int day = failed ? _hazardStream.NextInt(30) : -1;

            // The service clock runs whether or not the item failed: a
            // component that broke has still not been serviced, and stopping the
            // count would make a neglected item look freshly maintained the
            // moment it gave out.
            next.Add(component with
            {
                Condition = condition,
                Failed = failed,
                TicksSinceService = component.TicksSinceService + 1,
            });

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

// SDD-012 §3's THREE STRATEGIES ARE NOT A TYPE IN THIS ENGINE, and the record
// that used to be here is deleted rather than left unused (§3's R20d.26.4
// amendment). A strategy is how a player drives ordinary operations — §3's own
// "all three produce ordinary SDD-007 operations, no special execution path" —
// so run-to-failure is answering only failures, scheduled is servicing on a
// clock, and condition-based is servicing on what the chain view reports. A
// declared policy the engine scheduled from would be a second way to state one
// law, and L5 allows one owner per fact.
//
// Its monitoring gate now lives where the decision is: `AssetIntegrity`
// remembers which items are instrumented, the read model publishes a condition
// only for those, and `service-equipment` is refused on the rest. That gate had
// been stated in two places and enforced in neither, because `IsDue` was called
// by its own unit test alone (finding 191).
