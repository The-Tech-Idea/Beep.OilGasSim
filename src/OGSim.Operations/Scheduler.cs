// R12.2 / R12.3 — scheduling, reservation and contention (SDD-007 §2, R12 §2.3).
//
// CONTENTION IS A REJECTION WITH A REASON, NEVER A SILENT QUEUE. A silent queue
// hides the constraint; an explicit "no rig of this class until 1978-03" tells
// the player they need another rig, which is the decision the phase exists to
// put in front of them.
//
// Resources are reserved for the WORST CASE duration (SDD-007 §2, pinned), so a
// delayed operation never finds its rig double-booked. The alternative is a
// cascading re-plan solver, and a schedule that silently re-plans is a schedule
// the player cannot reason about.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Operations;

/// <summary>Why a submission was refused, in terms the player can act on.</summary>
public sealed record ScheduleRefusal(IReadOnlyList<string> Reasons)
{
    // Finding 131.
    public bool Equals(ScheduleRefusal? other) =>
        other is not null && Structural.Equal(Reasons, other.Reasons);

    public override int GetHashCode() => Structural.HashOf(Reasons);
}

public abstract record ScheduleResult;

public sealed record Scheduled(Operation Operation) : ScheduleResult;

public sealed record Refused(ScheduleRefusal Refusal) : ScheduleResult;

/// <summary>
/// A rig's day-granular occupancy on the /30ths grid (SDD-007 §2).
/// </summary>
internal sealed class RigCalendar
{
    private readonly List<(int Start, int End, EntityId<IOperation> By)> _reserved = [];

    /// <summary>The first day at or after <paramref name="from"/> on which the
    /// rig is free for <paramref name="days"/> — the number the refusal quotes,
    /// because "unavailable" without a date is not actionable.</summary>
    public int NextFreeFrom(int from, int days)
    {
        int candidate = from;

        // Reservations are kept sorted, so one pass finds the first gap.
        for (int i = 0; i < _reserved.Count; i++)
        {
            (int start, int end, _) = _reserved[i];
            if (candidate + days <= start) return candidate;
            if (end > candidate) candidate = end;
        }

        return candidate;
    }

    public bool IsFree(int start, int days)
    {
        int end = start + days;

        for (int i = 0; i < _reserved.Count; i++)
        {
            (int reservedStart, int reservedEnd, _) = _reserved[i];
            if (start < reservedEnd && reservedStart < end) return false;
        }

        return true;
    }

    public void Reserve(int start, int days, EntityId<IOperation> by)
    {
        _reserved.Add((start, start + days, by));
        _reserved.Sort((a, b) => a.Start.CompareTo(b.Start));
    }

    public void Release(EntityId<IOperation> by) =>
        _reserved.RemoveAll(r => r.By == by);
}

/// <summary>
/// SDD-007 §2. Validates, reserves, and draws the outcome.
/// </summary>
public sealed class OperationScheduler
{
    private readonly IRandomStream _outcomes;
    private readonly IAuditTrail _audit;
    private readonly int _materialCount;
    private readonly Dictionary<EntityId<IRig>, RigCalendar> _calendars = [];
    private readonly List<EntityId<IRig>> _rigOrder = [];

    private ulong _nextOperationId = 1;

    /// <summary>
    /// The next id this scheduler will issue — SAVED STATE, not a derived value
    /// (finding 215).
    ///
    /// <para><see cref="Reinstate"/> pushes the counter past every RUNNING
    /// operation it restores, which is enough only while nothing has finished:
    /// an operation that completed before the save is gone from the register, so
    /// its id is not among those restored and the counter comes back LOWER than
    /// the game it was saved from. The next job then takes an id its twin in the
    /// original engine had already used, and two engines that agree about every
    /// value disagree about identity.</para>
    /// </summary>
    public ulong NextOperationId => _nextOperationId;

    /// <summary>Resumes the sequence, never rewinding it: the restored running
    /// operations have already pushed it up, and their ids must stay unused.</summary>
    public void ResumeIdsFrom(ulong next) =>
        _nextOperationId = Math.Max(_nextOperationId, next);

