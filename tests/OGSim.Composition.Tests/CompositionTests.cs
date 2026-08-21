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

        // AND THE TECHNOLOGY REGISTRY, which the engine reads from the same
        // source since R20d.10 — sixty-five nodes that shipped at R20c.9 and
        // were read by a fixture test alone.
        string tech = Path.Combine(here.Parent!.Parent!.FullName, "content", "technologies");

        foreach (string path in Directory.EnumerateFiles(tech, "*.json")
                                         .OrderBy(p => p, StringComparer.Ordinal))
            files.Add(new ContentFile("technologies/" + Path.GetFileName(path),
                                      File.ReadAllText(path)));

        // AND THE TERRAIN CLASSES (R20d.8.9) — the same door.
        string terrain = Path.Combine(here.Parent!.Parent!.FullName, "content", "terrain-classes");

        foreach (string path in Directory.EnumerateFiles(terrain, "*.json")
                                         .OrderBy(p => p, StringComparer.Ordinal))
            files.Add(new ContentFile("terrain-classes/" + Path.GetFileName(path),
                                      File.ReadAllText(path)));

        // AND THE ONE SALES CONTRACT (SDD-009 §7's R13.3 amendment, finding
        // 250) — the same door.
        string contracts = Path.Combine(here.Parent!.Parent!.FullName, "content", "contracts");

        foreach (string path in Directory.EnumerateFiles(contracts, "*.json")
                                         .OrderBy(p => p, StringComparer.Ordinal))
            files.Add(new ContentFile("contracts/" + Path.GetFileName(path),
                                      File.ReadAllText(path)));

        // AND THE ONE ROD-PUMP TIER (SDD-003 §6.2's R12b.2 amendment, finding
        // 255) — the same door.
        string wells = Path.Combine(here.Parent!.Parent!.FullName, "content", "wells");

        foreach (string path in Directory.EnumerateFiles(wells, "*.json")
                                         .OrderBy(p => p, StringComparer.Ordinal))
            files.Add(new ContentFile("wells/" + Path.GetFileName(path),
                                      File.ReadAllText(path)));

        return new DirectorySource(files);
    }

    /// <summary>The shipped ladders, for the two tests that build the module
    /// list themselves rather than going through <c>EngineBuilder.Build</c>.</summary>
    public static FacilityLadders Ladders() => FacilityLadders.From(Loaded());

    /// <summary>The shipped content, through the real loader.</summary>
    private static ICatalogSet Loaded()
    {
        var loader = new ContentLoader(
            [
                new SeparatorContentKind(), new TankContentKind(), new TreaterContentKind(),
                new GasPlantContentKind(), new ExportLineContentKind(), new ManifoldContentKind(),
                new CompressorContentKind(),
                new OGSim.Capabilities.TechnologyContentKind(),
                new OGSim.World.TerrainClassContentKind(),
                new TakeOrPayContentKind(),
                new DisplacementPumpContentKind("rod-pump"),
                new DisplacementPumpContentKind("pcp"),
                new EspContentKind(),
                new GasLiftContentKind(),
            ],
            new PluginRegistry());

        ContentLoadResult result = loader.LoadAll([ShippedContent()]);

        if (result is ContentFailures failed)
            throw new InvalidOperationException(
                "the shipped content does not load: " + string.Join(
                    "; ", failed.Failures.Select(f => $"{f.File} {f.JsonPath} {f.Message}")));

        return ((ContentLoaded)result).Catalogues;
    }

    /// <summary>The shipped technology registry, for the tests that build the
    /// module list themselves.</summary>
    public static IReadOnlyList<OGSim.Capabilities.TechnologyNode> Registry()
    {
        var graph = new List<OGSim.Capabilities.TechnologyNode>();

        foreach (OGSim.Capabilities.TechnologyDefinition node in
                 Loaded().Of<OGSim.Capabilities.TechnologyDefinition>().All)
        {
            var prerequisites = new List<TechnologyId>(node.Prerequisites.Count);

            for (int i = 0; i < node.Prerequisites.Count; i++)
                prerequisites.Add(new TechnologyId(node.Prerequisites[i]));

            graph.Add(new OGSim.Capabilities.TechnologyNode(
                new TechnologyId(node.Id), node.AvailableFrom, node.DiffusionLagTicks,
                prerequisites, node.Effects, node.GrantsDetectClass, node.Routes));
        }

        return graph;
    }

    /// <summary>The shipped terrain classes, for the tests that build the
    /// module list themselves.</summary>
    public static IReadOnlyList<OGSim.World.TerrainClassDefinition> TerrainClasses() =>
        Loaded().Of<OGSim.World.TerrainClassDefinition>().All;

    /// <summary>The shipped take-or-pay contract (SDD-009 §7's R13.3
    /// amendment), for the tests that build the module list themselves.</summary>
    public static OGSim.Company.TakeOrPayTerms TakeOrPay()
    {
        TakeOrPayDefinition definition =
            Loaded().Of<TakeOrPayDefinition>()[new ContentId("oil-take-or-pay")];

        return new OGSim.Company.TakeOrPayTerms(
            definition.CommittedVolume, definition.WindowMonths, definition.PenaltyRate);
    }

    /// <summary>The four shipped lift tiers (SDD-003 §6.2's R12b.2
    /// amendment), for the tests that build the module list themselves.</summary>
    public static OGSim.Wells.LiftTiers LiftTiers()
    {
        ICatalogSet loaded = Loaded();

        DisplacementPumpDefinition rod =
            loaded.Of<DisplacementPumpDefinition>()[new ContentId("rod-pump-a")];
        DisplacementPumpDefinition pcp =
            loaded.Of<DisplacementPumpDefinition>()[new ContentId("pcp-a")];
        EspDefinition esp = loaded.Of<EspDefinition>()[new ContentId("esp-a")];
        GasLiftDefinition gasLift =
            loaded.Of<GasLiftDefinition>()[new ContentId("gas-lift-a")];

        return new OGSim.Wells.LiftTiers(
            DisplacementPump(rod), DisplacementPump(pcp),
            new OGSim.Wells.EspTier(
                esp.Id, Envelope(esp.MinRate, esp.MaxRate, esp.MaxDepth, esp.MaxDeviationDegrees,
                                  esp.MaxGasFraction, esp.MaxTemperature, esp.MaxSolidsFraction),
                esp.HeadCurve, esp.Efficiency),
            new OGSim.Wells.GasLiftTier(
                gasLift.Id,
                Envelope(gasLift.MinRate, gasLift.MaxRate, gasLift.MaxDepth,
                         gasLift.MaxDeviationDegrees, gasLift.MaxGasFraction,
                         gasLift.MaxTemperature, gasLift.MaxSolidsFraction),
                gasLift.InjectionRate.CubicMetresPerSecond, gasLift.GasDensityKgPerM3));
    }

    private static OGSim.Wells.DisplacementPumpTier DisplacementPump(
        DisplacementPumpDefinition definition) =>
        new(definition.Id,
            Envelope(definition.MinRate, definition.MaxRate, definition.MaxDepth,
                     definition.MaxDeviationDegrees, definition.MaxGasFraction,
                     definition.MaxTemperature, definition.MaxSolidsFraction),
            definition.Displacement.CubicMetresPerSecond);

    private static LiftEnvelope Envelope(
        ReservoirRate minRate, ReservoirRate maxRate, Length maxDepth,
        double maxDeviationDegrees, double maxGasFraction, Temperature maxTemperature,
        double maxSolidsFraction) =>
        new(minRate, maxRate, maxDepth, maxDeviationDegrees, maxGasFraction, maxTemperature,
            maxSolidsFraction);

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
            Defaults.Simulation, Fixture.Ladders(), Fixture.Registry(), Fixture.TerrainClasses(),
            Fixture.TakeOrPay(), Fixture.LiftTiers());

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
            Defaults.Simulation, Fixture.Ladders(), Fixture.Registry(), Fixture.TerrainClasses(),
            Fixture.TakeOrPay(), Fixture.LiftTiers()));
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
    ///
    /// <para>R20d.9 filled <b>Company</b>, a slot the fourteen-stage order has
    /// carried since design 03 §6 and no module had ever contributed to: a
    /// licence's work commitment is assessed here, so a missed deadline forfeits
    /// the bond and blocks further drilling. **Company now has TWO
    /// contributors**, corrected here after a first pass wrongly placed the
    /// second at Environment: technology diffusion runs at order 1, after the
    /// licence assessment at order 0, because SDD-005 §4.2 places it there —
    /// "applied when acquisition completes (a stage-11 state change), taking
    /// effect next tick" — and the first placement (beside weather, reasoning by
    /// analogy rather than checking §4.2) would have let a diffusing node reach
    /// stage 4's segmentation the SAME month it diffused (SDD-005's R20d.10
    /// correction).</para>
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

             // Three StageId.Company participants: CompanyModule's
             // LicenceStage (order 0), CapabilitiesModule's diffusion
             // (order 1), and FieldModule's TakeOrPayStage (order 2,
             // SDD-009 §7's R13.3 amendment, finding 250).
             StageId.Company, StageId.Company, StageId.Company,
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

