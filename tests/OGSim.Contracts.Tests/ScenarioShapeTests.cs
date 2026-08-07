// SDD-014 §5 — is the scenario shape the right shape?
//
// The test of a vocabulary is not that it compiles: it is that everything the
// design says the game must express CAN be expressed in it, and that nothing
// needed an extra field. Design 18 §3.3 lists ten challenge patterns, and each
// one below is built from the same nine members. A pattern that needed a tenth
// would mean the vocabulary was wrong, not that the pattern was unusual.
//
// These are shape tests. They assert what a scenario IS, not what running one
// does — there is no runner yet, and writing one before the shape settled is how
// a contract ends up describing its first implementation.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Contracts.Tests;

public sealed class ScenarioShapeTests
{
    private static Objective Goal(string id, Predicate condition) =>
        new(new ContentId(id), condition, Deadline: null, Weight: 1.0, Visible: true);

    private static Predicate CashAtLeast(double millions) =>
        new Compare(
            new Metric(new ReadModelPath("company.cash")),
            CompareOp.Ge,
            new Const(millions));

    private static Scenario Blank(
        string id,
        WorldSource? world = null,
        string startingState = "standard-start",
        IReadOnlyList<Objective>? objectives = null,
        IReadOnlyList<Objective>? failures = null,
        IReadOnlyList<ScoreWeight>? scoring = null,
        string realityProfile = "engineer",
        IReadOnlyList<ScriptedEntry>? script = null,
        int deadline = 120) =>
        new(new ContentId(id),
            world ?? new GeneratedWorld(1965UL),
            new ContentId(startingState),
            objectives ?? [],
            failures ?? [],
            scoring ?? [],
            new ContentId(realityProfile),
            script ?? [],
            new Tick(deadline));

    // ---------------------------------------- design 18 §3.3's ten patterns

    [Fact] // "Develop this field with $200M and one rig"
    public void Constrained_resource_is_a_starting_state()
    {
        Scenario challenge = Blank(
            "constrained-resource",
            startingState: "two-hundred-million-one-rig",
            objectives: [Goal("develop-the-field", CashAtLeast(250.0))]);

        Assert.Equal(new ContentId("two-hundred-million-one-rig"), challenge.StartingState);
    }

    [Fact] // "Take over a mismanaged asset at 40% uptime and fix it"
    public void Turnaround_is_a_starting_state_and_an_uptime_score()
    {
        Scenario challenge = Blank(
            "turnaround",
            startingState: "mismanaged-asset",
            scoring: [new ScoreWeight(ScoreDimension.Uptime, 1.0)]);

        Assert.Equal(ScoreDimension.Uptime, Assert.Single(challenge.Scoring).Dimension);
    }

    [Fact] // "Highest recovery factor from a marginal field"
    public void Maximise_recovery_is_a_single_scored_dimension()
    {
        Scenario challenge = Blank(
            "maximise-recovery",
            scoring: [new ScoreWeight(ScoreDimension.Recovery, 1.0)]);

        Assert.Equal(ScoreDimension.Recovery, Assert.Single(challenge.Scoring).Dimension);
    }

    /// <summary>
    /// "Develop with zero routine flaring." A hard limit is a FAILURE, not an
    /// objective — it ends the run when it breaks rather than being something
    /// the player accumulates toward.
    /// </summary>
    [Fact]
    public void Hard_limit_is_a_failure_condition()
    {
        Scenario challenge = Blank(
            "zero-flaring",
            failures:
            [
                Goal("no-routine-flaring",
                     new Never(new Compare(
                         new Metric(new ReadModelPath("hse.flaringIntensity")),
                         CompareOp.Gt,
                         new Const(0.0)))),
            ]);

        Objective limit = Assert.Single(challenge.Failures);
        Assert.IsType<Never>(limit.Condition);
        Assert.Empty(challenge.Objectives);
    }

    [Fact] // "Satisfy a work commitment in 18 months"
    public void Beat_the_clock_is_a_deadline()
    {
        Scenario challenge = Blank("beat-the-clock", deadline: 18);

        Assert.Equal(18, challenge.Deadline.Value);
    }

    [Fact] // "Arctic, four-month windows, no infrastructure"
    public void Hostile_setting_is_an_authored_world_and_a_profile()
    {
        Scenario challenge = Blank(
            "arctic",
            world: new AuthoredWorld(new ContentId("arctic-basin")),
            realityProfile: "simulation");

        AuthoredWorld world = Assert.IsType<AuthoredWorld>(challenge.World);
        Assert.Equal(new ContentId("arctic-basin"), world.World);
    }

