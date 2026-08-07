// Composition — the thirteen modules, declared (design 03 §3.1, §8).
//
// THIS IS THE ONLY PROJECT THAT NAMES CONCRETE TYPES. Every other assembly
// depends downward on Kernel and Contracts alone; somebody has to know what
// implements what, and confining that knowledge to one project is exactly what
// keeps the rest honest.
//
// A MODULE DECLARES BEFORE IT IS BUILT. Provides, Requires, OwnsState, Stages,
// Commands — all of it stated in a manifest that ModuleComposer validates as a
// SET before anything is constructed. Composition is all-or-nothing: either the
// engine builds, or it refuses naming EVERY unmet requirement. There is no
// partially-composed engine and no degraded mode, because an engine missing a
// module is an engine whose failure surfaces fifty ticks later as an
// inexplicable number.
//
// The stage numbering is design 03 §6's, pinned in StageId. A module says WHICH
// stage it works in; it does not get to decide what a stage means or when it
// happens.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Composition;

/// <summary>
/// What every module here has in common: a manifest, and a Compose that
/// publishes what it provides and resolves what it needs.
///
/// <para>Resolution happens AFTER the whole set validates, so a module can never
/// observe a half-built world — the reason <c>Compose</c> is separate from the
/// constructor at all.</para>
/// </summary>
internal abstract class EngineModule(ModuleManifest manifest) : IModule
{
    /// <summary>
    /// No stage claims yet — and deliberately none rather than empty ones.
    ///
    /// <para>A stage a module claims must now be FILLED with an
    /// <c>ITickStage</c> during Compose, or composition refuses (SDD-001 §9,
    /// finding 125). Per-tick work needs the module to hold live entities —
    /// compartments, completions, tanks, ledger accounts — and no module
    /// implements <c>IStateOwner</c> yet, so there is nothing for a stage body
    /// to act on. Claiming the slots anyway would be law L3's "declaration with
    /// no behaviour", which is the exact thing the new check refuses.</para>
    ///
    /// <para><c>OwnsState</c> below IS declared, because it is a claim the
    /// composer validates for uniqueness (check 3) and nothing yet enforces an
    /// owner behind it — the state keys say which module will own which fact
    /// when the owners are built.</para>
    /// </summary>
    protected static IReadOnlyList<StageParticipation> NoStagesYet { get; } = [];

    public ModuleManifest Manifest { get; } = manifest;

    public abstract void Compose(IModuleComposition composition);

    /// <summary>Convenience for the common shape: no state, no commands.</summary>
    protected static ModuleManifest Declare(
        string name,
        IReadOnlyList<Type> provides,
        IReadOnlyList<Type> requires,
        IReadOnlyList<string> ownsState,
        IReadOnlyList<StageParticipation> stages) =>
        new(new ModuleName(name), provides, requires,
            [.. ownsState.Select(s => new StateKey(s))], stages, []);
}

// ---------------------------------------------------------------- subsurface

/// <summary>
/// R5. Owns the compartments — and owns them <b>internally</b>: the module
/// provides `IDriveMechanism` and `IAquiferModel`, never the compartment itself,
/// because `IReservoirCompartment` is internal to `OGSim.Subsurface` and
/// nothing outside can name it.
/// </summary>
internal sealed class SubsurfaceModule() : EngineModule(Declare(
    "subsurface",
    provides: [typeof(IDriveMechanism), typeof(IAquiferModel)],
    requires: [typeof(IFluidPropertyModel)],
    ownsState: ["subsurface.compartments"],
    stages: NoStagesYet))
{
    public override void Compose(IModuleComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        // The drive is content-selected in the real composition; the solution-gas
        // drive is the one every compartment falls back to having, not a default
        // dependency — a compartment declares which drive it has.
        composition.Provide<IDriveMechanism>(new OGSim.Subsurface.SolutionGasDrive());
        composition.Provide<IAquiferModel>(new OGSim.Subsurface.FetkovichAquifer(
            productivityIndex: 1e-9, new Pressure(30e6), new ReservoirVolume(1e6)));
    }
}

// ---------------------------------------------------------------- wells

