// Oilfield Engineer — the realistic build of the production and workflow cycle
// (plans 23 §4's 2026-08-23 amendment).
//
// THESE ARE THE TESTS THAT SAY THE SECOND PRODUCT EXISTS AND WORKS. Not that the
// mode record has the right fields — GM1-GM5 in the composition suite do that —
// but that a client composed at `engineer` walks the whole cycle and comes out
// the far side with oil sold and a company still solvent.

using OGSim.Composition;
using OGSim.Contracts;
using OGSim.Engineer;
using OGSim.Kernel;

namespace OGSim.Engineer.Tests;

public sealed class WorkflowCycleTests
{
    /// <summary>Ten years, which is the arc the shipped objective is written over.</summary>
    private const int Decade = 120;

    /// <summary>
    /// THE HEADLINE: the operator's cycle finds oil, produces it, sells it, and
    /// ends richer than it started.
    /// </summary>
    /// <remarks>
    /// <para>One test that fails for a hundred reasons is normally a bad test.
    /// This one is deliberate: it is the claim that there IS a second product,
    /// and every part of the cycle has to work for it to pass. When it breaks,
    /// the phase log in the failure message says which part.</para>
    /// </remarks>
    [Fact]
    public void EN1_the_operators_cycle_runs_a_decade_and_turns_a_profit()
    {
        CycleRun run = Run(Decade);

        Assert.False(run.Insolvent, Phases(run));
        Assert.True(run.Wells > 0, "a decade of operating should have drilled something: " + Phases(run));
        Assert.True(run.Lifetime.CubicMetres > 0.0, "and sold something: " + Phases(run));
        Assert.True(run.Closing > Money.FromMillions(50.0),
                    "and finished ahead of the opening balance: " + Phases(run));
    }

    /// <summary>
    /// AND IT IS A CYCLE, not a sequence. Every phase an operating company has
    /// appears in a decade: ground is looked at, prospects are sharpened, holes
    /// are drilled, the plant is widened where it refuses, and what breaks is
    /// fixed.
    /// </summary>
    [Fact]
    public void EN2_a_decade_walks_every_phase_an_operating_company_has()
    {
        CycleRun run = Run(Decade);

        Phase[] operating =
        [
            Phase.Produce, Phase.Explore, Phase.Appraise,
            Phase.Drill, Phase.Debottleneck, Phase.Maintain,
        ];

        foreach (Phase phase in operating)
            Assert.Contains(run.Months, month => month.Phase == phase);

        // AND NOT THE ONE IT DOES NOT HAVE. An operating company opens holding
        // a train and never commissions a first one — asserted rather than
        // merely absent from the list above, because it is a claim about the
        // product and not an omission in the test.
        Assert.DoesNotContain(run.Months, month => month.Phase == Phase.Build);
    }

    /// <summary>
    /// THE SAME CLIENT, COMPOSED AT THE OTHER PRODUCT, IS A DIFFERENT GAME.
    /// </summary>
    /// <remarks>
    /// <para>Nothing in <see cref="WorkflowCycle"/> knows which mode it is in —
    /// it holds no branch on one — so any difference between these two runs came
    /// out of the engine, which is exactly what design 03 §3.2 asks for.</para>
    ///
    /// <para>What differs: Oilfield Days opens on bare ground, so its chain is
    /// empty and there is nothing to maintain or widen until it has built one.
    /// Oilfield Engineer opens holding a train.</para>
    /// </remarks>
    [Fact]
    public void EN3_the_same_cycle_at_the_other_mode_opens_on_bare_ground()
    {
        CycleRun engineer = Run(1, GameStyles.Engineer);
        CycleRun days = Run(1, GameStyles.Days);

        Assert.NotEmpty(Chain(GameStyles.Engineer));
        Assert.Empty(Chain(GameStyles.Days));

        // Both still RAN — one engine, two compositions, neither refused.
        Assert.Single(engineer.Months);
        Assert.Single(days.Months);
    }

    /// <summary>
    /// AND ON BARE GROUND THE CYCLE BUILDS ITS OWN PLANT.
    /// </summary>
    /// <remarks>
    /// <para>The Build phase exists only for a starting state that opens with
    /// nothing, so this is the only place it can be proved — and it is the whole
    /// of what makes Oilfield Days a different game rather than an easier one.
    /// The same <see cref="WorkflowCycle"/>, holding no branch on the mode.</para>
    ///
    /// <para><b>The field is DECLARED and the well is already down</b>
    /// (SDD-010 §4b), the same way the composition suite's own chain fixtures
    /// set one up. Left to explore for itself this run drills three dry holes
    /// at 17-19% and is insolvent by month 26 — at any richness, because
    /// richness moves volumes and not the chance of a closure, and since S1 a
    /// prospect is not even on the board until its block has been shot. That is
    /// the balance finding recorded in plans 22 §8 (a discovery costs about six
    /// holes; six holes plus a plant costs more than the company opens with),
    /// and it is a scenario problem rather than a defect in the cycle. What is
    /// under test here is what a company does once it HAS something.</para>
    /// </remarks>
    [Fact]
    public void EN4_on_bare_ground_the_cycle_commissions_its_own_plant()
    {
        Engine engine = Compose(GameStyles.Days);

        // A WELL ALREADY DOWN. The cycle's own exploration cannot reach one
        // here — see the remarks — and this test is about what happens NEXT.
        engine.Provided.Resolve<FieldControl>().Drill(Declare(engine), new Length(2000.0));

        CycleRun run = new WorkflowCycle(engine, drillAbove: 0.25).Run(Decade);

        Assert.True(run.Months.Any(month => month.Phase == Phase.Build), Phases(run));

        // AND THE TRAIN IS ACTUALLY THERE at the end of it. The phase says the
        // order was accepted; this says the plant got built — a commissioning
        // can fail, and the company is left on bare ground when it does.
        Assert.Contains(engine.ReadModel!.Chain, element => element.DisplayId == "manifold");
    }

