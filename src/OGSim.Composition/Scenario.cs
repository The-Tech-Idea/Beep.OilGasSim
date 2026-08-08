// R21e — the scenario runner (SDD-014 §5a, design 03 §3.3).
//
// A SCENARIO IS CONTENT, NOT CODE. R21c gave the game an end by compiling one
// into the engine: a `ScenarioGoal(targetCash, deadline)` record and a stage
// that read cash and compared it. That was a win condition nobody could author,
// load or vary without editing the engine — the thing design 03 §3.3 exists to
// prevent — and R21d declared the contracts that replace it. This is the debt,
// paid: the goal is now an `Objective` over a read-model path, evaluated by
// R24's predicate machinery, inside a `Scenario` a loader will one day build
// from JSON without a line of this file changing.
//
// THE RUNNER REPORTS AND NEVER ACTS (R24-V15). It reads a sealed position and
// answers how the run stands. It cannot spend, drill or change a number — a
// scenario that could act would be a second player, and the outcome would stop
// being the player's doing. Even the scripted entries are RETURNED for the
// engine to submit through the same command path a player uses.
//
// AN OBJECTIVE SEES ONLY WHAT THE PLAYER SEES. Every path it names is resolved
// against a schema built from the read model's own projections, so a mission
// cannot reference data the player has not been shown — and cannot silently
// read zero from a projection that does not exist yet.

using OGSim.Company;
using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Objectives;

namespace OGSim.Composition;

// ------------------------------------------------------ the read-model paths

/// <summary>
/// One path an objective may name, and how it is read.
///
/// <para>The schema and the snapshot are both built from the same list, which is
/// what makes them unable to disagree — <see cref="ObjectiveEvaluator"/> throws
/// an <see cref="InvariantFault"/> for "validated at load but absent from the
/// snapshot", and that fault is unreachable by construction rather than merely
/// unobserved.</para>
/// </summary>
internal sealed record ProjectedPath(string Path, Func<FieldPosition, double> Read);

/// <summary>
/// SDD-014 §2's registry, for the read model this composition actually
/// publishes.
///
/// <para>It lists what the loop can honestly fill and NOT what SDD-017 §2
/// eventually will, so an objective naming <c>company.rrr</c> is refused at load
/// with a reason rather than evaluated against a zero nobody computed. It grows
/// one line per path as R20d wires each subsystem in.</para>
///
/// <para><b>Money is in CENTS.</b> A path is a <c>double</c> and the ledger is a
/// scaled integer, so the projection carries the integer's own unit rather than
/// dividing to dollars — SDD-014 §4 scores in integer cents for the same reason,
/// and a division here would be a second rounding rule (SDD-001 §1.3).</para>
/// </summary>
internal sealed class ReadModelPaths
{
    private readonly List<ProjectedPath> _paths;

    public ReadModelPaths(IReadOnlyList<ProjectedPath> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        _paths = [.. paths];

        var scalars = new List<string>(_paths.Count);
        for (int i = 0; i < _paths.Count; i++) scalars.Add(_paths[i].Path);

        // No collections and no item fields yet: the read model publishes no
        // per-well or per-compartment list, and declaring one an Aggregate could
        // quantify over would promise a fleet-level objective nothing could
        // answer. They arrive with the projections that carry them (R21.6).
        Schema = new ReadModelSchema(scalars, collectionPaths: [], itemFields: []);
    }

    public ReadModelSchema Schema { get; }

    /// <summary>
    /// The tick's position, in the one shape an objective may see.
    ///
    /// <para>Events are empty and that is a known gap, not an oversight: the
    /// pipeline seals the tick's events at the CLOSE, after stage 12, so nothing
    /// is observable at the point objectives run (SDD-014's open item S014-3).
    /// A scenario carrying an <c>OnEvent</c> is refused by
    /// <see cref="ScenarioRunner"/> rather than evaluated against this empty
    /// list.</para>
    /// </summary>
    public ObjectiveSnapshot SnapshotOf(FieldPosition position)
    {
        ArgumentNullException.ThrowIfNull(position);

        var values = new Dictionary<string, double>(_paths.Count, StringComparer.Ordinal);

        for (int i = 0; i < _paths.Count; i++)
            values[_paths[i].Path] = _paths[i].Read(position);

        return new ObjectiveSnapshot(
            values,
            new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, double>>>(
                StringComparer.Ordinal),
            []);
    }
}

// ------------------------------------------------------------- the runner

/// <summary>
/// SDD-014 §5a's runner, for one scenario.
///
/// <para>Every refusal it can make happens in the CONSTRUCTOR, because a
/// scenario the engine cannot honestly run must stop the engine from starting
/// rather than surface as an objective that never fires. That is composition's
/// all-or-nothing rule reaching content (design 03 §3.1).</para>
/// </summary>
internal sealed class ScenarioRunner : IScenarioRunner
{
    private readonly Scenario _scenario;
    private readonly ObjectiveEvaluator _evaluator = new();

