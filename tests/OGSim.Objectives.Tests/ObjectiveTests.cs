// R24's verification suite (SDD-014, GM1–GM13).

using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Objectives;

namespace OGSim.Objectives.Tests;

public static class Fx
{
    public static ReadModelPath Path(string p) => new(p);

    public static Objective Obj(Predicate condition, string id = "objective-1") =>
        new(new ContentId(id), condition, Deadline: null, Weight: 1.0, Visible: true);

    public static ObjectiveSnapshot Snapshot(
        IReadOnlyDictionary<string, double>? values = null,
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, double>>>? collections = null,
        IReadOnlyList<EngineEvent>? events = null) =>
        new(values ?? new Dictionary<string, double>(),
            collections ?? new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, double>>>(),
            events ?? []);

    public static ObjectiveSnapshot With(params (string Key, double Value)[] values) =>
        Snapshot(values.ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal));

    public static ObjectiveSnapshot Wells(params double[] waterCuts) =>
        Snapshot(collections: new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, double>>>
        {
            ["wells"] = [.. waterCuts.Select(w =>
                (IReadOnlyDictionary<string, double>)new Dictionary<string, double>
                {
                    ["waterCut"] = w,
                })],
        });

    public static Predicate Above(string path, double value) =>
        new Compare(new Metric(Path(path)), CompareOp.Gt, new Const(value));

    /// <summary>A concrete event, since EngineEvent is abstract — categories and
    /// severities are what an objective may filter on, and nothing else.</summary>
    private sealed record TestEvent(
        EventId Id, EventCategory Category, StageId Stage, Tick Tick, int Day,
        EntityRef? Subject, Severity Severity, AuditId Cause, LoopRole LoopRole,
        bool IsSegmentBoundary)
        : EngineEvent(Id, Category, Stage, Tick, Day, Subject, Severity, Cause,
                      LoopRole, IsSegmentBoundary);

    public static EngineEvent Event(EntityKind kind, ulong id) =>
        new TestEvent(
            new EventId(1), EventCategory.Reservoir, StageId.MaterialBalance, new Tick(5),
            Day: 0, new EntityRef(kind, id), Severity.Warning, new AuditId(1),
            LoopRole.Entry, IsSegmentBoundary: false);

    public static ReadModelSchema Schema { get; } = new(
        scalarPaths: ["company.rrr", "company.cash", "field.rate", "flag"],
        collectionPaths: ["wells"],
        itemFields: ["waterCut"]);
}

public class PredicateTests
{
    private static readonly ObjectiveEvaluator Evaluator = new();

    private static bool Eval(Predicate condition, ObjectiveSnapshot snapshot, ObjectiveState? state = null) =>
        Evaluator.Evaluate(Fx.Obj(condition), snapshot, state ?? new ObjectiveState());

    [Fact] // Comparison, all six ways
    public void GM1_comparisons_evaluate_against_the_read_model()
    {
        ObjectiveSnapshot snapshot = Fx.With(("company.rrr", 1.2));

        Assert.True(Eval(Fx.Above("company.rrr", 1.0), snapshot));
        Assert.False(Eval(Fx.Above("company.rrr", 1.5), snapshot));

        Assert.True(Eval(new Compare(
            new Metric(Fx.Path("company.rrr")), CompareOp.Ge, new Const(1.2)), snapshot));
        Assert.True(Eval(new Compare(
            new Metric(Fx.Path("company.rrr")), CompareOp.Ne, new Const(0.0)), snapshot));
    }

    // ------------------------------------------------------------ R24.3

    [Fact] // all-of / any-of
    public void GM1_all_of_and_any_of_combine_conditions()
    {
        ObjectiveSnapshot snapshot = Fx.With(("company.rrr", 1.2), ("company.cash", 50.0));

        Assert.True(Eval(new All([Fx.Above("company.rrr", 1.0), Fx.Above("company.cash", 10.0)]), snapshot));
        Assert.False(Eval(new All([Fx.Above("company.rrr", 1.0), Fx.Above("company.cash", 100.0)]), snapshot));

        Assert.True(Eval(new Any([Fx.Above("company.rrr", 5.0), Fx.Above("company.cash", 10.0)]), snapshot));
        Assert.False(Eval(new Any([Fx.Above("company.rrr", 5.0), Fx.Above("company.cash", 100.0)]), snapshot));
    }