    /// <param name="materialCount">How wide a composition is in this game.
    /// Operations that move mass (SDD-007 §5b) report one, and a zero movement
    /// is still a composition of a particular width — so the scheduler carries
    /// the catalogue's width rather than letting each operation guess.</param>
    public OperationScheduler(
        IRandomStream outcomeStream, IAuditTrail audit, int materialCount)
    {
        ArgumentNullException.ThrowIfNull(outcomeStream);
        ArgumentNullException.ThrowIfNull(audit);

        if (materialCount < 0)
            throw new ModelFault("SDD-002 §2", null,
                $"a catalogue cannot have {materialCount} materials");

        _outcomes = outcomeStream;
        _audit = audit;
        _materialCount = materialCount;
    }

    /// <summary>Rigs are registered before they can be reserved. A rig the
    /// scheduler has never heard of is a composition error, not an empty
    /// calendar that silently accepts everything.</summary>
    public void Register(EntityId<IRig> rig)
    {
        if (_calendars.ContainsKey(rig)) return;

        _calendars.Add(rig, new RigCalendar());
        _rigOrder.Add(rig);
    }

    /// <summary>
    /// SDD-007 §2's Submit. Every check runs and EVERY failure is reported —
    /// a player who fixes one reason and resubmits only to hit the next has
    /// been made to pay twice for one piece of information.
    /// </summary>
    public ScheduleResult Submit(
        OperationSpec spec,
        int startDay,
        IReadOnlyList<TechnologyId> availableCapabilities,
        Func<EntityRef, bool> targetExists)
    {
        IReadOnlyList<string> reasons =
            Refusals(spec, startDay, availableCapabilities, targetExists);

        if (reasons.Count > 0) return new Refused(new ScheduleRefusal(reasons));

        var id = new EntityId<IOperation>(_nextOperationId++);
        DrawnOutcome outcome = Draw(spec, id);

        if (spec.Resources.Rig is EntityId<IRig> committed)
            _calendars[committed].Reserve(startDay, WorstCaseDays(spec), id);

        return new Scheduled(new Operation(id, spec, outcome, _audit, _materialCount));
    }

    /// <summary>
    /// Every reason this submission would be refused, and <b>no side effect</b>
    /// (SDD-007 §2; R1 §2.5's two-phase rule).
    ///
    /// <para>Separated from <see cref="Submit"/> so a command VALIDATOR can ask
    /// the question without reserving a rig. The two were fused, which was
    /// invisible while operations were only ever submitted directly — but a
    /// validator that had to call <c>Submit</c> to find out whether it could
    /// would have booked the calendar as a side effect of saying "no", and a
    /// validator is required to be pure.</para>
    /// </summary>
    public IReadOnlyList<string> Refusals(
        OperationSpec spec,
        int startDay,
        IReadOnlyList<TechnologyId> availableCapabilities,
        Func<EntityRef, bool> targetExists)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(availableCapabilities);
        ArgumentNullException.ThrowIfNull(targetExists);

        var reasons = new List<string>();

        // 1. Tech gating, at SCHEDULING only (R12 §2.5b). Execution never
        //    re-checks, so a mid-operation technology change strands nothing.
        foreach (TechnologyId required in spec.Requirements.Tech)
            if (!availableCapabilities.Contains(required))
                reasons.Add($"missing capability {required.Value}");

        // 2. Prerequisites.
        if (!targetExists(spec.Target))
            reasons.Add($"target {spec.Target.Kind} {spec.Target.Value} does not exist");

        if (spec.BaseDurationDays <= 0)
            reasons.Add($"duration {spec.BaseDurationDays} days is not positive");

        // 3. Resource reservation, for the WORST-CASE duration.
        int worstCase = WorstCaseDays(spec);

