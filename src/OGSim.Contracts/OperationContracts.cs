// SDD-007 — the one scheduled-activity engine. Outcome drawn ONCE at start
// (audited, unexploitable); standby costs day rates only; reservations are
// worst-case so a delayed operation never finds its rig double-booked.

using OGSim.Kernel;

namespace OGSim.Contracts;

public enum OperationState { Scheduled, Active, Standby, Completed, Failed, Cancelled }

public enum OutcomeGrade { OnTime, Delayed, OverBudget, Partial, Failure, Disaster }

/// <summary>DisasterDay is an INTEGER day index — /30ths-grid exact (SDD-007 §4).</summary>
public sealed record OutcomeRow(
    OutcomeGrade Grade,
    double Probability,
    double DurationFactor,
    double CostFactor,
    int? DisasterDay);

public sealed record OutcomeTable(IReadOnlyList<OutcomeRow> Rows)   // probabilities sum to 1.0, load-checked
{
    // Finding 131.
    public bool Equals(OutcomeTable? other) =>
        other is not null && Structural.Equal(Rows, other.Rows);

    public override int GetHashCode() => Structural.HashOf(Rows);
}

/// <summary>Standby = day rates only; consumables stop (SDD-007 §3, pinned).</summary>
public sealed record CostProfile(
    Money Mobilisation,
    Money PerActiveDay,
    Money PerStandbyDay,
    Money Completion);

public interface IRig { }   // resource marker; calendars live in the scheduler

public sealed record ResourceNeeds(
    EntityId<IRig>? Rig,
    IReadOnlyList<(ContentId Discipline, int Count)> Crew)
{
    // Finding 131.
    public bool Equals(ResourceNeeds? other) =>
        other is not null && Rig.Equals(other.Rig) && Structural.Equal(Crew, other.Crew);

    public override int GetHashCode() => HashCode.Combine(Rig, Structural.HashOf(Crew));
}

public sealed record OperationSpec(
    ContentId Template,
    EntityRef Target,
    int BaseDurationDays,
    CostProfile Costs,
    ResourceNeeds Resources,
    Requirements Requirements,
    IReadOnlyList<ServiceRental> Rentals,
    OutcomeTable Outcomes)
{
    // Finding 131.
    public bool Equals(OperationSpec? other) =>
        other is not null && Template == other.Template && Target == other.Target
        && BaseDurationDays == other.BaseDurationDays && Costs == other.Costs
        && Resources == other.Resources && Requirements == other.Requirements
        && Outcomes == other.Outcomes
        && Structural.Equal(Rentals, other.Rentals);

    public override int GetHashCode() =>
        HashCode.Combine(Template, Target, BaseDurationDays, Costs, Resources,
                         Requirements, Outcomes, Structural.HashOf(Rentals));
}

public interface IOperation
{
    EntityId<IOperation> Id { get; }
    OperationSpec Spec { get; }
    OperationState State { get; }
    int ProgressDays { get; }
    Money Accrued { get; }
}

/// <summary>
/// SDD-007 §6 — registration is unconditional at asset creation (design 02
/// §3.4: no path skips abandonment); only a completed abandonment discharges.
/// Read by both the financial provision (SDD-009 §2) and the licence rules.
/// </summary>
public interface IObligationRegistry
{
    void Register(EntityRef asset, ContentId abandonmentTemplate);
    Money EstimatedCost(EntityRef asset);
    void Discharge(EntityRef asset, EntityId<IOperation> completedAbandonment);
}
