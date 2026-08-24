// SDD-014 §5 — scenarios, challenges and campaigns.
//
// A SCENARIO IS CONTENT, NOT CODE (design 03 §3.3, 18 §3). Every mission,
// challenge and campaign chapter the game ships is one of these records loaded
// from JSON: a world to open, a position to open it from, things to achieve,
// things that must never happen, what the run is scored on, and what the
// scenario itself does to the world while it runs.
//
// The design's ten challenge patterns (18 §3.3) are all expressible here and
// none of them needs a new type:
//
//   constrained resource   — a starting state with $200M and one rig
//   turnaround             — a starting state at 40% uptime
//   maximise recovery      — Scoring = [Recovery]
//   hard limit             — Failures = [Never(routine flaring > 0)]
//   beat the clock         — Deadline, and a work-commitment objective
//   hostile setting        — an authored world plus a reality profile
//   price shock            — a ScriptedParameter on the price model
//   exploration efficiency — Scoring = [FindingCost], with a cash constraint
//   HSE perfect run        — Failures = [Never(serious incident)]
//   one-shot               — a starting state with one well's worth of cash
//
// That is the test of whether this shape is right: a pattern that needed a
// eleventh field would mean the vocabulary was wrong, not that the pattern was
// unusual.

using OGSim.Kernel;

namespace OGSim.Contracts;

/// <summary>
/// Where a scenario's world comes from.
///
/// <para>An authored world is a content id rather than a blob: it goes through
/// the same load pipeline as everything else, so a hand-built field and a
/// generated one are the same kind of thing to every consumer downstream.</para>
/// </summary>
public abstract record WorldSource;

/// <summary>Procedural, from a seed — and the seed is what makes a challenge
/// comparable between players (18 §3.3, GM8).</summary>
public sealed record GeneratedWorld(ulong Seed) : WorldSource;

public sealed record AuthoredWorld(ContentId World) : WorldSource;

/// <summary>
/// One scripted intervention: something the SCENARIO does, at a stated tick.
///
/// <para>A command or a model-parameter override, executed at stages 1–2 —
/// never a raw published event. A notification without its occurrence would
/// violate design 16 §1: you script the price shock's PARAMETER and let the
/// market model publish the event honestly, so the player's feed never carries
/// something that did not happen.</para>
/// </summary>
public abstract record ScriptedEntry(Tick At);

public sealed record ScriptedCommand(Tick At, Command Command) : ScriptedEntry(At);

public sealed record ScriptedParameter(
    Tick At, ModelSlot Slot, ParameterKey Key, double Value) : ScriptedEntry(At);

/// <summary>The eight dimensions of SDD-014 §4. Always reported individually
/// (18 §4) — a composite is a presentation, never the record.</summary>
public enum ScoreDimension
{
    Reserves,
    Recovery,
    CapitalEfficiency,
    FindingCost,
    OperatingCost,
    Uptime,
    Hse,
    Legacy,
}

/// <summary>
/// One dimension a run is scored on, and how much it counts.
///
/// <para>A challenge NAMES its dimensions, because "maximise recovery" and
/// "minimise finding cost" reward genuinely different play (18 §3.3). An empty
/// scoring list is not a defect — it is what a sandbox is.</para>
/// </summary>
public sealed record ScoreWeight(ScoreDimension Dimension, double Weight);

/// <summary>
/// What an objective has come to. The ONLY thing a campaign may branch on: a
/// small enum rather than arbitrary state, so a branch cannot come to depend on
/// a number the next chapter is free to change (R24 risk note).
/// </summary>
public enum ObjectiveState
{
    /// <summary>Still open, still reachable.</summary>
    Pending,

    Met,

    /// <summary>A failure condition broke, or a `Never` was violated.</summary>
    Failed,

    /// <summary>The deadline passed with it unmet. Distinct from Failed because
    /// running out of time and getting it wrong are different stories, and a
    /// campaign may legitimately branch on which happened.</summary>
    Expired,
}

/// <summary>
/// A situation with a goal (18 §3.4) — and, with a fixed seed and tight
/// constraints, a challenge (18 §3.3). One type, because the difference between
/// them is what the fields say and not what they are.
/// </summary>
public sealed record Scenario(
    ContentId Id,
    WorldSource World,

    /// <summary>The state block a run opens from — a company's cash, its rigs,
    /// an inherited field. This is where "develop this with $200M and one rig"
    /// and "take over a mismanaged asset" both live.</summary>
    ContentId StartingState,

    /// <summary>What the player is working toward.</summary>
    IReadOnlyList<Objective> Objectives,

    /// <summary>
    /// What must never happen. A separate list rather than an objective with a
    /// flag: a failure condition ends the run the moment it breaks, and merging
    /// the two would make every consumer test which kind it was holding.
    /// </summary>
    IReadOnlyList<Objective> Failures,

    IReadOnlyList<ScoreWeight> Scoring,

    /// <summary>Modifiers — fidelity, assist levels, forgiveness (SDD-005,
    /// 18 §5b). A hostile-setting challenge is largely this plus a world.
    ///
    /// <para>NULLABLE (R24.8, finding 291): a scenario that NAMES a profile is
    /// a calibrated challenge and composition refuses a build at any other
    /// fidelity — its balance was authored against those physics, and running
    /// it laxer would award its scores for a different game. Null is a
    /// scenario that genuinely runs at whatever fidelity composed, which is
    /// what the shared default scenario is.</para></summary>
    ContentId? RealityProfile,

    IReadOnlyList<ScriptedEntry> Script,

    /// <summary>
    /// When the run ends whatever the objectives say.
    ///
    /// <para>An objective may carry its own deadline (§3), but the RUN needs
    /// one: a scenario whose goals were all open-ended would never resolve, and
    /// "did they manage it in time" is the question a challenge is asking.</para>
    /// </summary>
    Tick Deadline)
{
    // Finding 131 — five collection members, none of which compare by reference.
    public bool Equals(Scenario? other) =>
        other is not null && Id == other.Id && World == other.World
        && StartingState == other.StartingState && RealityProfile == other.RealityProfile
        && Deadline == other.Deadline
        && Structural.Equal(Objectives, other.Objectives)
        && Structural.Equal(Failures, other.Failures)
        && Structural.Equal(Scoring, other.Scoring)
        && Structural.Equal(Script, other.Script);

    public override int GetHashCode() =>
        HashCode.Combine(Id, World, StartingState, RealityProfile, Deadline,
            Structural.HashOf(Objectives), Structural.HashOf(Failures),
            HashCode.Combine(Structural.HashOf(Scoring), Structural.HashOf(Script)));
}