    [Fact] // count-of-N
    public void GM1_count_of_n_requires_that_many()
    {
        ObjectiveSnapshot snapshot = Fx.With(
            ("company.rrr", 1.2), ("company.cash", 50.0), ("field.rate", 0.0));

        Predicate[] items =
            [Fx.Above("company.rrr", 1.0), Fx.Above("company.cash", 10.0), Fx.Above("field.rate", 1.0)];

        Assert.True(Eval(new CountOf(2, items), snapshot));
        Assert.False(Eval(new CountOf(3, items), snapshot));
    }

    // ------------------------------------------------------------ sustained

    [Fact] // sustained-for needs CONSECUTIVE ticks
    public void GM1_sustained_for_counts_consecutive_ticks()
    {
        var state = new ObjectiveState();
        var condition = new SustainedFor(Fx.Above("field.rate", 100.0), Ticks: 3);

        Assert.False(Eval(condition, Fx.With(("field.rate", 150.0)), state));
        Assert.False(Eval(condition, Fx.With(("field.rate", 150.0)), state));
        Assert.True(Eval(condition, Fx.With(("field.rate", 150.0)), state));
    }

    [Fact] // An interruption RESETS the counter
    public void GM1_sustained_for_resets_when_the_condition_lapses()
    {
        var state = new ObjectiveState();
        var condition = new SustainedFor(Fx.Above("field.rate", 100.0), Ticks: 3);

        Eval(condition, Fx.With(("field.rate", 150.0)), state);
        Eval(condition, Fx.With(("field.rate", 150.0)), state);

        // One bad month.
        Assert.False(Eval(condition, Fx.With(("field.rate", 50.0)), state));

        // Resets rather than pauses: "sustained for three months" means three in
        // a row. A counter that merely paused would let a player satisfy a
        // stability objective with a decade of intermittent compliance.
        Assert.False(Eval(condition, Fx.With(("field.rate", 150.0)), state));
        Assert.False(Eval(condition, Fx.With(("field.rate", 150.0)), state));
        Assert.True(Eval(condition, Fx.With(("field.rate", 150.0)), state));
    }

    // ------------------------------------------------------------ sequence

    [Fact] // A sequence is satisfied IN ORDER
    public void GM1_a_sequence_advances_step_by_step()
    {
        var state = new ObjectiveState();
        var condition = new InSequence(
            [Fx.Above("company.cash", 10.0), Fx.Above("field.rate", 100.0)]);

        // Second step satisfied first: no progress at all.
        Assert.False(Eval(condition, Fx.With(("company.cash", 0.0), ("field.rate", 500.0)), state));

        // Now the first.
        Assert.False(Eval(condition, Fx.With(("company.cash", 50.0), ("field.rate", 0.0)), state));

        // And the second.
        Assert.True(Eval(condition, Fx.With(("company.cash", 0.0), ("field.rate", 500.0)), state));
    }

    [Fact] // The index never goes backwards
    public void GM1_a_completed_sequence_stays_completed()
    {
        var state = new ObjectiveState();
        var condition = new InSequence([Fx.Above("company.cash", 10.0)]);

        Assert.True(Eval(condition, Fx.With(("company.cash", 50.0)), state));

        // Letting an earlier step un-satisfy would make "drill, then complete,
        // then produce" satisfiable by producing first.
        Assert.True(Eval(condition, Fx.With(("company.cash", 0.0)), state));
    }

    // ------------------------------------------------------------ never

    [Fact] // `Never` is a FAILURE condition: once broken, broken for good
    public void GM1_never_stays_broken_once_it_has_been_broken()
    {
        var state = new ObjectiveState();
        var condition = new Never(Fx.Above("field.rate", 1000.0));

        Assert.True(Eval(condition, Fx.With(("field.rate", 500.0)), state));

        // One breach.
        Assert.False(Eval(condition, Fx.With(("field.rate", 2000.0)), state));

        // And it stays failed — which makes it a promise about the whole
        // scenario rather than a momentary check.
        Assert.False(Eval(condition, Fx.With(("field.rate", 0.0)), state));
    }

    // ------------------------------------------------------------ aggregate