    // One PredicateState per objective, so two objectives sharing a node path
    // cannot share a counter. Kept in the declared order of the scenario's
    // lists, which is the deterministic iteration SDD-014 §3 requires.
    private readonly List<(Objective Objective, PredicateState State, bool IsFailure)> _tracked = [];

    // Terminal states LATCH. A run does not un-fail, and a player who hit the
    // target in month 90 does not lose it in month 91 (SDD-014 §5a).
    private readonly List<ObjectiveState> _states = [];

    private ObjectiveState _overall = ObjectiveState.Pending;

    public ScenarioRunner(Scenario scenario, ReadModelSchema schema)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(schema);

        _scenario = scenario;

        Refuse(Problems(scenario, schema));

        for (int i = 0; i < scenario.Objectives.Count; i++) Track(scenario.Objectives[i], false);
        for (int i = 0; i < scenario.Failures.Count; i++) Track(scenario.Failures[i], true);
    }

    public ContentId Id => _scenario.Id;

    public IReadOnlyList<ScriptedEntry> EntriesFor(Tick tick)
    {
        var due = new List<ScriptedEntry>();

        for (int i = 0; i < _scenario.Script.Count; i++)
            if (_scenario.Script[i].At == tick) due.Add(_scenario.Script[i]);

        return due;
    }

    public ScenarioProgress Evaluate(ObjectiveSnapshot position, Tick tick)
    {
        ArgumentNullException.ThrowIfNull(position);

        var reported = new List<(ContentId, ObjectiveState, double)>(_tracked.Count);
        var anyFailed = false;
        var allMet = true;

        for (int i = 0; i < _tracked.Count; i++)
        {
            (Objective objective, PredicateState state, bool isFailure) = _tracked[i];

            // Evaluated even once terminal: a Never's counter has to keep
            // seeing the world or a broken condition would be forgotten, and
            // the latch below is what makes the answer stable rather than the
            // evaluation being skipped.
            bool holds = _evaluator.Evaluate(objective, position, state);

            _states[i] = Settle(_states[i], objective, holds, isFailure, tick);

            if (isFailure) anyFailed |= _states[i] == ObjectiveState.Failed;
            else allMet &= _states[i] == ObjectiveState.Met;

            reported.Add((objective.Id, _states[i], _states[i] == ObjectiveState.Met ? 1.0 : 0.0));
        }

        _overall = Overall(anyFailed, allMet, tick);

        // Scores are EMPTY, and honestly so: SDD-014 §4's eight formulas read
        // ledger and registry values this loop does not yet publish, and a
        // scenario with no scoring weights is what that document calls a
        // sandbox. Nothing here invents a number.
        return new ScenarioProgress(reported, [], _overall);
    }

    /// <summary>
    /// SDD-014 §5a's precedence: failure, then success, then expiry — and once
    /// terminal, final.
    ///
    /// <para>Failure first because a company that cannot pay has lost even in
    /// the month it would otherwise have hit the target: the money is gone
    /// before the goal is measured, and that is the order it happens in.</para>
    /// </summary>
    private ObjectiveState Overall(bool anyFailed, bool allMet, Tick tick)
    {
        if (_overall != ObjectiveState.Pending) return _overall;

        if (anyFailed) return ObjectiveState.Failed;
        if (allMet) return ObjectiveState.Met;
        if (tick.Value >= _scenario.Deadline.Value) return ObjectiveState.Expired;

        return ObjectiveState.Pending;
    }

    /// <summary>
    /// One objective's state. A failure objective's condition is a
    /// <see cref="Never"/>, which reads TRUE while it still holds — so the
    /// objective has failed the tick that condition evaluates FALSE. It is the
    /// one place in this design where a false predicate is the bad news, and
    /// getting it the other way round would end every run on month one.
    /// </summary>
    private static ObjectiveState Settle(
        ObjectiveState current, Objective objective, bool holds, bool isFailure, Tick tick)
    {
        if (current != ObjectiveState.Pending) return current;

        if (isFailure) return holds ? ObjectiveState.Pending : ObjectiveState.Failed;

        if (holds) return ObjectiveState.Met;

        return objective.Deadline is Tick own && tick.Value >= own.Value
            ? ObjectiveState.Expired
            : ObjectiveState.Pending;
    }

    private void Track(Objective objective, bool isFailure)
    {
        _tracked.Add((objective, new PredicateState(), isFailure));
        _states.Add(ObjectiveState.Pending);
    }

    /// <summary>
    /// Every reason this scenario cannot be run, not the first — the same rule
    /// as a command rejection and a composition refusal.
    /// </summary>
    private static List<string> Problems(Scenario scenario, ReadModelSchema schema)
    {
        var problems = new List<string>();

        for (int i = 0; i < scenario.Objectives.Count; i++)
            Check(scenario.Objectives[i], schema, problems);

        for (int i = 0; i < scenario.Failures.Count; i++)
            Check(scenario.Failures[i], schema, problems);

        for (int i = 0; i < scenario.Script.Count; i++)
            if (scenario.Script[i] is ScriptedParameter parameter)
                problems.Add(
                    $"'{scenario.Id.Value}' scripts a {parameter.Slot} parameter override at " +
                    $"tick {parameter.At.Value}, and no model exposes a parameter to override; " +
                    "the slot arrives with technology (R20d.10)");

        return problems;
    }

    private static void Check(Objective objective, ReadModelSchema schema, List<string> problems)
    {
        IReadOnlyList<string> unknown = schema.Validate(objective.Condition);

        for (int i = 0; i < unknown.Count; i++)
            problems.Add($"objective '{objective.Id.Value}': {unknown[i]}");

        // S014-3. Refused rather than evaluated: the pipeline seals the tick's
        // events at the close, AFTER stage 12, so an OnEvent at stage 12 reads
        // an empty list and is quietly false forever. A mission that depends on
        // one would look correct and never fire, which is worse than not
        // loading.
        if (MentionsAnEvent(objective.Condition))
            problems.Add(
                $"objective '{objective.Id.Value}' matches on an event, and events are not " +
                "observable at stage 12: the tick seals them at the close (SDD-014 open " +
                "item S014-3)");
    }

    private static bool MentionsAnEvent(Predicate predicate)
    {
        switch (predicate)
        {
            case OnEvent:
                return true;

            case Compare compare:
                return MentionsAnEvent(compare.L) || MentionsAnEvent(compare.R);

            case All all:
                return AnyOf(all.Items);

            case Any any:
                return AnyOf(any.Items);

            case CountOf countOf:
                return AnyOf(countOf.Items);

            case InSequence sequence:
                return AnyOf(sequence.Steps);

            case SustainedFor sustained:
                return MentionsAnEvent(sustained.Inner);

            case Never never:
                return MentionsAnEvent(never.Inner);

            case Metric or Const or Aggregate:
                return false;

            default:
                // The AST is CLOSED (SDD-014 §1). An unrecognised node means the
                // vocabulary grew without this walk growing with it.
                throw new ModelFault("SDD-014 §1", null,
                    $"unknown predicate {predicate.GetType().Name}; the AST is closed and " +
                    "every node must be walked");
        }
    }

    private static bool AnyOf(IReadOnlyList<Predicate> items)
    {
        for (int i = 0; i < items.Count; i++)
            if (MentionsAnEvent(items[i])) return true;

        return false;
    }

    private static void Refuse(List<string> problems)
    {
        if (problems.Count == 0) return;

        throw new ContentFault("SDD-014 §2", null,
            "this scenario cannot be run: " + string.Join("; ", problems));
    }
}

