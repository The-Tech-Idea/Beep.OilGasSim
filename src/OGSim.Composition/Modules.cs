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
    /// <para>A stage a module claims must be FILLED with an <c>ITickStage</c>
    /// during Compose, or composition refuses (SDD-001 §9, finding 125).
    /// Per-tick work needs live entities — compartments, completions, tanks —
    /// and content declares only property kinds, materials and rock types, so
    /// nothing can instantiate one. Claiming a slot anyway would be law L3's
    /// "declaration with no behaviour", which is exactly what the check
    /// refuses.</para>
    /// </summary>
    protected static IReadOnlyList<StageParticipation> NoStagesYet { get; } = [];

    /// <summary>
    /// No facts owned yet, on the same terms: a declared state key must receive
    /// an <c>IStateOwner</c> or composition refuses (finding 127). A module
    /// declares a key when it has an owner to put behind it and not before.
    ///
    /// <para>Two owners are BUILT — <c>Company.CompanyState</c> and
    /// <c>Capabilities.CapabilityState</c> — and neither can be composed here
    /// yet; each module says why at its own declaration. The mechanism is
    /// proven by their round-trip tests rather than by a manifest claim nothing
    /// could redeem.</para>
    /// </summary>
    protected static IReadOnlyList<string> NothingOwnedYet { get; } = [];

    public ModuleManifest Manifest { get; } = manifest;

    public abstract void Compose(IModuleComposition composition);

    /// <summary>Convenience for the common shape: no commands.</summary>
    protected static ModuleManifest Declare(
        string name,
        IReadOnlyList<Type> provides,
        IReadOnlyList<Type> requires,
        IReadOnlyList<string> ownsState,
        IReadOnlyList<StageParticipation> stages,
        IReadOnlyList<Type>? commands = null) =>
        new(new ModuleName(name), provides, requires,
            [.. ownsState.Select(s => new StateKey(s))], stages, commands ?? []);
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
    provides:
    [
        typeof(IDriveMechanism), typeof(IAquiferModel),
        typeof(OGSim.Subsurface.SubsurfaceState),
    ],
    requires: [typeof(IFluidPropertyModel), typeof(TickProduction)],

    // The FIRST module to own a fact and act on it. Both arrive together on
    // purpose: a stage with nothing to act on is law L3's declaration with no
    // behaviour, and state no stage ever changes is a fact the game cannot use.
    ownsState: ["subsurface.compartments"],
    stages: [new StageParticipation(StageId.MaterialBalance, Order: 0)]))
{
    public override void Compose(IModuleComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        // The drive is content-selected in the real composition; the solution-gas
        // drive is the one every compartment falls back to having, not a default
        // dependency — a compartment declares which drive it has.
        var drive = new OGSim.Subsurface.SolutionGasDrive();

        composition.Provide<IDriveMechanism>(drive);
        composition.Provide<IAquiferModel>(new OGSim.Subsurface.FetkovichAquifer(
            productivityIndex: 1e-9, new Pressure(30e6), new ReservoirVolume(1e6)));

        var state = new OGSim.Subsurface.SubsurfaceState(
            composition.Require<IFluidPropertyModel>(), drive,
            Defaults.MaxTickPressureDropFraction);

        composition.Own(state);

        // Published so the field module can wire stage 5's answer into stage 6's
        // commit. A module state is not an interface and this is the one project
        // allowed to name a concrete type (design 03 §8) — the alternative was a
        // contract per module state, which would be eleven interfaces existing
        // only so composition could avoid saying what it already knows.
        composition.Provide(state);

        // Withdrawal comes from stage 5, and stage 5 is the field module's:
        // subsurface owns the commit, not the solve that feeds it.
        TickProduction production = composition.Require<TickProduction>();

        composition.Contribute(
            order: 0,
            new OGSim.Subsurface.MaterialBalanceStage(state, () => production.Withdrawals));
    }
}

// ---------------------------------------------------------------- wells