    [Fact] // The quantifier: "any well's water cut above 0.6"
    public void GM1_aggregate_max_expresses_a_fleet_level_objective()
    {
        var condition = new Compare(
            new Aggregate(Fx.Path("wells"), AggOp.Max, Fx.Path("waterCut")),
            CompareOp.Gt, new Const(0.6));

        Assert.True(Eval(condition, Fx.Wells(0.2, 0.75, 0.4)));
        Assert.False(Eval(condition, Fx.Wells(0.2, 0.55, 0.4)));

        // Without this node, per-item objectives were expressible only one id at
        // a time — unusable for a fleet-level mission, which is most of them.
    }

    [Theory] // Every aggregation
    [InlineData(AggOp.Min, 0.2)]
    [InlineData(AggOp.Max, 0.75)]
    [InlineData(AggOp.Sum, 1.35)]
    [InlineData(AggOp.Count, 3.0)]
    public void GM1_each_aggregation_computes_what_it_says(AggOp op, double expected)
    {
        var condition = new Compare(
            new Aggregate(Fx.Path("wells"), op, Fx.Path("waterCut")),
            CompareOp.Eq, new Const(expected));

        Assert.True(Eval(condition, Fx.Wells(0.2, 0.75, 0.4)));
    }

    [Fact] // An EMPTY collection has no Max — and returning zero would lie
    public void GM1_an_empty_collection_refuses_max_rather_than_returning_zero()
    {
        var condition = new Compare(
            new Aggregate(Fx.Path("wells"), AggOp.Max, Fx.Path("waterCut")),
            CompareOp.Lt, new Const(0.5));

        // "The highest water cut across no wells" has no answer, and zero would
        // make a fleet objective trivially TRUE before the first well is drilled.
        var fault = Assert.Throws<ModelFault>(() => Eval(condition, Fx.Wells()));
        Assert.Contains("empty collection", fault.Fault.Detail);
    }

    [Fact] // Sum over nothing is zero; All over nothing is vacuously true
    public void GM1_empty_aggregations_with_an_identity_use_it()
    {
        Assert.True(Eval(new Compare(
            new Aggregate(Fx.Path("wells"), AggOp.Sum, Fx.Path("waterCut")),
            CompareOp.Eq, new Const(0.0)), Fx.Wells()));

        // As in logic: every member of the empty set satisfies anything.
        Assert.True(Eval(new Compare(
            new Aggregate(Fx.Path("wells"), AggOp.All, Fx.Path("waterCut")),
            CompareOp.Eq, new Const(1.0)), Fx.Wells()));
    }

    // ------------------------------------------------------------ events

    [Fact] // OnEvent is true for the tick the event fired, and no longer
    public void GM1_on_event_matches_only_the_tick_it_fired()
    {
        EngineEvent fired = Fx.Event(EntityKind.Compartment, 1);

        var condition = new OnEvent(EventCategory.Reservoir, new EventFilter(null, null));

        Assert.True(Eval(condition, Fx.Snapshot(events: [fired])));
        Assert.False(Eval(condition, Fx.Snapshot(events: [])));
    }

    [Fact] // An unset filter field does not narrow
    public void GM1_the_event_filter_narrows_only_what_it_sets()
    {
        EngineEvent fired = Fx.Event(EntityKind.Compartment, 7);

        ObjectiveSnapshot snapshot = Fx.Snapshot(events: [fired]);

        // Right subject.
        Assert.True(Eval(new OnEvent(EventCategory.Reservoir,
            new EventFilter(new EntityRef(EntityKind.Compartment, 7), null)), snapshot));

        // Wrong subject.
        Assert.False(Eval(new OnEvent(EventCategory.Reservoir,
            new EventFilter(new EntityRef(EntityKind.Compartment, 9), null)), snapshot));

        // Severity floor.
        Assert.True(Eval(new OnEvent(EventCategory.Reservoir,
            new EventFilter(null, Severity.Info)), snapshot));
        Assert.False(Eval(new OnEvent(EventCategory.Reservoir,
            new EventFilter(null, Severity.Critical)), snapshot));
    }
}

public class ReadModelSchemaTests
{
    // ------------------------------------------------------------ GM4

    [Fact] // GM4 / R24-V14: a path outside the read model is a CONTENT FAULT
    public void GM4_an_unknown_metric_path_is_reported_at_load()
    {
        IReadOnlyList<string> unknown =
            Fx.Schema.Validate(Fx.Above("company.secretTruth", 0.0));

        // An objective can never reference data the player cannot see — GM4
        // mechanised rather than promised.
        Assert.Contains("company.secretTruth", Assert.Single(unknown));
    }