/// <summary>
/// R22.6 — the access window, through a composed engine (SDD-016 §5b's R22.6
/// amendment).
/// </summary>
public sealed class AccessWindowTests
{
    /// <summary>An ice road: reachable December through March and no other
    /// month.</summary>
    private static OGSim.Environment.ClimateProfile Arctic() =>
        new(new ContentId("arctic-onshore"),
            Persistence: 0.75,
            Baseline: [.. Enumerable.Repeat(3.0, 12)],
            Amplitude: [.. Enumerable.Repeat(1.0, 12)],
            TemperatureBaseline: [.. Enumerable.Repeat(-15.0, 12)],
            TemperatureAmplitude: -4.0,
            AccessOpen: [.. Enumerable.Range(1, 12).Select(m => m <= 3 || m == 12)],
            Effects: []);

    /// <summary>The shipped set with the climate swapped, which is what makes
    /// this a test of the ENGINE rather than of the profile record.</summary>
    private static Engine OnAnIceRoad(GameDate epoch)
    {
        var clock = new SimulationClock(epoch);
        var audit = new AuditTrail(clock, new AuditRetention(12));

        var modules = new List<IModule>(EngineBuilder.ShippedModules(
            audit, clock, new RandomSource(20260806UL), Defaults.Simulation,
            Fixture.Ladders(), Fixture.Registry(), Fixture.TerrainClasses(), Fixture.TakeOrPay(),
            Fixture.LiftTiers()));

        for (int i = 0; i < modules.Count; i++)
            if (modules[i] is EnvironmentModule)
                modules[i] = new EnvironmentModule(Arctic());

        EngineSettings settings = Fixture.Settings() with { Epoch = epoch };

        Engine engine = Assert.IsType<Built>(EngineBuilder.Build(settings, modules)).Engine;

        // A FIELD TO WORK ON. The shared refusals return early on a company with
        // no compartment — "there is nothing here to work on" — and would answer
        // that instead of the window, which would make this test pass for the
        // wrong reason in the month it is supposed to fail.
        engine.Provided.Resolve<FieldControl>().AddCompartment(
            new GeneratedCompartment(
                PoreVolume: new ReservoirVolume(100.0e6),
                Porosity: 0.22,
                OilSaturation: 0.7,
                InitialPressure: new Pressure(30.0e6),
                Temperature: Temperature.FromCelsius(93.3),
                Depth: new Length(2000.0)),
            permeability: new Permeability(1.0e-13),
            netThickness: new Length(20.0),
            drainageArea: new Area(2.0e5),
            rockCompressibility: 4.5e-10,
            gasOilContact: new Length(1900.0),
            oilWaterContact: new Length(2100.0),
            Defaults.Wettability, Defaults.Drive,
            Defaults.AquiferStrength, Defaults.AquiferResponseTime);

        return engine;
    }

    /// <summary>
    /// EN3 — <b>a rig cannot be ordered onto a site the road does not reach</b>,
    /// and the player is told so rather than having the order quietly queued.
    ///
    /// <para><b>EN4 is NOT covered by this and is not claimed</b>: a delayed
    /// arctic operation waiting a full year while the licence clock keeps running
    /// is a second mechanic — the refusal here says no, and nothing yet models
    /// what that costs a company holding a licence with a commitment on it.</para>
    ///
    /// <para>Refused and not deferred, which is the whole mechanic: a window
    /// gates STARTING, so the decision it creates is a DEADLINE — the work has to
    /// be committed before the road shuts. An order silently held until June would
    /// take that decision away and hand back a surprise.</para>
    /// </summary>
    [Fact]
    public void EN3_work_that_must_be_mobilised_is_refused_while_the_road_is_shut()
    {
        Engine shut = OnAnIceRoad(new GameDate(1965, 7));

        // A VESSEL CRANED ONTO THE DECK: it has to arrive, so it is gated. The
        // command needs no world, which keeps this test about the window.
        Rejected refused = Assert.IsType<Rejected>(
            shut.Commands.Submit(new InstallSeparatorCommand()));

        Assert.Contains(refused.Reasons,
            reason => reason.LocId == "$loc:reject.access-closed");
    }