/// <summary>R6/R7. The completions are the network's source elements.</summary>
internal sealed class WellsModule() : EngineModule(Declare(
    "wells",
    provides: [typeof(IInflowModel), typeof(IOutflowModel), typeof(OGSim.Wells.WellsState)],

    // The registry is required now, because there is finally something to
    // register: a completion is a source element and stage 5 must see it.
    requires: [typeof(IFluidPropertyModel), typeof(IFlowElementRegistry)],
    ownsState: ["wells.completions"],
    stages: NoStagesYet))
{
    public override void Compose(IModuleComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        composition.Provide<IInflowModel>(new OGSim.Wells.CompositeInflowModel(Defaults.Inflow));
        composition.Provide<IOutflowModel>(new OGSim.Wells.HydrostaticFrictionOutflowModel(
            Defaults.Tubing, Density.FromSpecificGravity(0.85), lift: null));

        var wells = new OGSim.Wells.WellsState(composition.Require<IFlowElementRegistry>());

        composition.Own(wells);
        composition.Provide(wells);
    }
}

// ---------------------------------------------------------------- flow

/// <summary>
/// R4. The one flow engine. It requires nothing from the domain modules — it
/// knows only `IFlowElement`, which is why adding equipment never touches it.
/// </summary>
internal sealed class FlowModule() : EngineModule(Declare(
    "flow",
    // The registry is provided HERE because it is the solver's input and the
    // solver is what gives it meaning (SDD-002 §6). Wells and Facilities will
    // REQUIRE it — a contract dependency, never an assembly one — on the day
    // they hold elements to register; declaring that requirement before they
    // resolve it would be the same empty claim as an unfilled stage slot.
    provides: [typeof(IFlowSolver), typeof(IFlowElementRegistry), typeof(TickProduction)],
    // The solver audits every non-convergence, so the trail is a REQUIREMENT
    // and is declared as one — a Require that the manifest does not name is a
    // dependency the composer cannot order.
    requires: [typeof(IAuditTrail)],
    ownsState: NothingOwnedYet,
    stages: NoStagesYet))
{
    public override void Compose(IModuleComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        composition.Provide<IFlowSolver>(new OGSim.Flow.FlowSolver(
            OGSim.Flow.SolverSettings.Pinned, composition.Require<IAuditTrail>()));

        composition.Provide<IFlowElementRegistry>(new FlowElementRegistry());

        // The solve's per-tick output, handed to stage 6. Provided by the flow
        // layer because that is whose answer it is.
        composition.Provide(new TickProduction());
    }
}

// ---------------------------------------------------------------- facilities