    [Fact] // A known path validates
    public void GM4_a_known_path_passes()
    {
        Assert.Empty(Fx.Schema.Validate(Fx.Above("company.rrr", 1.0)));
    }

    [Fact] // EVERY unknown path is reported, not just the first
    public void GM4_all_unknown_paths_are_reported_together()
    {
        IReadOnlyList<string> unknown = Fx.Schema.Validate(new All(
        [
            Fx.Above("company.ghost", 0.0),
            Fx.Above("field.phantom", 0.0),
            new Compare(
                new Aggregate(Fx.Path("rigs"), AggOp.Max, Fx.Path("utilisation")),
                CompareOp.Gt, new Const(0.5)),
        ]));

        // A content author fixing one typo and reloading to find the next has
        // been made to pay twice for one piece of information.
        Assert.Equal(4, unknown.Count);
    }

    [Fact] // Validation walks the WHOLE tree, including stateful nodes
    public void GM4_validation_reaches_nested_and_stateful_nodes()
    {
        IReadOnlyList<string> unknown = Fx.Schema.Validate(
            new SustainedFor(
                new Never(new Any([Fx.Above("company.ghost", 0.0)])), Ticks: 3));

        Assert.Single(unknown);
    }

    [Fact] // A read-model RENAME breaks content loudly at load
    public void GM4_a_renamed_projection_breaks_content_at_load_not_at_runtime()
    {
        var renamed = new ReadModelSchema(
            scalarPaths: ["company.reserveReplacementRatio"], [], []);

        // The objective referencing the old name fails validation immediately
        // rather than evaluating to something arbitrary two hours in.
        Assert.Single(renamed.Validate(Fx.Above("company.rrr", 1.0)));
    }
}

public class ObserveNeverInfluenceTests
{
    // ------------------------------------------------------------ R24-V15

    [Fact] // R24-V15: this assembly holds NO reference to the command bus
    public void R24V15_the_objectives_assembly_cannot_act()
    {
        var referenced = typeof(ObjectiveEvaluator).Assembly
            .GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();

        // An objective that could act would make a scenario a second player,
        // and the game's outcome would stop being the player's doing. The
        // guarantee is structural: there is no command bus here to call.
        Assert.DoesNotContain(
            typeof(ObjectiveEvaluator).Assembly.GetTypes(),
            t => t.Name.Contains("Command", StringComparison.Ordinal));

        // And nothing it references is a module that owns state.
        Assert.All(referenced, name =>
            Assert.True(
                name is null
                || !name.StartsWith("OGSim.", StringComparison.Ordinal)
                || name is "OGSim.Kernel" or "OGSim.Contracts",
                $"objectives should not reference {name}"));
    }

    [Fact] // Evaluation mutates only the objective's own state
    public void R24V4_evaluation_does_not_touch_the_snapshot()
    {
        var values = new Dictionary<string, double> { ["field.rate"] = 150.0 };
        var snapshot = new ObjectiveSnapshot(
            values,
            new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, double>>>(),
            []);

        var state = new ObjectiveState();
        new ObjectiveEvaluator().Evaluate(
            Fx.Obj(new SustainedFor(Fx.Above("field.rate", 100.0), 3)), snapshot, state);

        // The snapshot is sealed. Only the counter moved.
        Assert.Single(values);
        Assert.Equal(150.0, values["field.rate"], 12);
        Assert.Equal(1, state.SustainedTicks("objective-1.sustained"));
    }

    [Fact] // A validated path missing from the snapshot is an ENGINE bug, not a zero
    public void R24V4_a_missing_snapshot_value_is_an_invariant_fault()
    {
        var fault = Assert.Throws<InvariantFault>(() => new ObjectiveEvaluator().Evaluate(
            Fx.Obj(Fx.Above("company.rrr", 1.0)), Fx.Snapshot(), new ObjectiveState()));

        // Defaulting to zero would make an objective silently true — and a
        // scenario that completed itself because a projection was renamed is
        // worse than one that refuses to load.
        Assert.Contains("registry and the projection disagree", fault.Fault.Detail);
    }
}
