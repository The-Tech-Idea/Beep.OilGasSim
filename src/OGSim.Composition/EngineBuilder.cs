// Composition — building the engine (design 03 §3.1, §8).
//
// COMPOSITION IS ALL-OR-NOTHING. ModuleComposer validates the whole set before
// anything is constructed: every Requires met, no contract provided twice, no
// state key owned twice, no dependency cycle, no two modules in one stage slot,
// every declared slot filled. Either the engine builds, or it refuses naming
// EVERY problem.
//
// There is no partially-composed engine and no degraded mode, because an engine
// missing a module is an engine whose failure surfaces fifty ticks later as an
// inexplicable number rather than at startup as a sentence.
//
// This file constructs concrete types on purpose: design 03 §8 makes Layer 4
// "the ONLY project naming concrete types", and somebody has to know what
// implements what. Confining that knowledge here is what keeps every other
// assembly depending on contracts alone.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition;

/// <summary>
/// The parameterisation the shipped modules are built with.
///
/// <para>These are values, not fallbacks: law L2 forbids a defaulted
/// dependency, and every one of them is passed explicitly at the one place
/// entitled to name a concrete type. Content replaces them wholesale once the
/// pipeline populates modules (R3 §5).</para>
/// </summary>
internal static class Defaults
{
    public static BlackOilInputs Fluid { get; } = new(
        OilGravity: new ApiGravity(35.0),
        GasSpecificGravity: 0.75,
        ReservoirTemperature: Temperature.FromCelsius(93.3),
        SolutionGorAtBubblePoint: 100.0,
        Form: FluidForm.BlackOil);

    public static ValidityRange Validity { get; } = new(
        new Pressure(500.0), new Pressure(60e6),
        Temperature.FromCelsius(10.0), Temperature.FromCelsius(180.0));

    public static Wells.InflowConditions Inflow { get; } = new(
        new Permeability(1.0e-13), new Length(20.0), new Area(2.0e5),
        new Length(0.108), new Viscosity(2.0e-3), new Pressure(10.0e6));

    public static Wells.TubingGeometry Tubing { get; } = new(
        new Length(2000.0), new Length(2000.0), new Length(0.0889), 4.6e-5);

    /// <summary>
    /// SDD-003 §3.1's per-tick step limit. A reservoir that lost more than a
    /// fifth of its pressure in one month is not being modelled, it is being
    /// extrapolated — the solve refuses rather than reporting a number nobody
    /// should trust.
    /// </summary>
    public const double MaxTickPressureDropFraction = 0.2;

    /// <summary>Opening cash — what a new company starts the game with.</summary>
    public static Money OpeningCash { get; } = Money.FromMillions(50.0);

    /// <summary>
    /// Starting prices and costs. Balance content in a finished game (R20.4);
    /// stated here so the loop is playable and revisable rather than absent.
    ///
    /// <para>$377/m³ is ~$60/bbl at 6.29 bbl/m³. The fixed cost is $300k a month:
    /// a small onshore field's standing charge — people, power, chemicals, the
    /// road. It was $2M on the first pass, which is a multi-platform figure, and
    /// it made a single-well field lose money every month it produced. That is a
    /// real dynamic and it will be some fields' story, but as the SHIPPED
    /// starting point it says the game is unwinnable, which is a different claim
    /// from the one intended.</para>
    /// </summary>
    public static FieldEconomics Economics { get; } = new(
        OilPricePerCubicMetre: Money.FromMillions(377.0 / 1_000_000.0),
        FixedOperatingCostPerTick: Money.FromMillions(0.3));

    /// <summary>
    /// What a well costs and how deep the company can currently drill. $8M is a
    /// land well; the 4,000 m envelope is what rotary drilling opens before any
    /// technology is acquired, so drilling deeper is a thing the player has to
    /// go and earn (TECH_TREE: deep-drilling, E2).
    /// </summary>
    /// <summary>
    /// How many materials this composition's catalogue carries. One — oil —
    /// until R20c.9 loads the nine of `content/materials/`. Stated once because
    /// three places must agree on it: the completion's stream width, an
    /// operation's mass report, and any zero composition either of them builds.
    /// </summary>
    public const int MaterialCount = 1;