        if (spec.Resources.Rig is EntityId<IRig> rig)
        {
            if (!_calendars.TryGetValue(rig, out RigCalendar? calendar))
                reasons.Add($"rig {rig.Value} is not registered with the scheduler");
            else if (!calendar.IsFree(startDay, worstCase))
                reasons.Add(
                    $"rig {rig.Value} is committed; next free on day " +
                    $"{Format(calendar.NextFreeFrom(startDay, worstCase))}");
        }

        return reasons;
    }

    /// <summary>
    /// Puts a saved operation back, with the outcome it was already given and
    /// the progress it had made, and re-reserves its rig (SDD-013 §4).
    ///
    /// <para>The outcome is <b>restored, never redrawn</b>. It was drawn once
    /// when the operation began (SDD-007 §4), and redrawing it on load would
    /// let a player reload the month before a well finished and try again —
    /// the same exploit drawing-at-start exists to prevent, arriving through
    /// the save instead of through the clock.</para>
    ///
    /// <para>The id counter moves past the restored id, so operations started
    /// after a load cannot collide with ones started before it.</para>
    /// </summary>
    public Operation Reinstate(
        EntityId<IOperation> id,
        OperationSpec spec,
        DrawnOutcome outcome,
        int startDay,
        int progressDays,
        Money accrued,
        OperationState state)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(outcome);

        if (spec.Resources.Rig is EntityId<IRig> rig)
        {
            Register(rig);
            _calendars[rig].Reserve(startDay, WorstCaseDays(spec), id);
        }

        _nextOperationId = Math.Max(_nextOperationId, id.Value + 1);

        var operation = new Operation(id, spec, outcome, _audit, _materialCount);
        operation.Reinstate(progressDays, accrued, state);
        return operation;
    }

    /// <summary>R12-V8: cancelling frees the rig immediately.</summary>
    public void Release(Operation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (operation.Spec.Resources.Rig is EntityId<IRig> rig
            && _calendars.TryGetValue(rig, out RigCalendar? calendar))
            calendar.Release(operation.Id);
    }

    /// <summary>
    /// SDD-007 §4. One draw, at start, from the `operations` stream — audited
    /// with the draw and the threshold it crossed, so a player who suspects the
    /// dice can check them (design 09 §4.2, R12-V7).
    /// </summary>
    private DrawnOutcome Draw(OperationSpec spec, EntityId<IOperation> id)
    {
        double draw = _outcomes.NextUnit();

        double cumulative = 0.0;
        OutcomeRow chosen = spec.Outcomes.Rows[^1];
        double threshold = 1.0;

        for (int i = 0; i < spec.Outcomes.Rows.Count; i++)
        {
            cumulative += spec.Outcomes.Rows[i].Probability;
            if (draw >= cumulative) continue;

            chosen = spec.Outcomes.Rows[i];
            threshold = cumulative;
            break;
        }

        int effective = Math.Max(1, (int)Math.Round(
            spec.BaseDurationDays * chosen.DurationFactor, MidpointRounding.ToEven));

        _audit.Record(AuditCategory.StochasticOutcome, null, null, new Dictionary<string, AuditValue>
        {
            ["operation"] = new(Format((long)id.Value)),
            ["stream"] = new("operations"),
            ["draw"] = new(FormatDouble(draw)),
            ["threshold"] = new(FormatDouble(threshold)),
            ["grade"] = new(chosen.Grade.ToString()),
            ["effectiveDurationDays"] = new(Format(effective)),
        });

        return new DrawnOutcome(chosen, draw, effective);
    }

    /// <summary>
    /// SDD-007 §2's worst case: the longest any outcome could make this
    /// operation. Reserving for it is why a delayed job never finds its rig
    /// double-booked.
    /// </summary>
    internal static int WorstCaseDays(OperationSpec spec)
    {
        double worst = 1.0;
        for (int i = 0; i < spec.Outcomes.Rows.Count; i++)
            if (spec.Outcomes.Rows[i].DurationFactor > worst)
                worst = spec.Outcomes.Rows[i].DurationFactor;

        return Math.Max(1, (int)Math.Ceiling(spec.BaseDurationDays * worst));
    }

    private static string Format(int value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Format(long value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Format(ulong value) =>
        value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string FormatDouble(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}
