// Composition — the all-or-nothing guarantee (design 03 §3.1, SDD-001 §9).
//
// The point of every test here is the SAME point: a problem with the module set
// is a startup refusal naming what is wrong, never a running engine that
// misbehaves later. An engine that composed with a missing provider would fail
// in month 300 as an inexplicable number.

using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition.Tests;

/// <summary>Collects what the engine logs so a test can assert on it.</summary>
internal sealed class RecordingSink : ILogSink
{
    public List<LogRecord> Records { get; } = [];

    public void Emit(LogRecord record) => Records.Add(record);
}

/// <summary>A module declared entirely by a test, to exercise one refusal.</summary>
internal sealed class TestModule(
    ModuleManifest manifest, Action<IModuleComposition>? compose = null) : IModule
{
    public ModuleManifest Manifest { get; } = manifest;

    public void Compose(IModuleComposition composition) => compose?.Invoke(composition);
}

internal static class Fixture
{
    /// <summary>
    /// The default fixture plays at SIMULATION fidelity — the full models — so a
    /// test that does not say otherwise is testing the physics the design
    /// specifies rather than the simplified one (design 18 §5b).
    /// </summary>
    public static EngineSettings Settings(
        FaultHandling handling = FaultHandling.Strict, string profile = "simulation") =>
        new(new GameDate(1965, 1),
            WorldSeed: 20260806UL,
            new AuditRetention(DetailWindowTicks: 12),
            new RecordingSink(),
            LogLevel.Info,
            handling,
            new ContentId(profile));

    public static ModuleManifest Manifest(
        string name,
        IReadOnlyList<Type>? provides = null,
        IReadOnlyList<Type>? requires = null,
        IReadOnlyList<string>? ownsState = null,
        IReadOnlyList<StageParticipation>? stages = null) =>
        new(new ModuleName(name),
            provides ?? [],
            requires ?? [],
            [.. (ownsState ?? []).Select(key => new StateKey(key))],
            stages ?? [],
            []);

    public static IReadOnlyList<CompositionProblem> ProblemsFrom(BuildResult result) =>
        Assert.IsType<BuildRefused>(result).Problems;
}

public sealed class ShippedSetTests
{
    /// <summary>
    /// The headline: the fourteen shipped modules compose. Every Requires is met
    /// by something in the set, nothing is provided twice, no state key is owned
    /// twice, and the graph is acyclic — checked as a SET, which is the only way
    /// the answer means anything.
    /// </summary>
    [Fact]
    public void The_shipped_module_set_composes()
    {
        BuildResult result = EngineBuilder.Build(Fixture.Settings());

        Built built = Assert.IsType<Built>(result);
        Assert.Equal(15, built.Engine.Modules.Count);
    }

    /// <summary>
    /// Every contract a module declares in Requires is declared in some module's
    /// Provides. This is the check that makes the manifest worth writing: a
    /// module that took a dependency without declaring it would compose by luck
    /// of list order.
    /// </summary>
    [Fact]
    public void Every_declared_requirement_is_declared_by_some_provider()
    {
        var audit = new AuditTrail(
            new SimulationClock(new GameDate(1965, 1)), new AuditRetention(12));

        IReadOnlyList<IModule> modules = EngineBuilder.ShippedModules(
            audit, new SimulationClock(new GameDate(1965, 1)), new RandomSource(1UL),
            Defaults.Simulation);

        var provided = new HashSet<Type>();
        foreach (IModule module in modules)
            foreach (Type contract in module.Manifest.Provides)
                Assert.True(provided.Add(contract), $"{contract.Name} is provided twice.");

        foreach (IModule module in modules)
            foreach (Type contract in module.Manifest.Requires)
                Assert.True(provided.Contains(contract),
                    $"{module.Manifest.Name.Value} requires {contract.Name}, which nothing provides.");
    }

