// R21e — the scenario runner (SDD-014 §5a).
//
// What is asserted here is the difference between a win condition that is
// CONTENT and one that was compiled in: a scenario that asks for something this
// engine cannot measure is refused when the engine composes, with a reason a
// mission author can act on — rather than loading clean and quietly never
// firing, which is what every shape of this defect looks like from the outside.

using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Objectives;

namespace OGSim.Composition.Tests;

public sealed class ScenarioTests
{
    private static readonly ReadModelPaths Paths = new(
    [
        new("company.cash", position => position.Cash.Cents),
        new("field.wells", position => position.Wells),
    ]);

    private static Scenario Asking(
        Predicate condition,
        IReadOnlyList<ScriptedEntry>? script = null,
        Predicate? failure = null) =>
        new(
            Id: new ContentId("test-scenario"),
            World: new GeneratedWorld(Seed: 1),
            StartingState: new ContentId("opening-position"),
            Objectives: [Objective("goal", condition)],
            Failures: failure is null ? [] : [Objective("limit", failure)],
            Scoring: [],
            RealityProfile: new ContentId("standard"),
            Script: script ?? [],
            Deadline: new Tick(120));

    private static Objective Objective(string id, Predicate condition) =>
        new(new ContentId(id), condition, Deadline: null, Weight: 1.0, Visible: true);

    private static Predicate CashAtLeast(double cents) =>
        new Compare(new Metric(new ReadModelPath("company.cash")), CompareOp.Ge, new Const(cents));

    private static FieldPosition Position(long cents, int wells = 0) =>
        new(new Tick(1), new GameDate(1970, 1), new Money(cents), wells,
            ActivitiesRunning: 0, new SurfaceVolume(0.0), Insolvent: false);

    private static ObjectiveState Ask(ScenarioRunner runner, FieldPosition position, int tick) =>
        runner.Evaluate(Paths.SnapshotOf(position), new Tick(tick)).Overall;

    // ------------------------------------------------- content, not code

    /// <summary>
    /// The whole point of R21e: the win condition is a predicate over a
    /// read-model path, so changing what the game asks for is an edit to a
    /// record and not to a stage.
    /// </summary>
    [Fact]
    public void A_goal_is_a_predicate_over_the_read_model()
    {
        var runner = new ScenarioRunner(Asking(CashAtLeast(1_000)), Paths.Schema);

        Assert.Equal(ObjectiveState.Pending, Ask(runner, Position(cents: 999), tick: 1));
        Assert.Equal(ObjectiveState.Met, Ask(runner, Position(cents: 1_000), tick: 2));
    }

    /// <summary>
    /// A verdict, once reached, stands (SDD-014 §5a). Spending the money again
    /// does not un-win the run.
    /// </summary>
    [Fact]
    public void A_terminal_verdict_is_final()
    {
        var runner = new ScenarioRunner(Asking(CashAtLeast(1_000)), Paths.Schema);

        Assert.Equal(ObjectiveState.Met, Ask(runner, Position(cents: 1_000), tick: 1));
        Assert.Equal(ObjectiveState.Met, Ask(runner, Position(cents: 0), tick: 2));
    }

    /// <summary>The run ends when the scenario says so, whatever the objectives
    /// have come to — otherwise a goal nobody reaches never resolves.</summary>
    [Fact]
    public void An_unmet_run_expires_at_the_deadline()
    {
        var runner = new ScenarioRunner(Asking(CashAtLeast(1_000)), Paths.Schema);

        Assert.Equal(ObjectiveState.Pending, Ask(runner, Position(cents: 0), tick: 119));
        Assert.Equal(ObjectiveState.Expired, Ask(runner, Position(cents: 0), tick: 120));
    }

    /// <summary>
    /// SDD-014 §5a's precedence. A failure objective's condition is a
    /// <c>Never</c>, which reads TRUE while it still holds — so the run fails the
    /// tick that condition evaluates false, and it does so even in a month the
    /// goal was met.
    /// </summary>
    [Fact]
    public void A_broken_hard_limit_beats_a_met_goal_in_the_same_month()
    {
        var runner = new ScenarioRunner(
            Asking(CashAtLeast(1_000), failure: new Never(CashAtLeast(5_000))),
            Paths.Schema);

        Assert.Equal(ObjectiveState.Failed, Ask(runner, Position(cents: 9_000), tick: 1));
    }

    /// <summary>A hard limit that has never broken does not end anything.</summary>
    [Fact]
    public void An_unbroken_hard_limit_leaves_the_run_alone()
    {
        var runner = new ScenarioRunner(
            Asking(CashAtLeast(1_000), failure: new Never(CashAtLeast(5_000))),
            Paths.Schema);

        Assert.Equal(ObjectiveState.Met, Ask(runner, Position(cents: 1_000), tick: 1));
    }

    // ------------------------------------------------------- refusals