    /// <summary>
    /// And accepted in a month the road is open — the other half, without which
    /// the test above would pass against a survey that could never be ordered at
    /// all.
    /// </summary>
    [Fact]
    public void EN3_the_same_work_is_accepted_while_the_road_is_open()
    {
        Engine open = OnAnIceRoad(new GameDate(1965, 1));

        Assert.IsType<Accepted>(open.Commands.Submit(new InstallSeparatorCommand()));
    }

    /// <summary>
    /// Work done with what is already on site is NOT gated. A repair is an
    /// emergency fix with the spares aboard, and refusing it for eight months of
    /// the year would shut the field in for a road it never needed.
    /// </summary>
    [Fact]
    public void Work_needing_no_mobilisation_is_not_gated_by_the_window()
    {
        Assert.False(Defaults.RepairEquipmentTerms.RequiresAccess);
        Assert.False(Defaults.ServiceEquipmentTerms.RequiresAccess);
        Assert.False(Defaults.WellTestTerms.RequiresAccess);
        Assert.False(Defaults.RemediateInjectorTerms.RequiresAccess);

        Assert.True(Defaults.DrillWellTerms.RequiresAccess);
        Assert.True(Defaults.SeismicSurveyTerms.RequiresAccess);
        Assert.True(Defaults.ExpandExportTerms.RequiresAccess);
    }

    /// <summary>
    /// AND NO SHIPPED CLIMATE CLOSES, stated as a test so it is a decision rather
    /// than an oversight. `temperate-offshore` is reached by boat in every month;
    /// what stops work there is the sea state on the day, which `WeatherLimit`
    /// already prices. A window belongs to an ice road or a monsoon coast, and
    /// authoring one is R20's scenario content.
    /// </summary>
    [Fact]
    public void No_shipped_climate_closes_and_that_is_deliberate()
    {
        for (var month = 0; month < 12; month++)
            Assert.True(Defaults.Climate.AccessOpen[month],
                $"month {month + 1} of the shipped climate is closed; if that is " +
                "intended, the slow suite's timelines change and this test should say so");
    }

    /// <summary>
    /// R21.6's coverage record named "access windows with time remaining" as
    /// ABSENT while `WeatherState.MonthsUntilAccessCloses` already answered
    /// it, joined to nothing (finding 253's shape again). Proved on the road
    /// that actually shuts, not the shipped climate that never does — see
    /// <see cref="No_shipped_climate_closes_and_that_is_deliberate"/> for why
    /// that fixture would read 12 every month and prove nothing.
    /// </summary>
    [Fact]
    public void The_read_model_carries_months_until_the_window_shuts()
    {
        // Read as of the date a tick LEAVES the clock at, not the one it
        // simulated — a forward answer to a forward question ("how long have
        // I got"), the same direction `AccessOpenIn`'s own refusal check asks
        // in when validating a NEW command against `clock.Date`.
        Engine engine = OnAnIceRoad(new GameDate(1965, 1));   // January

        engine.Pipeline.AdvanceTick();   // clock is now at February

        Assert.Equal(2, engine.ReadModel!.Weather.MonthsUntilAccessCloses);
    }

    [Fact]
    public void The_read_model_reports_zero_once_the_window_has_shut()
    {
        Engine engine = OnAnIceRoad(new GameDate(1965, 7));   // July: shut until December

        engine.Pipeline.AdvanceTick();

        Assert.Equal(0, engine.ReadModel!.Weather.MonthsUntilAccessCloses);
    }
}

/// <summary>
/// R20d.10 — the technology arc, joined (SDD-005 §2's R20d.10 amendment).
/// </summary>
public sealed class TechnologyArcTests
{
    /// <summary>
    /// <b>A company acquires technology over a campaign</b>, which it could not
    /// before: `CapabilityState` was constructed NOWHERE — not in the engine and
    /// not in a test — so the shipped composition ran `AllCapabilities`, the
    /// sandbox all-tech mode, and the sixty-five nodes in
    /// `content/technologies/` had nothing to be acquired into (finding 235).
    ///
    /// <para>Three joins had to land together for this to be observable, and any
    /// one alone would have been another mechanism wired to nothing: the era is
    /// derived from the date so it advances at all, `ApplyDiffusion` runs at
    /// stage 2 so "eventually standard practice" becomes a date, and the registry
    /// is loaded from content so there is a graph to diffuse through.</para>
    ///
    /// <para>Twelve years, because the shipped start is 1965 and E2 opens in
    /// 1970: nothing can arrive on a lag measured from an era that has not begun.
    /// The assertion is that SOMETHING was granted and not which, because which
    /// node arrives when is the registry's business and this test is about the
    /// mechanism being connected.</para>
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void A_company_acquires_technology_as_the_decades_pass()
    {
        Built built = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings()));

        var capabilities =
            built.Engine.Provided.Resolve<OGSim.Capabilities.CapabilityState>();

        Assert.Empty(capabilities.Technology.Acquired);
        Assert.Equal(Era.E1, capabilities.Era);

        for (var month = 0; month < 12 * 12; month++) built.Engine.Pipeline.AdvanceTick();

        Assert.Equal(Era.E2, capabilities.Era);

        Assert.NotEmpty(capabilities.Technology.Acquired);
    }

    /// <summary>
    /// And the era follows the calendar rather than standing still — the whole of
    /// finding 191, which recorded that a 1965-to-2005 campaign stayed in E1
    /// throughout because `CapabilityState.Era` had a private setter written only
    /// by its constructor and its `Restore`.
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void R20dV10_the_era_advances_with_the_calendar()
    {
        Built built = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings()));

        var capabilities =
            built.Engine.Provided.Resolve<OGSim.Capabilities.CapabilityState>();

        // 1965 + 300 months = 1990, which is E3's first year.
        for (var month = 0; month < 300; month++) built.Engine.Pipeline.AdvanceTick();

        Assert.Equal(Era.E3, capabilities.Era);
    }

    /// <summary>
    /// Only nodes the registry marks <b>D</b> arrive free. The other three routes
    /// are things a company must go and get, and erasing that difference would
    /// make the four routes one (finding 128, guarded here through the engine
    /// rather than through a fixture graph).
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void Only_nodes_with_a_diffusion_route_arrive_free()
    {
        Built built = Assert.IsType<Built>(EngineBuilder.Build(Fixture.Settings()));

        var capabilities =
            built.Engine.Provided.Resolve<OGSim.Capabilities.CapabilityState>();

        for (var month = 0; month < 12 * 30; month++) built.Engine.Pipeline.AdvanceTick();

        IReadOnlyList<OGSim.Capabilities.TechnologyNode> registry = Fixture.Registry();

        foreach (TechnologyId held in capabilities.Technology.Acquired)
        {
            OGSim.Capabilities.TechnologyNode? node = null;

            for (int i = 0; i < registry.Count; i++)
                if (registry[i].Id.Equals(held)) node = registry[i];

            Assert.NotNull(node);
            Assert.Contains(OGSim.Capabilities.AcquisitionRoute.Diffusion, node!.Routes);
        }
    }
}

