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
    /// Run months the way a company runs them: fixing what breaks.
    ///
    /// <para>Since R20d.22 equipment ages and fails, and the route law shuts in
    /// everything behind whatever went — so a bare loop of <c>AdvanceTick</c>
    /// measures a field that died of its first unlucky draw rather than the
    /// thing the test came to measure. Every test that runs a field for years
    /// and asks about water, money or decline goes through here.</para>
    ///
    /// <para>RUN-TO-FAILURE, deliberately the least attentive strategy SDD-012
    /// §3 offers: repair what has broken, never what is merely worn. A test
    /// fixture that maintained better than a player could would be measuring a
    /// field nobody can have.</para>
    /// </summary>
    public static void Run(Engine engine, int months)
    {
        ArgumentNullException.ThrowIfNull(engine);

        for (var month = 0; month < months; month++)
        {
            Repair(engine);
            engine.Pipeline.AdvanceTick();
        }
    }

    /// <summary>One month's maintenance: order a repair for anything the chain
    /// view reports as down. The refusals sort out what is already under
    /// way.</summary>
    public static void Repair(Engine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);

        FieldReadModel? seen = engine.ReadModel;
        if (seen is null) return;

        for (var i = 0; i < seen.Chain.Count; i++)
            if (seen.Chain[i].Failed)
                engine.Commands.Submit(new RepairEquipmentCommand(seen.Chain[i].Element));
    }

    /// <summary>
    /// The default fixture plays at SIMULATION fidelity — the full models — so a
    /// test that does not say otherwise is testing the physics the design
    /// specifies rather than the simplified one (design 18 §5b).
    /// </summary>
    public static EngineSettings Settings(
        FaultHandling handling = FaultHandling.Strict, string profile = "simulation",
        ulong seed = 20260806UL, IReadOnlyList<IContentSource>? content = null) =>
        new(new GameDate(1965, 1),
            WorldSeed: seed,
            new AuditRetention(DetailWindowTicks: 12),
            new RecordingSink(),
            LogLevel.Info,
            handling,
            new ContentId(profile),
            content ?? [ShippedContent()]);

    /// <summary>
    /// The repository's own <c>content/</c>, read from disk — which is what a
    /// HOST does (SDD-004 §7). The engine never opens a file; a source hands it
    /// text it has already read, so these tests exercise the same path a shipped
    /// game takes rather than a fixture that hands over hand-built definitions.
    ///
    /// <para>Anchored on this file's compile-time path so the suite finds the
    /// directory from any working directory or runner — the same trick
    /// <c>EngineCorpus</c> and <c>ShippedContentTests</c> use.</para>
    /// </summary>
    public static IContentSource ShippedContent(
        [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
    {
        DirectoryInfo here = new FileInfo(thisFile).Directory!;   // tests/OGSim.Composition.Tests
        string root = Path.Combine(here.Parent!.Parent!.FullName, "content", "facilities");

        var files = new List<ContentFile>();

        foreach (string path in Directory.EnumerateFiles(root, "*.json")
                                         .OrderBy(p => p, StringComparer.Ordinal))
            files.Add(new ContentFile("facilities/" + Path.GetFileName(path),
                                      File.ReadAllText(path)));

        return new DirectorySource(files);
    }

    /// <summary>The shipped ladders, for the two tests that build the module
    /// list themselves rather than going through <c>EngineBuilder.Build</c>.</summary>
    public static FacilityLadders Ladders()
    {
        var loader = new ContentLoader(
            [
                new SeparatorContentKind(), new TankContentKind(), new TreaterContentKind(),
                new GasPlantContentKind(), new ExportLineContentKind(), new ManifoldContentKind(),
            ],
            new PluginRegistry());

        ContentLoadResult result = loader.LoadAll([ShippedContent()]);

        if (result is ContentFailures failed)
            throw new InvalidOperationException(
                "the shipped facility content does not load: " + string.Join(
                    "; ", failed.Failures.Select(f => $"{f.File} {f.JsonPath} {f.Message}")));

        return FacilityLadders.From(((ContentLoaded)result).Catalogues);
    }

    private sealed class DirectorySource(IReadOnlyList<ContentFile> files) : IContentSource
    {
        public string Name => "base";

        /// <summary>Base content is 0; a mod declares higher (SDD-004 §7).</summary>
        public int DeclaredOrder => 0;

        public IReadOnlyList<ContentFile> Files => files;
    }

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
        Assert.Equal(16, built.Engine.Modules.Count);
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
            Defaults.Simulation, Fixture.Ladders());

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
            Defaults.Simulation, Fixture.Ladders()));
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
    ///
    /// <para>R20d.28 added <b>Information</b>, the eleventh slot, where beliefs go
    /// stale on what the field produced. It is the first stage in this list that
    /// exists to make what the company KNOWS decay rather than to move fluid or
    /// money — and this test is why adding it was a visible edit rather than a
    /// silent one (SDD-008 §2d.3, finding 200).</para>
    ///
    /// <para>R22.17 made <b>Availability</b> appear TWICE, which is a slot with two
    /// contributors rather than a stage declared twice: the bow-tie's threat pass
    /// runs first and may take an element out, and the hazard/segmentation pass
    /// then plans the month around whatever survived. A stage is a point in the
    /// tick, not a monopoly — this list is of contributions in execution order, so
    /// a second one at an existing point is exactly as visible as a new point.</para>
    /// </summary>
    [Fact]
    public void The_shipped_engine_runs_the_stages_its_modules_declared()
    {
        Built built = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings()));

        Assert.Equal(
            [StageId.Environment, StageId.Operations,
             StageId.Availability, StageId.Availability,
             StageId.SolveFlow, StageId.MaterialBalance, StageId.Custody,
             StageId.Economics, StageId.HseRegulation, StageId.Information,
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

/// <summary>
/// R20c.9.2 — the eleventh non-negotiable, as a run rather than an assertion in
/// a design document: <b>rebalancing is a content edit.</b>
/// </summary>
public sealed class FacilityContentTests
{
    /// <summary>
    /// The shipped sheets with one file's text replaced.
    ///
    /// <para>All fifteen, not just the edited one: a ladder with a missing kind
    /// is a refusal, so handing over one sheet would test that refusal rather
    /// than the edit. The point is a rebalance of a REAL game.</para>
    /// </summary>
    private static IContentSource Edited(string id, string find, string replace)
    {
        var files = new List<ContentFile>();

        foreach (ContentFile file in Fixture.ShippedContent().Files)
            files.Add(file.RelativePath.EndsWith(id + ".json", StringComparison.Ordinal)
                ? file with { Json = Replaced(file.Json, find, replace) }
                : file);

        return new Many(files);
    }

    private static string Replaced(string json, string find, string replace)
    {
        Assert.Contains(find, json, StringComparison.Ordinal);
        return json.Replace(find, replace, StringComparison.Ordinal);
    }

    private sealed class Many(IReadOnlyList<ContentFile> files) : IContentSource
    {
        public string Name => "base";
        public int DeclaredOrder => 0;
        public IReadOnlyList<ContentFile> Files => files;
    }

    /// <summary>
    /// <b>A number moves in a JSON file and the composed engine is different</b> —
    /// no engine assembly edited, no rebuild of anything but the test.
    ///
    /// <para>This is what R20c.9 was for. Until the join, the six ladders were C#
    /// records in composition's <c>Defaults</c>, so design 03's eleventh
    /// non-negotiable was false of the only equipment the game shipped: moving a
    /// separator's capacity meant editing an engine assembly. The sheets existed
    /// from R20c.9.1 and nothing read them, which is the same *built and joined to
    /// nothing* shape as findings 164–177 — so this test, not the sheets, is what
    /// closes the task.</para>
    ///
    /// <para>Asserted on the LIQUID leg because that is the one this chain jams
    /// on: <c>R20dV1</c> proves the shipped vessel binds on liquid capacity, so
    /// doubling it in content and reading it back through the engine's own
    /// contract is a change a player would feel.</para>
    /// </summary>
    [Fact]
    public void A_content_edit_changes_the_engine_with_no_code_edit()
    {
        Built shipped = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings()));

        Assert.Equal(
            12.0,
            shipped.Engine.Provided.Resolve<FacilityLadders>().Separator[0].LiquidCapacity.KgPerSecond,
            precision: 9);

        Built rebalanced = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings(
            content: [Edited("separator-3phase-e1", "\"liquidCapacity\": \"12 kg/s\"",
                                                   "\"liquidCapacity\": \"24 kg/s\"")])));

        Assert.Equal(
            24.0,
            rebalanced.Engine.Provided.Resolve<FacilityLadders>().Separator[0].LiquidCapacity.KgPerSecond,
            precision: 9);
    }

    /// <summary>
    /// Content that will not load is a REFUSAL to start, naming the file and the
    /// stage (design 10 §3's G2). Not a warning and not a default vessel: a game
    /// that cannot read its own equipment has nothing to start.
    /// </summary>
    [Fact]
    public void Content_that_will_not_load_refuses_the_engine()
    {
        BuildResult result = EngineBuilder.Build(Fixture.Settings(
            content: [Edited("separator-3phase-e1", "\"gasCapacity\": \"50 kg/s\"",
                                                   "\"gasCapacity\": \"50 furlongs\"")]));

        LoadFailure failure = Assert.Single(
            Assert.IsType<BuildRefusedByContent>(result).Failures);

        Assert.Contains("separator-3phase-e1", failure.File, StringComparison.Ordinal);
    }

    /// <summary>
    /// And a ladder with a hole is refused too — this one at COMPOSITION rather
    /// than at load, because a gap is a statement about a set and the loader's
    /// consistency pass sees one definition at a time.
    /// </summary>
    [Fact]
    public void A_ladder_with_a_missing_rung_is_refused()
    {
        ContentFault fault = Assert.Throws<ContentFault>(() => EngineBuilder.Build(
            Fixture.Settings(content:
            [
                Edited("gas-plant-e1", "\"rung\": 1", "\"rung\": 3"),
            ])));

        Assert.Contains("gas-plant", fault.Message, StringComparison.Ordinal);
    }
}