    /// <summary>
    /// "Sanction, then a 60% crash at first steel." The scenario scripts the
    /// price model's PARAMETER; the market model then publishes the event
    /// honestly. Scripting a raw event would put a notification in the player's
    /// feed for something that never happened (design 16 §1).
    /// </summary>
    [Fact]
    public void Price_shock_is_a_scripted_parameter_never_an_event()
    {
        Scenario challenge = Blank(
            "price-shock",
            script:
            [
                new ScriptedParameter(
                    new Tick(24), new ModelSlot("price-model"), new ParameterKey("level"), 0.4),
            ]);

        ScriptedEntry entry = Assert.Single(challenge.Script);
        ScriptedParameter shock = Assert.IsType<ScriptedParameter>(entry);

        Assert.Equal(24, shock.At.Value);
        Assert.Equal(0.4, shock.Value);
    }

    [Fact] // "Lowest finding cost per barrel in a fixed budget"
    public void Exploration_efficiency_is_a_scored_dimension_and_a_budget()
    {
        Scenario challenge = Blank(
            "exploration-efficiency",
            startingState: "fixed-exploration-budget",
            scoring: [new ScoreWeight(ScoreDimension.FindingCost, 1.0)]);

        Assert.Equal(ScoreDimension.FindingCost, Assert.Single(challenge.Scoring).Dimension);
    }

    [Fact] // "Full development, zero serious incidents"
    public void Hse_perfect_run_is_a_failure_condition_and_a_score()
    {
        Scenario challenge = Blank(
            "hse-perfect-run",
            failures:
            [
                Goal("no-serious-incidents",
                     new Never(new OnEvent(
                         EventCategory.Hse,
                         new EventFilter(Subject: null, Severity.Critical)))),
            ],
            scoring: [new ScoreWeight(ScoreDimension.Hse, 1.0)]);

        Assert.Single(challenge.Failures);
        Assert.Single(challenge.Scoring);
    }

    [Fact] // "One well, one chance — pick the prospect"
    public void One_shot_is_a_starting_state_with_one_wells_worth_of_cash()
    {
        Scenario challenge = Blank("one-shot", startingState: "one-well-budget");

        Assert.Equal(new ContentId("one-well-budget"), challenge.StartingState);
    }

    /// <summary>
    /// All ten, and none of them reached for a field that is not there. That is
    /// the actual claim this file makes.
    /// </summary>
    [Fact]
    public void Every_pattern_is_expressible_without_a_new_member()
    {
        Assert.Equal(9, typeof(Scenario).GetProperties().Length);
    }

    // ---------------------------------------------------------- campaigns

    /// <summary>
    /// A campaign branches on a small enum and on nothing else. Branching on
    /// arbitrary state is R24's named risk: a branch that read a number would
    /// come to depend on one the next chapter is free to change.
    /// </summary>
    [Fact]
    public void A_campaign_branches_only_on_an_objective_state()
    {
        var campaign = new Campaign(
            new ContentId("wildcatter"),
            [new ContentId("chapter-1"), new ContentId("chapter-2")],
            [new ReadModelPath("company.cash")],
            [
                new ChapterLink(ObjectiveState.Met, new ContentId("chapter-2")),
                new ChapterLink(ObjectiveState.Failed, new ContentId("chapter-1-retry")),
            ]);

        Assert.All(campaign.Branches,
            link => Assert.IsType<ObjectiveState>(link.Outcome));
    }

    /// <summary>
    /// The whitelist is a whitelist. Anything not named RESETS — a blacklist
    /// would leak every field somebody forgot to add, and the leak would show up
    /// as a chapter playing differently because of something three chapters ago
    /// that nobody designed (R24-V17).
    /// </summary>
    [Fact]
    public void A_campaign_carries_forward_only_what_it_names()
    {
        var campaign = new Campaign(
            new ContentId("wildcatter"),
            [new ContentId("chapter-1")],
            [new ReadModelPath("company.cash"), new ReadModelPath("company.technology")],
            []);

        Assert.Equal(2, campaign.Persisted.Count);
        Assert.DoesNotContain(new ReadModelPath("field.wells"), campaign.Persisted);
    }

    // ------------------------------------------------------------- equality

    /// <summary>
    /// Finding 131 — a scenario carries four collections, and two loads of one
    /// mission file must compare equal or "did this mod change the scenario?"
    /// has no answer.
    /// </summary>
    [Fact]
    public void Two_identical_scenarios_are_equal()
    {
        Assert.Equal(
            Blank("same", objectives: [Goal("a", CashAtLeast(100.0))]),
            Blank("same", objectives: [Goal("a", CashAtLeast(100.0))]));
    }

    [Fact]
    public void Two_scenarios_differing_in_one_objective_are_not_equal()
    {
        Assert.NotEqual(
            Blank("same", objectives: [Goal("a", CashAtLeast(100.0))]),
            Blank("same", objectives: [Goal("a", CashAtLeast(200.0))]));
    }
}
