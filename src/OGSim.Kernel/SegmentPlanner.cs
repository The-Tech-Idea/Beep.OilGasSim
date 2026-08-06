// R1.14 — sub-tick segmentation (SDD-001 §9, design 15 §6, R1 §2.7).
//
// Availability is SEGMENTED, never averaged, and that is a correctness decision
// rather than a fidelity one: the network solve is non-linear, so a compressor
// available for 60% of a month is not the same as a compressor at 60% capacity.
// Segmenting is exact; averaging is simply wrong.
//
// Boundaries live on the /30ths grid as whole days, so INV9 — "durations sum to
// exactly one tick" — is integer arithmetic that cannot drift. The budget is four
// segments per tick (TM-D2); beyond it, boundaries merge to the nearest kept one
// AND EVERY MERGE IS AUDITED, so the approximation is never invisible.

namespace OGSim.Kernel;

/// <summary>
/// One entity's availability changing partway through a tick.
///
/// <paramref name="LastCommittedThroughput"/> is the merge-ranking input pinned
/// by SDD-001 §9: last-committed rather than current, because true impact would
/// need the solve this plan precedes. It is deterministic, cheap, and wrong only
/// where it does not matter — a boundary on an idle element.
/// </summary>
public sealed record AvailabilityChange(
    int Day,
    EntityRef Subject,
    bool Available,
    double LastCommittedThroughput);

public sealed class SegmentPlanner
{
    /// <summary>Design 15 §6 / TM-D2.</summary>
    public const int MaxSegments = 4;

    private static readonly int DaysPerTick = (int)Duration.DaysPerTick;

    private readonly IAuditTrail _audit;

    public SegmentPlanner(IAuditTrail audit)
    {
        ArgumentNullException.ThrowIfNull(audit);
        _audit = audit;
    }

    /// <summary>
    /// Built at stage 4, once every constraint-changing event for the tick is
    /// known. Later stages cannot add a boundary: by stage 5 the plan is what the
    /// solver runs against.
    /// </summary>
    public SegmentPlan Plan(
        IReadOnlyCollection<EntityRef> availableAtStart,
        IReadOnlyList<AvailabilityChange> changes)
    {
        ArgumentNullException.ThrowIfNull(availableAtStart);
        ArgumentNullException.ThrowIfNull(changes);

        // Day 0 is the tick's own start, not a boundary; day 30 is next tick.
        for (int i = 0; i < changes.Count; i++)
            if (changes[i].Day < 1 || changes[i].Day >= DaysPerTick)
                throw new InvariantFault("INV9", changes[i].Subject,
                    $"Availability change at day {changes[i].Day} is outside the " +
                    $"interior of the /30ths grid [1, {DaysPerTick - 1}].");

        // Impact per candidate day: throughput x the duration still to run.
        var impactByDay = new SortedDictionary<int, double>();
        for (int i = 0; i < changes.Count; i++)
        {
            AvailabilityChange change = changes[i];
            double impact = change.LastCommittedThroughput * (DaysPerTick - change.Day);
            impactByDay[change.Day] = impactByDay.TryGetValue(change.Day, out double running)
                ? running + impact
                : impact;
        }

        var candidateDays = new List<int>(impactByDay.Keys);
        List<int> keptDays = SelectWithinBudget(candidateDays, impactByDay);

        // Each dropped boundary moves to the nearest kept one — including the
        // tick's own start — and says so in the audit trail.
        var effectiveDay = new Dictionary<int, int>();
        for (int i = 0; i < candidateDays.Count; i++)
        {
            int day = candidateDays[i];
            int landing = keptDays.Contains(day) ? day : NearestBoundary(day, keptDays);
            effectiveDay[day] = landing;

            if (landing != day)
                _audit.Record(AuditCategory.Merge, null, null, new Dictionary<string, AuditValue>
                {
                    ["boundaryDay"] = new(Format(day)),
                    ["mergedToDay"] = new(Format(landing)),
                    ["impact"] = new(Format(impactByDay[day])),
                    ["reason"] = new($"segment budget {MaxSegments} exceeded"),
                });
        }

        return Build(availableAtStart, changes, keptDays, effectiveDay);
    }