    /// <summary>
    /// SDD-014 §2's no-invention rule, reaching content: an objective naming a
    /// projection this engine does not publish is refused when the engine
    /// composes.
    ///
    /// <para>Without it the path would resolve to nothing, the evaluator would
    /// read zero, and a mission asking for a reserve replacement ratio would be
    /// satisfied on month one by a number nobody computed.</para>
    /// </summary>
    [Fact]
    public void R24V14_a_goal_naming_a_projection_that_does_not_exist_is_refused()
    {
        var fault = Assert.Throws<ContentFault>(() => new ScenarioRunner(
            Asking(new Compare(
                new Metric(new ReadModelPath("company.rrr")), CompareOp.Gt, new Const(1.0))),
            Paths.Schema));

        Assert.Contains("company.rrr", fault.Fault.Detail);
        Assert.Equal(FaultClass.Content, fault.Fault.Class);
    }

    /// <summary>Every problem, never the first — the same rule as a command
    /// rejection and a composition refusal.</summary>
    [Fact]
    public void A_refusal_names_every_unknown_path()
    {
        var fault = Assert.Throws<ContentFault>(() => new ScenarioRunner(
            Asking(new All(
            [
                new Compare(new Metric(new ReadModelPath("company.rrr")),
                            CompareOp.Gt, new Const(1.0)),
                new Compare(new Metric(new ReadModelPath("hse.spills")),
                            CompareOp.Lt, new Const(1.0)),
            ])),
            Paths.Schema));

        Assert.Contains("company.rrr", fault.Fault.Detail);
        Assert.Contains("hse.spills", fault.Fault.Detail);
    }

    /// <summary>
    /// SDD-014's open item S014-3. The pipeline seals the tick's events at the
    /// CLOSE, after stage 12, so an <c>OnEvent</c> would read an empty list and
    /// be quietly false forever.
    ///
    /// <para>Refused rather than evaluated, because a mission that depends on one
    /// would look correct and never fire — and that is a worse failure than not
    /// loading at all.</para>
    /// </summary>
    [Fact]
    public void S014_3_a_goal_that_matches_on_an_event_is_refused_until_events_reach_stage_12()
    {
        var fault = Assert.Throws<ContentFault>(() => new ScenarioRunner(
            Asking(new SustainedFor(
                new OnEvent(EventCategory.Reservoir, new EventFilter(null, null)), 3)),
            Paths.Schema));

        Assert.Contains("S014-3", fault.Fault.Detail);
    }

    /// <summary>A scripted parameter override names a model slot nothing exposes.
    /// Refused for the same reason: a price shock that silently never lands is a
    /// scenario that quietly is not the one that was authored.</summary>
    [Fact]
    public void A_scripted_parameter_override_is_refused_while_no_model_exposes_one()
    {
        var fault = Assert.Throws<ContentFault>(() => new ScenarioRunner(
            Asking(
                CashAtLeast(1_000),
                script:
                [
                    new ScriptedParameter(
                        new Tick(12), new ModelSlot("price"), new ParameterKey("brent"), 0.5),
                ]),
            Paths.Schema));

        Assert.Contains("parameter override", fault.Fault.Detail);
    }

    // ------------------------------------------------------- the script

    /// <summary>
    /// Entries are RETURNED for the engine to submit, never applied — a runner
    /// that acted would be a second player (R24-V15).
    /// </summary>
    [Fact]
    public void R24V15_the_runner_hands_back_the_entries_due_this_tick()
    {
        var atTwelve = new ScriptedCommand(new Tick(12), new SeismicSurveyCommand(
            new EntityId<IReservoirCompartmentEntity>(1)));

        var runner = new ScenarioRunner(
            Asking(CashAtLeast(1_000), script: [atTwelve]), Paths.Schema);

        Assert.Empty(runner.EntriesFor(new Tick(11)));
        Assert.Same(atTwelve, Assert.Single(runner.EntriesFor(new Tick(12))));
        Assert.Empty(runner.EntriesFor(new Tick(13)));
    }

    /// <summary>
    /// The runner holds no command bus and no engine state — the structural half
    /// of "observes, never influences". Asserted on the TYPE, so the day someone
    /// gives it one, this fails.
    /// </summary>
    [Fact]
    public void R24V15_the_runner_cannot_reach_anything_it_could_act_through()
    {
        string[] fields = [.. typeof(ScenarioRunner)
            .GetFields(System.Reflection.BindingFlags.Instance
                       | System.Reflection.BindingFlags.NonPublic
                       | System.Reflection.BindingFlags.Public)
            .Select(field => field.FieldType.Name)];

        Assert.DoesNotContain(nameof(ICommandBus), fields);
        Assert.DoesNotContain(nameof(FieldControl), fields);
        Assert.DoesNotContain(nameof(IAuditTrail), fields);
    }

    // --------------------------------------------- the shipped scenario

    /// <summary>
    /// The scenario the engine ships composes against the read model the engine
    /// publishes. It is the same check R21f's twelve missions will get, run
    /// against the one that exists — a goal and a projection that drifted apart
    /// would otherwise surface as a game nobody can win.
    /// </summary>
    [Fact]
    public void The_shipped_scenario_only_asks_for_what_the_read_model_publishes()
    {
        var paths = new ReadModelPaths(Defaults.ProjectedPaths);

        var runner = new ScenarioRunner(Defaults.FirstField, paths.Schema);

        Assert.Equal(new ContentId("first-field"), runner.Id);
    }
}