internal sealed class FacilitiesModule() : EngineModule(Declare(
    "facilities",

    // The chain, as the elements every barrel crosses between the wellhead and
    // the sale (R20d.2, R20d.5). Provided rather than merely registered, because
    // two things above need to name them: a well has to be tied into the header,
    // and stage 5 has to know which element METERS — and asking an element what
    // it is would be the type switch design 04 §1 exists to prevent.
    provides: [typeof(ISeparationModel), typeof(IHydraulicModel), typeof(SurfaceChain)],
    requires: [typeof(IFluidPropertyModel), typeof(IFlowElementRegistry)],
    ownsState: NothingOwnedYet,
    stages: NoStagesYet))
{
    public override void Compose(IModuleComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        var separation = new OGSim.Facilities.FixedEfficiencySeparationModel();

        composition.Provide<ISeparationModel>(separation);
        composition.Provide<IHydraulicModel>(new OGSim.Facilities.LiquidHydraulicModel(
            Density.FromSpecificGravity(0.85), new Viscosity(3e-3), new Length(0.0)));

        IFlowElementRegistry network = composition.Require<IFlowElementRegistry>();

        var manifold = new OGSim.Facilities.Manifold(
            Defaults.TheManifold, Defaults.ManifoldTier, Defaults.MaterialCount);

        var separator = new OGSim.Facilities.Separator(
            Defaults.TheSeparator, Defaults.SeparatorTier, separation,
            composition.Require<IFluidPropertyModel>(), Defaults.MaterialCount);

        var custody = new OGSim.Facilities.CustodyTransferPoint(
            Defaults.TheCustodyPoint, Defaults.SalesSpec, Defaults.MaterialCount,
            Defaults.MeasureStream);

        // THE GAS LEG GOES TO A FLARE. An E1 field with no gas infrastructure
        // burns its associated gas, which is both the historical answer and the
        // one the ESG mechanics are built to make expensive later (design 13).
        // What it is NOT is an unconnected port: mass leaving the network at one
        // would vanish from the tick's conservation terms silently, and a flare
        // accounts for it — combusted and unburnt, both reported as Disposed.
        var flare = new OGSim.Facilities.Flare(
            Defaults.TheFlare, Defaults.FlareCapacity, Defaults.FlareCombustionEfficiency,
            Defaults.MaterialCount);

        network.Add(manifold);
        network.Add(separator);
        network.Add(custody);
        network.Add(flare);

        network.Connect(new FlowConnection(
            manifold.Id, manifold.Outlet,
            separator.Id, OGSim.Facilities.Separator.Inlet));

        // The LIQUID leg to the meter, the GAS leg to the flare. The water leg
        // stays unconnected and carries nothing: there is no water material yet,
        // so the split puts nothing in it (R20d.4). It is piped the day there is
        // water to put down it, rather than now to an element that would receive
        // zero for a decade.
        network.Connect(new FlowConnection(
            separator.Id, OGSim.Facilities.Separator.LiquidOutlet,
            custody.Id, OGSim.Facilities.CustodyTransferPoint.Inlet));

        network.Connect(new FlowConnection(
            separator.Id, OGSim.Facilities.Separator.GasOutlet,
            flare.Id, OGSim.Facilities.Flare.Inlet));

        composition.Provide(new SurfaceChain(manifold, separator, custody, flare));
    }
}

/// <summary>
/// The surface elements every well flows into, and which of them meters.
///
/// <para>It exists so that the two things above the facilities module can name
/// what they need without asking an element what it IS — design 04 §1's rule
/// that the solver knows only <see cref="IFlowElement"/> applies just as much to
/// the loop above it. The module that BUILT the meter says which one it is;
/// nothing downstream infers it from a type.</para>
/// </summary>
internal sealed record SurfaceChain(
    OGSim.Facilities.Manifold Manifold,
    OGSim.Facilities.Separator Separator,
    OGSim.Facilities.CustodyTransferPoint Custody,
    OGSim.Facilities.Flare Flare)
{
    /// <summary>Where a well ties in, and how many can. One list rather than a
    /// count, so a caller cannot forget which port a slot index means.</summary>
    public int Slots => Manifold.Slots;

    public IReadOnlyList<EntityId<IFlowElement>> MeteredPoints => [Custody.Id];

    /// <summary>
    /// What to call an element on screen.
    ///
    /// <para>The module that BUILT each element names it, because nothing
    /// downstream may ask an element what it is (design 04 §1) — and a host that
    /// had to render "element 1000002" would be showing a player an id instead
    /// of a separator. A completion is not in this list: wells are named by the
    /// module that opens them, and a chain row for one falls back to its id
    /// until R21.6's `WellView` carries a display id.</para>
    /// </summary>
    public string NameOf(EntityId<IFlowElement> element)
    {
        if (element == Manifold.Id) return "manifold";
        if (element == Separator.Id) return "separator";
        if (element == Custody.Id) return "custody-meter";
        if (element == Flare.Id) return "flare";

        return "well-" + element.Value.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
    }
}

// ---------------------------------------------------------------- operations

internal sealed class OperationsModule() : EngineModule(Declare(
    "operations",
    provides: [],
    requires: [typeof(IAuditTrail)],
    ownsState: NothingOwnedYet,
    stages: NoStagesYet))
{
    public override void Compose(IModuleComposition composition) =>
        ArgumentNullException.ThrowIfNull(composition);
}

// ---------------------------------------------------------------- company