    /// <summary>
    /// A reservoir the company already knows is there (SDD-010 §4b).
    /// </summary>
    /// <remarks>
    /// Placed and found in one step, carrying no exploration risk because there
    /// is nothing left to be wrong about — which is what lets a test about
    /// BUILDING be about building.
    /// </remarks>
    internal static EntityId<IReservoirCompartmentEntity> DeclareFor(Engine engine) => Declare(engine);

    private static EntityId<IReservoirCompartmentEntity> Declare(Engine engine)
    {
        EntityId<IReservoirCompartmentEntity> field =
            engine.Provided.Resolve<FieldControl>().AddCompartment(
                new GeneratedCompartment(
                    PoreVolume: new ReservoirVolume(100.0e6),
                    Porosity: 0.22,
                    OilSaturation: 0.7,
                    InitialPressure: new Pressure(30.0e6),
                    Temperature: Temperature.FromCelsius(93.3),
                    Depth: new Length(2000.0),
                    FluidSystem: new ContentId("medium-crude")),
                permeability: new Permeability(1.0e-13),
                netThickness: new Length(20.0),
                drainageArea: new Area(2.0e5),
                rockCompressibility: 4.5e-10,
                gasOilContact: new Length(1900.0),
                oilWaterContact: new Length(2100.0),
                // THE SHIPPED VALUES, RESTATED. `Defaults` is internal to
                // composition and this project is a CLIENT — it gets the
                // published surface and nothing else, which is the constraint
                // that makes it worth having. A fixture that had to reach past
                // it would be saying the surface is short of something.
                wettability: RelativePermeabilityCurve.Validated(
                    swc: 0.30, sor: 0.25, krwMax: 0.35, kroMax: 0.90, nw: 3.0, no: 2.0),
                drive: new ContentId("water-drive"),
                aquiferStrength: 4.0,
                aquiferResponseTime: Duration.FromTicks(40.0 * 12.0));

        engine.Provided.Resolve<WorldState>()
              .DeclareKnownField(field, new ReservoirVolume(100.0e6));

        return field;
    }

    /// <summary>The elements a company holds on its first morning.</summary>
    private static IReadOnlyList<ChainElementView> Chain(IGameStyle mode)
    {
        Engine engine = Compose(mode);

        engine.Pipeline.AdvanceTick();

        return engine.ReadModel!.Chain;
    }

    private static CycleRun Run(int months, IGameStyle? mode = null) =>
        new WorkflowCycle(Compose(mode ?? GameStyles.Engineer), drillAbove: 0.25).Run(months);

    /// <summary>
    /// The engine the shipped client composes, at whichever mode is asked for.
    /// </summary>
    /// <remarks>
    /// Through <see cref="EngineBuilder.CreateNew"/> and the repository's own
    /// content, because a test that composed some other way would be testing
    /// some other thing than the product.
    /// </remarks>
    internal static Engine ComposeFor(IGameStyle mode) => Compose(mode);

    private static Engine Compose(IGameStyle mode)
    {
        var settings = mode.Compose(new EngineSettings(
            Epoch: new GameDate(1965, 1),
            WorldSeed: 20260806UL,
            Retention: new AuditRetention(DetailWindowTicks: 12),
            LogSink: new SilentSink(),
            MinimumLogLevel: LogLevel.Error,
            FaultHandling: FaultHandling.Resilient,
            RealityProfile: mode.RealityProfile,
            Content: [RepositoryContent.Read()],
            StartingState: mode.StartingState,
            Rules: mode.Rules,
            Style: mode.Id));

        var world = new WorldParameters(
            new ContentId("world-template-basin"),
            WidthCells: 32,
            HeightCells: 32,
            LandFraction: 0.72,
            ResourceRichness: 1.0,
            BasinMaturity: 0.5,
            ClimateSeverity: 0.28,
            RivalCount: 4,
            StartEra: Era.E1);

        return Assert.IsType<Built>(EngineBuilder.CreateNew(settings, world)).Engine;
    }

    /// <summary>What the run actually did, for a failure that has to explain itself.</summary>
    private static string Phases(CycleRun run)
    {
        var counted = new Dictionary<Phase, int>();

        for (var i = 0; i < run.Months.Count; i++)
            counted[run.Months[i].Phase] = counted.GetValueOrDefault(run.Months[i].Phase) + 1;

        var said = new List<string>();

        foreach ((Phase phase, int months) in counted.OrderBy(p => p.Key))
            said.Add($"{phase} x{months}");

        return $"{run.Months.Count} months [{string.Join(", ", said)}], " +
               $"closed {run.Closing.Cents / 100.0 / 1.0e6:F1}M with {run.Wells} wells";
    }

    private sealed class SilentSink : ILogSink
    {
        public void Emit(LogRecord record)
        {
        }
    }
}
