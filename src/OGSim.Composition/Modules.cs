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
/// provides `IDriveMechanism` and its own state, never the compartment itself,
/// because `IReservoirCompartment` is internal to `OGSim.Subsurface` and
/// nothing outside can name it.
/// </summary>
internal sealed class SubsurfaceModule() : EngineModule(Declare(
    "subsurface",
    provides:
    [
        typeof(IDriveMechanism),
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

        // NO ENGINE-WIDE AQUIFER (finding 164). One was provided here, and one
        // body of water shared by every compartment is two fields spending the
        // same water — and, sized for either, wrong for the other. A compartment
        // now builds its own from its pore volume when it is created
        // (SDD-003 §3.3a).

        var state = new OGSim.Subsurface.SubsurfaceState(
            composition.Require<IFluidPropertyModel>(), drive,

            // THE LONG ARC (SDD-012 §5). Sea water bought for a flood sours the
            // rock over decades, and the H2S eats the plant — which is why the
            // curve lives with the reservoir that makes it rather than with the
            // equipment that suffers it.
            Defaults.SourCurve,
            Defaults.TheRock,
            Defaults.SouringReferencePpm,
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
    requires:
    [
        typeof(IFluidPropertyModel), typeof(IFlowElementRegistry),
    ],
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

internal sealed class FacilitiesModule(
    FacilityLadders ladders,
    ContentId startingState) : EngineModule(Declare(
    "facilities",

    // The chain, as the elements every barrel crosses between the wellhead and
    // the sale (R20d.2, R20d.5). Provided rather than merely registered, because
    // two things above need to name them: a well has to be tied into the header,
    // and stage 5 has to know which element METERS — and asking an element what
    // it is would be the type switch design 04 §1 exists to prevent.
    provides:
    [
        typeof(ISeparationModel), typeof(IHydraulicModel), typeof(SurfaceChain),

        // What a company can buy, read from content (SDD-004 §6's R20c.9
        // amendment). Provided rather than reached for as a static: a static
        // would have to load from somewhere, and law L2 gives no dependency a
        // default.
        typeof(FacilityLadders),

        // WHAT ERECTS A PLANT (plans 22 §4, S2 step 3). Declared like every
        // other contract this module hands out, so the activity that pays for a
        // facility can ask for it rather than build a second one of its own.
        typeof(PlantBuilder),
    ],
    requires: [typeof(IFluidPropertyModel), typeof(IFlowElementRegistry)],
    ownsState: ["facilities.units"],
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

        // THE PLANT IS ERECTED BY THE THING THAT KNOWS HOW (plans 22 §4, S2
        // step 3). This was sixty lines of construction inline, which was fine
        // while composition was the only thing that ever built a chain — and
        // wrong the moment a player can commission one mid-game. One builder,
        // two callers: this, and the activity a company pays for.
        var works = new PlantBuilder(
            ladders,
            separation,
            composition.Require<IFluidPropertyModel>(),
            composition.Require<IHydraulicModel>(),
            network);

        var chain = new SurfaceChain();

        // WHAT THE COMPANY OPENS WITH, and the whole of S2's behaviour change
        // (plans 22 §4). A refinery on the first day was never a decision
        // anybody took — it was this line, unconditional, standing in for a
        // scenario that had no way to say otherwise.
        //
        // AN UNKNOWN VALUE IS A REFUSAL rather than a default. Composition is
        // all-or-nothing (design 03 §3), and guessing here would hand a
        // misspelled scenario a free plant and call it a starting position.
        if (startingState == Defaults.OpeningPosition)
        {
            works.Commission(chain);
        }
        else if (startingState != Defaults.BareGround)
        {
            throw new InvariantFault("SDD-001 §9", null,
                $"'{startingState.Value}' is not a starting state this build knows; " +
                $"it declares '{Defaults.OpeningPosition.Value}' and " +
                $"'{Defaults.BareGround.Value}'");
        }

        composition.Provide(works);

        // OWNED AS WELL AS PROVIDED (SDD-006 §8b). Six sockets carry a fitted
        // tier and facilities registered no owner, so a reload returned the
        // equipment a company started with and kept the money it had spent
        // (finding 197).
        composition.Own(new FacilitiesState(chain, ladders, works));

        composition.Provide(ladders);
        composition.Provide(chain);
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
/// <summary>
/// The surface plant: what the company has actually built.
/// </summary>
/// <remarks>
/// <para><b>Every element is optional, and that is the whole of S2</b> (plans 22
/// §4). This was an eleven-field record composed whole before the game started,
/// which said — in the only place that could say it — that a company begins
/// owning a complete processing train. It does not; it begins with a yard and a
/// bank balance, and the train is a construction project.</para>
///
/// <para><b>Nullable rather than a throwing accessor.</b> A property that threw
/// when absent would move the failure to run time and leave every reader looking
/// correct; nullable makes the compiler name every place that assumed a plant
/// existed, which is exactly the list this phase has to work through.</para>
///
/// <para>The verb is <c>Install</c> and not <c>Fit</c>: an element already
/// <c>Fit</c>s a TIER onto itself, and one word for two concepts is the naming
/// law's first rule (19 §N1). A separator is installed once and fitted many
/// times.</para>
/// </remarks>
internal sealed class SurfaceChain
{
    public OGSim.Facilities.Manifold? Manifold { get; private set; }

    public OGSim.Facilities.Pipeline? Flowline { get; private set; }

    public OGSim.Facilities.Separator? Separator { get; private set; }

    public OGSim.Facilities.CustodyTransferPoint? Custody { get; private set; }

    public OGSim.Facilities.Treater? Treater { get; private set; }

    public OGSim.Facilities.GasCapture? GasPlant { get; private set; }

    public OGSim.Facilities.Flare? Flare { get; private set; }

    public OGSim.Wells.Injector? Disposal { get; private set; }

    public OGSim.Facilities.WaterIntake? Intake { get; private set; }

    public OGSim.Facilities.Tank? Tank { get; private set; }

    public OGSim.Facilities.OffSpecSink? OffSpecSink { get; private set; }

    // ONE OVERLOAD PER ELEMENT TYPE, so a caller cannot install a separator into
    // the tank slot. The types are all distinct, which is what makes this safe
    // where a string key or an enum would not be.
    public void Install(OGSim.Facilities.Manifold built) => Manifold = built;

    public void Install(OGSim.Facilities.Pipeline built) => Flowline = built;

    public void Install(OGSim.Facilities.Separator built) => Separator = built;

    public void Install(OGSim.Facilities.CustodyTransferPoint built) => Custody = built;

    public void Install(OGSim.Facilities.Treater built) => Treater = built;

    public void Install(OGSim.Facilities.GasCapture built) => GasPlant = built;

    public void Install(OGSim.Facilities.Flare built) => Flare = built;

    public void Install(OGSim.Wells.Injector built) => Disposal = built;

    public void Install(OGSim.Facilities.WaterIntake built) => Intake = built;

    public void Install(OGSim.Facilities.Tank built) => Tank = built;

    public void Install(OGSim.Facilities.OffSpecSink built) => OffSpecSink = built;

    /// <summary>
    /// Connect whatever can now be connected.
    /// </summary>
    /// <remarks>
    /// <para><b>The chain's shape is a fact about the chain, so it lives here</b>
    /// rather than being typed out at the one place that used to build the whole
    /// thing at once. A plant assembled a piece at a time has to be wired a piece
    /// at a time, and in any order the player chooses to build in.</para>
    ///
    /// <para><b>Each edge is made exactly once, when its SECOND end appears.</b>
    /// <c>Connect</c> appends without checking, and one edge per port is SDD-002
    /// §6's rule — so a second call for the same pair would quietly double it and
    /// the solver would see a port feeding two of the same thing. The flags are
    /// what make calling this after every install safe.</para>
    ///
    /// <para>Declared in the order the original composition connected them, so a
    /// plant built all at once produces a byte-identical topology to the one that
    /// was hand-wired.</para>
    /// </remarks>
    public void Wire(IFlowElementRegistry network)
    {
        ArgumentNullException.ThrowIfNull(network);

        Edge(network, 0, Manifold?.Id, OGSim.Facilities.Manifold.Outlet,
             Flowline?.Id, OGSim.Facilities.Pipeline.Inlet);

        Edge(network, 1, Flowline?.Id, OGSim.Facilities.Pipeline.Outlet,
             Separator?.Id, OGSim.Facilities.Separator.Inlet);

        // THE OIL LEG GOES THROUGH TREATING (SDD-006 §2). The treater ships
        // taking nothing out, so a young field is unaffected; it earns its place
        // when the water cut rises far enough to put the stream off spec.
        Edge(network, 2, Separator?.Id, OGSim.Facilities.Separator.LiquidOutlet,
             Treater?.Id, OGSim.Facilities.Treater.Inlet);

        Edge(network, 3, Treater?.Id, OGSim.Facilities.Treater.Outlet,
             Custody?.Id, OGSim.Facilities.CustodyTransferPoint.Inlet);

        // THE GAS LEG HAS A CHOICE (finding 172). It ran straight to the flare,
        // so a company charged for flaring could do nothing about it but produce
        // less oil — a tax rather than a decision. The plant ships at capacity
        // ZERO, which is a field with no gas handling: everything still burns
        // until somebody buys one.
        Edge(network, 4, Separator?.Id, OGSim.Facilities.Separator.GasOutlet,
             GasPlant?.Id, OGSim.Facilities.GasCapture.Inlet);

        Edge(network, 5, GasPlant?.Id, OGSim.Facilities.GasCapture.RejectOutlet,
             Flare?.Id, OGSim.Facilities.Flare.Inlet);

        Edge(network, 6, Separator?.Id, OGSim.Facilities.Separator.WaterOutlet,
             Disposal?.Id, OGSim.Wells.Injector.Inlet);

        // THE FLOOD JOINS ON ITS OWN PORT. One edge per port is §6's rule, and
        // it is what makes commingling declared rather than emergent: the two
        // streams meet inside the well, share one injectivity, and are told
        // apart at stage 6 by which element made them.
        Edge(network, 7, Intake?.Id, OGSim.Facilities.WaterIntake.Outlet,
             Disposal?.Id, OGSim.Wells.Injector.ImportInlet);

        Edge(network, 8, Custody?.Id, OGSim.Facilities.CustodyTransferPoint.OnSpecOutlet,
             Tank?.Id, OGSim.Facilities.Tank.Inlet);

        // AND WHAT FAILS THE SPEC GOES TO THE SINK, not nowhere (finding 252).
        Edge(network, 9, Custody?.Id, OGSim.Facilities.CustodyTransferPoint.RejectOutlet,
             OffSpecSink?.Id, OGSim.Facilities.OffSpecSink.Inlet);
    }

    /// <summary>How many edges the chain has when it is whole.</summary>
    private const int EdgeCount = 10;

    private readonly bool[] _wired = new bool[EdgeCount];

    private void Edge(
        IFlowElementRegistry network,
        int edge,
        EntityId<IFlowElement>? from,
        PortId fromPort,
        EntityId<IFlowElement>? to,
        PortId toPort)
    {
        if (_wired[edge]) return;

        // BOTH ENDS OR NEITHER. An element whose neighbour has not been built is
        // simply not joined yet; it gets its edge the month the other one lands.
        if (from is not EntityId<IFlowElement> tail || to is not EntityId<IFlowElement> head)
            return;

        network.Connect(new FlowConnection(tail, fromPort, head, toPort));
        _wired[edge] = true;
    }

    /// <summary>
    /// The field itself, as an activity aims at it.
    /// </summary>
    /// <remarks>
    /// What an order names when there is no element to name. Commissioning a
    /// facility aims here because none of its vessels exist yet; an upgrade aims
    /// here only when it is about to be refused for the same reason.
    /// </remarks>
    public static EntityRef TheField { get; } = new(EntityKind.Field, 1);

    /// <summary>
    /// Why an upgrade cannot be bought on bare ground.
    /// </summary>
    /// <remarks>
    /// It names the remedy rather than the problem. "There is no separator" is
    /// true and leaves a player looking for a separator to buy; what they need
    /// to know is that vessels come with the facility, and are enlarged after
    /// it (plans 22 §4).
    /// </remarks>
    public static RejectionReason NothingToUpgrade(string named) => new(
        "$loc:reject.no-plant",
        $"there is no {named} here to enlarge; a field is brought on by commissioning " +
        "an early production facility, and its vessels are upgraded after that");

    /// <summary>
    /// Where a well ties in, and how many can.
    /// </summary>
    /// <remarks>
    /// Zero without a manifold, which is the truth rather than a fallback: there
    /// is nowhere to tie a well in until a header is built, and the refusal a
    /// player reads comes from this being honestly zero.
    /// </remarks>
    public int Slots => Manifold?.Slots ?? 0;

    public IReadOnlyList<EntityId<IFlowElement>> MeteredPoints =>
        Custody is null ? [] : [Custody.Id];

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
        if (Manifold is not null && element == Manifold.Id) return "manifold";
        if (Flowline is not null && element == Flowline.Id) return "flowline";
        if (Separator is not null && element == Separator.Id) return "separator";
        if (Custody is not null && element == Custody.Id) return "custody-meter";
        if (Treater is not null && element == Treater.Id) return "treater";
        if (GasPlant is not null && element == GasPlant.Id) return "gas-plant";
        if (Flare is not null && element == Flare.Id) return "flare";
        if (Disposal is not null && element == Disposal.Id) return "water-disposal";
        if (Intake is not null && element == Intake.Id) return "water-intake";
        if (Tank is not null && element == Tank.Id) return "tank";
        if (OffSpecSink is not null && element == OffSpecSink.Id) return "off-spec-sink";

        // A gathering line, numbered by the well it serves (SDD-006 §1c). Named
        // rather than left to the well-N fallback because a player watching the
        // chain has to be able to tell a tieback that is choking from the well
        // behind it — they are different problems with different answers.
        if (element.Value >= Defaults.FirstGatheringLine)
            return "gathering-" + (element.Value - Defaults.FirstGatheringLine + 1).ToString(
                System.Globalization.CultureInfo.InvariantCulture);

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
    provides:
    [
        typeof(IFiscalRegime), typeof(IPriceModel),
        typeof(OGSim.Company.MarketState), typeof(OGSim.Company.CompanyState),
        typeof(ReservesBook), typeof(IReserveBasedLending),

        // The one licence this composition's company holds (SDD-011 §1's
        // R20d.9 amendment).
        typeof(OGSim.Company.Licence),
    ],

    // The belief store, because reserves are worked out from what the company
    // BELIEVES is down there rather than from what is (SDD-009 §4). Declaring it
    // is also what puts this module after the one that owns beliefs — the
    // composer refused outright until it was said, which is the ordering rule
    // doing its job rather than a coincidence of registration order.
    requires:
    [
        typeof(IAuditTrail), typeof(IBeliefStore),
        typeof(OGSim.Company.MarketState),   // finding 229
    ],

    // The licence's commitment progress and whether it has been forfeited are
    // STATE and were not (R20d.9): both change over the game's life and
    // neither is recomputed from Terms alone.
    ownsState: ["company.ledger", "company.market", "company.licence"],
    stages: [new StageParticipation(StageId.Company, Order: 0)]))
{
    public override void Compose(IModuleComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        composition.Provide<IFiscalRegime>(new OGSim.Company.RoyaltyTaxRegime(
            new ContentId("concession"), royaltyRate: 0.125, taxRate: 0.40));

        // THE MARKET (SDD-009 §6). `IPriceModel` was declared in the contract
        // layer and implemented by nobody, so the oil price was a constant and
        // two of the kernel's eight named streams existed for a market that
        // never moved.
        composition.Provide(Defaults.Market);

        // ONE OWNER FOR THE MARKET (law L5). The ledger prices barrels through
        // it and the scheduler prices work through it, and a month in which
        // those two disagreed about what oil was worth is a month whose
        // accounts do not close.
        // OWNED as well as provided (R20d.12). The market carries this month's
        // price and the year the cost index is driven from, and it was in no
        // save at all — so a reloaded game resumed the price walk from the right
        // dice at the wrong place, and sold the same barrels for different money.
        var market = new OGSim.Company.MarketState(
            Defaults.Economics.OilPricePerTonne,
            Defaults.CostElasticity,
            Defaults.CostDrift);

        composition.Own(market);
        composition.Provide(market);

        // ONE OWNER FOR THE RESERVES CALCULATION (law L5). The read model
        // reports what is left and stage 8 accrues the abandonment provision
        // against what the field will ultimately give — and a month where those
        // two disagreed about the size of the field would post a provision
        // against a number nobody was shown.
        // THE FACILITY (SDD-009 §5). `IReserveBasedLending` was declared at
        // finding 147 and implemented by nobody, which had a good reason until
        // R20d.13: there were no reserves to lend against.
        composition.Provide<IReserveBasedLending>(Defaults.Lender);

        composition.Provide(new ReservesBook(
            composition.Require<IBeliefStore>(),
            composition.Require<OGSim.Company.MarketState>(),
            Defaults.TypeCurve));

        IAuditTrail audit = composition.Require<IAuditTrail>();

        // Revenue may only be caused by a custody transfer (SDD-009 §1), and the
        // ledger asks the TRAIL rather than trusting the posting: a movement
        // cannot claim to be a sale, it can only cite an entry that was one.
        var company = new OGSim.Company.CompanyState(
            Defaults.OpeningCash, cause => IsCustodyTransfer(audit, cause));

        composition.Own(company);
        composition.Provide(company);

        // THE ONE LICENCE (SDD-011 §1's R20d.9 amendment). Granted at tick 0
        // always: this composition's company holds one licence for the one
        // field it generates, and nothing here starts a game mid-licence.
        var licence = new OGSim.Company.Licence(
            new EntityId<ILicence>(1), Defaults.LicenceTerms, granted: new Tick(0));

        composition.Own(licence);
        composition.Provide(licence);

        composition.Contribute(order: 0, new LicenceStage(licence, company, audit));
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

/// <summary>
/// Stage 11 — <c>StageId.Company</c>, a slot the fourteen-stage order has
/// carried since design 03 §6 and no module has ever contributed to (SDD-011
/// §1's R20d.9 amendment).
///
/// <para>On loss: the bond posts to <c>Account.Penalty</c> against
/// <c>Account.Cash</c> under <c>MovementCategory.Contractual</c> — both
/// declared in the ledger's own <c>Causes</c> list since R21 §2.4b and posted
/// to by nothing until now — with an <c>AuditCategory.Financial</c> cause,
/// which is durable (never pruned, finding 236) and is how "never silent" is
/// satisfied: through the door that demonstrably works today rather than a
/// new <c>EngineEvent</c> this task would have had to invent from nothing
/// (SDD-011's R20d.9b correction).</para>
/// </summary>
internal sealed class LicenceStage(
    OGSim.Company.Licence licence, OGSim.Company.CompanyState company, IAuditTrail audit)
    : ITickStage
{
    public StageId Id => StageId.Company;

    public void Execute(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // ONLY WHILE LIVE. `AssessAt` re-detects the SAME unmet item on every
        // call after the deadline has passed — nothing marks a forfeited item
        // resolved, because SDD-011 §1's "unmet ⇒ bond forfeit + licence loss"
        // is a ONE-TIME transition and `Licence` was never called more than once
        // per test before this join made it a per-tick call. Calling it every
        // tick regardless would forfeit the SAME bond every month for the rest
        // of the game.
        if (!licence.IsLive) return;

        OGSim.Company.CommitmentAssessment assessment = licence.AssessAt(context.Tick);

        if (assessment.LicenceLost)
        {
            AuditId cause = audit.Record(
                AuditCategory.Financial, subject: null, cause: null,
                new Dictionary<string, AuditValue>(StringComparer.Ordinal)
                {
                    ["spend"] = new("licence-bond-forfeit"),
                    ["unmet-count"] = new(assessment.Unmet.Count.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)),
                });

            company.Ledger.Post(new OGSim.Company.Movement(
                context.Tick, OGSim.Company.Account.Penalty, OGSim.Company.Account.Cash,
                assessment.BondForfeit, OGSim.Company.MovementCategory.Contractual,
                Asset: null, Cause: cause));

            return;
        }

        // THE OTHER WAY A LICENCE ENDS (SDD-011 §1's R16 amendment, finding
        // 254). Checked only once the commitment is confirmed met this tick —
        // a company that broke its promise the same month the term ran out is
        // told the truer of the two reasons. `StateTransition`, not
        // `Financial`: no `Movement` posts, because nothing was broken and
        // there is nothing to forfeit — the same category and the same
        // `kind`-keyed shape `ObjectiveStage` already uses for a verdict that
        // is a fact about state rather than about money (SDD-014 §3).
        if (licence.ExpireIfDue(context.Tick))
            audit.Record(
                AuditCategory.StateTransition, subject: null, cause: null,
                new Dictionary<string, AuditValue>(StringComparer.Ordinal)
                {
                    ["kind"] = new("licence.expired"),
                });
    }
}

/// <summary>
/// Stage 11, beside <see cref="LicenceStage"/> — SDD-009 §7's R13.3 amendment
/// (finding 250). Records the tick's delivered volume every tick, and posts
/// the SAME shape of movement the licence bond does — <c>Account.Penalty</c>
/// against <c>Account.Cash</c>, <c>MovementCategory.Contractual</c> — the
/// tick a window closes short.
/// </summary>
internal sealed class TakeOrPayStage(
    OGSim.Company.TakeOrPayContract contract,
    ProductionLoop loop,
    OGSim.Company.CompanyState company,
    IAuditTrail audit) : ITickStage
{
    public StageId Id => StageId.Company;

    public void Execute(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        contract.RecordDelivery(loop.ProducedThisTick);

        OGSim.Company.TakeOrPayAssessment? assessment = contract.AssessAt(context.Tick);
        if (assessment is null || assessment.Penalty.Cents <= 0) return;

        AuditId cause = audit.Record(
            AuditCategory.Financial, subject: null, cause: null,
            new Dictionary<string, AuditValue>(StringComparer.Ordinal)
            {
                ["spend"] = new("take-or-pay-shortfall"),
                ["delivered-m3"] = new(assessment.Delivered.CubicMetres.ToString(
                    "R", System.Globalization.CultureInfo.InvariantCulture)),
                ["shortfall-m3"] = new(assessment.Shortfall.CubicMetres.ToString(
                    "R", System.Globalization.CultureInfo.InvariantCulture)),
            });

        company.Ledger.Post(new OGSim.Company.Movement(
            context.Tick, OGSim.Company.Account.Penalty, OGSim.Company.Account.Cash,
            assessment.Penalty, OGSim.Company.MovementCategory.Contractual,
            Asset: null, Cause: cause));
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
internal sealed class FieldModule(
    FacilityLadders ladders,
    OGSim.Company.TakeOrPayTerms takeOrPay,
    RuleSet rules) : EngineModule(Declare(
    "field",
    provides:
    [
        typeof(FieldControl), typeof(CloseStage), typeof(IObligationRegistry),
        typeof(Bank), typeof(ReserveHistory),

        // SDD-009 §7's R13.3 amendment (finding 250) — the one take-or-pay
        // contract, provided so a test can resolve and assess it directly,
        // the same reason Licence is.
        typeof(OGSim.Company.TakeOrPayContract),
    ],
    requires:
    [
        typeof(OGSim.Subsurface.SubsurfaceState),
        typeof(OGSim.Wells.WellsState),
        typeof(OGSim.Company.CompanyState),

        // WHAT ERECTS A PLANT (plans 22 §4, S2). Declared because the manifest
        // is the composer's ordering graph: resolving it in code alone leaves
        // the edge out, and the facilities module could compose after the field
        // that asks it for a builder.
        typeof(PlantBuilder),

        // The weather stage 3 loses days to (SDD-016 §3). Declared as well as
        // required, because the manifest is what the composer ORDERS modules by:
        // requiring it in code alone leaves the graph without the edge and the
        // environment module composes after the field that reads it.
        typeof(OGSim.Environment.WeatherState),

        // The one standing (finding 222). Declared as well as required, for the
        // reason the weather state above carries: the manifest is what the
        // composer orders modules by, and requiring in code alone leaves the
        // graph without the edge.
        typeof(EsgAssessment),
        typeof(TickProduction),
        typeof(IFluidPropertyModel),
        typeof(IAuditTrail),
        typeof(IRandomSource),
        typeof(SimulationClock),
        typeof(IBeliefStore),
        typeof(OGSim.Information.ObservationSampler),
        typeof(IFlowSolver),
        typeof(IFiscalRegime),
        typeof(IPriceModel),
        typeof(OGSim.Company.MarketState),
        typeof(IFlowElementRegistry),
        typeof(SurfaceChain),
        typeof(WorldState),
        typeof(OGSim.Information.ProspectRisks),
        typeof(OGSim.Integrity.AssetIntegrity),

        // What the company holds, so the scheduler can be told (R20d.10).
        typeof(OGSim.Capabilities.CapabilityState),

        // The one licence, so DrillWellActivity can record delivery against it
        // and refuse when it has been lost (R20d.9).
        typeof(OGSim.Company.Licence),

        // R20d.10b — the era and technology gate on equipment: an equipment
        // rung needs the same check an activity does.
        typeof(IGatingValidator), typeof(IEffectState),

        // finding 229 — required in code and not in the manifest until the
        // scan found them.
        typeof(IHydraulicModel),
        typeof(IReserveBasedLending),
        typeof(ReserveHistory),
        typeof(ReservesBook),
    ],
    // Provided here because the field is where an asset is CREATED, and
    // registration is unconditional at creation (SDD-007 §6).

    ownsState: [
        "field.activities", "company.obligations", "field.flood", "field.export",
        "company.facility", "company.reserve-history", "field.abandoned",
        "company.take-or-pay"],
    stages:
    [
        new StageParticipation(StageId.Operations, Order: 0),
        new StageParticipation(StageId.Availability, Order: 0),
        new StageParticipation(StageId.SolveFlow, Order: 0),
        new StageParticipation(StageId.Custody, Order: 0),
        new StageParticipation(StageId.Economics, Order: 0),

        // SDD-009 §7's R13.3 amendment (finding 250) — TakeOrPayStage, the
        // same stage LicenceStage posts a contractual penalty from, for the
        // same reason: after stage 8 has settled the month's custody and
        // before stage 12 reports it. Order 2: CompanyModule's LicenceStage
        // claims order 0 and CapabilitiesModule's diffusion claims order 1 on
        // this stage, and within-stage order must be unambiguous across
        // modules — the three are independent, so which runs first does not
        // matter beyond that.
        new StageParticipation(StageId.Company, Order: 2),

        // STALENESS (SDD-008 §2d.3). Declared by THIS module rather than by the
        // information one, and the choice is worth stating: what fixes the
        // ordering is the stage ID, and drift is charged to the compartments
        // that produced — which only the field knows, because it is the module
        // holding the tick's withdrawals. The information module would have had
        // to require the production loop to declare it, which is a dependency
        // pointing the wrong way for one line of wiring.
        new StageParticipation(StageId.Information, Order: 0),

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
        typeof(SurveyBlockCommand),
        typeof(InstallEarlyProductionFacilityCommand),
        typeof(InstallSeparatorCommand),
        typeof(ExpandExportCommand),
        typeof(InstallGasPlantCommand),
        typeof(RemediateInjectorCommand),
        typeof(StimulateWellCommand),
        typeof(ServiceEquipmentCommand),
        typeof(InstallMonitoringCommand),
        typeof(RepairEquipmentCommand),
        typeof(InstallManifoldCommand),
        typeof(InstallTankCommand),
        typeof(InstallTreaterCommand),
        typeof(BorrowCommand),
        typeof(RepayCommand),
        typeof(SetWellChokeCommand),
        typeof(SetVoidageReplacementCommand),
        typeof(AbandonWellCommand),
    ]))
{
    public override void Compose(IModuleComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        IFlowElementRegistry network = composition.Require<IFlowElementRegistry>();
        SurfaceChain chain = composition.Require<SurfaceChain>();

        var obligations = new OGSim.Operations.ObligationRegistry(Defaults.AbandonmentCostOf);
        composition.Own(obligations);
        composition.Provide<IObligationRegistry>(obligations);

        // The gathering lines a field will need are built on demand, so their
        // dependencies are captured once here rather than resolved per well.
        IHydraulicModel hydraulics = composition.Require<IHydraulicModel>();
        IFluidPropertyModel fluid = composition.Require<IFluidPropertyModel>();

        var gatheringLines = 0UL;

        var field = new FieldControl(
            composition.Require<OGSim.Subsurface.SubsurfaceState>(),
            composition.Require<OGSim.Wells.WellsState>(),
            network,
            chain,
            obligations,
            Defaults.AbandonWellTerms.Template,
            composition.Require<WorldState>(),

            // A LINE PER WELL (SDD-006 §1c). Ids are allocated from a block
            // above the fixed chain elements so a gathering line can never
            // collide with the header or the trunk, and the registry is
            // write-once — adding is what it is for.
            run => new OGSim.Facilities.Pipeline(
                new EntityId<IFlowElement>(Defaults.FirstGatheringLine + gatheringLines++),
                Defaults.Flowline with { PipeLength = run },
                Defaults.FlowlineRating,
                new ContentId("gathering-4in"),
                hydraulics,
                fluid,
                Defaults.SurfaceOilDensity,
                Defaults.MaterialCount),

            Defaults.CompletionFor);


        // THE ROUTE TO MARKET. One per field, so its identity is the field's
        // own — a company with two export lines has two fields, and that is
        // R20d.8's world rather than this composition's.
        var terminal = new OGSim.Facilities.ExportTerminal(
            new EntityRef(EntityKind.Facility, 1), ladders.Export[0]);

        var loop = new ProductionLoop(
            composition.Require<OGSim.Subsurface.SubsurfaceState>(),
            composition.Require<OGSim.Wells.WellsState>(),
            composition.Require<OGSim.Company.CompanyState>(),
            composition.Require<TickProduction>(),
            composition.Require<IFluidPropertyModel>(),
            composition.Require<IAuditTrail>(),
            composition.Require<IFlowSolver>(),
            network,
            chain.MeteredPoints,
            chain.NameOf,
            composition.Require<OGSim.Integrity.AssetIntegrity>(),
            chain,
            terminal,
            composition.Require<IFiscalRegime>(),

            // The market, and the ONE stream it may draw from (SDD-009 §6). The
            // stream is handed to the model rather than held by it, so a model
            // that wanted to draw from the weather could not.
            composition.Require<IPriceModel>(),
            composition.Require<IRandomSource>().Stream(StreamId.Price),
            composition.Require<OGSim.Company.MarketState>(),
            obligations,
            composition.Require<ReservesBook>(),
            Defaults.GasPricePerTonne,
            Defaults.LiquidOrdinals,
            () => field.IsAbandoned,
            Defaults.Economics,
            Defaults.ReservoirTemperature,
            composition.Require<OGSim.Environment.WeatherState>(),
            Defaults.SurfaceOilDensity,
            Defaults.MaterialCount);

        // Stage 4 before stage 5 before stage 7: the plan, the solve, the meter.
        // Three slots in design 03 §6's order rather than one function, so a
        // failed solve commits nothing and an unmetered barrel earns nothing.
        // THE FACILITY, built where the loop is: it needs what the company has
        // produced, and the loop is what knows.
        var bank = new Bank(
            composition.Require<IReserveBasedLending>(),
            composition.Require<ReservesBook>(),
            composition.Require<OGSim.Company.CompanyState>(),
            composition.Require<IAuditTrail>(),
            () => loop.CumulativeProduced);

        // The voidage set point is a standing decision, not a per-tick number,
        // and it was in no save at all: a reloaded game kept the water already
        // injected and quietly stopped buying more (R20d.12).
        composition.Own(loop);
        composition.Own(field);

        // AND THE EXPORT LINE, the most expensive purchase in the catalogue.
        // Owned here because this module composes the terminal; the five rungs
        // on the surface chain are `facilities.units`, and one fact has one
        // owner (SDD-006 §8b).
        composition.Own(new FacilitiesState.ExportState(terminal, ladders));

        // AND THE FACILITY'S OWN STANDING. The base is re-derived every settle,
        // but the covenant is a clock that reads its own previous value, so a
        // reloaded company was coming back Clear however deep in breach it was
        // — a breach curable by saving and loading (finding 210).
        composition.Own(bank);

        // THE RESERVE RECORD, which is what makes RRR measurable at all: the
        // ratio is derived, but "what proved reserves stood at a year ago" is
        // recoverable from nothing else in a save (finding 208).
        var history = new ReserveHistory();

        composition.Own(history);
        composition.Provide(history);

        composition.Provide(bank);
        composition.Contribute(order: 0, new SegmentationStage(
            network, composition.Require<OGSim.Integrity.AssetIntegrity>(), loop,
            composition.Require<IAuditTrail>()));
        composition.Contribute(order: 0, new SolveFlowStage(loop));

        // Beliefs go stale on what was PRODUCED FROM, so this reads the tick's
        // withdrawals rather than asking each compartment whether it was open —
        // a compartment that gave up nothing is simply not in that list
        // (SDD-008 §2d.2, finding 200).
        composition.Contribute(order: 0, new StalenessStage(
            composition.Require<IBeliefStore>(),
            composition.Require<TickProduction>(),
            Defaults.DriftPerYearFor,

            // The kinds production moves. Pressure is the only one this engine
            // files beliefs about today; §2d.1's table is the rule for the rest.
            [Defaults.PressureKind]));
        composition.Contribute(order: 0, new CustodyStage(loop));
        composition.Contribute(order: 0, new EconomicsStage(
            loop, bank, composition.Require<ReservesBook>(), history,
            composition.Require<EsgAssessment>()));

        // The scenario's door onto the field. Provided rather than reachable, so
        // building a field is something composition hands out deliberately.
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

        // The era gate on equipment: what a company may buy is a calendar fact as
        // well as a cash one (SDD-005 §2's R20d.10b amendment).
        var capabilities = composition.Require<OGSim.Capabilities.CapabilityState>();
        OGSim.Capabilities.EraCalendar eras = Defaults.Eras;
        var gate = composition.Require<IGatingValidator>();
        var effects = composition.Require<IEffectState>();

        IActivity[] catalogue =
        [
            new DrillWellActivity(
                Defaults.DrillWellTerms, Defaults.MaximumDrillingDepth, field,
                composition.Require<OGSim.Information.ProspectRisks>(),
                composition.Require<WorldState>(),
                composition.Require<IBeliefStore>(),
                subsurface, door,
                composition.Require<OGSim.Company.Licence>(),
                rules.Drilling),

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
                Defaults.StructureCapacityKind, composition.Require<WorldState>(),
                composition.Require<OGSim.Information.ProspectRisks>(), door),

            // FIND, then firm up (SDD-010 §4b's S1 amendment). The survey above
            // sharpens a structure the company has already found; this one is
            // how it finds any at all.
            new SurveyBlockActivity(
                Defaults.BlockSurveyTerms, composition.Require<WorldState>(),
                composition.Require<OGSim.Information.ProspectRisks>()),

            // THE VERB THAT STARTS A FIELD (plans 22 §4, S2 step 3). Every
            // install below adds capacity to a plant that exists; this is the
            // one that makes there be a plant, and it is aimed at the field
            // rather than at any element because none of them exist yet.
            new InstallEarlyProductionFacilityActivity(
                Defaults.EarlyProductionFacilityTerms,
                chain,
                composition.Require<PlantBuilder>(),
                field),

            // The verb that answers a bottleneck (R12b.8). It could not exist
            // until the chain was wired: an installed vessel would have been
            // paid for and bypassed (finding 153).
            new InstallSeparatorActivity(
                Defaults.InstallSeparatorTerms, chain, ladders.Separator, ladders, capabilities, eras, gate, effects),

            // THE FIELD'S LAST CEILING (R20d.8). Debottleneck everything upstream
            // and a field still sells only what the export line takes — which is
            // why, until this, ten times the oil earned the same money.
            new ExpandExportActivity(
                Defaults.ExpandExportTerms, terminal, ladders.Export),

            // THE ANSWER TO THE FLARING PENALTY (finding 172). Without it a
            // company charged for flaring could only respond by producing less
            // oil, which is a tax rather than a decision.
            new InstallGasPlantActivity(
                Defaults.InstallGasPlantTerms, chain, ladders.GasPlant, ladders, capabilities, eras, gate, effects),

            // THE ANSWER TO A BROKEN ANYTHING (SDD-012 §3). Stage 4 now takes
            // equipment out of the network and the route law shuts in whatever
            // was behind it, so without this the first unlucky draw would end
            // the game — a cost with no response, for the third time.
            new RepairEquipmentActivity(
                Defaults.RepairEquipmentTerms,
                composition.Require<OGSim.Integrity.AssetIntegrity>(),
                composition.Require<IFlowElementRegistry>()),

            // AND THE WAY TO NOT NEED IT (SDD-012 §3, R20d.26.2). Planned work
            // on equipment that still runs, at the planned price — without this
            // the two prices were one and waiting was free, so run-to-failure
            // dominated on every seed (finding 185).
            new ServiceEquipmentActivity(
                Defaults.ServiceEquipmentTerms,
                composition.Require<OGSim.Integrity.AssetIntegrity>(),
                composition.Require<IFlowElementRegistry>()),

            // AND WHAT MAKES THAT SELECTABLE (C14, R20d.26.4). §3 has always
            // required a monitoring tier for condition-based work; the gate was
            // implemented in a record nothing called, so the strategy that pays
            // was the one nobody had to buy (finding 191).
            new InstallMonitoringActivity(
                Defaults.InstallMonitoringTerms,
                composition.Require<OGSim.Integrity.AssetIntegrity>(),
                composition.Require<IFlowElementRegistry>()),

            // THE ANSWER TO A PLUGGED INJECTOR (R10-V4). R20d.18 made the
            // plugging real and left no way to clear it, which is a decline the
            // player watches rather than a decision they take.
            new RemediateInjectorActivity(
                Defaults.RemediateInjectorTerms, chain),

            // A WELL DRILLED CLEAN CAN STILL BE MADE BETTER (R12b.7, finding
            // 253). Every completion opens at zero skin, so this is a genuine
            // improvement rather than a restoration — the acid job, not yet
            // the frac or multi-stage that name is short for.
            new StimulateWellActivity(
                Defaults.StimulateWellTerms, field),

            // THE ANSWER THE DRILLING REFUSAL ALREADY NAMED. "A bigger header
            // has to be installed first" has been the reason a well is turned
            // away since R12b, and nothing could install one.
            new InstallManifoldActivity(
                Defaults.InstallManifoldTerms, chain, ladders.Manifold, ladders, capabilities, eras, gate, effects),

            // THE THIRD ANSWER TO A FULL TANK. Stage 6 offers "more storage,
            // more export and less production"; the other two shipped and this
            // did not.
            new InstallTankActivity(
                Defaults.InstallTankTerms, chain, ladders.Tank, ladders, capabilities, eras, gate, effects),

            // THE ANSWER TO WET OIL (finding 173). A field that waters out sells
            // a stream the meter turns away, and without this that is a tax on
            // getting old rather than a decision.
            new InstallTreaterActivity(
                Defaults.InstallTreaterTerms, chain, ladders.Treater, ladders, capabilities, eras, gate, effects),

            // The ENDING (R12b.10). Finding 153's other reason is gone too: opex
            // scales with the liquid lifted, so a watered-out well genuinely
            // costs more than it earns and stopping it is a real decision.
            new AbandonWellActivity(
                Defaults.AbandonWellTerms, field, obligations,
                composition.Require<OGSim.Company.CompanyState>()),
        ];

        var activities = new ActivityState(
            scheduler, company, catalogue,
            composition.Require<OGSim.Company.MarketState>());
        composition.Own(activities);

        var projection = new FieldProjection(
            loop, company, field, activities, composition.Require<IBeliefStore>(),
            composition.Require<WorldState>(),
            composition.Require<OGSim.Information.ProspectRisks>(),
            composition.Require<ReservesBook>(),
            bank,
            composition.Require<ReserveHistory>(),
            composition.Require<OGSim.Environment.WeatherState>(),
            composition.Require<EsgAssessment>());

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

        // THE ONE TAKE-OR-PAY CONTRACT (SDD-009 §7's R13.3 amendment, finding
        // 250), granted at tick 0 like the one licence: this composition's
        // company holds one offtake commitment against the one field it
        // generates.
        var takeOrPayContract = new OGSim.Company.TakeOrPayContract(takeOrPay, start: new Tick(0));

        composition.Own(takeOrPayContract);
        composition.Provide(takeOrPayContract);
        composition.Contribute(order: 2, new TakeOrPayStage(takeOrPayContract, loop, company, audit));

        // Stage 3: rigs that finished this month hand over a well or a dry hole,
        // BEFORE stage 5 solves — so a well completed in January produces in
        // January rather than waiting a month for the tick to come round again.
        composition.Contribute(order: 0, new ActivityStage(
            activities, audit, composition.Require<OGSim.Environment.WeatherState>()));

        // Every activity wires its own command pair, because only the activity
        // knows its command's type. The manifest above lists the same five, and
        // the composer holds the two lists against each other (finding 139) — so
        // a template added to the catalogue and forgotten in the manifest refuses
        // to compose rather than shipping an order nothing listens to.
        // No FieldControl: the shared refusals no longer ask what the field holds
        // (SDD-007 §2's GC-4 amendment), and a dependency nothing reads is one
        // more thing that has to be true at composition for no reason (L2).
        var orders = new ActivityOrders(
            company, composition.Require<OGSim.Company.MarketState>(), activities,
            composition.Require<SimulationClock>(),
            composition.Require<OGSim.Environment.WeatherState>(),
            composition.Require<OGSim.Capabilities.CapabilityState>(),

            // WHAT THE RUN IS PLAYED UNDER (plans 23). The subject rule is the
            // one GC-4 was about: an operator has a field to work on and a
            // frontier company is out to find one, and both are correct under
            // their own rules.
            rules.WorkSubject,
            field);

        for (int i = 0; i < activities.Catalogue.Count; i++)
            activities.Catalogue[i].Register(composition, orders);

        // NOT an activity: a valve turn is not a project (SDD-003 §5.1's R20.4
        // amendment), so it is a command pair of its own rather than a template
        // on the scheduled-activity engine.
        composition.HandleCommand(
            new SetWellChokeValidator(field), new SetWellChokeApplier(field, audit));

        // NOR IS A FLOOD TARGET (SDD-003 §3.1d's R20d.24 amendment). The water
        // costs money by the cubic metre in the month it is lifted; deciding how
        // much to lift is a set point, and one a reservoir engineer moves far
        // more often than a rig moves.
        composition.HandleCommand(
            new SetVoidageReplacementValidator(loop),
            new SetVoidageReplacementApplier(loop, audit));

        // NOR IS A DRAWDOWN (SDD-009 §5). A well is a project; a phone call to
        // the bank is not, and putting it on the activity engine would make a
        // company wait a quarter for money it already had a facility for.
        var borrower = composition.Require<OGSim.Company.CompanyState>();

        composition.HandleCommand(
            new BorrowValidator(borrower, bank), new BorrowApplier(borrower, audit));

        composition.HandleCommand(
            new RepayValidator(borrower), new RepayApplier(borrower, audit));
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
        typeof(OGSim.Information.ProspectRisks),
    ],
    requires: [typeof(IAuditTrail), typeof(IRandomSource), typeof(SimulationClock), typeof(WorldState)],
    ownsState: ["information.beliefs", "information.prospect-risk"],
    stages: NoStagesYet))
{
    public override void Compose(IModuleComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        IAuditTrail audit = composition.Require<IAuditTrail>();

        // OWNED as well as provided (SDD-008 §4b). Everything this store holds
        // was bought — surveys, well tests, logs, cores, the dry hole that
        // re-priced a play — and none of it was in a save until R20d.12.10, so a
        // reloaded company was solvent, drilled, producing, and had forgotten
        // every survey it had ever paid for (finding 198).
        // WHEN, FROM THE CLOCK. SDD-008 §2.1's update rule ends "AsOf = now", and
        // this passed a literal epoch instead — so every belief the engine has
        // ever held claimed to have been learned in January 1965, including the
        // ones a company spent forty years and a great deal of money buying. It
        // is player-visible: `AsOf` is one of the five fields a belief projects
        // (§8) and it is what makes a survey shot last month distinguishable from
        // the regional guess the game opened with. Nothing caught it because a
        // constant is perfectly self-consistent — every belief agreed with every
        // other, the projection was populated, the round trip was exact
        // (finding 199).
        //
        // Held as ISimulationClock rather than the concrete type: `Advance` is on
        // SimulationClock alone, so a module that reads the date this way cannot
        // move it even by mistake.
        ISimulationClock clock = composition.Require<SimulationClock>();

        var beliefs = new OGSim.Information.BeliefStore(
            audit, Defaults.SigmaFloorFor, () => clock.Date);

        composition.Own(beliefs);
        composition.Provide<IBeliefStore>(beliefs);

        var model = new RegionalObservationModel(composition.Require<WorldState>());
        composition.Provide<IObservationModel>(model);

        // R20d.7's POS, composed at last. `ProspectRisk` was built, tested and
        // consumed by nobody for four phases — because a probability of success
        // is a statement about a PROSPECT and nothing generated prospects. The
        // world does now.
        // OWNED for the same reason the belief store is: what a play believes IS
        // the campaign, and a reload that dropped it handed the company back its
        // opening conviction (SDD-008 §4b).
        var risks = new OGSim.Information.ProspectRisks(Defaults.ExplorationPrior);

        composition.Own(risks);
        composition.Provide(risks);

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

internal sealed class WorldModule(
    IReadOnlyList<OGSim.World.TerrainClassDefinition> terrainClasses,
    ContentId climateId) : EngineModule(Declare(
    "world",
    provides: [typeof(IWorldGenerator), typeof(WorldState)],
    requires: [],
    ownsState: ["world.decisions"],
    stages: NoStagesYet))   // world-gen runs once, at tick zero, not in the loop
{
    public override void Compose(IModuleComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        composition.Provide<IWorldGenerator>(
            new OGSim.World.BasinWorldGenerator(terrainClasses, climateId));

        // EMPTY, and filled once by generation before the first tick. Composed
        // rather than created by `CreateNew` because the FIELD reads it — a well
        // tied in has to know where its prospect is — and a module cannot depend
        // on something built after composition finished.
        // OWNED as well as provided (SDD-010 §4c). Where a company built its
        // header is a decision rather than a function of the seed, and every
        // later well's gathering line is as long as its field is from it —
        // unsaved, a reloaded campaign re-sited it at whichever field it next
        // tied in (finding 195).
        var world = new WorldState();

        composition.Own(world);
        composition.Provide(world);
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
internal sealed class CapabilitiesModule(
    IReadOnlyList<OGSim.Capabilities.TechnologyNode> registry,
    OGSim.Capabilities.EraCalendar eras,
    SimulationClock clock) : EngineModule(Declare(
    "capabilities",
    provides:
    [
        typeof(IGatingValidator), typeof(ICapabilitySet), typeof(IEffectState),

        // What the company has actually acquired, so the scheduler can be told
        // rather than handed an empty list (SDD-005 §2's R20d.10 amendment).
        typeof(OGSim.Capabilities.CapabilityState),

        // THE CONCRETE TYPE TOO (SDD-005 §4.2's R22.2 amendment) — environment
        // and technology apply through the SAME `EffectState.Apply`, which is
        // deliberately not on `IEffectState`, so the module that calls it for
        // weather has to resolve the concrete object this one built rather
        // than a second instance of its own.
        typeof(OGSim.Capabilities.EffectState),
    ],
    requires: [],

    // The holdings are STATE and always were: `Acquire` replays them in order
    // through the graph that authorised them. Nothing owned this key because
    // nothing composed the owner.
    ownsState: ["capabilities.technology"],

    // StageId.Company (11), not Environment — SDD-005 §4.2's own words:
    // "applied when acquisition completes (a stage-11 state change), taking
    // effect next tick — technology never creates a segment boundary." A first
    // pass placed this at stage 2 by analogy with weather and was corrected
    // (SDD-005's R20d.10 amendment) after checking it against §4.2, which had
    // already stated the right stage and the reason for it.
    stages: [new StageParticipation(StageId.Company, Order: 1)]))
{
    public override void Compose(IModuleComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        composition.Provide<IGatingValidator>(new OGSim.Capabilities.GatingValidator());

        // THE CAMPAIGN'S OWN HOLDINGS, replacing `AllCapabilities` (SDD-005 §2's
        // R20d.10 amendment). That is a SHIPPED MODE and not a stub — the sandbox
        // all-tech modifier of design 18 §5, and the composition every pre-R17
        // phase ran under — but it is the wrong one for a campaign: `Has` returns
        // true for everything, so no gate could ever refuse and the sixty-five
        // nodes in `content/technologies/` had nothing to be acquired INTO.
        var state = new OGSim.Capabilities.CapabilityState(
            registry, eras, () => clock.Date, clock.Epoch);

        composition.Own(state);
        composition.Provide(state);
        composition.Provide<ICapabilitySet>(state.Technology);

        // HELD AS WELL AS PROVIDED. `DiffusionStage` needs the concrete
        // `EffectState.Apply` — deliberately not on `IEffectState`, whose three
        // readers are the whole surface a model may use (SDD-005 §4.2) — so the
        // module keeps the object it built rather than resolving it back through
        // the interface it just gave away.
        var effects = new OGSim.Capabilities.EffectState(new Dictionary<EnvelopeKind, double>());

        composition.Provide<IEffectState>(effects);
        composition.Provide(effects);

        composition.Contribute(order: 1, new DiffusionStage(state, effects));
    }
}

/// <summary>
/// Stage 11 — <c>StageId.Company</c>. What has become standard practice
/// (SDD-005 §2, design 07 §3).
///
/// <para>"Everything eventually becomes standard practice" is a DATE, not an
/// event: a node with a <b>D</b> route arrives at its era's start plus its
/// content lag, in the same month of every game with the same start. No draw,
/// so it costs the hazard stream nothing and cannot shift another mechanic's
/// dice.</para>
///
/// <para>Nothing called <c>ApplyDiffusion</c> before this — the third of four
/// acquisition routes existed for its own unit tests, which is why a campaign
/// could run forty years and hold nothing it did not start with (finding 191).</para>
///
/// <para><b>Stage 11, not stage 2</b> (SDD-005 §4.2's R20d.10 correction): a
/// first pass placed this beside weather at stage 2, which would let a node
/// diffusing this month reach stage 4's segmentation THIS SAME month. §4.2
/// already said the right stage and why — "technology never creates a segment
/// boundary" — so this runs after the solve, and a newly diffused node is a
/// genuinely NEXT-tick fact.</para>
///
/// <para><b>And what it grants now REACHES a model</b> (SDD-005 §4.2's R20d.10e
/// amendment): <c>TechnologyState.ActiveEffects()</c> existed and nothing ever
/// read it. Recomputed from it every tick rather than applied incrementally on
/// acquisition — checked rather than assumed to differ: `EffectState`'s three
/// combinators are each idempotent under a repeat, and nothing in this design
/// ever revokes a technology, so a full recompute and an incremental apply
/// reach the same state. The "next tick" property §4.2 asks for comes from the
/// STAGE, not from how the combinators are invoked.</para>
/// </summary>
internal sealed class DiffusionStage(
    OGSim.Capabilities.CapabilityState capabilities, OGSim.Capabilities.EffectState effects)
    : ITickStage
{
    public StageId Id => StageId.Company;

    public void Execute(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        capabilities.Technology.ApplyDiffusion(
            capabilities.Era, capabilities.EraStart, context.Tick);

        effects.Apply(capabilities.Technology.ActiveEffects());
    }
}

// -------------------------------------------------------------- environment

/// <summary>
/// Stage 2 (SDD-016 §1). Thirty days of weather per region, before anything
/// decides what it can do this month.
/// </summary>
internal sealed class WeatherStage(
    OGSim.Environment.WeatherState weather,
    IWeatherModel model,
    IRandomStream stream,

    /// <summary>SDD-005 §4.2's R22.2 amendment: the same path technology's
    /// `DiffusionStage` applies through, at the stage design 07 §1 = 13 §2.1
    /// already puts weather at.</summary>
    OGSim.Capabilities.EffectState effects,
    OGSim.Environment.ClimateProfile climate) : ITickStage
{
    public StageId Id => StageId.Environment;

    public void Execute(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        weather.Advance(context.Date, model, stream);
        effects.Apply(climate.Effects);
    }
}

internal sealed class EnvironmentModule(OGSim.Environment.ClimateProfile climate)
    : EngineModule(Declare(
    "environment",
    provides: [typeof(IWeatherModel), typeof(OGSim.Environment.WeatherState)],
    requires: [typeof(IRandomSource), typeof(OGSim.Capabilities.EffectState)],

    // THE CARRY, which is the one value that crosses a tick. `StreamId.Weather`
    // has existed since R1 and was drawn by nothing until now — the ninth
    // declared-and-unjoined mechanism this project has found, and the first
    // whose consumer is a stage that was in the declared tick order from the
    // start and had no participant.
    ownsState: ["environment.weather"],
    stages: [new StageParticipation(StageId.Environment, Order: 0)]))
{
    public override void Compose(IModuleComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        // THE CLIMATE IS AN ARGUMENT, not a static read (law L2). It was
        // `Defaults.Climate` in both lines, which is a dependency with a default
        // wearing a constant's clothes — and it made the access window of
        // SDD-016 §5b's R22.6 amendment untestable through a composed engine,
        // since no test could hand the module a coast that closes.
        var model = new OGSim.Environment.Ar1Weather(climate.Persistence);
        var weather = new OGSim.Environment.WeatherState([climate]);

        composition.Own(weather);
        composition.Provide<IWeatherModel>(model);
        composition.Provide(weather);

        composition.Contribute(order: 0, new WeatherStage(
            weather, model, composition.Require<IRandomSource>().Stream(StreamId.Weather),
            composition.Require<OGSim.Capabilities.EffectState>(), climate));
    }
}

// ---------------------------------------------------------------- integrity

internal sealed class IntegrityModule() : EngineModule(Declare(
    "integrity",
    provides:
    [
        typeof(IDegradationModel),
        typeof(IHazardModel),
        typeof(OGSim.Integrity.AssetIntegrity),
    ],
    requires: [typeof(IAuditTrail), typeof(IRandomSource), typeof(SurfaceChain)],

    // THE CONDITIONS, and this module is the only writer of them. Declared now
    // because until R20d.22 it owned nothing and ran nowhere: two correct models
    // composed and reachable from no tick, so equipment never aged and never
    // failed. The stage is not declared here — stage 4 belongs to the module
    // that builds the segment plan, and integrity contributes the state it
    // reads rather than a second participant in the same slot.
    ownsState: ["integrity.conditions"],
    stages: NoStagesYet))
{
    public override void Compose(IModuleComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        var degradation = new OGSim.Integrity.SeverityWeightedDegradation(
            new ContentId("standard"), Defaults.Decay);
        var hazard = new OGSim.Integrity.ExponentialHazardModel(
            new ContentId("standard"), baseRatePerYear: 0.05, conditionExponent: 4.0);

        composition.Provide<IDegradationModel>(degradation);
        composition.Provide<IHazardModel>(hazard);

        OGSim.Composition.SurfaceChain chain = composition.Require<SurfaceChain>();

        var integrity = new OGSim.Integrity.AssetIntegrity(
            new OGSim.Integrity.IntegrityPass(
                degradation, hazard,
                composition.Require<IRandomSource>().Stream(StreamId.Hazard),
                composition.Require<IAuditTrail>()),

            // THE CHAIN NAMES ITS OWN EQUIPMENT (design 04 §1). An audit row
            // reading "separator" instead of "element 1000002" costs one
            // function, and asking the element what it IS would be the thing
            // that rule exists to forbid.
            element => new ContentId(chain.NameOf(element.Id)));

        composition.Provide(integrity);
        composition.Own(integrity);
    }
}

// ---------------------------------------------------------------- hse

/// <summary>
/// R23. Separate from integrity because it owns different state and runs in a
/// different stage — the bow-tie reads conditions integrity owns, and reading is
/// not owning (law L5).
/// </summary>
/// <summary>
/// Stage 9 (SDD-012 §4b). The company's standing with the world, and the one
/// place it is computed.
///
/// <para>THE MODULE WAS AN EMPTY SHELL: it declared two requirements it never
/// resolved, owned nothing, filled no stage, and its `Compose` was a null check
/// — while §4b's `EsgStanding`, incident points and decay sat built and uncalled
/// in `OGSim.Integrity`, and the engine priced its borrowing against a
/// FLARING-ONLY standing in `OGSim.Company` (finding 222). Two owners of one
/// fact, and the one that could see an incident was the one nobody called.</para>
/// </summary>
internal sealed class HseModule() : EngineModule(Declare(
    "hse",
    provides: [typeof(EsgAssessment)],
    requires:
    [
        typeof(IHazardModel), typeof(IAuditTrail),

        // finding 229. The threat stage draws from the hazard stream and reads
        // the condition of the elements the barriers are defined over.
        typeof(IRandomSource),
        typeof(OGSim.Integrity.AssetIntegrity),
    ],

    // The incident record decays, so it is a quantity recomputed from its own
    // past — state, on the same argument as the covenant clock (finding 210).
    ownsState: ["integrity.esg"],
    stages:
    [
        new StageParticipation(StageId.Availability, Order: 1),
        new StageParticipation(StageId.HseRegulation, Order: 0),
    ]))
{
    public override void Compose(IModuleComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        var standing = new OGSim.Integrity.EsgStanding(Defaults.EsgIncidentHalfLifeTicks);

        composition.Own(standing);

        var assessment = new EsgAssessment(standing, Defaults.Record);

        composition.Provide(assessment);
        composition.Contribute(order: 1, new ThreatStage(
            new OGSim.Integrity.BowTie(
                composition.Require<IRandomSource>().Stream(StreamId.Hazard),
                composition.Require<IAuditTrail>()),
            composition.Require<OGSim.Integrity.AssetIntegrity>(),
            standing));

        composition.Contribute(order: 0, new EsgStage(standing));
    }
}

/// <summary>
/// ONE OWNER OF THE STANDING (law L5, finding 222). The flaring term and the
/// incident term were separate implementations returning separate answers on
/// separate scales; this is the single place they combine.
///
/// <para>Identical to the old flaring-only number while nothing has happened:
/// `EsgRecord` gives a 0–1 score, the weight turns it into §4b's penalty out of
/// a hundred, and with no incident points the quotient is what the bank read
/// before. That is deliberate — the join is a refactor today and becomes a
/// mechanic the day the bow-tie is wired to it.</para>
/// </summary>
public sealed class EsgAssessment(
    OGSim.Integrity.EsgStanding standing, OGSim.Company.EsgRecord record)
{
    /// <summary>The 0–1 fraction a contract takes (SDD-012 §4b's R20d.16
    /// amendment: 0–100 is the presentation scale, 0–1 crosses the wire).</summary>
    /// <para>Takes NOTHING, since SDD-012 §4b's R23.1 amendment: the flaring the
    /// record is scored on is the aged window <c>EsgStanding</c> owns, not a
    /// lifetime tally a caller happens to hold. A parameter here would have been
    /// a second place the answer could come from, and it was — the caller passed
    /// cumulative totals and the standing could never fall (finding 228).</para>
    /// <summary>
    /// One month enters the record (SDD-012 §4b's R23.1 amendment).
    ///
    /// <para>Called from stage 8, where the month's flaring and production are
    /// both final, and BEFORE <see cref="Of"/> is read there — a lender prices
    /// against the month that has just happened. Stage 9 then ages the window,
    /// so the declared order within a tick is OBSERVE then AGE.</para>
    ///
    /// <para>Here rather than on the stage that ages, because the loop that
    /// accounts the flaring is owned by the field module and the standing by the
    /// HSE module, and the field already requires this contract. Reaching the
    /// other way would put a cycle in the manifest graph.</para>
    /// </summary>
    public void Observe(Mass flared, SurfaceVolume produced) =>
        standing.Observe(flared, produced);

    public double Of() =>
        standing.Standing(
            [(FlaringWeight,
              1.0 - record.Standing(standing.WindowedFlared, standing.WindowedProduced))])
        / FlaringWeight;

    /// <summary>Flaring is the whole of the intensity term today, so it carries
    /// the full hundred points §4b's formula distributes across intensities.
    /// Emissions and methane join it when equipment vents rather than burns.</summary>
    private const double FlaringWeight = 100.0;
}

/// <summary>
/// Stage 4 (SDD-012 §4b). A threat materialises and the barriers decide whether
/// the field hears a warning or an incident. The rate is driven by CONDITION, so
/// a maintained field is not merely safer — it is not being asked to roll.
/// </summary>
internal sealed class ThreatStage(
    OGSim.Integrity.BowTie bowTie,
    OGSim.Integrity.AssetIntegrity integrity,
    OGSim.Integrity.EsgStanding standing) : ITickStage
{
    public StageId Id => StageId.Availability;

    public void Execute(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        double worst = 1.0;
        for (int i = 0; i < Defaults.Barriers.Count; i++)
        {
            IReadOnlyList<EntityId<IFlowElement>> elements = Defaults.Barriers[i].Elements;

            for (int e = 0; e < elements.Count; e++)
            {
                double condition = integrity.ConditionOf(elements[e]);
                if (condition < worst) worst = condition;
            }
        }

        if (worst >= 1.0) return;

        if (!bowTie.Materialises(Defaults.ThreatRateAtFailure * (1.0 - worst))) return;

        OGSim.Integrity.ThreatResolution resolved = bowTie.Resolve(
            Defaults.ContainmentThreat,
            Defaults.Barriers,
            barrier => barrier.StrengthGiven(
                integrity.ConditionOf, Defaults.CrewCompetency, Defaults.ProcedureCompliance));

        if (resolved.Outcome == OGSim.Integrity.ThreatOutcome.TopEvent)
            standing.RecordIncident(Defaults.TopEventPoints);
    }
}

/// <summary>Stage 9: the record ages, which is the rehabilitation §4b promises
/// and the only thing that happens to it when nothing goes wrong.</summary>
internal sealed class EsgStage(OGSim.Integrity.EsgStanding standing) : ITickStage
{
    public StageId Id => StageId.HseRegulation;

    public void Execute(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        standing.Age(Duration.FromTicks(1.0));
    }
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
    AuditTrail audit, SimulationClock clock, IRandomSource random) : EngineModule(Declare(
    "diagnostics",

    // The clock and the RNG join the trail here for the same reason it is here:
    // they are kernel facilities every module may need and none may own, and
    // composing them makes them declared dependencies rather than the ambient
    // singletons law L2 forbids.
    // THE CONCRETE TRAIL BESIDE THE INTERFACE, exactly as the clock is. `Prune`
    // and `RestoreFrom` are on `AuditTrail` and not on `IAuditTrail`, so a module
    // that takes the interface can record and query and cannot rewrite history —
    // and the two things entitled to (the pipeline, and a load) ask for the
    // concrete by name (SDD-001 §5).
    provides:
    [
        typeof(IAuditTrail), typeof(AuditTrail),
        typeof(SimulationClock), typeof(IRandomSource),
    ],
    requires: [],
    ownsState: NothingOwnedYet,
    stages: NoStagesYet))
{
    public override void Compose(IModuleComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);

        composition.Provide<IAuditTrail>(audit);
        composition.Provide(audit);
        composition.Provide(clock);
        composition.Provide(random);
    }
}