// -------------------------------------------------------------- the stage

/// <summary>
/// Stage 12. Builds the position the objectives see, and asks the runner how the
/// run stands.
///
/// <para>Before stage 13, so the read model the host publishes carries the
/// verdict on the month it describes rather than on the one before it.</para>
/// </summary>
internal sealed class ObjectiveStage(
    CompanyState company,
    IScenarioRunner runner,
    ReadModelPaths paths,
    FieldProjection projection,
    IAuditTrail audit) : ITickStage
{
    private ObjectiveState _reported = ObjectiveState.Pending;

    public StageId Id => StageId.Objectives;

    /// <summary>
    /// Once true, always true: a company does not recover from having been wound
    /// up, and a later month's revenue must not quietly un-fail it.
    ///
    /// <para>It is a FACT the read model carries and the scenario's failure
    /// objective reads — not the scenario's opinion about it. One owner (law
    /// L5): the ledger decides whether there is money, and content decides
    /// whether running out ends the run.</para>
    /// </summary>
    public bool Insolvent { get; private set; }

    /// <summary>
    /// The month's facts, taken at stage 12 and published at stage 13.
    ///
    /// <para>Taken HERE because the objectives have to read the month they are
    /// judging: a verdict evaluated against the read model the host already
    /// holds would be a verdict on last month. Stage 13 republishes this rather
    /// than re-reading the field, so there is one projection and not two that
    /// can disagree (law L5).</para>
    /// </summary>
    public FieldPosition? Position { get; private set; }

    public ScenarioProgress Progress { get; private set; } =
        new([], [], ObjectiveState.Pending);

    public void Execute(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (company.Ledger.Cash.Cents < 0) Insolvent = true;

        Position = projection.Take(context.Tick, context.Date, Insolvent);
        Progress = runner.Evaluate(paths.SnapshotOf(Position), context.Tick);

        if (Progress.Overall == _reported) return;

        _reported = Progress.Overall;

        audit.Record(
            AuditCategory.StateTransition, subject: null, cause: null,
            new Dictionary<string, AuditValue>(StringComparer.Ordinal)
            {
                ["scenario"] = new(runner.Id.Value),
                ["overall"] = new(Progress.Overall.ToString()),
                ["tick"] = new(context.Tick.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)),
            });
    }
}