/// <summary>R6/R7. The completions are the network's source elements.</summary>
internal sealed class WellsModule() : EngineModule(Declare(
    "wells",
    provides: [typeof(IInflowModel), typeof(IOutflowModel)],
    requires: [typeof(IFluidPropertyModel)],
    ownsState: ["wells.completions", "wells.components"],
    stages: NoStagesYet))
{
    public override void Compose(IModuleComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        composition.Provide<IInflowModel>(new OGSim.Wells.CompositeInflowModel(Defaults.Inflow));
        composition.Provide<IOutflowModel>(new OGSim.Wells.HydrostaticFrictionOutflowModel(
            Defaults.Tubing, Density.FromSpecificGravity(0.85), lift: null));
    }
}

// ---------------------------------------------------------------- flow

/// <summary>
/// R4. The one flow engine. It requires nothing from the domain modules — it
/// knows only `IFlowElement`, which is why adding equipment never touches it.
/// </summary>
internal sealed class FlowModule() : EngineModule(Declare(
    "flow",
    provides: [typeof(IFlowSolver)],
    // The solver audits every non-convergence, so the trail is a REQUIREMENT
    // and is declared as one — a Require that the manifest does not name is a
    // dependency the composer cannot order.
    requires: [typeof(IAuditTrail)],
    ownsState: [],
    stages: NoStagesYet))
{
    public override void Compose(IModuleComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        composition.Provide<IFlowSolver>(new OGSim.Flow.FlowSolver(
            OGSim.Flow.SolverSettings.Pinned, composition.Require<IAuditTrail>()));
    }
}

// ---------------------------------------------------------------- facilities

internal sealed class FacilitiesModule() : EngineModule(Declare(
    "facilities",
    provides: [typeof(ISeparationModel), typeof(IHydraulicModel)],
    requires: [typeof(IFluidPropertyModel)],
    ownsState: ["facilities.tanks", "facilities.linefill"],
    stages: NoStagesYet))
{
    public override void Compose(IModuleComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        composition.Provide<ISeparationModel>(new OGSim.Facilities.FixedEfficiencySeparationModel());
        composition.Provide<IHydraulicModel>(new OGSim.Facilities.LiquidHydraulicModel(
            Density.FromSpecificGravity(0.85), new Viscosity(3e-3), new Length(0.0)));
    }
}

// ---------------------------------------------------------------- operations

internal sealed class OperationsModule() : EngineModule(Declare(
    "operations",
    provides: [],
    requires: [typeof(IAuditTrail)],
    ownsState: ["operations.scheduled", "operations.calendars"],
    stages: NoStagesYet))
{
    public override void Compose(IModuleComposition composition) =>
        ArgumentNullException.ThrowIfNull(composition);
}

// ---------------------------------------------------------------- company

internal sealed class CompanyModule() : EngineModule(Declare(
    "company",
    provides: [typeof(IFiscalRegime)],
    requires: [typeof(IAuditTrail)],
    ownsState: ["company.ledger", "company.licences"],
    stages: NoStagesYet))
{
    public override void Compose(IModuleComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        composition.Provide<IFiscalRegime>(new OGSim.Company.RoyaltyTaxRegime(
            new ContentId("concession"), royaltyRate: 0.125, taxRate: 0.40));
    }
}

// ---------------------------------------------------------------- information

/// <summary>
/// R14. The truth wall's owner. It provides the belief store and the observation
/// model; the truth it samples from never leaves the assembly.
/// </summary>
internal sealed class InformationModule() : EngineModule(Declare(
    "information",
    provides: [typeof(IBeliefStore), typeof(IObservationModel)],
    requires: [typeof(IAuditTrail)],
    ownsState: ["information.beliefs", "information.truth"],
    stages: NoStagesYet))
{
    public override void Compose(IModuleComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        composition.Provide<IBeliefStore>(new OGSim.Information.BeliefStore(
            composition.Require<IAuditTrail>(), _ => 0.02, () => new GameDate(1965, 1)));

        composition.Provide<IObservationModel>(new RegionalObservationModel());
    }
}

// ---------------------------------------------------------------- world

internal sealed class WorldModule() : EngineModule(Declare(
    "world",
    provides: [typeof(IWorldGenerator)],
    requires: [],
    ownsState: ["world.surface", "world.jurisdictions"],
    stages: NoStagesYet))   // world-gen runs once, at tick zero, not in the loop
{
    public override void Compose(IModuleComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        composition.Provide<IWorldGenerator>(new OGSim.World.BasinWorldGenerator());
    }
}

