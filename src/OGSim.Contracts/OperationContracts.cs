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

/// <summary>
/// What an operation put into or took out of the world this tick, outside the
/// routed network (SDD-007 §5b).
///
/// <para>A well test produces and flares real barrels. Frac-fluid recovery
/// returns real mass. These are small volumes and they are not exceptions to
/// conservation — they post into the same tick terms as everything else, with
/// the operation as the audited element, so INV1 covers them without a special
/// ledger.</para>
/// </summary>
public sealed record OperationMass(Composition Sourced, DisposedMass Disposed)
{
    /// <summary>
    /// The ordinary case: an operation that moves no mass of its own. Not a
    /// fallback — most operations genuinely move none, and saying so explicitly
    /// is how the conservation check knows it has covered them.
    ///
    /// <para>Takes the catalogue's material count because a zero composition is
    /// still a composition of a particular width; a shared empty singleton would
    /// be the one value in the engine that does not know how many materials
    /// exist.</para>
    /// </summary>
    public static OperationMass None(int materialCount)
    {
        Composition zero = Composition.Zero(materialCount);
        return new OperationMass(zero, new DisposedMass(zero, zero, zero));
    }
}

public interface IOperation
{
    EntityId<IOperation> Id { get; }
    OperationSpec Spec { get; }
    OperationState State { get; }
    int ProgressDays { get; }
    Money Accrued { get; }

    /// <summary>
    /// This tick's mass movement (SDD-007 §5b).
    ///
    /// <para><b>If the site has a routed test separator, the network path wins
    /// and this reports nothing.</b> Both paths reporting would double-count the
    /// same barrels — the operation is the fallback route for mass that has
    /// nowhere else to go, never a second one alongside it.</para>
    /// </summary>
    OperationMass MassThisTick { get; }
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

    /// <summary>
    /// Everything still owed, at today's estimate — what a company owes the
    /// future (SDD-007 §6).
    ///
    /// <para>On the CONTRACT because two things outside the registry need it:
    /// stage 8 accrues the abandonment provision against it (SDD-009 §2), and a
    /// player deciding whether they can afford to stop is deciding against this
    /// number. It existed on the implementation and nowhere a consumer could
    /// reach.</para>
    /// </summary>
    Money TotalOutstanding { get; }
    void Discharge(EntityRef asset, EntityId<IOperation> completedAbandonment);
}