/// <summary>Which chapter follows which outcome.</summary>
public sealed record ChapterLink(ObjectiveState Outcome, ContentId NextChapter);

/// <summary>
/// Linked chapters with a persistent company (design 18, GM-D1).
/// </summary>
public sealed record Campaign(
    ContentId Id,

    /// <summary>Scenario ids, in order. A campaign is chapters and the rules for
    /// moving between them; it holds no simulation of its own.</summary>
    IReadOnlyList<ContentId> Chapters,

    /// <summary>
    /// The persistence WHITELIST: exactly what a chapter carries forward — cash,
    /// technology nodes, reputation, named assets.
    ///
    /// <para>A whitelist rather than a blacklist, and that is the whole of
    /// R24-V17: anything not named RESETS. A blacklist would leak every field
    /// somebody forgot to add to it, and the leak would show up as a chapter
    /// that plays differently depending on what happened three chapters ago in
    /// a way nobody designed.</para>
    /// </summary>
    IReadOnlyList<ReadModelPath> Persisted,

    IReadOnlyList<ChapterLink> Branches)
{
    // Finding 131.
    public bool Equals(Campaign? other) =>
        other is not null && Id == other.Id
        && Structural.Equal(Chapters, other.Chapters)
        && Structural.Equal(Persisted, other.Persisted)
        && Structural.Equal(Branches, other.Branches);

    public override int GetHashCode() =>
        HashCode.Combine(Id, Structural.HashOf(Chapters),
            Structural.HashOf(Persisted), Structural.HashOf(Branches));
}

/// <summary>
/// How a run stands: each objective's state, and the score so far.
///
/// <para>Rebuilt at stage 12 and read at stage 13. It is a REPORT — the runner
/// observes and never acts (R24-V15), so nothing here can change what the
/// engine does next.</para>
/// </summary>
public sealed record ScenarioProgress(
    IReadOnlyList<(ContentId Objective, ObjectiveState State, double Progress)> Objectives,
    IReadOnlyList<(ScoreDimension Dimension, double Score)> Scores,
    ObjectiveState Overall,

    /// <summary>
    /// What the visible objectives ask for, so a host can state the goal
    /// instead of holding its own copy of the number.
    /// </summary>
    IReadOnlyList<ObjectiveGoal> Goals,

    /// <summary>
    /// The tick the run is judged at, when the scenario sets one.
    /// </summary>
    /// <remarks>
    /// Published for the same reason the goals are: a host that wants to show
    /// "eleven months left" was working it out from a copy of the deadline it
    /// kept itself, and a scenario that moved would have left it counting down
    /// to the wrong month.
    /// </remarks>
    Tick? Deadline)
{
    // Finding 131 — and Deadline is IN it (finding 284): it joined this record
    // in the same edit as Goals and only Goals made it into the comparison, so
    // two reports differing only in the tick the run is judged at compared
    // equal.
    public bool Equals(ScenarioProgress? other) =>
        other is not null && Overall == other.Overall
        && Deadline.Equals(other.Deadline)
        && Structural.Equal(Objectives, other.Objectives)
        && Structural.Equal(Scores, other.Scores)
        && Structural.Equal(Goals, other.Goals);

    public override int GetHashCode() =>
        HashCode.Combine(Overall, Deadline, Structural.HashOf(Objectives),
                         Structural.HashOf(Scores), Structural.HashOf(Goals));
}

/// <summary>
/// The one thing that evaluates a scenario against a tick.
///
/// <para>Deliberately narrow: it takes the run's position and returns how the
/// run stands. It cannot submit a command, spend money or touch a model — a
/// scenario that could act would be a second player, and the outcome would stop
/// being the player's doing (R24-V15).</para>
/// </summary>
public interface IScenarioRunner
{
    ContentId Id { get; }

    /// <summary>Everything the scenario scripts for this tick, for the engine to
    /// execute at stages 1–2. Returned rather than applied, so the runner still
    /// never acts — the engine does, through the same command path a player
    /// uses.</summary>
    IReadOnlyList<ScriptedEntry> EntriesFor(Tick tick);

    /// <summary>
    /// Stage 12. Reads a sealed position and reports how the run stands.
    ///
    /// <para>The POSITION, not <see cref="ReadModel"/> (SDD-014 §5a). An
    /// objective sees the read model through paths validated against the
    /// registry, and the snapshot is that view; handing a runner the record
    /// would make it flatten fifteen nested views itself, duplicating the
    /// registry SDD-017 §3 generates from those same records — and a plugin that
    /// flattened differently would evaluate content against paths it was never
    /// validated against.</para>
    /// </summary>
    ScenarioProgress Evaluate(ObjectiveSnapshot position, Tick tick);
}
