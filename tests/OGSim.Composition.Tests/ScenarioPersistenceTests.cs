// SDD-014 §5a's finding-266 amendment (GC-2). A run's verdict is history
// across ticks — a latched per-objective state, a latched overall, and a
// stateful predicate's counters — not a value the next evaluation alone
// reproduces. `ScenarioRunner` carried all of it in memory only, so a reload
// composed a fresh one from the same content and got fresh, empty counters:
// `SustainedFor` resumed from zero, and a scenario whose objective set the
// save no longer matches would have been restored onto the wrong tracked
// list with nothing to say so.

using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Objectives;

namespace OGSim.Composition.Tests;

public sealed class ScenarioPersistenceTests
{
    private static Scenario Custom() => new(
        Id: new ContentId("test-scenario"),
        World: new GeneratedWorld(Seed: 1),
        StartingState: new ContentId("opening-position"),
        Objectives:
        [
            new Objective(
                new ContentId("stayed-hot"),
                new SustainedFor(
                    new Compare(new Metric(new ReadModelPath("temp")), CompareOp.Ge, new Const(1.0)),
                    Ticks: 3),
                Deadline: null, Weight: 1.0, Visible: true),
        ],
        Failures:
        [
            new Objective(
                new ContentId("broke"),
                new Never(new Compare(new Metric(new ReadModelPath("bad")), CompareOp.Ge, new Const(1.0))),
                Deadline: null, Weight: 1.0, Visible: true),
        ],
        Scoring: [],
        RealityProfile: null,
        Script: [],
        Deadline: new Tick(1000));

    private static ReadModelSchema Schema() =>
        new(scalarPaths: ["temp", "bad"], collectionPaths: [], itemFields: []);

    private static ObjectiveSnapshot Snap(double temp, double bad) =>
        new(
            new Dictionary<string, double>(StringComparer.Ordinal) { ["temp"] = temp, ["bad"] = bad },
            new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, double>>>(),
            []);

    [Fact] // A SustainedFor mid-count resumes rather than restarting at zero
    public void A_sustained_objective_resumes_its_count_after_a_restore()
    {
        var runner = new ScenarioRunner(Custom(), Schema(), new ContentId("simulation"));

        runner.Evaluate(Snap(temp: 1.0, bad: 0.0), new Tick(1));
        runner.Evaluate(Snap(temp: 1.0, bad: 0.0), new Tick(2));

        var state = OGSim.Persistence.StateBlock.Capture(runner).Written();

        var restored = new ScenarioRunner(Custom(), Schema(), new ContentId("simulation"));
        OGSim.Persistence.StateBlock.Restore(restored, state);

        // The THIRD consecutive tick, against the RESTORED runner. A counter
        // that came back at zero reads Pending here (1 of 3) instead of Met.
        ScenarioProgress progress = restored.Evaluate(Snap(temp: 1.0, bad: 0.0), new Tick(3));

        Assert.Equal(ObjectiveState.Met, progress.Objectives[0].State);
    }

    [Fact] // A latched Met survives even once the condition it was met BY lapses
    public void A_met_objective_stays_met_after_a_restore_even_if_the_reading_later_lapses()
    {
        var runner = new ScenarioRunner(Custom(), Schema(), new ContentId("simulation"));

        runner.Evaluate(Snap(temp: 1.0, bad: 0.0), new Tick(1));
        runner.Evaluate(Snap(temp: 1.0, bad: 0.0), new Tick(2));
        ScenarioProgress met = runner.Evaluate(Snap(temp: 1.0, bad: 0.0), new Tick(3));
        Assert.Equal(ObjectiveState.Met, met.Objectives[0].State);

        var state = OGSim.Persistence.StateBlock.Capture(runner).Written();

        var restored = new ScenarioRunner(Custom(), Schema(), new ContentId("simulation"));
        OGSim.Persistence.StateBlock.Restore(restored, state);

        // temp has fallen back below target. The SUSTAINED COUNTER alone would
        // reset and read Pending here — it is the per-objective LATCH that has
        // to have survived for this to still read Met, independent of the
        // counter the previous test checks.
        ScenarioProgress after = restored.Evaluate(Snap(temp: 0.0, bad: 0.0), new Tick(4));

        Assert.Equal(ObjectiveState.Met, after.Objectives[0].State);
    }

    [Fact] // A failure already latched Failed stays Failed, whatever the next reading says
    public void A_failed_objective_stays_failed_after_a_restore()
    {
        var runner = new ScenarioRunner(Custom(), Schema(), new ContentId("simulation"));

        // The one breach.
        runner.Evaluate(Snap(temp: 0.0, bad: 1.0), new Tick(1));
        ScenarioProgress before = runner.Evaluate(Snap(temp: 0.0, bad: 0.0), new Tick(2));
        Assert.Equal(ObjectiveState.Failed, before.Objectives[1].State);
        Assert.Equal(ObjectiveState.Failed, before.Overall);

        var state = OGSim.Persistence.StateBlock.Capture(runner).Written();

        var restored = new ScenarioRunner(Custom(), Schema(), new ContentId("simulation"));
        OGSim.Persistence.StateBlock.Restore(restored, state);

        // An innocuous reading, against the RESTORED runner. A latch that came
        // back cleared would read Pending here instead of the terminal Failed
        // the save actually held.
        ScenarioProgress after = restored.Evaluate(Snap(temp: 0.0, bad: 0.0), new Tick(3));

        Assert.Equal(ObjectiveState.Failed, after.Objectives[1].State);
        Assert.Equal(ObjectiveState.Failed, after.Overall);
    }

    [Fact] // The tracked set is content, not something a save reshapes
    public void A_save_naming_a_different_objective_at_the_same_position_is_refused()
    {
        var runner = new ScenarioRunner(Custom(), Schema(), new ContentId("simulation"));
        runner.Evaluate(Snap(temp: 1.0, bad: 0.0), new Tick(1));

        var state = OGSim.Persistence.StateBlock.Capture(runner).Written();

        Scenario renamed = Custom() with
        {
            Objectives =
            [
                Custom().Objectives[0] with { Id = new ContentId("renamed") },
            ],
        };
        var restored = new ScenarioRunner(renamed, Schema(), new ContentId("simulation"));

        SaveDataFault fault = Assert.Throws<SaveDataFault>(
            () => OGSim.Persistence.StateBlock.Restore(restored, state));

        Assert.Contains("stayed-hot", fault.Fault.Detail, StringComparison.Ordinal);
    }
}