    /// <summary>
    /// Declaration order determines nothing. Reversing the list must produce the
    /// same engine — construction order comes from the dependency graph, not
    /// from the caller (finding 126). Before the fix this threw, because
    /// `flow` resolved `IAuditTrail` before `diagnostics` had provided it.
    /// </summary>
    [Fact]
    public void Reversing_the_declaration_order_composes_identically()
    {
        var audit = new AuditTrail(
            new SimulationClock(new GameDate(1965, 1)), new AuditRetention(12));

        var reversed = new List<IModule>(EngineBuilder.ShippedModules(
            audit, new SimulationClock(new GameDate(1965, 1)), new RandomSource(1UL),
            Defaults.Simulation));
        reversed.Reverse();

        Built forward = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings()));
        Built backward = Assert.IsType<Built>(
            EngineBuilder.Build(Fixture.Settings(), reversed));

        Assert.Equal(forward.Engine.Modules.Count, backward.Engine.Modules.Count);

        for (int i = 0; i < forward.Engine.Modules.Count; i++)
            Assert.Equal(
                forward.Engine.Modules[i].Manifest.Name,
                backward.Engine.Modules[i].Manifest.Name);
    }

    /// <summary>
    /// A composed engine yields a real pipeline running the fourteen-stage order,
    /// and advancing it does real work: the subsurface's material balance is
    /// stage 6, and it runs.
    /// </summary>
    [Fact]
    public void The_composed_engine_advances_a_tick()
    {
        Built built = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings()));

        Assert.IsType<TickCompleted>(built.Engine.Pipeline.AdvanceTick());
        Assert.Equal(1, built.Engine.Pipeline.CurrentTick.Value);
    }

    /// <summary>
    /// The shipped engine runs exactly the stages its modules declared, in
    /// design 03 §6's order — plan, solve, commit, meter, pay, close. Named
    /// rather than counted, so adding a stage without declaring it fails here.
    ///
    /// <para>R20d.1 added two: <b>Availability</b>, which says which elements are
    /// available and for how long, and <b>Custody</b>, which records what crossed
    /// the meter. Both are slots design 03 §6 has always had and nothing filled —
    /// stage 5 now iterates a plan instead of assuming the month, and stage 8
    /// prices a metered delivery instead of whatever the wells produced.</para>
    /// </summary>
    [Fact]
    public void The_shipped_engine_runs_the_stages_its_modules_declared()
    {
        Built built = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings()));

        Assert.Equal(
            [StageId.Operations, StageId.Availability, StageId.SolveFlow,
             StageId.MaterialBalance, StageId.Custody, StageId.Economics,
             StageId.Objectives, StageId.Close],
            built.Engine.Pipeline.DeclaredOrder());
    }

    /// <summary>
    /// Many ticks, not one. A stage that worked once and then faulted — on a
    /// second commit, a growing history, an accumulating rounding error — would
    /// pass the single-tick test above and fail a game.
    /// </summary>
    [Fact]
    public void The_composed_engine_advances_a_hundred_ticks()
    {
        Built built = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings()));

        for (int tick = 0; tick < 100; tick++)
            Assert.IsType<TickCompleted>(built.Engine.Pipeline.AdvanceTick());

        Assert.Equal(100, built.Engine.Pipeline.CurrentTick.Value);
    }
}