    /// <summary>
    /// At most MaxSegments-1 interior boundaries. Ranked by impact, ties broken
    /// by day so the choice is total: two boundaries of equal impact must not
    /// resolve differently on two runs.
    /// </summary>
    private static List<int> SelectWithinBudget(
        List<int> candidateDays, SortedDictionary<int, double> impactByDay)
    {
        int budget = MaxSegments - 1;
        if (candidateDays.Count <= budget) return new List<int>(candidateDays);

        var ranked = new List<int>(candidateDays);
        ranked.Sort((left, right) =>
        {
            int byImpact = impactByDay[right].CompareTo(impactByDay[left]);   // descending
            return byImpact != 0 ? byImpact : left.CompareTo(right);
        });

        var kept = ranked.GetRange(0, budget);
        kept.Sort();
        return kept;
    }

    /// <summary>Nearest kept boundary, or the tick start. Ties go earlier, which
    /// is the conservative direction: the change takes effect sooner.</summary>
    private static int NearestBoundary(int day, List<int> keptDays)
    {
        int best = 0;
        int bestDistance = day;
        for (int i = 0; i < keptDays.Count; i++)
        {
            int distance = Math.Abs(keptDays[i] - day);
            if (distance < bestDistance || (distance == bestDistance && keptDays[i] < best))
            {
                best = keptDays[i];
                bestDistance = distance;
            }
        }
        return best;
    }

    private SegmentPlan Build(
        IReadOnlyCollection<EntityRef> availableAtStart,
        IReadOnlyList<AvailabilityChange> changes,
        List<int> keptDays,
        Dictionary<int, int> effectiveDay)
    {
        var starts = new List<int> { 0 };
        starts.AddRange(keptDays);

        var segments = new List<Segment>(starts.Count);
        for (int i = 0; i < starts.Count; i++)
        {
            int start = starts[i];
            int end = i + 1 < starts.Count ? starts[i + 1] : DaysPerTick;

            // Availability at this segment's start: every change whose EFFECTIVE
            // day has been reached, applied in day then subject order so the
            // result cannot depend on the order changes were reported in.
            var available = new SortedSet<EntityRef>(availableAtStart);
            var applicable = new List<AvailabilityChange>();
            for (int c = 0; c < changes.Count; c++)
                if (effectiveDay[changes[c].Day] <= start) applicable.Add(changes[c]);

            applicable.Sort((left, right) =>
            {
                int byDay = effectiveDay[left.Day].CompareTo(effectiveDay[right.Day]);
                return byDay != 0 ? byDay : left.Subject.CompareTo(right.Subject);
            });

            for (int c = 0; c < applicable.Count; c++)
            {
                if (applicable[c].Available) available.Add(applicable[c].Subject);
                else available.Remove(applicable[c].Subject);
            }

            segments.Add(new Segment(start, end - start, new List<EntityRef>(available)));
        }

        AssertInv9(segments);
        return new SegmentPlan(segments);
    }

    /// <summary>INV9 as integer arithmetic — the reason boundaries are whole days.</summary>
    private static void AssertInv9(List<Segment> segments)
    {
        int total = 0;
        for (int i = 0; i < segments.Count; i++)
        {
            if (segments[i].DurationDays <= 0)
                throw new InvariantFault("INV9", null,
                    $"Segment at day {segments[i].StartDay} has non-positive duration " +
                    $"{segments[i].DurationDays}.");
            total += segments[i].DurationDays;
        }

        if (total != DaysPerTick)
            throw new InvariantFault("INV9", null,
                $"Segment durations sum to {total}, not {DaysPerTick}.");

        if (segments.Count > MaxSegments)
            throw new InvariantFault("TM4", null,
                $"Plan has {segments.Count} segments, over the budget of {MaxSegments}.");
    }

    private static string Format(int value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}
