// SDD-014 §1 — the objective predicate AST.
//
// A CLOSED HIERARCHY, like Effect (SDD-005 §4). Content expresses these as a
// small JSON tree validated at load, so an objective is data rather than code
// and a scenario cannot smuggle behaviour in through a mission file.
//
// Declared at R24. SDD-014's own pass-10 note records that four of these types
// were used in its declarations and declared nowhere — the same defect as
// SDD-002's ConstraintWriter and SDD-005's EnvelopeContext, a signature that
// cannot be implemented because a type it names does not exist.

using OGSim.Kernel;

namespace OGSim.Contracts;

/// <summary>
/// A dotted key resolved against the read-model schema registry (SDD-014 §2).
///
/// <para>A struct over a string rather than a bare string, so a path cannot be
/// confused with a display id or a localisation key at a call site — the same
/// reasoning that makes every id in the engine a typed wrapper.</para>
/// </summary>
public readonly record struct ReadModelPath(string Path);

public enum CompareOp { Lt, Le, Eq, Ne, Ge, Gt }

public enum AggOp { Max, Min, Sum, Count, Any, All }

/// <summary>
/// Narrows an <see cref="OnEvent"/> match. Both fields optional: an unset field
/// does not narrow.
///
/// <para>Category is already <c>OnEvent</c>'s first argument, so this covers the
/// remaining two. Deliberately no payload fields — that keeps objectives unable
/// to see anything the event does not already carry (open item S014-1).</para>
/// </summary>
public sealed record EventFilter(EntityRef? Subject, Severity? MinimumSeverity);

public abstract record Predicate;

/// <summary>A value from the read model — the ONLY way an objective sees state.</summary>
public sealed record Metric(ReadModelPath Path) : Predicate;

public sealed record Const(double Value) : Predicate;

public sealed record Compare(Predicate L, CompareOp Op, Predicate R) : Predicate;

// Finding 131 on all four combinators: an objective tree is compared when a
// scenario asks whether a loaded mission is the one it shipped, and the tree is
// exactly where the collections are.
public sealed record All(IReadOnlyList<Predicate> Items) : Predicate
{
    public bool Equals(All? other) => other is not null && Structural.Equal(Items, other.Items);

    public override int GetHashCode() => Structural.HashOf(Items);
}

public sealed record Any(IReadOnlyList<Predicate> Items) : Predicate
{
    public bool Equals(Any? other) => other is not null && Structural.Equal(Items, other.Items);

    public override int GetHashCode() => Structural.HashOf(Items);
}

public sealed record CountOf(int N, IReadOnlyList<Predicate> Items) : Predicate
{
    public bool Equals(CountOf? other) =>
        other is not null && N == other.N && Structural.Equal(Items, other.Items);

    public override int GetHashCode() => HashCode.Combine(N, Structural.HashOf(Items));
}

/// <summary>Stateful: a consecutive-true counter, persisted with the objective.</summary>
public sealed record SustainedFor(Predicate Inner, int Ticks) : Predicate;

/// <summary>Stateful: a current-step index, persisted with the objective.</summary>
public sealed record InSequence(IReadOnlyList<Predicate> Steps) : Predicate
{
    public bool Equals(InSequence? other) =>
        other is not null && Structural.Equal(Steps, other.Steps);

    public override int GetHashCode() => Structural.HashOf(Steps);
}

/// <summary>A failure condition — true while the inner predicate has never held.</summary>
public sealed record Never(Predicate Inner) : Predicate;

/// <summary>True for the tick the event fired, and no longer.</summary>
public sealed record OnEvent(EventCategory Category, EventFilter Filter) : Predicate;

/// <summary>
/// The quantifier: "any well's water cut above 0.6" is
/// <c>Compare(Aggregate(wells, Max, waterCut), Gt, Const(0.6))</c>.
///
/// <para>Without it, per-item objectives were expressible only one id at a time
/// — unusable for a fleet-level mission, which is most of them.</para>
/// </summary>
public sealed record Aggregate(
    ReadModelPath Collection, AggOp Op, ReadModelPath ItemField) : Predicate;