/// <summary>
/// R20d.10b — equipment cannot be bought before it is invented (SDD-005 §2's
/// R20d.10b amendment).
/// </summary>
public sealed class EquipmentEraTests
{
    /// <summary>The shipped sheets with one file's text replaced.</summary>
    private static IContentSource Edited(string id, string find, string replace)
    {
        var files = new List<ContentFile>();

        foreach (ContentFile file in Fixture.ShippedContent().Files)
            files.Add(file.RelativePath.EndsWith(id + ".json", StringComparison.Ordinal)
                ? file with { Json = Replaced(file.Json, find, replace) }
                : file);

        return new Edit(files);
    }

    private static string Replaced(string json, string find, string replace)
    {
        Assert.Contains(find, json, StringComparison.Ordinal);
        return json.Replace(find, replace, StringComparison.Ordinal);
    }

    private sealed class Edit(IReadOnlyList<ContentFile> files) : IContentSource
    {
        public string Name => "base";
        public int DeclaredOrder => 0;
        public IReadOnlyList<ContentFile> Files => files;
    }

    /// <summary>
    /// An engine with something to work on. The shared refusals return early on a
    /// company with no compartment — "there is nothing here to work on" — which
    /// would answer instead of the era and make these tests pass for the wrong
    /// reason.
    /// </summary>
    private static Engine Field(IContentSource? content = null)
    {
        Built built = Assert.IsType<Built>(EngineBuilder.Build(
            content is null ? Fixture.Settings() : Fixture.Settings(content: [content])));

        built.Engine.Provided.Resolve<FieldControl>().AddCompartment(
            new GeneratedCompartment(
                PoreVolume: new ReservoirVolume(100.0e6),
                Porosity: 0.22,
                OilSaturation: 0.7,
                InitialPressure: new Pressure(30.0e6),
                Temperature: Temperature.FromCelsius(93.3),
                Depth: new Length(2000.0)),
            permeability: new Permeability(1.0e-13),
            netThickness: new Length(20.0),
            drainageArea: new Area(2.0e5),
            rockCompressibility: 4.5e-10,
            gasOilContact: new Length(1900.0),
            oilWaterContact: new Length(2100.0),
            Defaults.Wettability, Defaults.Drive,
            Defaults.AquiferStrength, Defaults.AquiferResponseTime);

        return built.Engine;
    }