// ---------------------------------------------------------------- capabilities

internal sealed class CapabilitiesModule() : EngineModule(Declare(
    "capabilities",
    provides: [typeof(IGatingValidator), typeof(ICapabilitySet), typeof(IEffectState)],
    requires: [],
    ownsState: ["capabilities.technology"],
    stages: NoStagesYet))
{
    public override void Compose(IModuleComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        composition.Provide<IGatingValidator>(new OGSim.Capabilities.GatingValidator());

        // AllCapabilities is a SHIPPED MODE (SDD-005 §2) — the sandbox all-tech
        // modifier, and the composition every pre-R17 phase ran under. A
        // campaign composes TechnologyState instead; both are real.
        composition.Provide<ICapabilitySet>(new OGSim.Capabilities.AllCapabilities());
        composition.Provide<IEffectState>(new OGSim.Capabilities.EffectState(
            new Dictionary<EnvelopeKind, double>()));
    }
}

// ---------------------------------------------------------------- integrity

internal sealed class IntegrityModule() : EngineModule(Declare(
    "integrity",
    provides: [typeof(IDegradationModel), typeof(IHazardModel)],
    requires: [typeof(IAuditTrail)],
    ownsState: ["integrity.conditions", "integrity.barriers"],
    stages: NoStagesYet))
{
    public override void Compose(IModuleComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        composition.Provide<IDegradationModel>(new OGSim.Integrity.SeverityWeightedDegradation(
            new ContentId("standard"), Defaults.Decay));
        composition.Provide<IHazardModel>(new OGSim.Integrity.ExponentialHazardModel(
            new ContentId("standard"), baseRatePerYear: 0.05, conditionExponent: 4.0));
    }
}

// ---------------------------------------------------------------- hse

/// <summary>
/// R23. Separate from integrity because it owns different state and runs in a
/// different stage — the bow-tie reads conditions integrity owns, and reading is
/// not owning (law L5).
/// </summary>
internal sealed class HseModule() : EngineModule(Declare(
    "hse",
    provides: [],
    requires: [typeof(IHazardModel), typeof(IAuditTrail)],
    ownsState: ["hse.esg", "hse.incidents"],
    stages: NoStagesYet))
{
    public override void Compose(IModuleComposition composition) =>
        ArgumentNullException.ThrowIfNull(composition);
}

// ---------------------------------------------------------------- objectives

/// <summary>
/// R24. Requires NOTHING and provides NOTHING — it observes.
///
/// <para>The empty Requires list is the architectural statement: an objective
/// module that required a command bus could act, and a scenario that could act
/// would be a second player.</para>
/// </summary>
internal sealed class ObjectivesModule() : EngineModule(Declare(
    "objectives",
    provides: [],
    requires: [],
    ownsState: ["objectives.progress"],
    stages: NoStagesYet))
{
    public override void Compose(IModuleComposition composition) =>
        ArgumentNullException.ThrowIfNull(composition);
}

// ---------------------------------------------------------------- materials

/// <summary>
/// R2. The fluid model everything else requires. It sits at the bottom of the
/// dependency graph — five modules require `IFluidPropertyModel` and nothing it
/// requires is provided by any of them.
/// </summary>
internal sealed class MaterialsModule() : EngineModule(Declare(
    "materials",
    provides: [typeof(IFluidPropertyModel)],
    requires: [],
    ownsState: ["materials.catalogue"],
    stages: NoStagesYet))
{
    public override void Compose(IModuleComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        composition.Provide<IFluidPropertyModel>(new BlackOilModel(Defaults.Fluid, Defaults.Validity));
    }
}

// ---------------------------------------------------------------- diagnostics

/// <summary>
/// The kernel services every module requires. Provided as a module so that the
/// audit trail is composed like anything else rather than passed around as an
/// ambient singleton — law L2 forbids the singleton, and this is what replaces
/// it.
/// </summary>
internal sealed class DiagnosticsModule(IAuditTrail audit) : EngineModule(Declare(
    "diagnostics",
    provides: [typeof(IAuditTrail)],
    requires: [],
    ownsState: ["diagnostics.audit"],
    stages: NoStagesYet))
{
    public override void Compose(IModuleComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);
        composition.Provide(audit);
    }
}