public sealed class RefusalTests
{
    /// <summary>
    /// The all-or-nothing law. A set missing a provider does not build a partial
    /// engine — it refuses, and it names the requirement.
    /// </summary>
    [Fact]
    public void An_unmet_requirement_refuses_the_whole_composition()
    {
        IReadOnlyList<IModule> modules =
        [
            new TestModule(Fixture.Manifest("consumer", requires: [typeof(IFlowSolver)])),
        ];

        IReadOnlyList<CompositionProblem> problems =
            Fixture.ProblemsFrom(EngineBuilder.Build(Fixture.Settings(), modules));

        CompositionProblem problem = Assert.Single(problems);
        Assert.Equal(CompositionProblemKind.UnmetRequirement, problem.Kind);
        Assert.Contains("IFlowSolver", problem.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// EVERY problem, never just the first (R1 §2.9). A developer fixing
    /// composition one error per run is the failure mode the rule exists to
    /// prevent, so three unmet requirements come back as three problems.
    /// </summary>
    [Fact]
    public void Every_unmet_requirement_is_named_not_only_the_first()
    {
        IReadOnlyList<IModule> modules =
        [
            new TestModule(Fixture.Manifest("a", requires: [typeof(IFlowSolver)])),
            new TestModule(Fixture.Manifest("b", requires: [typeof(IFiscalRegime)])),
            new TestModule(Fixture.Manifest("c", requires: [typeof(IBeliefStore)])),
        ];

        IReadOnlyList<CompositionProblem> problems =
            Fixture.ProblemsFrom(EngineBuilder.Build(Fixture.Settings(), modules));

        Assert.Equal(3, problems.Count);

        string detail = string.Join(" ", problems.Select(p => p.Detail));
        Assert.Contains("IFlowSolver", detail, StringComparison.Ordinal);
        Assert.Contains("IFiscalRegime", detail, StringComparison.Ordinal);
        Assert.Contains("IBeliefStore", detail, StringComparison.Ordinal);
    }

    /// <summary>Two modules cannot both provide one contract — which one won
    /// would otherwise be decided by list position.</summary>
    [Fact]
    public void A_contract_provided_twice_refuses()
    {
        IReadOnlyList<IModule> modules =
        [
            new TestModule(Fixture.Manifest("first", provides: [typeof(IFlowSolver)])),
            new TestModule(Fixture.Manifest("second", provides: [typeof(IFlowSolver)])),
        ];

        IReadOnlyList<CompositionProblem> problems =
            Fixture.ProblemsFrom(EngineBuilder.Build(Fixture.Settings(), modules));

        Assert.Contains(problems, p => p.Kind == CompositionProblemKind.DuplicateProvider);
    }

    /// <summary>Law L5 at composition time: one owner per fact.</summary>
    [Fact]
    public void A_state_key_owned_twice_refuses()
    {
        IReadOnlyList<IModule> modules =
        [
            new TestModule(Fixture.Manifest("first", ownsState: ["wells.completions"])),
            new TestModule(Fixture.Manifest("second", ownsState: ["wells.completions"])),
        ];

        IReadOnlyList<CompositionProblem> problems =
            Fixture.ProblemsFrom(EngineBuilder.Build(Fixture.Settings(), modules));

        CompositionProblem problem = Assert.Single(problems);
        Assert.Equal(CompositionProblemKind.DuplicateStateKey, problem.Kind);
        Assert.Contains("wells.completions", problem.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// Two modules in one stage at one order: the tick would run them in an
    /// order nobody declared, so the set is refused (03 §6).
    /// </summary>
    [Fact]
    public void Two_modules_claiming_one_stage_slot_refuse()
    {
        IReadOnlyList<IModule> modules =
        [
            new TestModule(Fixture.Manifest("first",
                stages: [new StageParticipation(StageId.Economics, Order: 0)])),
            new TestModule(Fixture.Manifest("second",
                stages: [new StageParticipation(StageId.Economics, Order: 0)])),
        ];

        IReadOnlyList<CompositionProblem> problems =
            Fixture.ProblemsFrom(EngineBuilder.Build(Fixture.Settings(), modules));

        Assert.Contains(problems, p => p.Kind == CompositionProblemKind.StageConflict);
    }

    /// <summary>
    /// A cycle means no construction order exists. Discovered at startup as a
    /// sentence rather than at runtime as a stack overflow.
    /// </summary>
    [Fact]
    public void A_dependency_cycle_refuses()
    {
        IReadOnlyList<IModule> modules =
        [
            new TestModule(Fixture.Manifest("a",
                provides: [typeof(IFlowSolver)], requires: [typeof(IFiscalRegime)])),
            new TestModule(Fixture.Manifest("b",
                provides: [typeof(IFiscalRegime)], requires: [typeof(IFlowSolver)])),
        ];

        IReadOnlyList<CompositionProblem> problems =
            Fixture.ProblemsFrom(EngineBuilder.Build(Fixture.Settings(), modules));

        Assert.Contains(problems, p => p.Kind == CompositionProblemKind.DependencyCycle);
    }

    /// <summary>
    /// Declaring a contract in Provides and never handing one over is caught
    /// too — validation cannot see it, only running Compose can.
    /// </summary>
    [Fact]
    public void A_declared_provider_that_provides_nothing_refuses()
    {
        IReadOnlyList<IModule> modules =
        [
            new TestModule(Fixture.Manifest("liar", provides: [typeof(IFlowSolver)])),
        ];

        IReadOnlyList<CompositionProblem> problems =
            Fixture.ProblemsFrom(EngineBuilder.Build(Fixture.Settings(), modules));

        CompositionProblem problem = Assert.Single(problems);
        Assert.Equal(CompositionProblemKind.UnmetRequirement, problem.Kind);
        Assert.Contains("declared but never delivered", problem.Detail, StringComparison.Ordinal);
    }
}

public sealed class StageWiringTests
{
    /// <summary>A stage that records that it ran, and when.</summary>
    private sealed class RecordingStage(StageId id, List<StageId> ran) : ITickStage
    {
        public StageId Id => id;

        public void Execute(TickContext context) => ran.Add(id);
    }

    /// <summary>
    /// Finding 125's headline: a module's declared slot is filled with real work,
    /// and the tick runs it. Before the fix `TickPipeline` took its stages from
    /// nowhere and the manifest's stage list was decorative.
    /// </summary>
    [Fact]
    public void A_contributed_stage_runs_in_the_tick()
    {
        var ran = new List<StageId>();

        IReadOnlyList<IModule> modules =
        [
            new TestModule(
                Fixture.Manifest("worker",
                    stages: [new StageParticipation(StageId.Economics, Order: 0)]),
                composition => composition.Contribute(
                    order: 0, new RecordingStage(StageId.Economics, ran))),
        ];

        Built built = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings(), modules));

        Assert.IsType<TickCompleted>(built.Engine.Pipeline.AdvanceTick());
        Assert.Equal([StageId.Economics], ran);
    }

    /// <summary>
    /// Stages run in the fourteen-stage order regardless of which module
    /// declared them or in what order the modules were listed (03 §6).
    /// </summary>
    [Fact]
    public void Stages_run_in_declared_stage_order_not_module_order()
    {
        var ran = new List<StageId>();

        IReadOnlyList<IModule> modules =
        [
            new TestModule(
                Fixture.Manifest("late",
                    stages: [new StageParticipation(StageId.Company, Order: 0)]),
                c => c.Contribute(0, new RecordingStage(StageId.Company, ran))),
            new TestModule(
                Fixture.Manifest("early",
                    stages: [new StageParticipation(StageId.Environment, Order: 0)]),
                c => c.Contribute(0, new RecordingStage(StageId.Environment, ran))),
            new TestModule(
                Fixture.Manifest("middle",
                    stages: [new StageParticipation(StageId.Custody, Order: 0)]),
                c => c.Contribute(0, new RecordingStage(StageId.Custody, ran))),
        ];

        Built built = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings(), modules));
        built.Engine.Pipeline.AdvanceTick();

        Assert.Equal([StageId.Environment, StageId.Custody, StageId.Company], ran);
    }