    /// <summary>
    /// The company's one rig. **One**, deliberately: a rig drills a single well
    /// at a time, so a company that wants two wells at once needs a second rig,
    /// and that is a decision rather than an accounting entry. The bespoke timer
    /// this replaced had no rig at all, which made cash the only limit on how
    /// fast a field could be developed.
    /// </summary>
    public static EntityId<IRig> TheRig { get; } = new(1);

    /// <summary>
    /// SDD-007 §4's outcome table for a development well, as content would carry
    /// it. Probabilities sum to 1.0 (load-checked).
    ///
    /// <para>Success is the 0.60 the bespoke path drew directly — but it is now
    /// the sum of three grades rather than a single number, so a well can come
    /// in late or over budget instead of only being dry. `DisasterDay` on the
    /// disaster row is the day a blowout would occur; R18 consumes it, and until
    /// then a disaster is simply the worst kind of dry hole.</para>
    /// </summary>
    public static OutcomeTable DrillingOutcomes { get; } = new(
    [
        new OutcomeRow(OutcomeGrade.OnTime, Probability: 0.40,
                       DurationFactor: 1.00, CostFactor: 1.00, DisasterDay: null),
        new OutcomeRow(OutcomeGrade.Delayed, Probability: 0.14,
                       DurationFactor: 1.50, CostFactor: 1.15, DisasterDay: null),
        new OutcomeRow(OutcomeGrade.OverBudget, Probability: 0.06,
                       DurationFactor: 1.10, CostFactor: 1.60, DisasterDay: null),
        new OutcomeRow(OutcomeGrade.Failure, Probability: 0.38,
                       DurationFactor: 0.80, CostFactor: 0.90, DisasterDay: null),
        new OutcomeRow(OutcomeGrade.Disaster, Probability: 0.02,
                       DurationFactor: 1.80, CostFactor: 2.50, DisasterDay: 45),
    ]);

    /// <summary>
    /// What a well costs, how deep the company can reach, and how it can go
    /// wrong. Four months on a land well — long enough that money leaves well
    /// before oil arrives, which is what makes timing a decision rather than an
    /// afterthought.
    ///
    /// <para>Declared AFTER <see cref="TheRig"/> and
    /// <see cref="DrillingOutcomes"/>: static initialisers run in declaration
    /// order, and reading them from above would have taken a null table and a
    /// default rig id. The compiler said so; the same trap is noted on
    /// `EngineCorpus.Subsurface` for the same reason.</para>
    /// </summary>
    public static DrillingTerms Drilling { get; } = new(
        CostPerWell: Money.FromMillions(8.0),
        MaximumDepth: new Length(4000.0),
        DurationTicks: 4,
        Template: new ContentId("drill-development-well"),
        Rig: TheRig,
        Outcomes: DrillingOutcomes);

    /// <summary>
    /// The well a drilling command produces. One completion on one compartment,
    /// naturally flowing, wide open — the E1 well, and the only one the current
    /// content can describe.
    /// </summary>
    public static Wells.Completion CompletionFor(
        ulong id, EntityId<IReservoirCompartmentEntity> compartment, Length totalDepth)
    {
        var tubing = new Wells.TubingGeometry(
            totalDepth, totalDepth, new Length(0.0889), 4.6e-5);

        return new Wells.Completion(
            new EntityId<ICompletion>(id),
            new EntityId<IWellbore>(id),
            [new Perforation(compartment, totalDepth, totalDepth + new Length(30.0),
                             Skin: 0.0, Isolated: false)],
            new Wells.CompositeInflowModel(Inflow),
            new Wells.HydrostaticFrictionOutflowModel(
                tubing, Density.FromSpecificGravity(0.85), lift: null),
            new Wells.CompletionFluid(
                Density.FromSpecificGravity(0.85),
                new FormationVolumeFactor(1.2),
                Allocation.Validated(
                    [(new EntityRef(EntityKind.Compartment, compartment.Value), 1.0)]),
                new Pressure(30.0e6),
                ReservoirTemperature),
            Wells.ChokeSetting.Open,
            materialOrdinal: 0,
            materialCount: 1,
            lift: null);
    }