    /// <summary>
    /// <b>A 1965 company cannot buy 1970s equipment</b>, and until now it could:
    /// every facility sheet declared an `availableFromEra` because SDD-004 §6's
    /// gate requires one, `FacilityLadders` built every rung regardless, and
    /// nothing anywhere read the field — so `gas-plant-e2` was purchasable on the
    /// first tick of a game set five years before it existed (finding 234).
    ///
    /// <para>The refusal is a CALENDAR statement and not a `Requirements` miss:
    /// an era that has not arrived is not something a company can go and get, so
    /// it is not a missing item the gating validator could name a remedy for. It
    /// names the era and the year, because "not invented yet, and here is when"
    /// is actionable where "requirements not met" is not.</para>
    /// </summary>
    [Fact]
    public void Equipment_from_a_later_era_is_refused_with_the_year_it_arrives()
    {
        Engine engine = Field();

        // The gas plant's rung 1 is E1 and its rung 2 is E2, so climb one first:
        // the refusal we want is the one ABOVE what a 1965 field can have.
        Assert.IsType<Accepted>(engine.Commands.Submit(new InstallGasPlantCommand()));

        engine.Pipeline.AdvanceTick();
        while (engine.ReadModel!.ActivitiesRunning > 0) engine.Pipeline.AdvanceTick();

        Rejected refused = Assert.IsType<Rejected>(
            engine.Commands.Submit(new InstallGasPlantCommand()));

        RejectionReason reason = Assert.Single(
            refused.Reasons, r => r.LocId == "$loc:reject.not-yet-invented");

        Assert.Contains("E2", reason.Detail, StringComparison.Ordinal);
        Assert.Contains("1970", reason.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// And it becomes buyable when the decade turns — the other half, without
    /// which the refusal above could be a rung that is simply unreachable.
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void The_same_equipment_is_accepted_once_its_era_arrives()
    {
        Engine engine = Field();

        Assert.IsType<Accepted>(engine.Commands.Submit(new InstallGasPlantCommand()));

        // 1965 to 1970: the year E2 opens.
        for (var month = 0; month < 60; month++) engine.Pipeline.AdvanceTick();

        // THE ERA STOPS REFUSING, which is the claim. Not "the order is
        // accepted": this fixture has a compartment and no wells, so five years
        // of standing charges may leave it unable to afford one — and asserting
        // acceptance would make an era test fail for the price of a gas plant.
        CommandResult result = engine.Commands.Submit(new InstallGasPlantCommand());

        if (result is Rejected refused)
            Assert.DoesNotContain(refused.Reasons,
                reason => reason.LocId == "$loc:reject.not-yet-invented");
    }

    /// <summary>
    /// <b>And a rung can require a TECHNOLOGY, which is the half no shipped
    /// sheet uses yet.</b> `requiresTech` has been on every facility definition
    /// since R20c.9.1 and read by nothing — the same shape `availableFromEra`
    /// had, and it would have passed silently the first day content used it
    /// (finding 236).
    ///
    /// <para>Exercised through an EDITED sheet rather than by shipping a
    /// requirement, because which node opens which rung is a mapping the
    /// TECH_TREE only gestures at — *the tiers span E1–E3* — and authoring it is
    /// R20's content pass. The gate is proved to work so that pass can rely on
    /// it; inventing the mapping here to give the test something to bite on
    /// would be choosing balance numbers to make a mechanism look joined.</para>
    ///
    /// <para>The refusal NAMES the technology, per R17 §2.6b: a player is told
    /// what to go and get, not that requirements were not met. That is the whole
    /// difference between this and the era refusal beside it — an era is a date
    /// to wait for and a technology is an errand.</para>
    /// </summary>
    [Fact]
    public void A_rung_requiring_a_technology_the_company_lacks_is_refused()
    {
        // `directional` is a real node in the shipped registry, and a 1965
        // company holds nothing at all.
        BuildResult result = EngineBuilder.Build(Fixture.Settings(content: [Edited(
            "separator-3phase-e2",
            "\"rung\": 1,",
            "\"rung\": 1, \"requiresTech\": \"directional\",")]));

        Engine engine = Assert.IsType<Built>(result).Engine;

        Rejected refused = Assert.IsType<Rejected>(
            engine.Commands.Submit(new InstallSeparatorCommand()));

        RejectionReason reason = Assert.Single(
            refused.Reasons, r => r.LocId == "$loc:reject.technology-not-held");

        Assert.Contains("directional", reason.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// A rung of the CURRENT era is not gated, which is what stops the check
    /// being a blanket refusal: the separator ladder is E1 throughout and a
    /// field debottlenecks itself in 1965 exactly as it did before.
    /// </summary>
    [Fact]
    public void Equipment_of_the_current_era_is_not_gated()
    {
        Assert.IsType<Accepted>(Field().Commands.Submit(new InstallSeparatorCommand()));
    }
}

/// <summary>
/// R20d.9 — the licence, joined (SDD-011 §1's R20d.9 amendment). One composed
/// licence, checked through a real engine rather than the unit tests
/// <c>Licence</c> already had — those proved the class; these prove the join.
/// </summary>
public sealed class LicenceTests
{
    private static (Engine Engine, EntityId<IReservoirCompartmentEntity> Target) Undrilled(
        ulong seed = 20260806UL)
    {
        Built built = Assert.IsType<Built>(EngineBuilder.Build(
            Fixture.Settings() with { WorldSeed = seed }));

        EntityId<IReservoirCompartmentEntity> target =
            built.Engine.Provided.Resolve<FieldControl>().AddCompartment(
                new GeneratedCompartment(
                    PoreVolume: new ReservoirVolume(100.0e6),
                    Porosity: 0.22,
                    OilSaturation: 0.7,
                    InitialPressure: new Pressure(30.0e6),
                    Temperature: Temperature.FromCelsius(93.3),
                    Depth: new Length(2000.0)),
                permeability: new Permeability(1.0e-13),
                netThickness: new Length(20.0),
                drainageArea: new Area(2.0e5),
                rockCompressibility: 4.5e-10,
                gasOilContact: new Length(1900.0),
                oilWaterContact: new Length(2100.0),
                Defaults.Wettability, Defaults.Drive,
                Defaults.AquiferStrength, Defaults.AquiferResponseTime);

        built.Engine.Provided.Resolve<WorldState>()
            .DeclareKnownField(target, new ReservoirVolume(100.0e6));

        return (built.Engine, target);
    }

    private static EntityId<IProspect> Structure(
        Engine engine, EntityId<IReservoirCompartmentEntity> field) =>
        engine.Provided.Resolve<WorldState>().ProspectFor(field);

    /// <summary>Keeps drilling until one lands — every hole is paid for either
    /// way, which is the point of the loop and not an inefficiency in the
    /// test (the same pattern <c>ActivityTests.Drilled</c> uses).</summary>
    private static void Drilled(Engine engine, EntityId<IReservoirCompartmentEntity> target)
    {
        while (engine.ReadModel is null || engine.ReadModel.Wells == 0)
        {
            if (engine.ReadModel?.ActivitiesRunning == 0)
                engine.Commands.Submit(
                    new DrillWellCommand(Structure(engine, target), new Length(2000.0)));

            engine.Pipeline.AdvanceTick();
        }
    }

    /// <summary>
    /// <b>A well that stands satisfies the commitment</b> — drilled well inside
    /// the month-60 deadline, the licence stays live past it, and no bond is
    /// ever forfeited.
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void A_well_drilled_before_the_deadline_keeps_the_licence_live()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        Drilled(engine, target);

        var licence = engine.Provided.Resolve<OGSim.Company.Licence>();

        for (var month = 0; month < 65; month++) engine.Pipeline.AdvanceTick();

        Assert.True(licence.IsLive, "a company that drilled a standing well keeps its licence");

        Assert.DoesNotContain(
            engine.Audit.Query(new AuditQuery(null, AuditCategory.Financial, null, null)),
            entry => entry.Data.TryGetValue("spend", out AuditValue spend)
                     && spend.Value == "licence-bond-forfeit");
    }

    /// <summary>
    /// <b>An unmet commitment forfeits the bond, once, and blocks further
    /// drilling</b> — the three consequences SDD-011 §1's R20d.9 amendment
    /// promises, all through the real tick loop rather than the unit-tested
    /// <c>Licence</c> class directly.
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void An_unmet_commitment_forfeits_the_bond_and_blocks_further_drilling()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        var licence = engine.Provided.Resolve<OGSim.Company.Licence>();
        OGSim.Company.CompanyState company = engine.Provided.Resolve<OGSim.Company.CompanyState>();

        // AN UNDRILLED FIELD ALSO OWES TAKE-OR-PAY (R13.3, finding 258): it
        // delivers nothing, so every window this fixture crosses is a full
        // shortfall against the shipped contract's committed volume — the
        // same formula `TakeOrPayContract.AssessAt` posts, read from the
        // shipped terms rather than a second hardcoded number.
        OGSim.Company.TakeOrPayTerms takeOrPay = Fixture.TakeOrPay();
        Money windowPenalty = Money.RoundHalfEven(
            takeOrPay.PenaltyRate.Cents * takeOrPay.CommittedVolume.CubicMetres);

        // Up to but not including the deadline tick — the commitment is still
        // outstanding and nothing has been forfeited yet.
        for (var month = 0; month < 59; month++) engine.Pipeline.AdvanceTick();

        Assert.True(licence.IsLive, "the deadline has not arrived yet");
        Money cashBeforeForfeit = company.Ledger.Cash;

        // The deadline tick itself: the commitment is unmet, so the bond
        // forfeits ON TOP OF the month's ordinary standing charge — and this
        // tick is ALSO a take-or-pay window boundary (12-month windows from
        // tick 0 land on every multiple of 12, and the deadline is tick 60).
        engine.Pipeline.AdvanceTick();

        Assert.False(licence.IsLive, "the commitment went unmet at its own deadline");

        Assert.Equal(
            cashBeforeForfeit.Cents - Defaults.LicenceTerms.Bond.Cents
                - Defaults.Economics.FixedOperatingCostPerTick.Cents
                - windowPenalty.Cents,
            company.Ledger.Cash.Cents);

        AuditEntry forfeit = Assert.Single(
            engine.Audit.Query(new AuditQuery(null, AuditCategory.Financial, null, null)),
            entry => entry.Data.TryGetValue("spend", out AuditValue spend)
                     && spend.Value == "licence-bond-forfeit");

        Assert.True(forfeit.Data.ContainsKey("unmet-count"));

        // NEVER TWICE (the repeated-forfeit bug this join found and fixed):
        // another sixty months costs nothing further FROM THE LICENCE — but
        // take-or-pay is its own clock, independent of the licence's, and
        // keeps assessing a still-undrilled field every window regardless of
        // whether the licence is even live. Five more windows close in the
        // next sixty months (ticks 72, 84, 96, 108, 120).
        Money afterFirstForfeit = company.Ledger.Cash;

        for (var month = 0; month < 60; month++) engine.Pipeline.AdvanceTick();

        Assert.Equal(
            0L,
            afterFirstForfeit.Cents - company.Ledger.Cash.Cents
                - (60L * Defaults.Economics.FixedOperatingCostPerTick.Cents)
                - (5L * windowPenalty.Cents));

        // AND FURTHER DRILLING REFUSES, naming the reason.
        Rejected refused = Assert.IsType<Rejected>(
            engine.Commands.Submit(
                new DrillWellCommand(Structure(engine, target), new Length(2000.0))));

        Assert.Contains(refused.Reasons, reason => reason.LocId == "$loc:reject.licence-lost");
    }

    /// <summary>
    /// <b>Every well as <c>IWell</c>, through a real engine</b> — the
    /// reachable status subset, the licence reference, and a resolvable
    /// wellbore whose contact length matches the completion's own
    /// perforation.
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void AsWells_reports_a_drilled_well_against_the_composed_licence()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        Drilled(engine, target);

        FieldControl field = engine.Provided.Resolve<FieldControl>();
        var licence = engine.Provided.Resolve<OGSim.Company.Licence>();

        IReadOnlyList<IWell> wells = field.AsWells(licence.Id);
        IWell well = Assert.Single(wells);

        Assert.Equal(WellStatus.Producing, well.Status);
        Assert.Equal(WellClassification.Development, well.Classification);
        Assert.Equal(licence.Id, well.Licence);

        EntityId<IWellbore> wellboreId = Assert.Single(well.Wellbores);
        IWellbore? wellbore = field.WellboreNamed(wellboreId);

        Assert.NotNull(wellbore);
        Assert.Equal(well.Id.Value, wellbore!.Well.Value);
        Assert.True(wellbore.ContactLengthIn(target).Metres > 0.0,
            "the wellbore must contact the compartment it was drilled into");
    }

    /// <summary>
    /// <b>The licence's own state survives a reload</b> — progress toward the
    /// commitment and whether it has been forfeited both change over the
    /// game's life and neither is recomputed from <c>Terms</c> alone.
    /// </summary>
    [Fact]
    [Trait("Speed", "Slow")]
    public void The_licence_stays_lost_after_a_reload()
    {
        (Engine engine, EntityId<IReservoirCompartmentEntity> target) = Undrilled();

        for (var month = 0; month < 61; month++) engine.Pipeline.AdvanceTick();

        var licence = engine.Provided.Resolve<OGSim.Company.Licence>();
        Assert.False(licence.IsLive);

        var container = new MemoryStream();
        SaveGame.Write(engine, Fixture.Settings().WorldSeed, container);
        container.Position = 0;

        Engine reloaded = Assert.IsType<Built>(
            SaveGame.Load(container, Fixture.Settings())).Engine;

        var reloadedLicence = reloaded.Provided.Resolve<OGSim.Company.Licence>();
        Assert.False(reloadedLicence.IsLive);

        // AND THE REFUSAL STAYS TOO — proving the RESTORED field, not a
        // freshly-composed one that happens to agree.
        reloaded.Pipeline.AdvanceTick();

        Rejected refused = Assert.IsType<Rejected>(
            reloaded.Commands.Submit(
                new DrillWellCommand(Structure(reloaded, target), new Length(2000.0))));

        Assert.Contains(refused.Reasons, reason => reason.LocId == "$loc:reject.licence-lost");
    }
}

/// <summary>
/// SDD-005 §4.2's R20d.10e amendment, proved against the ACTUAL composed
/// classes rather than reconstructed types (design 03 §6, R20d-V11).
///
/// <para><c>R17V2_technology_and_environment_apply_through_the_same_method</c>
/// (<c>GatingTests.cs</c>) already proves <c>EffectState.Apply</c> combines
/// technology and environment effects correctly at the unit level. What that
/// test cannot show is whether <c>CapabilitiesModule</c> actually WIRES a
/// diffused node's effects into the state it provides — <c>ActiveEffects()</c>
/// existed and nothing called it before this join (finding 240's amendment).
/// These tests call <c>DiffusionStage</c> itself, constructed exactly as
/// <c>CapabilitiesModule.Compose</c> constructs it.</para>
///
/// <para>Built from a synthetic one-node graph rather than the shipped
/// registry: <c>TechnologyContentKind.Read</c> hardcodes <c>Effects</c> to
/// <c>[]</c> for every loaded node (no SDD ever specified a JSON grammar for
/// the field, and none of the sixty-five shipped nodes needs one — SDD-005's
/// own R20c.9 correction says a node carries an effect only where it changes
/// "a number nobody bought", which is true of at most one shipped node,
/// Arctic operations, and that node's other half — an environment-side
/// restriction and a consumer that checks the envelope — does not exist yet
/// either). Recorded as finding 241 rather than fixed here: building the JSON
/// grammar today would have no real consumer, which is the same defect from
/// the other side (CLAUDE.md rule 6). A synthetic node exercises the STAGE,
/// which is what this join is actually about.</para>
/// </summary>
public sealed class DiffusionStageTests
{
    private static readonly GameDate Epoch = new(1965, 1);