    /// <summary>
    /// Law L3 on the stage side: a slot claimed and left empty is a declaration
    /// with no behaviour behind it, and the tick would silently skip it.
    /// </summary>
    [Fact]
    public void A_declared_slot_with_no_work_refuses()
    {
        IReadOnlyList<IModule> modules =
        [
            new TestModule(Fixture.Manifest("idle",
                stages: [new StageParticipation(StageId.Economics, Order: 0)])),
        ];

        IReadOnlyList<CompositionProblem> problems =
            Fixture.ProblemsFrom(EngineBuilder.Build(Fixture.Settings(), modules));

        CompositionProblem problem = Assert.Single(problems);
        Assert.Contains("declared but never delivered", problem.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// The other direction: a module cannot act in a stage it never declared.
    /// Without this the manifest would police an order the tick could ignore.
    /// </summary>
    [Fact]
    public void Contributing_to_an_undeclared_slot_throws()
    {
        var ran = new List<StageId>();

        IReadOnlyList<IModule> modules =
        [
            new TestModule(
                Fixture.Manifest("sneak"),
                c => c.Contribute(0, new RecordingStage(StageId.Economics, ran))),
        ];

        InvariantFault fault = Assert.Throws<InvariantFault>(
            () => EngineBuilder.Build(Fixture.Settings(), modules));

        Assert.Contains("never declared", fault.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Within one stage, declared Order decides. Two modules in Economics run in
    /// the order they declared, not the order they were listed — 03 §6 requires
    /// that order be declared rather than emergent.
    /// </summary>
    [Fact]
    public void Within_a_stage_declared_order_decides()
    {
        var ran = new List<string>();

        IReadOnlyList<IModule> modules =
        [
            new TestModule(
                Fixture.Manifest("second",
                    stages: [new StageParticipation(StageId.Economics, Order: 1)]),
                c => c.Contribute(1, new NamingStage(StageId.Economics, "second", ran))),
            new TestModule(
                Fixture.Manifest("first",
                    stages: [new StageParticipation(StageId.Economics, Order: 0)]),
                c => c.Contribute(0, new NamingStage(StageId.Economics, "first", ran))),
        ];

        Built built = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings(), modules));
        built.Engine.Pipeline.AdvanceTick();

        Assert.Equal(["first", "second"], ran);
    }

    private sealed class NamingStage(StageId id, string name, List<string> ran) : ITickStage
    {
        public StageId Id => id;

        public void Execute(TickContext context) => ran.Add(name);
    }
}
