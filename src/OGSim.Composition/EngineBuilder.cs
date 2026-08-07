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
    AuditRetention Retention,
    ILogSink LogSink,
    LogLevel MinimumLogLevel,
    FaultHandling FaultHandling);

/// <summary>A composed engine: the validated module set and the tick it runs.</summary>
public sealed record Engine(
    IReadOnlyList<IModule> Modules,
    TickPipeline Pipeline,
    IAuditTrail Audit,
    EventBus Events);

public abstract record BuildResult;

public sealed record Built(Engine Engine) : BuildResult;

/// <summary>Every problem, never just the first (R1 §2.9).</summary>
public sealed record BuildRefused(IReadOnlyList<CompositionProblem> Problems) : BuildResult;

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
    internal static IReadOnlyList<IModule> ShippedModules(IAuditTrail audit) =>
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
        new DiagnosticsModule(audit),
    ];

    /// <summary>Composes the shipped set.</summary>
    public static BuildResult Build(EngineSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var clock = new SimulationClock(settings.Epoch);
        var audit = new AuditTrail(clock, settings.Retention);

        return Build(settings, ShippedModules(audit), clock, audit);
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

        if (result is CompositionRefused refused) return new BuildRefused(refused.Problems);

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

        return new Built(new Engine(composed.OrderedModules, pipeline, audit, events));
    }
}