    private static (OGSim.Capabilities.CapabilityState Capabilities, OGSim.Capabilities.EffectState Effects, DiffusionStage Stage)
        Build(OGSim.Capabilities.TechnologyNode node)
    {
        var capabilities = new OGSim.Capabilities.CapabilityState(
            [node], Defaults.Eras, () => Epoch, Epoch);
        var effects = new OGSim.Capabilities.EffectState(new Dictionary<EnvelopeKind, double>());
        var stage = new DiffusionStage(capabilities, effects);

        return (capabilities, effects, stage);
    }

    /// <summary>
    /// A node that diffuses this tick moves an envelope that had no other
    /// contribution — the case §4.2 exists to describe: technology reaching a
    /// model through the one path environment also uses.
    /// </summary>
    [Fact]
    public void R20dV11_a_diffused_nodes_envelope_extension_reaches_the_effect_state()
    {
        var node = new OGSim.Capabilities.TechnologyNode(
            new TechnologyId(new ContentId("test-arctic-kit")),
            Era.E1,
            DiffusionLagTicks: 0,
            Prerequisites: [],
            Effects: [new MoveEnvelope(EnvelopeKind.ArcticOperability, EnvelopeContributionKind.Extension, 8.0)],
            GrantsDetectClass: null,
            Routes: [OGSim.Capabilities.AcquisitionRoute.Diffusion]);

        (_, OGSim.Capabilities.EffectState effects, DiffusionStage stage) = Build(node);

        Assert.Equal(0.0, effects.EffectiveEnvelope(EnvelopeKind.ArcticOperability));

        stage.Execute(new TickContext { Tick = new Tick(0), Date = Epoch });

        Assert.Equal(8.0, effects.EffectiveEnvelope(EnvelopeKind.ArcticOperability));
    }