/// <summary>
/// R13/R16. Owns the ledger — the one fact that must survive a save exactly,
/// because cash conservation is checked to the cent (INV2).
/// </summary>
internal sealed class CompanyModule() : EngineModule(Declare(
    "company",
    provides: [typeof(IFiscalRegime), typeof(OGSim.Company.CompanyState)],
    requires: [typeof(IAuditTrail)],
    ownsState: ["company.ledger"],
    stages: NoStagesYet))
{
    public override void Compose(IModuleComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        composition.Provide<IFiscalRegime>(new OGSim.Company.RoyaltyTaxRegime(
            new ContentId("concession"), royaltyRate: 0.125, taxRate: 0.40));

        IAuditTrail audit = composition.Require<IAuditTrail>();

        // Revenue may only be caused by a custody transfer (SDD-009 §1), and the
        // ledger asks the TRAIL rather than trusting the posting: a movement
        // cannot claim to be a sale, it can only cite an entry that was one.
        var company = new OGSim.Company.CompanyState(
            Defaults.OpeningCash, cause => IsCustodyTransfer(audit, cause));

        composition.Own(company);
        composition.Provide(company);
    }

    private static bool IsCustodyTransfer(IAuditTrail audit, AuditId cause)
    {
        IReadOnlyList<AuditEntry> transfers = audit.Query(
            new AuditQuery(Subject: null, AuditCategory.CustodyTransfer, Range: null,
                           CauseChainLeaf: null));

        for (int i = 0; i < transfers.Count; i++)
            if (transfers[i].Id == cause) return true;

        return false;
    }
}

// ---------------------------------------------------------------- field

