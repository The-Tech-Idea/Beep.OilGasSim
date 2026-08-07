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

public sealed record All(IReadOnlyList<Predicate> Items) : Predicate;

public sealed record Any(IReadOnlyList<Predicate> Items) : Predicate;

public sealed record CountOf(int N, IReadOnlyList<Predicate> Items) : Predicate;

/// <summary>Stateful: a consecutive-true counter, persisted with the objective.</summary>
public sealed record SustainedFor(Predicate Inner, int Ticks) : Predicate;

/// <summary>Stateful: a current-step index, persisted with the objective.</summary>
public sealed record InSequence(IReadOnlyList<Predicate> Steps) : Predicate;

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