    /// <summary>
    /// A node whose lag has not elapsed diffuses nothing, and the stage must
    /// not apply an effect that was never granted — the "next tick" property
    /// §4.2 asks for starts here, at "not yet acquired at all".
    /// </summary>
    [Fact]
    public void R20dV11b_a_not_yet_diffused_nodes_effect_does_not_reach_the_effect_state()
    {
        var node = new OGSim.Capabilities.TechnologyNode(
            new TechnologyId(new ContentId("test-future-kit")),
            Era.E1,
            DiffusionLagTicks: 600,
            Prerequisites: [],
            Effects: [new MoveEnvelope(EnvelopeKind.ArcticOperability, EnvelopeContributionKind.Extension, 8.0)],
            GrantsDetectClass: null,
            Routes: [OGSim.Capabilities.AcquisitionRoute.Diffusion]);

        (_, OGSim.Capabilities.EffectState effects, DiffusionStage stage) = Build(node);

        stage.Execute(new TickContext { Tick = new Tick(0), Date = Epoch });

        Assert.Equal(0.0, effects.EffectiveEnvelope(EnvelopeKind.ArcticOperability));
    }

    /// <summary>
    /// Recomputing every tick is equivalent to applying once, on the idempotency
    /// proof SDD-005's R20d.10e amendment relies on: calling <c>Execute</c>
    /// again after the node is already held must not double the extension.
    /// </summary>
    [Fact]
    public void R20dV11c_recomputing_every_tick_does_not_double_an_already_held_effect()
    {
        var node = new OGSim.Capabilities.TechnologyNode(
            new TechnologyId(new ContentId("test-arctic-kit-2")),
            Era.E1,
            DiffusionLagTicks: 0,
            Prerequisites: [],
            Effects: [new MoveEnvelope(EnvelopeKind.ArcticOperability, EnvelopeContributionKind.Extension, 8.0)],
            GrantsDetectClass: null,
            Routes: [OGSim.Capabilities.AcquisitionRoute.Diffusion]);

        (_, OGSim.Capabilities.EffectState effects, DiffusionStage stage) = Build(node);

        for (var tick = 0; tick < 5; tick++)
            stage.Execute(new TickContext { Tick = new Tick(tick), Date = Epoch });

        Assert.Equal(8.0, effects.EffectiveEnvelope(EnvelopeKind.ArcticOperability));
    }
}

/// <summary>
/// SDD-005 §5's R12b.19 amendment: below its tier, a survey source sees
/// nothing — checked against the actual composition-internal classes
/// (<c>WorldState</c>, <c>RegionalObservationModel</c>), not a reimplemented
/// double, exactly as <c>DiffusionStageTests</c> already does for R20d-V11.
/// </summary>
public sealed class DetectClassGatingTests
{
    private static (WorldState World, RegionalObservationModel Model) Build() =>
        Build(out _);

    private static (WorldState World, RegionalObservationModel Model) Build(
        out EntityId<IProspect> prospect, DetectClass subtlety = DetectClass.D1)
    {
        var world = new WorldState();
        prospect = world.Place(default, new ReservoirVolume(1.0e6), subtlety);

        return (world, new RegionalObservationModel(world));
    }

    /// <summary>2-D seismic's ceiling is D0 (design 06 §2.3); a D1 trap is
    /// below it, and the rule is "nothing", not a wide sigma.</summary>
    [Fact]
    public void R12bV19_a_below_tier_survey_sees_nothing()
    {
        (_, RegionalObservationModel model) = Build(out EntityId<IProspect> prospect, DetectClass.D1);

        double? sigma = model.SigmaFor(
            new ContentId("seismic-2d"), new ContentId("structure-capacity"),
            new EntityRef(EntityKind.Prospect, prospect.Value));

        Assert.Null(sigma);
    }

    /// <summary>3-D seismic's ceiling is D1 (design 06 §2.3); at the tier
    /// exactly, the survey sees the structure — this is the case that
    /// distinguishes "gated" from "always null".</summary>
    [Fact]
    public void R12bV19b_a_survey_at_its_own_tier_sees_the_structure()
    {
        (_, RegionalObservationModel model) = Build(out EntityId<IProspect> prospect, DetectClass.D1);

        double? sigma = model.SigmaFor(
            new ContentId("seismic-3d"), new ContentId("structure-capacity"),
            new EntityRef(EntityKind.Prospect, prospect.Value));

        Assert.NotNull(sigma);
    }