    /// <summary>
    /// What this scenario asks for: double the opening cash inside ten years.
    /// Reachable with a few good wells and out of reach if the early holes are
    /// dry — which is what makes the first drilling decision matter.
    /// </summary>
    public static ScenarioGoal Goal { get; } = new(Money.FromMillions(100.0), new Tick(120));

    public static Temperature ReservoirTemperature { get; } = Temperature.FromCelsius(93.3);

    /// <summary>Separator inlet pressure the wells flow against.</summary>
    public static Pressure WellheadBackpressure { get; } = Pressure.FromBar(15.0);

    public static Integrity.DegradationCoefficients Decay { get; } =
        new(BaseRatePerYear: 0.05, WaterCutFactor: 1.0, SourFactor: 2.0,
            DutyFactor: 0.5, TemperatureFactor: 1.5, ServiceIntervalFactor: 0.2);
}

/// <summary>
/// SDD-008 §3's slot, at its shipped setting. Regional data is deliberately
/// coarse: a player who could book reserves off a gravity and magnetics pass
/// would never buy seismic (R15-V10).
/// </summary>
internal sealed class RegionalObservationModel : IObservationModel
{
    public ContentId Id { get; } = new("regional-observation");

    public double? SigmaFor(ContentId source, ContentId propertyKind, EntityRef subject) =>
        source.Value switch
        {
            "regional" => 1.2,
            "seismic-2d" => 0.6,
            "seismic-3d" => 0.35,
            "well-log" => 0.12,
            "core" => 0.04,

            // NULL, not a wide sigma: a source that cannot see a kind sees
            // NOTHING, and the difference is what makes a subtle trap invisible
            // rather than merely uncertain (SDD-008 §3).
            _ => null,
        };
}

/// <summary>
/// Which fault policy the engine runs under — the one composition-time choice
/// the host must make and cannot be defaulted (law L2).
/// </summary>
public enum FaultHandling
{
    /// <summary>Every fault halts. CI and scenario runs, where a fault is a
    /// test failure and continuing would hide it.</summary>
    Strict,

    /// <summary>Model faults abandon the tick; invariant faults still halt.
    /// Shipping games, where one bad month is not a reason to end the save.</summary>
    Resilient,
}

/// <summary>What the host must supply. No member has a default.</summary>
public sealed record EngineSettings(
    GameDate Epoch,
    ulong WorldSeed,
    AuditRetention Retention,
    ILogSink LogSink,
    LogLevel MinimumLogLevel,
    FaultHandling FaultHandling);

/// <summary>
/// A composed engine: the validated module set, the tick it runs, and the two
/// things a player does — issue a command and read what happened.
/// </summary>
public sealed record Engine(
    IReadOnlyList<IModule> Modules,
    TickPipeline Pipeline,
    IAuditTrail Audit,
    EventBus Events,
    StateRegistry State,
    IResolvedContracts Provided,
    ICommandBus Commands)
{
    /// <summary>
    /// The tick just closed, as the player sees it — null before the first tick,
    /// because a game that has not started has nothing to show and a zeroed
    /// model would be a lie about a month that never happened.
    /// </summary>
    public FieldReadModel? ReadModel => Provided.Resolve<CloseStage>().Published;


    // Finding 131.
    public bool Equals(Engine? other) =>
        other is not null && ReferenceEquals(Pipeline, other.Pipeline)
        && ReferenceEquals(Audit, other.Audit) && ReferenceEquals(Events, other.Events)
        && ReferenceEquals(State, other.State) && ReferenceEquals(Provided, other.Provided)
        && ReferenceEquals(Commands, other.Commands)
        && Structural.Equal(Modules, other.Modules);

    public override int GetHashCode() =>
        HashCode.Combine(Pipeline, Audit, Events, State, Provided, Commands,
            Structural.HashOf(Modules));
}

/// <summary>
/// The build outcome. <c>Built</c> is composition's own — it carries an
/// <see cref="Engine"/>, which is a Layer 4 type the contract layer cannot name
/// — but a refusal is reported with the CONTRACT's
/// <see cref="EngineCompositionRefused"/> rather than a second record saying the
/// same thing. Two names for one concept is what glossary rule N1 forbids, and
/// a host that had to translate between them would be doing so at exactly the
/// moment it is trying to print why the engine would not start.
/// </summary>
public abstract record BuildResult;