/// <summary>
/// The loop: a well produces, its compartment loses pressure, the oil is sold.
///
/// <para>It is a module of its own because it is the only thing that legitimately
/// knows wells and compartments are both real. Neither domain module can see the
/// other — <c>OGSim.Wells</c> cannot name a compartment and
/// <c>OGSim.Subsurface</c> cannot name a completion — so the numbers crossing
/// between them cross HERE, at Layer 4, and the assembly boundary that keeps
/// reservoir truth out of the well stays exactly where it was.</para>
///
/// <para>It claims stages 5 and 8; subsurface keeps stage 6. Solve, commit and
/// pay are three stages in design 03 §6's order rather than one function,
/// because a failed solve must commit nothing.</para>
/// </summary>
internal sealed class FieldModule() : EngineModule(Declare(
    "field",
    provides: [typeof(FieldControl), typeof(CloseStage)],
    requires:
    [
        typeof(OGSim.Subsurface.SubsurfaceState),
        typeof(OGSim.Wells.WellsState),
        typeof(OGSim.Company.CompanyState),
        typeof(TickProduction),
        typeof(IFluidPropertyModel),
        typeof(IAuditTrail),
        typeof(IRandomSource),
        typeof(SimulationClock),
        typeof(IBeliefStore),
        typeof(OGSim.Information.ObservationSampler),
        typeof(IFlowSolver),
        typeof(IAquiferModel),
        typeof(IFlowElementRegistry),
        typeof(SurfaceChain),
    ],
    ownsState: ["field.activities"],
    stages:
    [
        new StageParticipation(StageId.Operations, Order: 0),
        new StageParticipation(StageId.Availability, Order: 0),
        new StageParticipation(StageId.SolveFlow, Order: 0),
        new StageParticipation(StageId.Custody, Order: 0),
        new StageParticipation(StageId.Economics, Order: 0),
        new StageParticipation(StageId.Objectives, Order: 0),
        new StageParticipation(StageId.Close, Order: 0),
    ],

    // Activities belong to THIS module and to no other: they spend the company's
    // money and they open wells and deliver beliefs, and the field module is the
    // one place entitled to know all three are real. Declaring them here rather
    // than registering them in the builder is what puts the engine's input
    // surface inside the set the composer validates (finding 139) — and, since
    // every one of these is wired by walking the activity catalogue, it is also
    // what catches a catalogue and a manifest that have drifted apart.
    commands:
    [
        typeof(DrillWellCommand),
        typeof(WellTestCommand),
        typeof(WirelineLogCommand),
        typeof(CutCoreCommand),
        typeof(SeismicSurveyCommand),
        typeof(InstallSeparatorCommand),
    ]))
{
    public override void Compose(IModuleComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        IFlowElementRegistry network = composition.Require<IFlowElementRegistry>();
        SurfaceChain chain = composition.Require<SurfaceChain>();

        var loop = new ProductionLoop(
            composition.Require<OGSim.Subsurface.SubsurfaceState>(),
            composition.Require<OGSim.Wells.WellsState>(),
            composition.Require<OGSim.Company.CompanyState>(),
            composition.Require<TickProduction>(),
            composition.Require<IFluidPropertyModel>(),
            composition.Require<IAuditTrail>(),
            composition.Require<IFlowSolver>(),
            composition.Require<IAquiferModel>(),
            network,
            chain.MeteredPoints,
            chain.NameOf,
            Defaults.Economics,
            Defaults.ReservoirTemperature,
            Defaults.SurfaceAmbient,
            Defaults.SurfaceOilDensity,
            Defaults.MaterialCount);

        // Stage 4 before stage 5 before stage 7: the plan, the solve, the meter.
        // Three slots in design 03 §6's order rather than one function, so a
        // failed solve commits nothing and an unmetered barrel earns nothing.
        composition.Contribute(order: 0, new SegmentationStage(network));
        composition.Contribute(order: 0, new SolveFlowStage(loop));
        composition.Contribute(order: 0, new CustodyStage(loop));
        composition.Contribute(order: 0, new EconomicsStage(loop));

        // The scenario's door onto the field. Provided rather than reachable, so
        // building a field is something composition hands out deliberately.
        var field = new FieldControl(
            composition.Require<OGSim.Subsurface.SubsurfaceState>(),
            composition.Require<OGSim.Wells.WellsState>(),
            network,
            chain);

        composition.Provide(field);

        var company = composition.Require<OGSim.Company.CompanyState>();
        IAuditTrail audit = composition.Require<IAuditTrail>();

        // The ONE scheduled-activity engine (SDD-007). Drilling runs on it, and
        // so will every other activity — the well test and the survey that open
        // the exploration game, the workover, the install, the abandonment.
        var scheduler = new OGSim.Operations.OperationScheduler(
            composition.Require<IRandomSource>().Stream(StreamId.Operations),
            audit,
            materialCount: Defaults.MaterialCount);

        scheduler.Register(Defaults.TheRig);

        // WHAT AN ACTIVITY MEANS lives in the activity, and composition is the
        // one layer entitled to build one: a finished hole becomes a well, a
        // finished build-up becomes a belief, and only here is it known that
        // wells, compartments and beliefs are all real (03 §2).
        var subsurface = composition.Require<OGSim.Subsurface.SubsurfaceState>();

        var door = new ObservationDoor(
            composition.Require<OGSim.Information.ObservationSampler>(),
            composition.Require<IBeliefStore>(),
            Defaults.SpaceOf);

        IActivity[] catalogue =
        [
            new DrillWellActivity(
                Defaults.DrillWellTerms, Defaults.MaximumDrillingDepth, field,
                Defaults.CompletionFor),

            new WellTestActivity(
                Defaults.WellTestTerms, Defaults.WellTestSource,
                Defaults.PressureKind, Defaults.PermeabilityKind,
                field, subsurface, door),

            new WirelineLogActivity(
                Defaults.WirelineLogTerms, Defaults.WellLogSource,
                Defaults.PorosityKind, Defaults.PermeabilityKind,
                field, subsurface, door),

            new CoringActivity(
                Defaults.CoringTerms, Defaults.CoreSource,
                Defaults.PorosityKind, Defaults.PermeabilityKind,
                field, subsurface, door),

            new SeismicSurveyActivity(
                Defaults.SeismicSurveyTerms, Defaults.SeismicSource,
                Defaults.OilInPlaceKind, subsurface, door),

            // The verb that answers a bottleneck (R12b.8). It could not exist
            // until the chain was wired: an installed vessel would have been
            // paid for and bypassed (finding 153).
            new InstallSeparatorActivity(
                Defaults.InstallSeparatorTerms, chain.Separator, Defaults.SeparatorLadder),
        ];

        var activities = new ActivityState(scheduler, company, catalogue);
        composition.Own(activities);

        var projection = new FieldProjection(
            loop, company, field, activities, composition.Require<IBeliefStore>());

        // The scenario is CONTENT (design 03 §3.3): the win condition is an
        // objective over a read-model path, not a comparison compiled into a
        // stage. Defaults.FirstField is the JSON a loader will hand over at
        // R21f without a line here changing — and the runner refuses at
        // composition time if it names a path this read model cannot fill.
        var paths = new ReadModelPaths(Defaults.ProjectedPaths);
        var runner = new ScenarioRunner(Defaults.FirstField, paths.Schema);

        var objectives = new ObjectiveStage(company, runner, paths, projection, audit);
        composition.Contribute(order: 0, objectives);

        var close = new CloseStage(projection, objectives);
        composition.Contribute(order: 0, close);
        composition.Provide(close);

        // Stage 3: rigs that finished this month hand over a well or a dry hole,
        // BEFORE stage 5 solves — so a well completed in January produces in
        // January rather than waiting a month for the tick to come round again.
        composition.Contribute(order: 0, new ActivityStage(activities, audit));

        // Every activity wires its own command pair, because only the activity
        // knows its command's type. The manifest above lists the same five, and
        // the composer holds the two lists against each other (finding 139) — so
        // a template added to the catalogue and forgotten in the manifest refuses
        // to compose rather than shipping an order nothing listens to.
        var orders = new ActivityOrders(
            company, field, activities, composition.Require<SimulationClock>());

        for (int i = 0; i < activities.Catalogue.Count; i++)
            activities.Catalogue[i].Register(composition, orders);
    }
}