    /// <summary>D0's own tier is the obvious-closure case (design 06 §2.3) —
    /// regional data, the free read every game gets, sees it.</summary>
    [Fact]
    public void R12bV19c_an_obvious_trap_is_seen_even_by_regional_data()
    {
        (_, RegionalObservationModel model) = Build(out EntityId<IProspect> prospect, DetectClass.D0);

        double? sigma = model.SigmaFor(
            new ContentId("regional"), new ContentId("oil-in-place"),
            new EntityRef(EntityKind.Prospect, prospect.Value));

        Assert.NotNull(sigma);
    }

    /// <summary>
    /// AND SUBTLETY NEVER GATES A DISCOVERED COMPARTMENT — a well test, a log,
    /// a core and a discovery well always ask about a COMPARTMENT, and
    /// `Subtlety` is a property of the trap's own geometry, not of what turns
    /// out to be inside it (SDD-005 §5's R12b.19 amendment). A subject that is
    /// not a prospect must reach the ordinary (source, kind) table untouched.
    /// </summary>
    [Fact]
    public void R12bV19d_a_compartment_subject_is_never_gated_by_subtlety()
    {
        (WorldState world, RegionalObservationModel model) = Build(out _, DetectClass.D3);

        double? sigma = model.SigmaFor(
            new ContentId("well-test"), new ContentId("reservoir-pressure"),
            new EntityRef(EntityKind.Compartment, 1UL));

        Assert.NotNull(sigma);
    }
}

/// <summary>
/// SDD-005 §4.2's R22.2 amendment: the environment applies through the SAME
/// path technology does, proved against the actual composed classes
/// (<c>WeatherStage</c>, <c>EnvironmentModule</c>) exactly as
/// <c>DiffusionStageTests</c> already does for R20d-V11 on the technology
/// side. Built from a synthetic climate rather than the shipped one:
/// `Defaults.Climate.Effects` is empty, and correctly so — no shipped climate
/// changes a number nobody bought, the identical relationship
/// <c>TechnologyContentKind</c>'s hardcoded `[]` has to the sixty-five
/// shipped technology nodes.
/// </summary>
public sealed class WeatherEffectTests
{
    private static (OGSim.Capabilities.EffectState Effects, WeatherStage Stage)
        Build(OGSim.Environment.ClimateProfile climate, ulong seed)
    {
        var weather = new OGSim.Environment.WeatherState([climate]);
        var model = new OGSim.Environment.Ar1Weather(climate.Persistence);
        IRandomStream stream = new RandomSource(seed).Stream(StreamId.Weather);
        var effects = new OGSim.Capabilities.EffectState(new Dictionary<EnvelopeKind, double>());

        return (effects, new WeatherStage(weather, model, stream, effects, climate));
    }

    private static OGSim.Environment.ClimateProfile ClimateWith(Effect effect) => new(
        new ContentId("test-climate"),
        Persistence: 0.75,
        Baseline: [.. Enumerable.Repeat(3.0, 12)],
        Amplitude: [.. Enumerable.Repeat(1.0, 12)],
        TemperatureBaseline: [.. Enumerable.Repeat(-15.0, 12)],
        TemperatureAmplitude: -4.0,
        AccessOpen: [.. Enumerable.Repeat(true, 12)],
        Effects: [effect]);

    /// <summary>
    /// The stage moves the envelope. Every composed `EffectState` starts with
    /// an empty base (0.0 for every kind — `CapabilitiesModule.Compose`'s own
    /// choice, matched here), so an EXTENSION is the effect kind whose result
    /// is directly observable against it: `Max(0, extension)` is the
    /// extension itself. A RESTRICTION against a zero base would leave
    /// `EffectiveEnvelope` at zero whether or not it were ever applied —
    /// which is why the restriction case is proved separately, in
    /// combination, in the next test.
    /// </summary>
    [Fact]
    public void R22V18_the_environment_applies_through_the_same_effect_state_as_technology()
    {
        (OGSim.Capabilities.EffectState effects, WeatherStage stage) = Build(
            ClimateWith(new MoveEnvelope(
                EnvelopeKind.ArcticOperability, EnvelopeContributionKind.Extension, 12.0)),
            seed: 11UL);

        Assert.Equal(0.0, effects.EffectiveEnvelope(EnvelopeKind.ArcticOperability));

        stage.Execute(new TickContext { Tick = new Tick(0), Date = new GameDate(1965, 1) });

        Assert.Equal(12.0, effects.EffectiveEnvelope(EnvelopeKind.ArcticOperability));
    }

    /// <summary>
    /// AND A RESTRICTION COMBINES WITH AN EXTENSION THE NORMAL WAY — the
    /// whole point of one shared path: an extension a HELD TECHNOLOGY would
    /// contribute, and this climate's OWN restriction capping it, resolve to
    /// `Min(Max(base, extension), restriction)`, exactly SDD-005 §4.1's form
    /// and its rule that restrictions always win. This is what proves the
    /// restriction reaches `EffectState` at all — the case a lone restriction
    /// against a zero base cannot show, since `Min(0, restriction)` is zero
    /// whether or not the restriction was ever applied.
    /// </summary>
    [Fact]
    public void R22V18b_the_climates_restriction_combines_with_an_extension_the_normal_way()
    {
        (OGSim.Capabilities.EffectState effects, WeatherStage stage) = Build(
            ClimateWith(new MoveEnvelope(
                EnvelopeKind.ArcticOperability, EnvelopeContributionKind.Restriction, 4.0)),
            seed: 12UL);

        stage.Execute(new TickContext { Tick = new Tick(0), Date = new GameDate(1965, 1) });

        // Simulating what a held technology would have contributed, through
        // the SAME method — SDD-005 §4.2's whole claim.
        effects.Apply([new MoveEnvelope(
            EnvelopeKind.ArcticOperability, EnvelopeContributionKind.Extension, 12.0)]);

        // Min(Max(0, 12), 4) = 4 — the climate's restriction wins.
        Assert.Equal(4.0, effects.EffectiveEnvelope(EnvelopeKind.ArcticOperability));
    }
}