public sealed record Built(Engine Engine) : BuildResult;

/// <summary>Every problem, never just the first (R1 §2.9).</summary>
public sealed record BuildRefused(EngineCompositionRefused Refusal) : BuildResult
{
    public IReadOnlyList<CompositionProblem> Problems => Refusal.Problems;
}

/// <summary>Design 03 §8's Layer 4 — the one place that knows what implements what.</summary>
public static class EngineBuilder
{
    /// <summary>
    /// The fourteen shipped modules, in declaration order.
    ///
    /// <para>Declaration order determines NOTHING. Construction order comes from
    /// the dependency graph and execution order from the stage numbering, both
    /// derived by the composer — so listing them here in any order yields the
    /// same engine, which is the property that makes Requires worth declaring
    /// (SDD-001 §9, finding 126).</para>
    /// </summary>
    internal static IReadOnlyList<IModule> ShippedModules(
        IAuditTrail audit, SimulationClock clock, IRandomSource random) =>
    [
        new SubsurfaceModule(),
        new WellsModule(),
        new FlowModule(),
        new FacilitiesModule(),
        new OperationsModule(),
        new CompanyModule(),
        new InformationModule(),
        new WorldModule(),
        new CapabilitiesModule(),
        new IntegrityModule(),
        new HseModule(),
        new ObjectivesModule(),
        new MaterialsModule(),
        new FieldModule(),
        new DiagnosticsModule(audit, clock, random),
    ];

    /// <summary>Composes the shipped set.</summary>
    public static BuildResult Build(EngineSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var clock = new SimulationClock(settings.Epoch);
        var audit = new AuditTrail(clock, settings.Retention);

        return Build(
            settings, ShippedModules(audit, clock, new RandomSource(settings.WorldSeed)),
            clock, audit);
    }

    /// <summary>
    /// Composes a DECLARED set — the door a scenario uses to compose a variant,
    /// and the door tests use to compose an incomplete set and read the refusal.
    /// </summary>
    public static BuildResult Build(EngineSettings settings, IReadOnlyList<IModule> modules)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(modules);

        var clock = new SimulationClock(settings.Epoch);
        var audit = new AuditTrail(clock, settings.Retention);

        return Build(settings, modules, clock, audit);
    }

    private static BuildResult Build(
        EngineSettings settings,
        IReadOnlyList<IModule> modules,
        SimulationClock clock,
        AuditTrail audit)
    {
        // Validation and resolution both happen here, once. A refusal returns
        // before any pipeline exists — an engine that could not compose must not
        // be half-constructed and handed back.
        CompositionResult result = new ModuleComposer().Compose(modules);

        if (result is CompositionRefused refused)
            return new BuildRefused(new EngineCompositionRefused(refused.Problems));

        var composed = (Composed)result;

        var log = new Log(settings.LogSink, settings.MinimumLogLevel);
        var events = new EventBus(clock);

        IFaultPolicy faults = settings.FaultHandling switch
        {
            FaultHandling.Strict => new StrictFaultPolicy(log, audit),
            FaultHandling.Resilient => new ResilientFaultPolicy(log, audit),

            // Not a fallback: an unhandled enum member means someone added a
            // policy and did not compose it, which must fail at startup.
            _ => throw new InvariantFault("SDD-001 §5", null,
                $"Unhandled fault handling mode {settings.FaultHandling}."),
        };

        var pipeline = new TickPipeline(clock, events, audit, faults, log, composed.Stages);

        // The bus binds what composition already validated. It could not be
        // built any earlier — it needs the audit trail and the event bus, which
        // are themselves composed — so the modules declared and handed over
        // their handlers, the composer checked the set, and this is the last
        // step: attaching pairs that are already known to exist and to match.
        var commands = new CommandBus(audit, events);

        for (int i = 0; i < composed.Commands.Count; i++) composed.Commands[i].BindTo(commands);

        return new Built(new Engine(
            composed.OrderedModules, pipeline, audit, events, composed.State,
            composed.Provided, commands));
    }
}