/// <summary>SDD-014 §3. What the player is asked to do.</summary>
public sealed record Objective(
    ContentId Id,
    Predicate Condition,
    Tick? Deadline,
    double Weight,
    bool Visible);

/// <summary>
/// What an objective actually ASKS FOR, published so a host can say it.
/// </summary>
/// <remarks>
/// <para><b>Without this a host cannot state the goal.</b> The read model
/// carried whether an objective was met and nothing about what it wanted, so
/// every screen that told the player the target held its own copy of the
/// number — and six of them in the Godot client were still saying $600M after
/// the scenario moved to $360M. The game was telling a player one figure and
/// scoring them against another (law L5: one owner per fact).</para>
///
/// <para><b>Only the shape that HAS a target.</b> An objective reading
/// <c>Compare(Metric, op, Const)</c> has one and it is published; anything else
/// — a <c>Never</c>, a compound — publishes none rather than inventing one.
/// Absence is the honest answer for a goal that is not a threshold.</para>
///
/// <para>The METRIC travels with it because a target is meaningless without
/// knowing what it measures: 360,000,000 is a sum of money or a volume or a
/// count depending entirely on the path beside it.</para>
/// </remarks>
public sealed record ObjectiveGoal(
    ContentId Objective,
    ReadModelPath Metric,
    CompareOp Op,
    double Target)
{
    /// <summary>
    /// The goal this objective states, or none when it does not state one.
    /// </summary>
    public static ObjectiveGoal? Of(Objective objective)
    {
        ArgumentNullException.ThrowIfNull(objective);

        return objective.Condition is Compare(Metric metric, var op, Const target)
            ? new ObjectiveGoal(objective.Id, metric.Path, op, target.Value)
            : null;
    }
}

/// <summary>
/// The sealed position an objective is evaluated against (SDD-014 §5a):
/// read-model values by PATH, the collections an <see cref="Aggregate"/>
/// quantifies over, and the tick's events. Nothing else.
///
/// <para>It lives here rather than in <c>OGSim.Objectives</c> because it crosses
/// a contract boundary — <see cref="IScenarioRunner.Evaluate"/> takes one. The
/// evaluator that consumes it stays in the module: the same split as
/// <c>Observation</c> and <c>ObservationSampler</c>, where the shape is
/// vocabulary and the thing that acts on it is not.</para>
/// </summary>
public sealed record ObjectiveSnapshot(
    IReadOnlyDictionary<string, double> Values,
    IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, double>>> Collections,
    IReadOnlyList<EngineEvent> Events)
{
    // Finding 131. Collections nests a list of maps inside a map, so the
    // per-element comparison has to reach all the way down — which is exactly
    // what the default equality does not do at any level.
    public bool Equals(ObjectiveSnapshot? other) =>
        other is not null
        && Structural.Equal(Values, other.Values)
        && CollectionsEqual(Collections, other.Collections)
        && Structural.Equal(Events, other.Events);

    private static bool CollectionsEqual(
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, double>>> left,
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, double>>> right)
    {
        if (left.Count != right.Count) return false;

        foreach (KeyValuePair<string, IReadOnlyList<IReadOnlyDictionary<string, double>>> pair in left)
        {
            if (!right.TryGetValue(pair.Key, out IReadOnlyList<IReadOnlyDictionary<string, double>>? other))
                return false;

            if (pair.Value.Count != other.Count) return false;

            for (int i = 0; i < pair.Value.Count; i++)
                if (!Structural.Equal(pair.Value[i], other[i])) return false;
        }

        return true;
    }

    public override int GetHashCode() =>
        HashCode.Combine(Structural.HashOf(Values), Collections.Count,
                         Structural.HashOf(Events));
}