// ---------------------------------------------------------------- information

/// <summary>
/// R14. The truth wall's owner. It provides the belief store and the observation
/// model; the truth it samples from never leaves the assembly.
/// </summary>
internal sealed class InformationModule() : EngineModule(Declare(
    "information",
    provides:
    [
        typeof(IBeliefStore),
        typeof(IObservationModel),
        typeof(OGSim.Information.ObservationSampler),
    ],
    requires: [typeof(IAuditTrail), typeof(IRandomSource)],
    ownsState: NothingOwnedYet,
    stages: NoStagesYet))
{
    public override void Compose(IModuleComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        IAuditTrail audit = composition.Require<IAuditTrail>();

        composition.Provide<IBeliefStore>(new OGSim.Information.BeliefStore(
            audit, Defaults.SigmaFloorFor, () => new GameDate(1965, 1)));

        var model = new RegionalObservationModel();
        composition.Provide<IObservationModel>(model);

        // R14.3's sampler, COMPOSED. It existed, was tested and was provided by
        // nobody, so the first activity that measured anything sampled truth by
        // hand and delivered a belief with no fairness record behind it
        // (SDD-008 §3, finding 149). It owns the stream choice — surveys draw
        // `exploration`, logs and tests `measurement` — so an activity says only
        // what it measured and never how.
        IRandomSource random = composition.Require<IRandomSource>();

        composition.Provide(new OGSim.Information.ObservationSampler(
            model,
            random.Stream(StreamId.Exploration),
            random.Stream(StreamId.Measurement),
            audit));
    }
}

// ---------------------------------------------------------------- world

internal sealed class WorldModule() : EngineModule(Declare(
    "world",
    provides: [typeof(IWorldGenerator)],
    requires: [],
    ownsState: NothingOwnedYet,
    stages: NoStagesYet))   // world-gen runs once, at tick zero, not in the loop
{
    public override void Compose(IModuleComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        composition.Provide<IWorldGenerator>(new OGSim.World.BasinWorldGenerator());
    }
}

// ---------------------------------------------------------------- capabilities

/// <summary>
/// R17. `Capabilities.CapabilityState` owns `capabilities.technology` and is
/// round-tripped in R20c.6 — but this composition provides `AllCapabilities`,
/// the sandbox all-tech mode, which holds no acquisitions to save. A campaign
/// composes `TechnologyState` and owns its state; that needs a technology graph,
/// which is content (`plans/catalog/`) and does not exist yet. Declaring the key
/// here would claim a fact this composition has none of.
/// </summary>
internal sealed class CapabilitiesModule() : EngineModule(Declare(
    "capabilities",
    provides: [typeof(IGatingValidator), typeof(ICapabilitySet), typeof(IEffectState)],
    requires: [],
    ownsState: NothingOwnedYet,
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
    ownsState: NothingOwnedYet,
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
    ownsState: NothingOwnedYet,
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
    ownsState: NothingOwnedYet,
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
internal sealed class MaterialsModule(RealityProfile profile) : EngineModule(Declare(
    "materials",
    provides: [typeof(IFluidPropertyModel), typeof(IMaterialCatalog)],
    requires: [],
    ownsState: NothingOwnedYet,
    stages: NoStagesYet))
{
    public override void Compose(IModuleComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        var catalogue = new MaterialCatalogue(Defaults.Materials);

        // THE FIDELITY AXIS, at the one slot that currently varies (SDD-005
        // §7b). Both implementations are registered under their own names and
        // the profile picks; an unnamed slot keeps the module's own choice,
        // which is why the simulation profile is empty rather than exhaustive.
        var plugins = new PluginRegistry();

        plugins.Register<IFluidPropertyModel>(
            new ContentId("black-oil-correlations"),
            () => Bound(new BlackOilModel(Defaults.Fluid, Defaults.Validity), catalogue));

        plugins.Register<IFluidPropertyModel>(
            new ContentId("arcade-fluid"),
            () => new ArcadeFluidModel(
                Defaults.Fluid, Defaults.CompletionBo, Defaults.Validity, catalogue));

        IFluidPropertyModel fluid = profile.Selected(Defaults.FluidSlot) is ContentId chosen
            ? plugins.Bind<IFluidPropertyModel>(chosen)
            : Bound(new BlackOilModel(Defaults.Fluid, Defaults.Validity), catalogue);

        composition.Provide(fluid);
        composition.Provide<IMaterialCatalog>(catalogue);
    }

    /// <summary>
    /// THE SECOND HALF OF A TWO-PHASE CONSTRUCTION, and it was missing.
    ///
    /// <para><c>BlackOilModel.SplitAt</c> asks the catalogue what phase a
    /// material is at standard conditions, and the binding is deferred because
    /// the fluid system and the catalogue both load from content and neither can
    /// be built first. Nothing called <c>SplitAt</c> until a separator did, so
    /// the engine composed and ran for four phases with the second half never
    /// performed — and then faulted at exactly the right moment naming the
    /// field, because the model refuses to default (law L2, finding 161).</para>
    ///
    /// <para>Here rather than at the call site so the plugin factory and the
    /// direct construction cannot bind differently.</para>
    /// </summary>
    private static IFluidPropertyModel Bound(BlackOilModel fluid, IMaterialCatalog catalogue)
    {
        fluid.BindMaterials(catalogue);
        return fluid;
    }
}

// ---------------------------------------------------------------- diagnostics

/// <summary>
/// The kernel services every module requires. Provided as a module so that the
/// audit trail is composed like anything else rather than passed around as an
/// ambient singleton — law L2 forbids the singleton, and this is what replaces
/// it.
/// </summary>
internal sealed class DiagnosticsModule(
    IAuditTrail audit, SimulationClock clock, IRandomSource random) : EngineModule(Declare(
    "diagnostics",

    // The clock and the RNG join the trail here for the same reason it is here:
    // they are kernel facilities every module may need and none may own, and
    // composing them makes them declared dependencies rather than the ambient
    // singletons law L2 forbids.
    provides: [typeof(IAuditTrail), typeof(SimulationClock), typeof(IRandomSource)],
    requires: [],
    ownsState: NothingOwnedYet,
    stages: NoStagesYet))
{
    public override void Compose(IModuleComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        composition.Provide(audit);
        composition.Provide(clock);
        composition.Provide(random);
    }
}
