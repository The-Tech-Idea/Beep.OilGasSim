// R20c.7 — the loop (design 03 §6 stages 5, 6 and 8).
//
// A well produces, the compartment it drained loses pressure, the oil is sold
// and the cash lands in the ledger. Next month the same well produces less
// because of what this month took. That circle is the game; everything else in
// the engine exists to make it interesting.
//
// It lives in COMPOSITION because it is the one place entitled to know that
// wells and compartments are both real (design 03 §8). Neither module can see
// the other: OGSim.Wells cannot name a compartment, OGSim.Subsurface cannot name
// a completion, and the truth boundary between them is an assembly boundary
// rather than a convention. What crosses is numbers, passed by the layer above.

using OGSim.Company;
using OGSim.Contracts;
using OGSim.Kernel;
using OGSim.Subsurface;
using OGSim.Wells;

namespace OGSim.Composition;

/// <summary>
/// What the field is worth per stock-tank cubic metre, and what it costs to run.
/// Content in a finished game; passed explicitly here because law L2 forbids a
/// dependency with a default.
/// </summary>
public sealed record FieldEconomics(
    /// <summary>PER TONNE, because custody meters MASS (SDD-017 §2's
    /// <c>MarketView.Prices</c>). A price per volume would have to pick a
    /// density, and the one place a density belongs is where mass becomes the
    /// barrels a player reads.</summary>
    Money OilPricePerTonne,

    /// <summary>The standing charge: people, power, chemicals, the road. Paid
    /// whether or not the field produced, which is the whole shape of the
    /// late-life decision.</summary>
    Money FixedOperatingCostPerTick,

    /// <summary>
    /// What it costs to lift a tonne of LIQUID — oil and water alike.
    ///
    /// <para>The variable half, and the one that ends a field's life: a well at
    /// 90% water cut pays to produce nine barrels of nothing for every one that
    /// sells. A flat operating cost cannot express that, so watering out would
    /// be something a player watches rather than something they answer.</para>
    /// </summary>
    Money LiftingCostPerTonne,

    /// <summary>
    /// What a cubic metre of IMPORTED flood water costs to lift, filter,
    /// deaerate and pump (SDD-003 §3.1d's R20d.24 amendment).
    ///
    /// <para>The price of the decision. A waterflood that was free would be a
    /// slider every player pushed to its limit on the first month of production,
    /// which is not a decision — what makes it one is that the money buys
    /// recovery years from now and is spent today, against a well that plugs
    /// faster the harder it is used.</para>
    ///
    /// <para>Produced water is NOT charged here: it is already paid for by the
    /// lifting cost that brought it up the hole, and charging it twice would
    /// make reinjecting cheaper to stop than to continue.</para>
    /// </summary>
    Money InjectionWaterCostPerCubicMetre);

/// <summary>
/// One element of the chain, as a player watches it (SDD-017 §2).
///
/// <para><b>Throughput is the flow and deferral is the jam</b>, and together
/// they are the whole of "where is my production going and what is stopping
/// it" — the question a production-chain game is played on. Both come off the
/// solver's own report: §8's attribution measured the refusal against what the
/// completions WANTED, so this is a number the solve committed to rather than a
/// second opinion computed beside it.</para>
///
/// <para>An empty <see cref="Deferred"/> list is an element that refused
/// nothing. That is the normal state and it is worth being able to see: a chain
/// where exactly one row has entries is a chain with exactly one
/// bottleneck.</para>
/// </summary>
public sealed record ChainElementView(
    EntityRef Element,
    string DisplayId,
    Mass Throughput,
    IReadOnlyList<(ConstraintKind Kind, Mass Deferred)> Deferred,

    /// <summary>
    /// What condition it is in, 0..1 (SDD-012 §1) — or NULL where nobody can
    /// tell, which is every element without a condition-monitoring kit fitted
    /// (C14, §3's R20d.26.4 amendment).
    ///
    /// <para>Wear is not something a company knows about its plant for free; it
    /// is something it instruments for. Publishing it unconditionally made the
    /// kit that "enables condition-based maintenance" content nobody needed, and
    /// made the strategy that pays the one strategy nobody has to buy.</para>
    ///
    /// <para>Null is UNKNOWN and never "as new". A host showing 1.0 for an
    /// unmonitored vessel would be reporting truth the company has not
    /// purchased, which is the same door the whole belief system exists to keep
    /// shut.</para>
    /// </summary>
    double? Condition,

    /// <summary>Whether it is out of service — the reason a row that used to
    /// carry the whole field's oil is suddenly carrying none.</summary>
    bool Failed)
{
    // Finding 131.
    public bool Equals(ChainElementView? other) =>
        other is not null && Element == other.Element && DisplayId == other.DisplayId
        && Throughput == other.Throughput
        && Condition == other.Condition && Failed == other.Failed
        && Structural.Equal(Deferred, other.Deferred);

    public override int GetHashCode() =>
        HashCode.Combine(Element, DisplayId, Throughput, Condition, Failed,
                         Structural.HashOf(Deferred));

    /// <summary>Whether this element refused anything this tick — what a host
    /// highlights, and what "the chain is jammed here" means.</summary>
    public bool IsBottleneck => Deferred.Count > 0;
}

/// <summary>
/// One element's tick, accumulating across segments.
///
/// <para>A class rather than a record because it is a running total the solve
/// adds to segment by segment; it becomes an immutable
/// <see cref="ChainElementView"/> at the close.</para>
/// </summary>
internal sealed class ChainElement(EntityId<IFlowElement> element)
{
    private readonly List<(ConstraintKind Kind, Mass Deferred)> _deferred = [];

    public EntityId<IFlowElement> Element { get; } = element;

    public double Throughput { get; set; }

    /// <summary>Accumulated per constraint kind, because the gas leg and the
    /// liquid leg of one vessel bind independently and a player debottlenecking
    /// needs to know WHICH (R8-V2).</summary>
    public void Refuse(ConstraintKind kind, Mass deferred)
    {
        for (int i = 0; i < _deferred.Count; i++)
        {
            if (_deferred[i].Kind != kind) continue;

            _deferred[i] = (kind, new Mass(_deferred[i].Deferred.Kilograms + deferred.Kilograms));
            return;
        }

        _deferred.Add((kind, deferred));
    }

    public ChainElementView Published(
        Func<EntityId<IFlowElement>, string> nameOf, double? condition, bool failed) =>
        new(new EntityRef(EntityKind.FlowElement, Element.Value),
            nameOf(Element),
            new Mass(Throughput),
            [.. _deferred],
            condition,
            failed);
}

/// <summary>
/// What stage 5 solved, waiting for stage 6 to commit it.
///
/// <para>A shared buffer rather than a static or a direct call: stage 5 and
/// stage 6 belong to different modules and run at different points in the tick,
/// and design 03 §6 is explicit that solve and commit are separated so a failed
/// solve commits nothing. Both modules require this one instance, so the
/// hand-off is a declared dependency the composer orders rather than an
/// arrangement two stages have privately agreed.</para>
/// </summary>
internal sealed class TickProduction
{
    private readonly List<CompartmentWithdrawal> _withdrawals = [];

    public IReadOnlyList<CompartmentWithdrawal> Withdrawals => _withdrawals;

    /// <summary>Replaces, never appends: a tick that produced nothing must not
    /// commit last month's volumes.</summary>
    public void Set(IReadOnlyList<CompartmentWithdrawal> withdrawals)
    {
        ArgumentNullException.ThrowIfNull(withdrawals);

        _withdrawals.Clear();
        for (int i = 0; i < withdrawals.Count; i++) _withdrawals.Add(withdrawals[i]);
    }
}

/// <summary>
/// Stage 5 → 6 → 8, wired. Each stage is contributed separately so the tick runs
/// them in design 03 §6's declared order; this holds the state they share.
/// </summary>
internal sealed class ProductionLoop : IStateOwner
{
    private readonly SubsurfaceState _subsurface;
    private readonly WellsState _wells;
    private readonly CompanyState _company;
    private readonly TickProduction _production;
    private readonly IFluidPropertyModel _fluid;
    private readonly IAuditTrail _audit;
    private readonly FieldEconomics _economics;
    private readonly Temperature _reservoirTemperature;
    private readonly IFlowSolver _solver;
    private readonly IFlowElementRegistry _network;
    private readonly OGSim.Environment.WeatherState _weather;
    private readonly Density _surfaceDensity;
    private readonly int _materialCount;

    // Which elements meter. A set, because stage 5 asks it once per element per
    // segment — and because the loop must not ask an element what it IS, only
    // whether composition told it this one is a meter.
    private readonly HashSet<EntityId<IFlowElement>> _meters = [];

    // Stage 5's answer, held for stage 6. Cleared at the start of every solve so
    // a tick that produced nothing cannot commit last month's volumes.
    private readonly Dictionary<EntityId<IReservoirCompartmentEntity>, double> _byCompartment = [];

    // THE LAST SEGMENT'S CONVERGED STATE PER COMPLETION (SDD-017 §2's R21.6
    // amendment) — what a read model reconstructs an operating point from.
    // Overwritten every segment, so by the tick's own close this holds the
    // final one; cleared at the same SolveFlow boundary `_byCompartment` is,
    // so a completion absent from every segment this tick (never drilled far
    // enough, or shut out of the whole month by an upstream failure) reports
    // none rather than a stale month.
    private readonly Dictionary<EntityId<ICompletion>, CompletionState> _lastSolved = [];

    // The chain, rebuilt each tick in the solver's topological order.
    private readonly List<ChainElement> _chain = [];

    private readonly Func<EntityId<IFlowElement>, string> _names;

    /// <summary>What an element is called, for a record a person will read.
    /// The loop owns the naming function, so asking it is one owner rather than
    /// a second copy handed to every stage that needs a label (law L5).</summary>
    public string NameOf(EntityRef element) =>
        _names(new EntityId<IFlowElement>(element.Value));

    /// <summary>
    /// A well's converged state as of the last segment it solved this tick, or
    /// <c>null</c> if it solved none (SDD-017 §2's R21.6 amendment). The loop
    /// owns the solve, so this is the one door a read model reconstructs an
    /// operating point through rather than a second copy of the same lookup.
    /// </summary>
    public CompletionState? LastSolvedStateOf(EntityId<ICompletion> well) =>
        _lastSolved.TryGetValue(well, out CompletionState? state) ? state : null;

    private readonly OGSim.Integrity.AssetIntegrity _integrity;

    private readonly OGSim.Facilities.Tank _tank;
    private readonly OGSim.Facilities.ExportTerminal _terminal;
    private readonly OGSim.Facilities.CustodyTransferPoint _custody;

    private OGSim.Kernel.Composition _stored;
    private Allocation _tankProvenance;

    // SDD-006 §7a.4's finding-269 amendment — how many ACTIVE days the cargo
    // now loading has taken, zero when the berth is idle. Persisted: a save
    // mid-overrun that came back at zero would let a player launder
    // demurrage by saving and loading (the same class SDD-013 §4's covenant
    // clock carve-out exists to close, finding 210).
    private double _cargoActiveDays;

    // What the field handled this tick, per material — what a lifting cost is
    // charged on, and the reason a watered-out field stops paying.
    private readonly double[] _handled;

    private readonly IFiscalRegime _regime;
    private readonly IPriceModel _prices;
    private readonly IObligationRegistry _obligations;
    private readonly ReservesBook _reserves;
    private readonly OGSim.Facilities.GasCapture _chainGasPlant;
    private readonly OGSim.Wells.Injector _disposal;
    private readonly Money _gasPrice;

    // Reset each tick by the plan that consumes it.
    private double _disposedThisTick;

    private readonly OGSim.Facilities.WaterIntake _intake;

    // THE FLOOD, a tick behind (SDD-003 §3.1d's R20d.24b amendment). Both are
    // stage 5's answers and the intake is commanded before stage 5 runs, so the
    // target is built from last month's — design 03 §6.1's declared lag, used
    // the way stage 4 uses it. It is the safety property rather than a
    // compromise: a field that produced nothing has no voidage to replace and
    // buys no water, so an idle field can never hand the solver a constraint it
    // cannot relieve.
    private double _voidageLastTick;
    private double _producedWaterLastTick;

    // AND THE ROCK'S OWN CEILING, which is the harder of the two limits and the
    // one that halts a tick rather than merely disappointing a player. See
    // ReservoirRoom.
    private double _reservoirRoom;
    private readonly OGSim.Company.MarketState _market;
    private readonly IRandomStream _priceStream;
    private readonly IReadOnlyList<int> _liquidOrdinals;
    private readonly Func<bool> _isAbandoned;

    public ProductionLoop(
        SubsurfaceState subsurface,
        WellsState wells,
        CompanyState company,
        TickProduction production,
        IFluidPropertyModel fluid,
        IAuditTrail audit,
        IFlowSolver solver,
        IFlowElementRegistry network,
        IReadOnlyList<EntityId<IFlowElement>> meteredPoints,
        Func<EntityId<IFlowElement>, string> names,

        // READ, never written: the chain view reports condition and stage 4 is
        // what changes it (law L5). The loop asks a question it does not own the
        // answer to, which is the whole reason this is a dependency rather than
        // a field.
        OGSim.Integrity.AssetIntegrity integrity,
        OGSim.Facilities.Tank tank,
        OGSim.Facilities.ExportTerminal terminal,
        OGSim.Facilities.CustodyTransferPoint custody,
        IFiscalRegime regime,
        IPriceModel prices,
        IRandomStream priceStream,
        OGSim.Company.MarketState market,
        IObligationRegistry obligations,
        ReservesBook reserves,
        OGSim.Facilities.GasCapture gasPlant,
        OGSim.Wells.Injector disposal,
        OGSim.Facilities.WaterIntake intake,
        Money gasPrice,
        IReadOnlyList<int> liquidOrdinals,
        Func<bool> isAbandoned,
        FieldEconomics economics,
        Temperature reservoirTemperature,
        OGSim.Environment.WeatherState weather,
        Density surfaceDensity,
        int materialCount)
    {
        ArgumentNullException.ThrowIfNull(subsurface);
        ArgumentNullException.ThrowIfNull(wells);
        ArgumentNullException.ThrowIfNull(company);
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(fluid);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(solver);
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(meteredPoints);
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(tank);
        ArgumentNullException.ThrowIfNull(terminal);
        ArgumentNullException.ThrowIfNull(custody);
        ArgumentNullException.ThrowIfNull(intake);
        ArgumentNullException.ThrowIfNull(regime);
        ArgumentNullException.ThrowIfNull(liquidOrdinals);
        ArgumentNullException.ThrowIfNull(isAbandoned);
        ArgumentNullException.ThrowIfNull(economics);

        _subsurface = subsurface;
        _wells = wells;
        _company = company;
        _production = production;
        _fluid = fluid;
        _audit = audit;
        _solver = solver;
        _network = network;
        _names = names;
        _integrity = integrity;
        _tank = tank;
        _terminal = terminal;
        _custody = custody;
        _regime = regime;
        _prices = prices;
        _priceStream = priceStream;
        _market = market;
        _obligations = obligations;
        _reserves = reserves;
        _chainGasPlant = gasPlant;
        _disposal = disposal;
        _intake = intake;
        _gasPrice = gasPrice;
        _liquidOrdinals = liquidOrdinals;
        _isAbandoned = isAbandoned;
        _economics = economics;
        _handled = new double[materialCount];
        _reservoirTemperature = reservoirTemperature;
        _weather = weather;
        _surfaceDensity = surfaceDensity;
        _materialCount = materialCount;

        for (int i = 0; i < meteredPoints.Count; i++) _meters.Add(meteredPoints[i]);

        Delivered = OGSim.Kernel.Composition.Zero(materialCount);
        _stored = OGSim.Kernel.Composition.Zero(materialCount);
        _tankProvenance = Allocation.FromSingle(new EntityRef(EntityKind.Compartment, 1));
    }

    /// <summary>Stock-tank oil produced in the tick just solved — what the read
    /// model reports and what a test asserts on.</summary>
    public SurfaceVolume ProducedThisTick { get; private set; } = new(0.0);

    /// <summary>
    /// Everything this company has ever lifted. What reserves are measured
    /// against — a field's remaining barrels are what it will ultimately give
    /// less what has already come up.
    /// </summary>
    public SurfaceVolume CumulativeProduced { get; private set; } = new(0.0);

    /// <summary>What crossed the custody meter this tick — the ONLY mass stage 8
    /// is allowed to price (SDD-009 §1).</summary>
    public OGSim.Kernel.Composition Delivered { get; private set; }

    /// <summary>
    /// The field's water cut, from what the chain delivered last tick
    /// (SDD-012 §1's k_w term).
    ///
    /// <para>DERIVED, never stored (law L5): it is a ratio of two numbers this
    /// object already owns, and a second copy updated beside them would be one
    /// more thing to forget. Stage 4 reads it a tick behind, which is design
    /// 03 §6.1's declared lag and the right way round — equipment corrodes in
    /// the service it has HAD, not the service it is about to get.</para>
    /// </summary>
    public double WaterCut
    {
        get
        {
            double water = Delivered[Defaults.WaterOrdinal].KgPerSecond;
            double oil = Delivered[Defaults.OilOrdinal].KgPerSecond;
            double liquid = water + oil;

            // A field that has produced nothing has no cut, rather than an
            // undefined one. Said here because the division below is the only
            // place it could go wrong.
            return liquid <= 0.0 ? 0.0 : water / liquid;
        }
    }

    // ------------------------------------------------------------ the flood

    /// <summary>
    /// How much of the voidage the company is trying to replace — the player's
    /// lever (SDD-003 §3.1d's R20d.24 amendment).
    ///
    /// <para>Zero is today's engine: produced water goes back down the hole
    /// because there is nowhere else for it, and nothing is bought. One replaces
    /// every reservoir cubic metre the field takes out, which is what a
    /// waterflood IS.</para>
    /// </summary>
    public double VoidageReplacement { get; private set; }

    /// <summary>The set point, from the command that carries it. Not validated
    /// here: the validator has already refused what is meaningless (R1 §2.5).</summary>
    public void SetVoidageReplacement(double ratio) => VoidageReplacement = ratio;

    // ---------------------------------------------------- the flood, saved
    //
    // ALMOST EVERYTHING ON THIS CLASS IS PER-TICK SCRATCH, rebuilt from the
    // solve every month, and SDD-013 §4 is explicit that derived state must
    // never be saved. THREE FIELDS ARE NOT: the voidage set point is a standing
    // player DECISION that outlives any tick, and the two "last tick" numbers it
    // is applied against are read at the start of the next one.
    //
    // Unsaved, a reloaded game silently stopped flooding — it kept the water
    // already injected, so it produced identically for a month and simply
    // stopped BUYING any, which showed up as an opex gap and nothing else
    // (R20d.12). A lever a player set twenty years earlier, forgotten by a
    // reload.

    public StateKey Key { get; } = new("field.flood");

    /// <summary>2 (SDD-006 §7a.4's finding-269 amendment): the cargo now
    /// loading's active days joined the block.</summary>
    public int SchemaVersion => 2;

    /// <summary>Nothing has to be back before this is (SDD-013 §2b).</summary>
    public IReadOnlyList<StateKey> RestoreAfter => [];

    public void Capture(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteDouble("voidage-replacement", VoidageReplacement);
        writer.WriteDouble("voidage-last-tick", _voidageLastTick);
        writer.WriteDouble("produced-water-last-tick", _producedWaterLastTick);

        // THE CARGO NOW LOADING'S ACTIVE DAYS (SDD-006 §7a.4's finding-269
        // amendment) — zero when the berth is idle between episodes.
        writer.WriteDouble("cargo-active-days", _cargoActiveDays);

        // WHICH COMPARTMENTS THE FLOOD IS SPLIT BETWEEN, and in what proportion.
        // The fourth cross-tick field on this class and the one that made a
        // reloaded field skip a month of injection: `ReservoirRoom` walks this
        // list, so an empty one leaves its cap at infinity and returns ZERO room
        // — a flood that stopped for a month and then caught up (S013-8).
        // THE TWO RUNNING TOTALS. Neither is scratch and neither was saved: what
        // a company has flared over its life is what the ESG record is scored on
        // and what its debt is priced against, and what it has PRODUCED is what
        // the bank lends against. A reload reset both to zero, so a
        // forty-year field came back with the flaring record of a new one
        // (S013-9).
        // WHAT THE CHAIN DELIVERED LAST TICK, and it is state rather than
        // scratch for one reason: design 03 §6.1's DECLARED LAG. Stage 4 ages
        // equipment on the service it has HAD, so last month's rates are an
        // input to next month — and a reloaded field read zeroes and aged itself
        // for a month as though it were dry (S013-9, measured at a cut of
        // 7.77e-6 against 0).
        //
        // SAVED AT ITS SOURCE, not at its ratio. `WaterCut` is derived from this
        // and saving the cut as well would give one fact two owners (L5); the
        // cut stays a property and this is what it reads.
        writer.WriteInt64("delivered-count", Defaults.MaterialCount);

        for (var i = 0; i < Defaults.MaterialCount; i++)
            writer.WriteDouble(
                "delivered." + i.ToString("D2", System.Globalization.CultureInfo.InvariantCulture),
                Delivered[new MaterialId(i)].KgPerSecond);

        writer.WriteDouble("cumulative-flared", CumulativeFlared.Kilograms);
        writer.WriteDouble("cumulative-produced", CumulativeProduced.CubicMetres);

        // THE DISPOSAL WELL'S PLUGGING, which IS its cumulative injection: §6c's
        // impairment scales with what has been put away against the reference
        // volume, so a restored injector came back with a clean formation
        // however many years it had been used. It is written here rather than in
        // a block of its own because facilities own no state — the day they do,
        // this moves and the key goes with it (S013-9).
        writer.WriteDouble("disposal-injected", _disposal.CumulativeInjected.CubicMetres);

        writer.WriteInt64("flood-share-count", _floodShares.Count);

        for (int i = 0; i < _floodShares.Count; i++)
        {
            string at = "flood-share."
                + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture) + ".";

            writer.WriteInt64(at + "compartment", (long)_floodShares[i].Item1.Value);
            writer.WriteDouble(at + "share", _floodShares[i].Item2);
        }
    }

    public void Restore(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        VoidageReplacement = reader.ReadDouble("voidage-replacement");
        _voidageLastTick = reader.ReadDouble("voidage-last-tick");
        _producedWaterLastTick = reader.ReadDouble("produced-water-last-tick");
        _cargoActiveDays = reader.ReadDouble("cargo-active-days");

        var materials = (int)reader.ReadInt64("delivered-count");
        var delivered = new double[materials];

        for (var i = 0; i < materials; i++)
            delivered[i] = reader.ReadDouble(
                "delivered." + i.ToString("D2", System.Globalization.CultureInfo.InvariantCulture));

        Delivered = OGSim.Kernel.Composition.Validated([.. delivered]);

        CumulativeFlared = new Mass(reader.ReadDouble("cumulative-flared"));
        CumulativeProduced = new SurfaceVolume(reader.ReadDouble("cumulative-produced"));

        _disposal.RestoreTo(new ReservoirVolume(reader.ReadDouble("disposal-injected")));

        _floodShares.Clear();

        long count = reader.ReadInt64("flood-share-count");

        for (long i = 0; i < count; i++)
        {
            string at = "flood-share."
                + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture) + ".";

            _floodShares.Add((
                new EntityId<IReservoirCompartmentEntity>(
                    (ulong)reader.ReadInt64(at + "compartment")),
                reader.ReadDouble(at + "share")));
        }
    }

    /// <summary>What the flood actually bought this tick, in reservoir m³ — and
    /// how much room the injector had left. Both, because a target met and a
    /// target clamped are different situations with different answers, and a
    /// player shown only the target could not tell them apart.</summary>
    public ReservoirVolume ImportedThisTick => new(_importedThisTick);

    /// <summary>How much more water the field can take this month, whichever
    /// limit binds — the well's or the rock's.</summary>
    public ReservoirVolume InjectionHeadroom
    {
        get
        {
            double well = Headroom();

            return new ReservoirVolume(well < _reservoirRoom ? well : _reservoirRoom);
        }
    }

    private double _importedThisTick;

    /// <summary>
    /// What the injector will still take this month once the produced water has
    /// had its share.
    ///
    /// <para>THE HEADROOM FORM RATHER THAN THE ACCEPTANCE FORM, and the
    /// difference is the whole stability of the mechanic. Clamping at the full
    /// acceptance would let a flood take the injectivity the produced water
    /// needs; S3 would throttle the producers to relieve it, which removes the
    /// voidage that justified the flood, which stops the flood, which lets the
    /// producers back — a field oscillating between drowning and dry. The flood
    /// gets what the disposal duty leaves and no more.</para>
    /// </summary>
    private double Headroom()
    {
        double room =
            (_disposal.Acceptance.CubicMetresPerSecond * TickSeconds) - _producedWaterLastTick;

        return room > 0.0 ? room : 0.0;
    }

    /// <summary>
    /// Stage 5's first act: tell the intake what to lift.
    ///
    /// <para><c>target = VRR · voidage</c>, less the water the field is already
    /// putting back, clamped by the injector's headroom (SDD-003 §3.1d). Every
    /// term is last tick's — see the note on the fields it reads.</para>
    /// </summary>
    private void CommandTheIntake()
    {
        _reservoirRoom = ReservoirRoom();

        double target =
            (VoidageReplacement * _voidageLastTick) - _producedWaterLastTick;

        if (target < 0.0) target = 0.0;

        double well = Headroom();
        if (target > well) target = well;

        // AND THE ROCK'S CEILING, which is not a disappointment but a halt.
        // SDD-003 §3.1's bisection searches [floor, discovery pressure] and
        // FAULTS when there is no root in it — so a compartment given more
        // replacement than it has voidage does not produce a wrong number, it
        // stops the tick. Measured: VRR 1.0 on the shipped water-drive field
        // faulted in exactly that way, because the aquifer was already
        // replacing most of the voidage and the flood replaced the rest twice.
        //
        // It nets the aquifer off for free, which is why there is no influx term
        // here: a field held up by strong natural water has little room, so a
        // company that orders a flood on one buys almost nothing. That is the
        // right answer rather than a special case.
        if (target > _reservoirRoom) target = _reservoirRoom;

        _intake.Command(new ReservoirRate(target / TickSeconds));
    }

    /// <summary>
    /// Stage 5. Refresh every well with the pressure its compartment is at NOW,
    /// then solve the NETWORK — once per segment, over the elements that segment
    /// has available (SDD-002 §9).
    ///
    /// <para>The refresh is why decline happens: without it a completion holds
    /// the pressure it was built with and produces at month one's rate
    /// forever.</para>
    ///
    /// <para>What this replaced is the whole of R20d.1. A per-well
    /// <c>SolveOperatingPoint</c> against a hard-coded backpressure could not
    /// express a separator's set point, a capacity that binds, a header that
    /// couples one well's rate to another's, or a segment where something is
    /// unavailable. All four now reach the reservoir because they are in the
    /// network the solver walks rather than in a document.</para>
    /// </summary>
    public void SolveFlow(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _wells.RefreshFromReservoir(
            _subsurface.TruePressureOf,
            _fluid.Rs,
            compartment => _subsurface.TrueWaterCutOf(
                compartment, WaterViscosity, _fluid.MuOil(_subsurface.TruePressureOf(compartment))),
            _reservoirTemperature);

        // Stage 4 owns the plan and stage 5 consumes it. Missing means the
        // availability stage did not run — a composition defect, not a tick with
        // no segments, and solving the whole month as one unplanned interval
        // would silently include whatever was unavailable.
        if (context.Segments is not SegmentPlan plan)
            throw new InvariantFault("SDD-002 §9", null,
                "stage 5 ran with no segment plan; stage 4 builds it and must run first");

        // BEFORE THE FIRST SEGMENT, because the commanded rate is what the
        // intake sources and every segment must lift at the same one — a flood
        // that changed rate at a failure boundary would be responding to a
        // month it has not solved yet.
        CommandTheIntake();

        _byCompartment.Clear();
        _lastSolved.Clear();
        _chain.Clear();
        _importedThisTick = 0.0;
        FlaredThisTick = new Mass(0.0);
        _stored = OGSim.Kernel.Composition.Zero(_materialCount);
        _tank.ForgetPromises();
        Array.Clear(_handled);
        double[] delivered = new double[_materialCount];

        var ambientDayDegrees = 0.0;
        var severityDayWeighted = 0.0;
        var weatheredDays = 0;

        for (int i = 0; i < plan.Segments.Count; i++)
        {
            Segment segment = plan.Segments[i];

            // THE SEGMENT'S OWN WEATHER (SDD-016 §3), which was two constants:
            // a fixed 15 °C ambient and a severity of literally zero, handed to
            // the solver every month of every game while `WeatherState` computed
            // the real seasonal values a few metres away and only the read model
            // read them.
            //
            // **What this does and does not buy, stated plainly.** Nothing in the
            // shipped chain derates on either yet — the `Compressor` that reads
            // §3.3's k_derate is built and not composed, and the berth that would
            // close on severity is R11's — so today these move stream
            // temperatures on zero-flow elements and little else. It is fixed
            // here anyway because the alternative is worse than a gap: the first
            // element that DOES read ambient would derate against 15 °C for forty
            // years and look entirely plausible doing it, which is the
            // accepted-then-ignored defect arriving pre-installed (finding 233).
            Temperature ambient = AmbientOver(segment);
            double severity = SeverityOver(segment);

            ambientDayDegrees += ambient.Kelvin * segment.DurationDays;
            severityDayWeighted += severity * segment.DurationDays;
            weatheredDays += segment.DurationDays;

            SolveReport report = _solver.Solve(
                new SegmentContext(segment.DurationDays, ambient, severity),
                _network.ViewFor(segment.Available));

            // DURATION-WEIGHTED (SDD-002 §9). Rates are per second and a segment
            // is a whole number of days on the /30ths grid, so the weight is
            // exact rather than nearly.
            Accumulate(report, segment.DurationDays * SecondsPerDay, delivered);
        }

        // What the month was actually solved at, for the projection to report
        // rather than compute a second way (law L5).
        AmbientThisTick = new Temperature(ambientDayDegrees / weatheredDays);
        SeverityThisTick = severityDayWeighted / weatheredDays;

        Delivered = OGSim.Kernel.Composition.Validated([.. delivered]);
        ProducedThisTick = new SurfaceVolume(
            Delivered.Total.KgPerSecond / _surfaceDensity.KgPerCubicMetre);

        PublishWithdrawals();
    }

    /// <summary>
    /// One segment's answer, added to the tick's.
    ///
    /// <para>Withdrawal comes from the completion's converged RATE and not from
    /// the mass it put on the network: a compartment is emptied of reservoir
    /// volume, and going mass → surface → reservoir to recover a number the
    /// solver already holds would round twice for nothing.</para>
    /// </summary>
    private void Accumulate(SolveReport report, double seconds, double[] delivered)
    {
        // THE CHAIN, as a player watches it: what crossed each element and what
        // it refused. Read straight off the report rather than recomputed —
        // SDD-002 §8's attribution already measured the refusal against what the
        // completions WANTED, and a second opinion here would be a different
        // number wearing the same name.
        for (int i = 0; i < report.Solutions.Count; i++)
        {
            ElementSolution solution = report.Solutions[i];

            TransformResult converged = solution.Converged;

            // EVERYTHING THAT LEFT, in any form — outlets, fuel burned, mass
            // disposed of. By SDD-002 §5's element conservation that equals what
            // entered, so it is exactly "what crossed this element".
            //
            // Outlets alone would read ZERO for a terminal sink: a flare has no
            // outlet ports, so a chain that measured only outlets would show the
            // flare passing nothing while it burned the field's entire gas
            // production.
            // EVERYTHING THIS COMPANY HAS EVER BURNED. Flaring is the one
            // term of SDD-012 §4's ESG standing that has a subject today, and
            // it is charged against the whole history rather than the month —
            // a record is what a lender has watched, and one bad month should
            // no more re-price a facility than one good month should clear it.
            // WHAT WENT DOWN THE DISPOSAL WELL. Captured here because stage 6
            // puts it back into the rock it came from (SDD-002 §9), and the
            // solve is the only place that knows how much the injector actually
            // took after its own injectivity limited it.
            if (solution.Element == _disposal.Id)
                _disposedThisTick +=
                    converged.Disposed.Discharged.Total.KgPerSecond * seconds
                    / PhysicalConstants.WaterDensityKgPerM3;

            // AND HOW MUCH OF IT WAS BOUGHT (SDD-003 §3.1d's R20d.24b
            // amendment). The two are allocated to compartments by different
            // rules at stage 6, and the intake's own Sourced is what separates
            // them: it is the only other thing feeding the injector, so what the
            // injector discharged less what the intake made is what the field
            // produced, by construction rather than by a second estimate.
            if (solution.Element == _intake.Id)
                _importedThisTick +=
                    converged.Sourced.Total.KgPerSecond * seconds
                    / PhysicalConstants.WaterDensityKgPerM3;

            double burned = converged.Disposed.Flared.Total.KgPerSecond * seconds;

            CumulativeFlared = new Mass(CumulativeFlared.Kilograms + burned);
            FlaredThisTick = new Mass(FlaredThisTick.Kilograms + burned);

            double throughput =
                converged.FuelConsumed.Total.KgPerSecond
                + converged.Disposed.Flared.Total.KgPerSecond
                + converged.Disposed.Vented.Total.KgPerSecond
                + converged.Disposed.Discharged.Total.KgPerSecond;

            IReadOnlyList<MaterialStream> outlets = converged.Outlets;
            for (int o = 0; o < outlets.Count; o++)
                throughput += outlets[o].MassRates.Total.KgPerSecond;

            Flowing(solution.Element).Throughput += throughput * seconds;
        }

        for (int i = 0; i < report.Deferrals.Count; i++)
        {
            Flowing(report.Deferrals[i].Element)
                .Refuse(report.Deferrals[i].Kind, report.Deferrals[i].Deferred);

            // AND IT GOES IN THE TRAIL, which is the fairness record for the one
            // question a player will actually argue with: why did my field make
            // less oil this month? `ChainElementView.Deferred` already publishes
            // this, and a projection cannot answer it — it is a snapshot of the
            // current tick, so it never speaks about month 214, and it carries no
            // cause chain (design 09 §4.2–4.3, finding 202).
            //
            // NOTHING IN THE ENGINE WROTE A `ConstraintBinding` BEFORE THIS.
            // SDD-001 §5 names the category in the retention partition as
            // per-tick per-element detail, and 09 §4.4's pruning computes a cause
            // closure over it so that "the tick-4 constraint that explains a
            // tick-400 shut-in" survives — machinery built, tested against
            // hand-made entries, and joined to nothing the engine produced.
            //
            // HERE rather than at the close: this is where the deferral is known,
            // and it runs once per segment because the solve did. The publish
            // loop looks like the tidier home and is the wrong one — it builds
            // the READ MODEL, so recording there would put a side effect in a
            // rebuild-from-state and write twice over anything that asked for the
            // chain twice.
            _audit.Record(
                AuditCategory.ConstraintBinding,
                new EntityRef(EntityKind.FlowElement, report.Deferrals[i].Element.Value),
                cause: null,
                new Dictionary<string, AuditValue>(StringComparer.Ordinal)
                {
                    ["element"] = new(_names(report.Deferrals[i].Element)),
                    ["kind"] = new(report.Deferrals[i].Kind.ToString()),
                    ["deferred-kg"] = new(Format(report.Deferrals[i].Deferred.Kilograms)),
                    ["segment-seconds"] = new(Format(seconds)),
                });
        }

        // WHAT THE FIELD HANDLED, from the completions' own sourced mass: every
        // barrel that came up the hole, whether it was sold, flared or disposed
        // of. That is what a lifting cost is charged on.
        for (int i = 0; i < report.Solutions.Count; i++)
        {
            // NOT THE INTAKE, which also sources (R20d.24). Imported flood water
            // never came up a hole, so charging the lifting cost on it would bill
            // a company twice — once at the water's own price and once for
            // producing something it bought — and would put sea water into the
            // number that says what the FIELD made.
            if (report.Solutions[i].Element == _intake.Id) continue;

            OGSim.Kernel.Composition sourced = report.Solutions[i].Converged.Sourced;

            for (int m = 0; m < _materialCount; m++)
                _handled[m] += sourced[new MaterialId(m)].KgPerSecond * seconds;
        }

        for (int i = 0; i < report.CompletionStates.Count; i++)
        {
            CompletionState state = report.CompletionStates[i];
            var well = new EntityId<ICompletion>(state.Completion.Value);

            EntityId<IReservoirCompartmentEntity> compartment = _wells.CompartmentOf(well);

            _byCompartment[compartment] =
                _byCompartment.GetValueOrDefault(compartment)
                + state.Rate.CubicMetresPerSecond * seconds;

            // LAST WRITE WINS (SDD-017 §2's R21.6 amendment): segments run in
            // order, so by the time the tick's last one has been accumulated
            // this holds what the well was doing when the month closed.
            _lastSolved[well] = state;
        }

        // What reached the meter. The custody point's ON-SPEC outlet only: its
        // Reject leg is deliberately not counted, which is the entire reason the
        // gate is a network element rather than a predicate at the ledger.
        for (int i = 0; i < report.Solutions.Count; i++)
        {
            ElementSolution solution = report.Solutions[i];
            if (!_meters.Contains(solution.Element)) continue;

            MaterialStream passed = solution.Converged.Outlets[OnSpecLeg];
            OGSim.Kernel.Composition onSpec = passed.MassRates;

            for (int m = 0; m < _materialCount; m++)
                delivered[m] += onSpec[new MaterialId(m)].KgPerSecond * seconds;

            // NOTHING PASSED, NOTHING TO RECORD — and the provenance is left
            // ALONE rather than overwritten. A shut-in field still has a custody
            // meter: the route law removes what feeds an element, not what it
            // feeds, so the meter solves with no inlet and returns a stream
            // whose allocation was never constructed. Storing that would hand
            // stage 6 an empty provenance for mass an earlier segment really did
            // deliver.
            if (onSpec.Total.KgPerSecond <= 0.0) continue;

            // What the tank receives is exactly what the meter passed, with the
            // provenance it carries — so a lifting out of storage allocates back
            // to the compartments that filled it (SDD-002 §3, design 04 §2.2).
            //
            // ACCUMULATED AND DURATION-WEIGHTED, not assigned. `_stored` is a
            // RATE that stage 6 multiplies by a whole tick, so keeping only the
            // last segment's rate applied a shut-in field's flow — or a flowing
            // one's — to the entire month. Weighting each segment by its share
            // of the tick makes the product exact rather than nearly.
            _stored = _stored.Plus(onSpec.Scaled(seconds / TickSeconds));
            _tankProvenance = passed.Provenance;

            // And the tank is told, so the NEXT segment sees the room this one
            // just took rather than the room the tick opened with.
            _tank.Promise(new Mass(onSpec.Total.KgPerSecond * seconds));
        }
    }

    /// <summary>
    /// This element's row, created on first sight so the chain reports itself in
    /// the solver's own topological order — sources first, the meter last, which
    /// is the order a player reads a production line in.
    /// </summary>
    private ChainElement Flowing(EntityId<IFlowElement> element)
    {
        for (int i = 0; i < _chain.Count; i++)
            if (_chain[i].Element == element) return _chain[i];

        var row = new ChainElement(element);
        _chain.Add(row);
        return row;
    }

    /// <summary>
    /// The tick's chain, as SDD-017 §2's <c>ChainElementView</c>.
    ///
    /// <para>EVERY REGISTERED ELEMENT, not only the ones that solved. A failed
    /// element is absent from the network by design (design 04 §4), so building
    /// this from the solve alone made the broken thing VANISH from the one view
    /// a player watches the chain through — the row that carried the field's oil
    /// last month simply stopped existing, and nothing on the surface said what
    /// to repair. An element that did not solve reports no throughput, which is
    /// true, and its condition, which is why.</para>
    /// </summary>
    public IReadOnlyList<ChainElementView> Chain()
    {
        IReadOnlyList<IFlowElement> ordered = FlowOrder();
        var rows = new List<ChainElementView>(ordered.Count);

        for (int i = 0; i < ordered.Count; i++)
        {
            EntityId<IFlowElement> element = ordered[i].Id;

            // CONDITION ONLY WHERE IT IS MEASURED, and FAILURE always: an item
            // that has stopped needs no instrument to notice, which is why
            // run-to-failure is the strategy that costs nothing to play.
            rows.Add(SolvedRow(element).Published(
                _names,
                _integrity.IsMonitored(element) ? _integrity.ConditionOf(element) : null,
                _integrity.HasFailed(element)));
        }

        return rows;
    }

    /// <summary>
    /// Every registered element in the order material crosses it — the WHOLE
    /// field's order, not this tick's.
    ///
    /// <para>Built from the network with everything present, which is the only
    /// thing that knows: registration order is the order modules composed
    /// (facilities before wells), and the solve's order omits whatever was
    /// unavailable — precisely the element a player is looking for. Asking the
    /// full topology gives the failed row its own place in the chain instead of
    /// appending it somewhere after the things it feeds.</para>
    ///
    /// <para>A read-model path rather than a per-tick simulation one, and it
    /// sorts on the order of a dozen elements. If a field ever grows large
    /// enough for that to matter, the order is a property of the topology and
    /// can be cached against the registration count that produced it.</para>
    /// </summary>
    private IReadOnlyList<IFlowElement> FlowOrder()
    {
        IReadOnlyList<IFlowElement> registered = _network.Registered;

        var everything = new List<EntityRef>(registered.Count);
        for (int i = 0; i < registered.Count; i++)
            everything.Add(FlowElementRegistry.ReferenceTo(registered[i]));

        // A field mid-composition can hold a topology that does not yet build —
        // a well registered before its tie-in, say. Registration order is a
        // worse answer than flow order and a much better one than throwing at a
        // host that only asked what its chain looked like.
        return OGSim.Flow.FlowNetwork.Build(_network.ViewFor(everything))
            is OGSim.Flow.NetworkBuilt built
                ? built.Network.Ordered
                : registered;
    }

    /// <summary>What the solve said about an element, or an empty row if it was
    /// not in the network to be asked.</summary>
    private ChainElement SolvedRow(EntityId<IFlowElement> element)
    {
        for (int i = 0; i < _chain.Count; i++)
            if (_chain[i].Element == element) return _chain[i];

        return new ChainElement(element);
    }

    /// <summary>
    /// The on-spec leg's index in a custody point's OUTLETS, which is 0 — not its
    /// port id, which is 1.
    ///
    /// <para>The two are different numbers and reading the wrong one is silent:
    /// index 1 is the Reject leg, so the field metered its rejected oil, sold
    /// nothing, and looked exactly like a field whose wells would not flow.</para>
    /// </summary>
    private const int OnSpecLeg = 0;

    private const double SecondsPerDay = 86_400.0;

    /// <summary>A whole tick in seconds — 30/360, so every month is the same
    /// length and a segment's share of one is exact (SDD-001 §3).</summary>
    private const double TickSeconds = Duration.DaysPerTick * SecondsPerDay;

    /// <summary>
    /// Hands stage 5's answer to stage 6 in the shape the compartments consume:
    /// what the wells took, charged to what they took it from — which is what
    /// makes next month's solve give a smaller answer.
    /// </summary>
    private void PublishWithdrawals()
    {
        var withdrawals = new List<CompartmentWithdrawal>(_byCompartment.Count);

        // Walked over the COMPLETIONS in their own order, never over the
        // dictionary (rule D-5): a hash walk here would make the order material
        // balance commits in depend on hashing, and two runs of one save could
        // deplete two compartments in different orders.
        IReadOnlyList<Completion> completions = _wells.Completions;
        var seen = new HashSet<EntityId<IReservoirCompartmentEntity>>();

        // TWO PASSES, because injection is shared out in proportion to the water
        // each compartment made and that total is not known until every
        // compartment has been asked. One pass would have to put the water
        // somewhere arbitrary — into the first compartment, or evenly — and
        // either would be putting water back where it did not come from.
        var producing = new List<(EntityId<IReservoirCompartmentEntity> Compartment,
                                  double ReservoirVolume, double WaterCut, Pressure At)>();

        double waterMade = 0.0;

        // THE VOIDAGE, which is what a flood replaces and therefore what the
        // imported share is split by (SDD-003 §3.1d's R20d.24b amendment).
        double voidage = 0.0;

        for (int i = 0; i < completions.Count; i++)
        {
            EntityId<IReservoirCompartmentEntity> compartment =
                _wells.CompartmentOf(completions[i].CompletionId);

            if (!seen.Add(compartment)) continue;
            if (!_byCompartment.TryGetValue(compartment, out double reservoirVolume)) continue;

            Pressure at = _subsurface.TruePressureOf(compartment);
            double cut = _subsurface.TrueWaterCutOf(compartment, WaterViscosity, _fluid.MuOil(at));

            producing.Add((compartment, reservoirVolume, cut, at));
            waterMade += reservoirVolume * cut;
            voidage += reservoirVolume;
        }

        double disposed = _disposedThisTick;
        _disposedThisTick = 0.0;

        // WHAT THE FIELD PUT BACK, as against what it BOUGHT. The injector's
        // discharge is both, and the intake is the only other thing feeding it,
        // so the subtraction is exact. Floored at nothing because two doubles
        // that ought to be equal need not be, and a negative volume would be
        // committed as one.
        double imported = _importedThisTick;
        double producedBack = disposed - imported;
        if (producedBack < 0.0) producedBack = 0.0;

        // NEXT MONTH'S TARGET IS BUILT FROM THESE. The produced-water figure is
        // taken at the INJECTOR rather than at the wells: it is measured in the
        // same units as the acceptance it will be subtracted from, and it is
        // what actually went down the hole after the treater took its cut.
        _voidageLastTick = voidage;
        _producedWaterLastTick = producedBack;

        _floodShares.Clear();
        for (int i = 0; i < producing.Count; i++)
            _floodShares.Add((producing[i].Compartment, producing[i].ReservoirVolume));

        for (int i = 0; i < producing.Count; i++)
        {
            (EntityId<IReservoirCompartmentEntity> compartment,
             double reservoirVolume, double waterCut, Pressure pressure) = producing[i];

            // Each compartment's OWN Bo, at its own pressure (R20c.11) — read
            // in the pass above, because the water total needed it first.

            // SDD-003 §6.1b splits the RATE: the Darcy form gives the total
            // liquid, and fw says how much of it is water. No singularity at
            // fw = 1 — the oil term simply goes to zero and the well is a water
            // producer, which is the physical statement.
            var oilReservoir = new ReservoirVolume(reservoirVolume * (1.0 - waterCut));
            var waterReservoir = new ReservoirVolume(reservoirVolume * waterCut);

            SurfaceVolume oil = _fluid.Bo(pressure).Shrink(oilReservoir);

            // AQUIFER INFLUX, over the month. A water-drive compartment is held
            // up by it and waters out because of it; a drive that refuses influx
            // faults on a non-zero one rather than absorbing it silently
            // (SDD-003 §4.2b).
            //
            // Asked of the COMPARTMENT: an aquifer belongs to one, and the engine
            // held a single shared one until finding 164.
            ReservoirVolume influx = _subsurface.InfluxFor(compartment, Duration.FromTicks(1.0));

            // WATER BACK INTO THE ROCK IT CAME OUT OF (SDD-002 §9, SDD-003
            // §3.1d). Produced water was going down a disposal well and out of
            // the game; injected instead, it replaces some of the voidage the
            // oil left behind, so the pressure falls more slowly and the field
            // lasts longer. That is a waterflood, and it is the oldest decision
            // in reservoir management.
            //
            // PRO RATA BY THE WATER EACH COMPARTMENT MADE, which is provenance
            // by another route: water is put back where it came from rather than
            // into whichever compartment happens to be first, and a compartment
            // that produces no water receives none.
            //
            // IMPORTED WATER IS SPLIT BY VOIDAGE INSTEAD, and the two rules have
            // to differ (SDD-003 §3.1d's R20d.24b amendment). A young field
            // makes almost no water — which is exactly when support is worth
            // most — so sharing bought water by water made would put every cubic
            // metre of it nowhere, in the one case the mechanic exists for, and
            // leave a discharge with no matching receipt. Voidage is what the
            // flood is replacing, so voidage is what it follows.
            var boughtHere = new ReservoirVolume(
                voidage <= 0.0 ? 0.0 : imported * (reservoirVolume / voidage));

            var injected = new ReservoirVolume(
                (waterMade <= 0.0
                    ? 0.0
                    : producedBack * (waterReservoir.CubicMetres / waterMade))
                + boughtHere.CubicMetres);

            withdrawals.Add(new CompartmentWithdrawal(
                compartment,
                oil,
                new StandardGasVolume(oil.CubicMetres * _fluid.Rs(pressure)),
                _fluid.Bw(pressure).Shrink(waterReservoir),
                Influx: influx,
                Injected: injected,

                // WHICH OF IT WAS BOUGHT (SDD-012 §5's R20d.25 amendment). The
                // compartment needs the provenance and not just the volume:
                // produced water put back has already been through the rock and
                // sours it least, so a field that only ever reinjects its own
                // water stays sweet however long it runs.
                Imported: boughtHere,
                ReservoirVolume: new ReservoirVolume(reservoirVolume)));

            // The injector wears out as it works (R10-V4): every cubic metre
            // plugs it a little further, its injectivity falls, and remediation
            // is a decision a player eventually has to price. Nothing committed
            // to it before this, so it never aged.
            _disposal.Commit(injected);
        }

        _production.Set(withdrawals);
    }

    /// <summary>
    /// The most water the FIELD can buy without over-filling any one compartment
    /// (SDD-003 §3.1d's R20d.24b amendment).
    ///
    /// <para>The intake is commanded one rate and stage 6 shares it out by
    /// voidage, so a compartment takes <c>imported · voidage_i / voidage</c> —
    /// and the field's cap is therefore the SMALLEST amount that keeps every
    /// compartment inside its own room. Taking the sum of the rooms instead
    /// would be right on average and wrong for the compartment that had least,
    /// which is the one that halts the tick.</para>
    /// </summary>
    /// <para>ASKED FRESH, every tick, and that is not an optimisation detail.
    /// The room is consumed by the AQUIFER as well as by the flood, so a figure
    /// cached at the end of last month has already been spent by water arriving
    /// this one — measured: a cached cap left 3,500 m³ of overfill on the
    /// shipped field and halted the tick. The shares are last month's, which is
    /// harmless because production does not jump; the room is now's, which is
    /// the number that must not be stale.</para>
    private double ReservoirRoom()
    {
        if (_voidageLastTick <= 0.0) return 0.0;

        double cap = double.PositiveInfinity;

        for (int i = 0; i < _floodShares.Count; i++)
        {
            (EntityId<IReservoirCompartmentEntity> compartment, double share) = _floodShares[i];

            // A compartment that produced nothing receives nothing, so it
            // constrains nothing. Said explicitly because the division below
            // would otherwise be by zero.
            if (share <= 0.0) continue;

            // LESS THE WATER THAT IS COMING ANYWAY. The aquifer commits into the
            // same room in the same stage 6 as the injection, so a flood that
            // claimed all of it would over-fill the compartment by exactly one
            // month's influx — which is what the tick that halted was.
            double room =
                _subsurface.TrueVoidageRoomOf(compartment).CubicMetres
                - _subsurface.InfluxFor(compartment, Duration.FromTicks(1.0)).CubicMetres;

            if (room <= 0.0) return 0.0;

            double allowed = room * _voidageLastTick / share;

            if (allowed < cap) cap = allowed;
        }

        return double.IsPositiveInfinity(cap) ? 0.0 : cap;
    }

    /// <summary>Which compartments the flood's water is shared between, and in
    /// what proportion — last month's voidage, refreshed at every commit.</summary>
    private readonly List<(EntityId<IReservoirCompartmentEntity> Compartment, double Share)>
        _floodShares = [];

    /// <summary>
    /// How sour the field's fluid is, 0..1 (SDD-012 §5) — what stage 4 charges
    /// the corrosion term on.
    ///
    /// <para>ASKED OF THE ROCK, not of what produced. The first version walked
    /// the flood's own share list — the compartments that produced last month —
    /// and so read ZERO for any month the chain was down, which is a soured
    /// reservoir healing itself every time a separator broke. Sourness is a
    /// property of the compartment and survives a shut-in, an abandonment and a
    /// save.</para>
    ///
    /// <para>DERIVED, never stored (law L5): it is a question the subsurface can
    /// answer at any moment, and a copy kept beside it would be one more thing
    /// to forget to update.</para>
    /// </summary>
    public double SourFraction => _subsurface.TrueWorstSourFraction();

    /// <summary>
    /// Stage 8. The oil is sold and the field is paid for.
    ///
    /// <para>Revenue is caused by a CUSTODY TRANSFER audit entry and by nothing
    /// else (SDD-009 §1): the ledger refuses a revenue credit whose cause is not
    /// one, so "where did this money come from?" always has an answer that
    /// points at a metered event.</para>
    /// </summary>
    /// <summary>
    /// Stage 7. What crossed the meter is recorded as a custody transfer, and
    /// nothing else is.
    ///
    /// <para>Its own stage in design 03 §6's own slot, because custody is an
    /// EVENT rather than a line in the pricing arithmetic: the ledger refuses a
    /// revenue credit whose cause is not a custody transfer (SDD-009 §1), so
    /// "where did this money come from?" resolves to a metered delivery instead
    /// of to whoever did the multiplication.</para>
    ///
    /// <para>Nothing delivered records nothing. An entry for a zero delivery
    /// would be a custody transfer that did not happen, and the ledger's rule
    /// would be satisfied by a fiction.</para>
    /// </summary>
    /// <summary>
    /// Stage 6, after the solve: what reached the tank is held, and a cargo
    /// loads against it (SDD-006 §7a.3's finding-268 amendment).
    ///
    /// <para>ONE CARGO AT A TIME, occupying the berth until it is full — a
    /// schedule rather than the continuous draw this replaced. A field
    /// producing below the berth's rate never assembles a cargo and never
    /// notices the berth exists; one producing above it fills the tank
    /// between departures, and when the tank is full the ullage constraint
    /// reaches back down the chain and shuts wells in (R8-V5) MORE OFTEN
    /// than a continuous draw ever did — which is the real lever this step
    /// adds, not a change to when revenue is recognised (§7a.3 corrects
    /// §7a.2 on exactly that point).</para>
    ///
    /// <para>THE GATE ITSELF NEEDS NO STATE OF ITS OWN. It reads
    /// <c>tank.Held</c>, which the tank already owns and already persists —
    /// a cargo in progress is oil still sitting in the tank, not a second
    /// store of how much has left it (law L5). What laytime needs is
    /// different: how many ACTIVE DAYS a loading episode has taken, which
    /// the tank's own mass cannot answer, so that alone is tracked
    /// (SDD-006 §7a.4's finding-269 amendment).</para>
    /// </summary>
    public void StoreAndExport(Tick tick, Duration duration)
    {
        _tank.Receive(_stored, _tankProvenance, duration);

        // Boil-off first, because oil that evaporated was never available to
        // lift. It is a conservation term, not a rounding: the tank reports it
        // and stage 9 will account it as fugitive emissions.
        _tank.VapourLossOver(duration);

        // ONE CARGO AT A TIME: nothing draws until the tank holds a full
        // cargo's worth, then the berth clears it — at its own rate, so a
        // very full tank still takes more than one tick — until the tank
        // drops back under that line (SDD-006 §7a.3's finding-268 amendment).
        // Through the berth (SDD-006 §7a's L5 decision, step 1, finding 251)
        // — the same rate, read through the seam a schedule now attaches to.
        bool cargoReady = _tank.Held.Total.Kilograms >= Defaults.CargoSize.Kilograms;

        MaterialInventory lifted = cargoReady
            ? _tank.Draw(new Mass(_terminal.Berth.LoadingRate.KgPerSecond * duration.Seconds))
            : MaterialInventory.Empty(_materialCount);

        Exported = lifted.Total;

        // LAYTIME (SDD-006 §7a.4's finding-269 amendment). Active days accrue
        // for every tick a cargo occupies the berth, whether or not this is
        // its first — a cargo that spans several ticks accrues DAYS across
        // all of them, not one tick's worth of some other unit.
        if (cargoReady) _cargoActiveDays += duration.Days;

        // DEPARTED. The tank just dropped back under a full cargo, so the
        // loading episode that started when it first crossed the line is
        // over — charged ONCE here, against the whole episode, never per
        // tick while still loading (which would double-count one overrun).
        bool stillLoading = _tank.Held.Total.Kilograms >= Defaults.CargoSize.Kilograms;

        if (cargoReady && !stillLoading)
        {
            ChargeDemurrageIfLate(tick);
            _cargoActiveDays = 0.0;
        }
    }

    /// <summary>
    /// SDD-006 §7a.4's finding-269 amendment: <c>max(0, actualDays −
    /// laytime) · rate</c>, §7's own formula, against numbers this
    /// composition already prices rather than a figure invented at the call
    /// site — <see cref="Defaults.CargoLaytimeDays"/> and
    /// <see cref="Defaults.DemurrageRateFraction"/> name where both come from.
    /// </summary>
    private void ChargeDemurrageIfLate(Tick tick)
    {
        double overrunDays = _cargoActiveDays - Defaults.CargoLaytimeDays;
        if (overrunDays <= 0.0) return;

        Money cargoValue = Scale(_market.OilPrice, Defaults.CargoSize.Kilograms / KilogramsPerTonne);
        Money demurrage = Scale(cargoValue, Defaults.DemurrageRateFraction * overrunDays);

        if (demurrage <= Money.Zero) return;

        _company.Ledger.Post(new Movement(
            tick, Account.Opex, Account.Cash, demurrage,
            MovementCategory.Production, Asset: null,
            Cause: _audit.Record(
                AuditCategory.Financial, subject: null, cause: null,
                new Dictionary<string, AuditValue>(StringComparer.Ordinal)
                {
                    ["accrual"] = new("demurrage"),
                    ["overrun-days"] = new(overrunDays.ToString(
                        "F1", System.Globalization.CultureInfo.InvariantCulture)),
                })));
    }

    /// <summary>
    /// What the tank holds and what room is left in it (R21 §2.4b, R21.6).
    ///
    /// <para>Read from the tank rather than tracked here: the tank owns its
    /// inventory and its capacity, and a second copy on the loop would be one
    /// more thing to forget (law L5). Projected each tick like everything else
    /// the read model carries.</para>
    /// </summary>
    public StorageView Storage => new(_tank.Held.Total, _tank.Ullage);

    /// <summary>Everything this company has ever flared (SDD-012 §4).</summary>
    public Mass CumulativeFlared { get; private set; }

    /// <summary>
    /// What this month burned — the ESG record's observation (SDD-012 §4b's
    /// R23.1 amendment), read at stage 9 after the solve has closed.
    ///
    /// <para>Beside <see cref="CumulativeFlared"/> and not instead of it: the
    /// tally is what a player is shown and the month is what the record ages,
    /// and they are two different questions about one accounted quantity rather
    /// than two accounts of it (law L5).</para>
    /// </summary>
    public Mass FlaredThisTick { get; private set; }

    /// <summary>
    /// The ambient the field actually SOLVED at this month, and the severity it
    /// solved through — duration-weighted across the segments (SDD-016 §3).
    ///
    /// <para><b>Published rather than recomputed by the projection</b>, which is
    /// law L5 and was being broken quietly: the read model asked
    /// <c>TemperatureOn(lastDayOfTheMonth)</c> while the solver used a per-segment
    /// mean, so a host was shown a temperature the field never ran at. One
    /// number, computed where it is used, reported from there.</para>
    ///
    /// <para>Weighted by DAYS, because segments are not equal: a month split 3/27
    /// by an outage would otherwise average a three-day cold snap against
    /// twenty-seven days as though they were the same amount of weather.</para>
    /// </summary>
    public Temperature AmbientThisTick { get; private set; } = new(0.0);

    public double SeverityThisTick { get; private set; }

    /// <summary>What left for market this tick. What the tank could not hold
    /// stays in it, and what it could not take never left the field.</summary>
    public Mass Exported { get; private set; }

    public void RecordCustody()
    {
        _sale = null;
        if (Delivered.Total.KgPerSecond > 0.0)
            _sale = _audit.Record(
                AuditCategory.CustodyTransfer,
                subject: null,
                cause: null,
                new Dictionary<string, AuditValue>(StringComparer.Ordinal)
                {
                    ["mass-kg"] = new(Format(Delivered.Total.KgPerSecond)),
                    ["volume-m3"] = new(Format(ProducedThisTick.CubicMetres)),
                });

        RecordRejection();
    }

    /// <summary>
    /// The other half of "a rejection with a reason" (SDD-006 §7d, finding
    /// 252). What fails and why: <see cref="OGSim.Facilities.OffSpecSink"/>
    /// accounts the MASS a rejection loses, and this accounts the CAUSE — the
    /// same split the chain view and the audit trail already draw everywhere
    /// else (how much vs why).
    /// </summary>
    private void RecordRejection()
    {
        IReadOnlyList<OGSim.Facilities.SpecBreach> breaches = _custody.LastBreaches;
        if (breaches.Count == 0) return;

        var data = new Dictionary<string, AuditValue>(StringComparer.Ordinal)
        {
            ["breach-count"] = new(breaches.Count.ToString(
                System.Globalization.CultureInfo.InvariantCulture)),
        };

        for (int i = 0; i < breaches.Count; i++)
        {
            string at = "breach-" + i.ToString(
                System.Globalization.CultureInfo.InvariantCulture) + "-";

            data[at + "property"] = new(breaches[i].Property.ToString());
            data[at + "limit"] = new(Format(breaches[i].Limit));
            data[at + "measured"] = new(Format(breaches[i].Measured));
            data[at + "margin"] = new(Format(breaches[i].Margin));
        }

        _audit.Record(
            AuditCategory.Rejection,
            subject: new EntityRef(EntityKind.FlowElement, _custody.Id.Value),
            cause: null,
            data);
    }

    private AuditId? _sale;

    /// <summary>
    /// A number as the trail carries it: round-trip ("R"), invariant, never
    /// rounded for display. An audit value is evidence a player checks against
    /// the formula (design 09 §4.2), and a figure formatted for reading is one
    /// they cannot — the same rule the canonical save form applies, for the same
    /// reason.
    /// </summary>
    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Stage 8's first act: move the market, then price the month against it.
    ///
    /// <para>Advanced ONCE per tick and before anything is sold, so every barrel
    /// in a month crosses at one price — a market that moved between two sales in
    /// the same month would make the order they were posted in matter.</para>
    /// </summary>
    public void AdvancePrices() => _market.Advance(_prices, _priceStream);

    /// <summary>The market this field sells into — one owner, read by the ledger
    /// and by the scheduler (law L5).</summary>
    public OGSim.Company.MarketState Market => _market;

    /// <summary>
    /// SDD-009 §2's abandonment provision, accrued PER BARREL from first
    /// production.
    ///
    /// <para>A well that is drilled will one day be plugged, and the bill is
    /// earned by the oil that made the hole worth drilling — so it is charged
    /// against the barrels rather than landing whole in the month somebody
    /// finally decides to stop. A company that met the cost only at the end
    /// would look profitable for thirty years and insolvent in one.</para>
    ///
    /// <para>Against ULTIMATE recovery, not remaining (SDD-009 §2's R20d.14
    /// amendment): a field that produces everything it will ever give accrues
    /// exactly its abandonment cost, which is the property that makes this
    /// telescope rather than drift. Against REMAINING it would accelerate as the
    /// field emptied and overshoot.</para>
    ///
    /// <para>NON-CASH. The money has not moved — the provision is what the
    /// company owes the future, and stage 8 posts it as an expense against a
    /// liability so the balance sheet carries it (SDD-009 §1).</para>
    /// </summary>
    private void AccrueAbandonment(Tick tick)
    {
        if (ProducedThisTick.CubicMetres <= 0.0) return;

        Money owed = _obligations.TotalOutstanding;

        if (owed == Money.Zero) return;

        double ultimate = _reserves.Ultimate().Probable.CubicMetres;

        // NOTHING TO ACCRUE AGAINST. A field with no bookable reserves is
        // producing oil the market will not pay to lift, and dividing by its
        // reserves would be dividing by zero — the accrual waits for the price
        // to make the field economic again, which is when the barrels start
        // earning the plugging bill.
        if (ultimate <= 0.0) return;

        Money provision = Scale(owed, ProducedThisTick.CubicMetres / ultimate);

        // NEVER MORE THAN THE BILL. The telescoping in SDD-009 §2 assumes the
        // ultimate recovery is a fixed estimate; it is not, because reserves
        // move with the market (§4). A field whose estimate falls part-way
        // through its life accrues at a higher rate against the barrels that
        // remain, and the sum overshoots — measured at $8.4M against a $3M
        // obligation before this cap.
        //
        // Capping is what the accrual MEANS rather than a fudge for it: a
        // provision is held against a known liability, and one larger than the
        // liability is a misstatement in the other direction. Real accounting
        // revises the rate when the estimate changes; this is the conservative
        // form of the same correction.
        //
        // Credits are negative in this ledger, so what is already held is the
        // negation of the balance.
        Money held = -_company.Ledger.BalanceOf(Account.AbandonmentProvision);
        Money room = owed - held;

        if (room <= Money.Zero) return;
        if (provision > room) provision = room;

        if (provision == Money.Zero) return;

        _company.Ledger.Post(new Movement(
            tick, Account.Depreciation, Account.AbandonmentProvision, provision,
            MovementCategory.Abandonment, Asset: null, Cause: _audit.Record(
                AuditCategory.Financial, subject: null, cause: null,
                new Dictionary<string, AuditValue>(StringComparer.Ordinal)
                {
                    ["accrual"] = new("abandonment-provision"),
                    ["against-2p"] = new(ultimate.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)),
                })));
    }

    /// <summary>
    /// SDD-009 §2's depreciation, UNITS OF PRODUCTION — the industry method, and
    /// the one that says what a wearing asset actually is.
    ///
    /// <para>A platform does not get a year older every year; it gets a barrel
    /// older every barrel. Straight-line would write a field's plant off on a
    /// calendar that has nothing to do with what it did, so a shut-in field
    /// would depreciate exactly as fast as a producing one — which is the
    /// opposite of true.</para>
    ///
    /// <para>Against REMAINING reserves, unlike the abandonment provision, and
    /// the difference is not an inconsistency. A provision is charged against
    /// what a field will EVER give, because the bill is fixed and has to
    /// telescope to it. Depreciation is charged against what is LEFT, because
    /// the value being written off is also what is left — both sides of the
    /// fraction shrink together, which is what keeps a nearly-spent field from
    /// carrying its plant at cost.</para>
    /// </summary>
    private void Depreciate(Tick tick)
    {
        if (ProducedThisTick.CubicMetres <= 0.0) return;

        Money capital = _company.Ledger.BalanceOf(Account.Capex_PPE);

        if (capital <= Money.Zero) return;

        double remaining = _reserves.Remaining(CumulativeProduced).Probable.CubicMetres;

        // NOTHING LEFT TO PRODUCE AGAINST. A field past its bookable reserves is
        // still lifting oil the market pays for, and dividing by what is left
        // would write the whole plant off in one month. The carrying value stays
        // where it is until a reserves revision gives it a denominator again —
        // which is what a revision is FOR.
        if (remaining <= 0.0) return;

        Money charge = Scale(capital, ProducedThisTick.CubicMetres / remaining);

        // Never below nothing: an asset cannot be worth less than written off.
        if (charge > capital) charge = capital;
        if (charge == Money.Zero) return;

        _company.Ledger.Post(new Movement(
            tick, Account.Depreciation, Account.Capex_PPE, charge,
            MovementCategory.Operating, Asset: null, Cause: _audit.Record(
                AuditCategory.Financial, subject: null, cause: null,
                new Dictionary<string, AuditValue>(StringComparer.Ordinal)
                {
                    ["accrual"] = new("units-of-production-depreciation"),
                    ["against-remaining-2p"] = new(remaining.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)),
                })));
    }

    public void PostEconomics(Tick tick)
    {
        // PRICED OFF THE METER (SDD-009 §1) — not off what the wells produced.
        // Everything the chain does between the two lives in that difference,
        // and oil that failed the spec gate is oil the company has and cannot
        // sell.
        // The month's OPEX first, because the fiscal assessment deducts it.
        OperatingCosts costs = OperatingCost();
        Money opex = costs.Total;

        CumulativeProduced = new SurfaceVolume(
            CumulativeProduced.CubicMetres + ProducedThisTick.CubicMetres);

        AccrueAbandonment(tick);
        Depreciate(tick);

        // SALES GAS. Gas the plant took is gas the company sold, and pricing it
        // here — not at the custody meter — is the honest simple model this
        // composition ships: the plant IS the sales point, and gas leaves the
        // field the moment it is processed (SDD-006 §3b).
        //
        // Its own audit cause rather than the oil sale's: a month that sold gas
        // and metered no oil is a real month, and revenue caused by a custody
        // transfer that did not happen would be a fiction the ledger's own rule
        // exists to refuse.
        double gasSold =
            _chainGasPlant.Captured.Total.KgPerSecond * Duration.FromTicks(1.0).Seconds;

        if (gasSold > 0.0)
        {
            Money gas = Scale(_gasPrice, gasSold / KilogramsPerTonne);

            if (gas > Money.Zero)
                _company.Ledger.Post(new Movement(
                    tick, Account.Cash, Account.Revenue, gas,
                    MovementCategory.Production, Asset: null,
                    Cause: _audit.Record(
                        AuditCategory.CustodyTransfer, subject: null, cause: null,
                        new Dictionary<string, AuditValue>(StringComparer.Ordinal)
                        {
                            ["sales-gas-kg"] = new(gasSold.ToString(
                                System.Globalization.CultureInfo.InvariantCulture)),
                        })));
        }

        if (_sale is AuditId sale)
        {
            Money gross = Scale(
                _market.OilPrice, Delivered.Total.KgPerSecond / KilogramsPerTonne);

            _company.Ledger.Post(new Movement(
                tick, Account.Cash, Account.Revenue, gross,
                MovementCategory.Production, Asset: null, Cause: sale));

            // THE STATE TAKES ITS SHARE (SDD-009 §3). A royalty on gross and a
            // tax on what is left after costs — the regime was composed at R16
            // and called by nobody, so a company kept every barrel's full price
            // and the fiscal terms of a licence meant nothing.
            FiscalResult assessed = _regime.Assess(new FiscalInput(
                GrossRevenue: gross,
                RecoverableOpex: opex,
                RecoverableCapex: Money.Zero,
                Depreciation: Money.Zero,
                CostPoolCarry: Money.Zero,
                PriorRFactor: 0.0));

            if (assessed.Royalty.Cents > 0)
                _company.Ledger.Post(new Movement(
                    tick, Account.Royalty, Account.Cash, assessed.Royalty,
                    MovementCategory.Fiscal, Asset: null, Cause: sale));

            if (assessed.Tax.Cents > 0)
                _company.Ledger.Post(new Movement(
                    tick, Account.Tax, Account.Cash, assessed.Tax,
                    MovementCategory.Fiscal, Asset: null, Cause: sale));
        }

        // The field costs money to run whether or not it produced — which is the
        // whole shape of the late-life decision, and it must not be conditional
        // on production or a shut-in field would be free to keep.
        // ITEMISED, because design 09 §7's money report is one of the two the
        // trail exists for and an empty entry answers nothing. The three lines
        // are what a player can act on: the standing charge is what abandonment
        // ends, the lifting cost is what a shut-in well stops, and the water bill
        // is what a flood is costing to run.
        AuditId operating = _audit.Record(
            AuditCategory.Financial, subject: null, cause: null,
            new Dictionary<string, AuditValue>(StringComparer.Ordinal)
            {
                ["spend"] = new("field-operating"),
                ["standing"] = new(Format(costs.Standing.Cents)),
                ["lifting"] = new(Format(costs.Lifting.Cents)),
                ["injection-water"] = new(Format(costs.InjectionWater.Cents)),
            });

        _company.Ledger.Post(new Movement(
            tick, Account.Opex, Account.Cash, opex,
            MovementCategory.Operating, Asset: null, Cause: operating));
    }

    /// <summary>
    /// Rounded ONCE, half-even, at the ledger boundary — the single
    /// double→Money rule (SDD-001 §1.3). Multiplying cents by a double anywhere
    /// else would round twice and INV2 reconciles to the cent.
    /// </summary>
    private static Money Scale(Money unitPrice, double quantity) =>
        // CENTS × quantity, and the result is cents. Dividing to dollars first
        // and handing those to a cents-based rounder made every sale a hundredth
        // of its value — which showed up as a producing field that could not
        // cover its own standing charge.
        Money.RoundHalfEven(unitPrice.Cents * quantity);

    /// <summary>
    /// What the field cost to run this month: a standing charge, plus a lifting
    /// cost on every tonne of LIQUID handled — oil and water alike.
    ///
    /// <para><b>Water costs the same to lift as oil</b>, and that is the whole
    /// economics of a watering-out field: the pumps, the power and the
    /// separation do not care which it is, so a well at 90% water cut is paying
    /// to produce nine barrels of nothing for every one that sells. It is what
    /// eventually makes a field uneconomic while it is still producing, which is
    /// the decision late life is made of — and without it watering out would be
    /// something a player watches rather than something they answer.</para>
    /// </summary>
    /// <summary>
    /// What a month of running the field cost, ITEMISED (design 09 §7).
    ///
    /// <para>The three components were computed and then added together inside
    /// the method, so the audit entry that recorded the spend had nothing to say
    /// and recorded an empty dictionary — 240 of them in a twenty-year game, one
    /// every month, backing the report design 09 §7 calls *where did my money go
    /// this quarter?* with no answer at all (finding 230).</para>
    /// </summary>
    private readonly record struct OperatingCosts(
        Money Standing, Money Lifting, Money InjectionWater)
    {
        public Money Total => Standing + Lifting + InjectionWater;
    }

    private OperatingCosts OperatingCost()
    {
        // A FIELD THAT HAS BEEN ABANDONED COSTS NOTHING. The standing charge is
        // the people, the power and the road, and none of them is there once the
        // last well is plugged — which is the ending: a player who cannot make
        // the field pay stops paying for it.
        //
        // ABANDONED, not merely empty. A field nobody has drilled yet still has
        // a licence and a standing charge, and that is what makes idling lose;
        // and a field whose wells are shut IN still has the site, the staff and
        // the licence, having only stopped lifting. Pausing is not leaving, and
        // the difference is what abandonment's price buys.
        if (_isAbandoned()) return default;

        var liquid = 0.0;
        for (int i = 0; i < _liquidOrdinals.Count; i++)
            liquid += _handled[_liquidOrdinals[i]];

        // AND THE WATER THE COMPANY BOUGHT (SDD-003 §3.1d's R20d.24 amendment).
        // Charged in the month it is lifted, against a recovery that arrives
        // years later — which is what makes the flood a decision rather than a
        // slider every player pushes to its limit on day one.
        return new OperatingCosts(
            _economics.FixedOperatingCostPerTick,
            Scale(_economics.LiftingCostPerTonne, liquid / KilogramsPerTonne),
            Scale(_economics.InjectionWaterCostPerCubicMetre, _importedThisTick));
    }

    /// <summary>
    /// The mean ambient over a segment's days (SDD-016 §3).
    ///
    /// <para>A MEAN and not the first day's, because a segment is a run of days
    /// and the element it is handed to solves once for the whole run. Taking the
    /// first day would make a segment boundary — which exists for availability
    /// reasons, not weather ones — decide which day's temperature the month is
    /// solved at.</para>
    /// </summary>
    private Temperature AmbientOver(Segment segment)
    {
        var sum = 0.0;

        for (var day = 0; day < segment.DurationDays; day++)
            sum += _weather.TemperatureOn(FieldRegion, segment.StartDay + day).Kelvin;

        return new Temperature(sum / segment.DurationDays);
    }

    /// <summary>The mean severity over the same days, on the same argument.</summary>
    private double SeverityOver(Segment segment)
    {
        var sum = 0.0;

        for (var day = 0; day < segment.DurationDays; day++)
            sum += _weather.SeverityOn(FieldRegion, segment.StartDay + day);

        return sum / segment.DurationDays;
    }

    /// <summary>One climate region per location (SDD-016 §1). The same constant
    /// `ActivityStage` uses, and it will become a field property when R22.1's
    /// environment profile lands.</summary>
    private const int FieldRegion = 0;

    private const double KilogramsPerTonne = 1000.0;

    /// <summary>
    /// μw at reservoir conditions, Pa·s. A property of the water rather than of
    /// the oil system, so <c>IFluidPropertyModel</c> does not carry one — and it
    /// is half of the mobility ratio that decides how steep the S-curve is
    /// (SDD-003 §3.1c). Content the day a water material has properties.
    /// </summary>
    private static Viscosity WaterViscosity { get; } = new(0.5e-3);
}

/// <summary>
/// How a scenario builds a field — the public seam over module state that is
/// internal by design.
///
/// <para>It exists because a compartment is truth: <c>SubsurfaceState</c> is
/// internal to <c>OGSim.Subsurface</c> and no consumer, test included, may name
/// it. What a scenario legitimately needs is not the truth object but the
/// ability to say "there is a reservoir here, with these properties, and a well
/// on it" — which is exactly what world generation says, and this is the same
/// door.</para>
///
/// <para>Reading back is deliberately NOT here. A caller can create a
/// compartment and cannot ask what pressure it is at: that answer belongs to the
/// belief store, through an observation, like every other measurement in the
/// game.</para>
/// </summary>
/// <summary>
/// Builds a well's gathering line, of a stated length (SDD-006 §1c).
///
/// <para>A delegate because the pipeline's dependencies — the hydraulic model,
/// the fluid system, the material count — belong to composition and a field does
/// not otherwise know them. Not a default: it is required, and there is no
/// fallback that would let a well tie in without one.</para>
/// </summary>
internal delegate OGSim.Facilities.Pipeline GatheringLine(Length run);

public sealed class FieldControl : IStateOwner
{
    private readonly SubsurfaceState _subsurface;
    private readonly WellsState _wells;
    private readonly IFlowElementRegistry _network;
    private readonly SurfaceChain _chain;
    private readonly IObligationRegistry _obligations;
    private readonly ContentId _abandonmentTemplate;
    private readonly WorldState _world;
    private readonly GatheringLine _gatheringLine;
    private readonly WellDesign _design;

    // THE OTHER HALF OF A MUTUAL DEPENDENCY (SDD-017 §2's R21.6 amendment):
    // `ProductionLoop` already closes over THIS object (`() => field.
    // IsAbandoned`) because it is built second, and a field needs the loop's
    // own solve state, which does not exist yet when a field is built first.
    // Composition breaks the cycle the same way it already does for the other
    // direction — a forward reference assigned once the loop exists, invoked
    // only much later, at read-model time.
    private readonly Func<EntityId<ICompletion>, CompletionState?> _lastSolvedStateOf;

    private int _slotsTaken;

    internal FieldControl(
        SubsurfaceState subsurface,
        WellsState wells,
        IFlowElementRegistry network,
        SurfaceChain chain,
        IObligationRegistry obligations,
        ContentId abandonmentTemplate,
        WorldState world,
        GatheringLine gatheringLine,
        WellDesign design,
        Func<EntityId<ICompletion>, CompletionState?> lastSolvedStateOf)
    {
        _subsurface = subsurface;
        _wells = wells;
        _network = network;
        _chain = chain;
        _obligations = obligations;
        _abandonmentTemplate = abandonmentTemplate;
        _world = world;
        _gatheringLine = gatheringLine;
        _design = design;
        _lastSolvedStateOf = lastSolvedStateOf;
    }

    /// <summary>
    /// Whether the header has room for another well.
    ///
    /// <para>Asked by the drilling command's own refusals, so a player who has
    /// filled their manifold is told BEFORE they pay for four months of rig time
    /// — the tie-in itself cannot report a player error, because by then the hole
    /// is drilled and the money is gone.</para>
    /// </summary>
    public bool HasFreeSlot => _slotsTaken < _chain.Slots;

    public int FreeSlots => _chain.Slots - _slotsTaken;

    public EntityId<IReservoirCompartmentEntity> AddCompartment(
        GeneratedCompartment generated,
        Permeability permeability,
        Length netThickness,
        Area drainageArea,
        double rockCompressibility,
        Length gasOilContact,
        Length oilWaterContact,
        RelativePermeabilityCurve wettability,
        ContentId drive,
        double aquiferStrength,
        Duration aquiferResponseTime) =>
        _subsurface.Create(
            generated, permeability, netThickness, drainageArea,
            rockCompressibility, gasOilContact, oilWaterContact, wettability, drive,
            aquiferStrength, aquiferResponseTime);

    /// <summary>A DRY-GAS compartment (SDD-003 §3.1b's finding-264 amendment)
    /// — hand-declared, the same reason and the same way <see cref="AddCompartment"/>
    /// hand-declares an oil one.</summary>
    public EntityId<IReservoirCompartmentEntity> AddGasCompartment(
        ReservoirVolume poreVolume,
        double porosity,
        double gasSaturation,
        Pressure initialPressure,
        Temperature reservoirTemperature,
        Permeability permeability,
        Length netThickness,
        Area drainageArea,
        double rockCompressibility,
        Length gasWaterContact,
        RelativePermeabilityCurve wettability,
        ContentId drive,
        ContentId fluidSystem,
        double aquiferStrength,
        Duration aquiferResponseTime) =>
        _subsurface.CreateGas(
            poreVolume, porosity, gasSaturation, initialPressure, reservoirTemperature,
            permeability, netThickness, drainageArea, rockCompressibility, gasWaterContact,
            wettability, drive, fluidSystem, aquiferStrength, aquiferResponseTime);

    /// <summary>
    /// Brings a completion online against a compartment and ties it into the
    /// header. From the next tick it is a source element the solver sees, flowing
    /// against whatever the surface holds.
    ///
    /// <para>The tie-in happens HERE because this is the one place that knows a
    /// completion and a manifold are both real (design 03 §8). A well opened
    /// without one would be a source element with nowhere to go: it would solve,
    /// produce, put its mass on an unconnected port, and look like it was
    /// working while earning nothing.</para>
    /// </summary>
    /// <summary>
    /// DRILLS A WELL INTO A COMPARTMENT, building it from that compartment's own
    /// rock (SDD-008 §2c).
    ///
    /// <para>The completion used to arrive ready-made from the caller, carrying
    /// whatever inflow conditions the caller happened to have — which was one
    /// fixed set, for every well in the game. Building it HERE is what makes a
    /// well's productivity a fact about the rock it is in rather than a constant
    /// (finding 170), and it removes the caller's ability to hand over a well
    /// that does not match the reservoir it is drilled into.</para>
    /// </summary>
    public EntityId<ICompletion> Drill(
        EntityId<IReservoirCompartmentEntity> drains, Length totalDepth)
    {
        Completion completion = _design(
            NextWellId(),
            drains,
            totalDepth,
            Defaults.Inflow with
            {
                Permeability = _subsurface.TruePermeabilityOf(drains),
                PerforatedInterval = _subsurface.TrueNetThicknessOf(drains),
                DrainageArea = _subsurface.TrueDrainageAreaOf(drains),
            });

        return OpenWell(completion, drains);
    }

    /// <summary>
    /// Re-open a well a save recorded, through the very path that drilled it
    /// (design 11 §2.1's loader, SDD-013's S013-5).
    ///
    /// <para>THE SAME `Drill`, and that is the whole point. Everything a well
    /// brings with it — the header slot, the trunk route and the manifold on the
    /// first tie-in, the abandonment obligation, the gathering line at that
    /// field's own distance, both network connections — is written once, and a
    /// rebuild that laid its own version beside it would be a second way to make
    /// a well (L5), drifting the first time either changed.</para>
    ///
    /// <para>THE ID FALLS OUT RATHER THAN BEING ASSIGNED. `NextWellId` is
    /// `_wells.Count + 1` — derived, not a counter — so replaying the drills in
    /// the order the save lists them reproduces the same ids. This checks that
    /// rather than trusting it: a mismatch means the save and this build
    /// disagree about what a field is, and design 11 §2.1 is explicit that a
    /// reference which fails to resolve on restore is a fault and never a silent
    /// drop.</para>
    /// </summary>
    internal void Reopen(
        EntityId<ICompletion> expected,
        EntityId<IReservoirCompartmentEntity> drains,
        Length totalDepth)
    {
        EntityId<ICompletion> opened = Drill(drains, totalDepth);

        if (opened != expected)
            throw new SaveDataFault("SDD-013 §2", null,
                $"rebuilding the field gave completion {opened.Value} where the save holds " +
                $"{expected.Value}; the wells were reopened in the order they were saved, so " +
                "a different id means this build numbers wells differently from the one that " +
                "wrote the save");
    }

    private EntityId<ICompletion> OpenWell(
        Completion completion, EntityId<IReservoirCompartmentEntity> drains)
    {
        ArgumentNullException.ThrowIfNull(completion);

        // A composition defect by the time it reaches here, not a player error:
        // the drilling command refuses a well with no slot to tie into, so
        // arriving with a full header means something bypassed the validator.
        if (!HasFreeSlot)
            throw new InvariantFault("SDD-006 §1b", null,
                $"the header has {_chain.Slots} slots and all are taken; a well with " +
                "nowhere to tie in must be refused when it is ordered, not when it lands");

        // LAY THE LINE TO WHERE THE FIELD ACTUALLY IS (SDD-006 §7c.1). The
        // first well tied in is the moment a company commits to a location, and
        // the flowline it runs to market is as long as the journey — so a
        // discovery out at the edge of the basin costs more pressure to produce
        // through than one beside the harbour.
        //
        // Only on the FIRST tie-in: later wells join a line that is already laid,
        // and re-routing under oil is refused by the pipeline itself.
        if (_slotsTaken == 0 && _world.DistanceToMarketOf(drains) is Length toMarket)
        {
            // FLOORED, like the gathering line. A field can sit on the harbour —
            // the generator places structures on the same grid the coast is
            // drawn on — and a trunk of zero length has no hydraulics to solve.
            // A plant is still not built on top of the wellhead.
            _chain.Flowline.Route(_chain.Flowline.Geometry with
            {
                PipeLength = toMarket.Metres > MinimumGatheringRun.Metres
                    ? toMarket
                    : MinimumGatheringRun,
            });

            // AND THE HEADER GOES UP AT THE FIELD BEING OPENED (SDD-006 §1c).
            // A manifold is a structure somebody builds somewhere; later fields
            // reach it rather than it reaching them, which is what makes a
            // distant second discovery a different proposition from a nearby
            // one.
            _world.HeaderAt(_world.PositionOf(_world.ProspectFor(drains)));
        }

        EntityId<ICompletion> opened = _wells.Open(completion, drains);

        // UNCONDITIONAL, at creation (SDD-007 §6, design 02 §3.4): a well that
        // is drilled will one day be plugged whatever else happens to it, and a
        // company able to create one without the liability could walk away from
        // the cost by never recording it.
        _obligations.Register(
            new EntityRef(EntityKind.Completion, opened.Value), _abandonmentTemplate);

        // THE GATHERING LINE, design 04 stage 3's wellhead-to-manifold run
        // (SDD-006 §1c). As long as this well's field is from the header, so a
        // tieback from across the basin costs pressure that a well on the host's
        // own field does not — the same well drilled into two structures is two
        // different propositions.
        //
        // Backpressure travels back up it unchanged, so §1b's commingling trap
        // still works: a strong new well raises manifold pressure and can shut
        // in weaker wells however far away they are.
        // NEVER SHORTER THAN THE MINIMUM. A well on the header's own field
        // measures zero metres to it, and a pipeline of zero length has no
        // hydraulics to solve — but the tree is not bolted to the manifold
        // either. The floor is the run every well has whatever else is true.
        Length toHeader = _world.DistanceToHeaderOf(drains) ?? MinimumGatheringRun;

        OGSim.Facilities.Pipeline tieback = _gatheringLine(
            toHeader.Metres > MinimumGatheringRun.Metres ? toHeader : MinimumGatheringRun);

        _network.Add(tieback);

        _network.Connect(new FlowConnection(
            completion.Id, WellheadOutlet, tieback.Id, PipelineInlet));

        _network.Connect(new FlowConnection(
            tieback.Id, PipelineOutlet,
            _chain.Manifold.Id, _chain.Manifold.SlotAt(_slotsTaken)));

        _slotsTaken++;
        return opened;
    }

    /// <summary>A completion's one outlet: the wellhead.</summary>
    private static PortId WellheadOutlet { get; } = new(0);

    private static PortId PipelineInlet { get; } = new(0);

    private static PortId PipelineOutlet { get; } = new(1);

    /// <summary>
    /// The shortest gathering run there is: a well on the header's own field
    /// still has flowline between the tree and the manifold. Two hundred metres
    /// — and a positive number matters, because a pipeline of zero length has no
    /// hydraulics to solve.
    /// </summary>
    private static Length MinimumGatheringRun { get; } = new(200.0);

    /// <summary>One open well by id, or null — the door a player's lever is
    /// pulled through.</summary>
    public Completion? WellNamed(EntityId<ICompletion> well) => _wells.Find(well);

    /// <summary>How many wells are still producing — abandoned ones are plugged
    /// and no longer part of the field a player is running.</summary>
    public int LiveWellCount => _wells.Count - _abandoned.Count;

    /// <summary>Whether THIS well specifically is plugged (SDD-003 §6's
    /// R12b.7 amendment, finding 253) — a job ordered against a well that is
    /// out of the network for good is a bill for nothing, and a player who
    /// ordered one deserves to be told rather than invoiced.</summary>
    public bool IsWellAbandoned(EntityId<ICompletion> well) => _abandoned.Contains(well);

    /// <summary>
    /// Whether the field is closed: it was developed, and every well it had is
    /// plugged.
    ///
    /// <para>Both halves matter. A field nobody has drilled is not abandoned, it
    /// is undeveloped — and it still costs its standing charge, which is what
    /// makes doing nothing lose.</para>
    /// </summary>
    public bool IsAbandoned => _wells.Count > 0 && _abandoned.Count == _wells.Count;

    /// <summary>
    /// Every well and its state (SDD-017 §2's R21.5/R21.6 amendments) — the
    /// list a well-level command is aimed with.
    ///
    /// <para>Walked in the order the wells were opened, so a host's list does
    /// not reshuffle between months (D-5). Production is deliberately absent
    /// here and reported as zero: what a WELL produced needs a per-completion
    /// split of the solve, and the loop totals the field — a number invented per
    /// well would be a plausible fiction, so the honest answer is the field's
    /// own total on the read model beside it.</para>
    ///
    /// <para>The operating point is RECONSTRUCTED against the last segment's
    /// converged wellhead backpressure the loop retained, not cached from the
    /// solve itself (law L5) — <c>null</c> where the loop retained nothing,
    /// which is a well that did not solve this tick rather than one guessed
    /// at.</para>
    /// </summary>
    public IReadOnlyList<WellStatusView> Wells()
    {
        IReadOnlyList<Completion> completions = _wells.Completions;
        var rows = new List<WellStatusView>(completions.Count);

        for (int i = 0; i < completions.Count; i++)
        {
            Completion well = completions[i];

            OperatingPoint? point = _lastSolvedStateOf(well.CompletionId) is CompletionState state
                ? well.SolveOperatingPoint(state.WellheadBackpressure)
                : null;

            IReadOnlyList<ContentId> installedTiers =
                well.Lift is ILiftMethod lift ? [lift.InstalledTier] : [];

            rows.Add(new WellStatusView(
                new EntityRef(EntityKind.Completion, well.CompletionId.Value),
                "well-" + well.CompletionId.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                StatusOf(well),
                new SurfaceVolume(0.0),
                point,
                installedTiers));
        }

        return rows;
    }

    /// <summary>
    /// The reachable three-way switch (SDD-003 §5's R20d.9 amendment), and the
    /// ONE place it is written: <see cref="Wells"/>'s <c>WellStatusView</c> rows
    /// and <see cref="AsWells"/>'s <c>IWell</c> entities both read this rather
    /// than each carrying their own copy (law L5) — a second implementation of
    /// one derivation is the second-owner shape even when both sides agree
    /// today.
    /// </summary>
    private WellStatus StatusOf(Completion completion) =>
        _abandoned.Contains(completion.CompletionId) ? WellStatus.Abandoned
        : completion.IsShutIn ? WellStatus.ShutIn
        : WellStatus.Producing;

    /// <summary>Every well as a map screen would want it, with no map to draw it
    /// on (SDD-003 §5's R20d.9 amendment) — every well answers the same origin,
    /// stated as a limit rather than a default: world generation has no (x, y)
    /// at all, and R20d.8's spatial half is what moves this.</summary>
    private static readonly Coordinate NoMapYet = new(0.0, 0.0);

    /// <summary>
    /// Every well as <see cref="IWell"/>, against ONE composition-wide licence —
    /// this composition generates one field, so there is exactly one licence
    /// for every well to reference (SDD-011 §1's R20d.9 amendment).
    ///
    /// <para>Walked in the same order as <see cref="Wells"/>, for the same
    /// reason (D-5): a host's list must not reshuffle between ticks.</para>
    /// </summary>
    public IReadOnlyList<IWell> AsWells(EntityId<ILicence> licence)
    {
        IReadOnlyList<Completion> completions = _wells.Completions;
        var result = new List<IWell>(completions.Count);

        for (int i = 0; i < completions.Count; i++)
        {
            Completion completion = completions[i];

            var wellId = new EntityId<IWell>(completion.CompletionId.Value);
            var wellboreId = new EntityId<IWellbore>(completion.CompletionId.Value);

            result.Add(new OGSim.Wells.Well(
                wellId,
                StatusOf(completion),

                // EVERY WELL IS DEVELOPMENT, honestly rather than by default:
                // the shipped catalogue has one drilling template and it is not
                // gated as exploration or appraisal work, so there is nothing
                // else a drilled well in this composition could be.
                WellClassification.Development,
                licence,
                NoMapYet,
                [wellboreId]));
        }

        return result;
    }

    /// <summary>The vertical trajectory's bottom station: the deepest
    /// perforation a completion has, since <c>DrillWellCommand</c>'s only
    /// geometry parameter is a single <see cref="Length"/> and every well this
    /// composition produces is vertical by construction.</summary>
    private static Length DeepestMd(Completion completion)
    {
        IReadOnlyList<Perforation> perforations = completion.Perforations;
        double deepest = 0.0;

        for (int i = 0; i < perforations.Count; i++)
            if (perforations[i].BottomMd.Metres > deepest) deepest = perforations[i].BottomMd.Metres;

        return new Length(deepest);
    }

    /// <summary>
    /// The full <see cref="IWellbore"/> behind an id from a
    /// <see cref="Well.Wellbores"/> list — reconstructed on demand rather than
    /// cached, since a wellbore is entirely a function of its completion (law
    /// L5: caching would be a second, staler copy of the same fact).
    /// </summary>
    public IWellbore? WellboreNamed(EntityId<IWellbore> id)
    {
        if (_wells.Find(new EntityId<ICompletion>(id.Value)) is not Completion completion)
            return null;

        return new OGSim.Wells.Wellbore(
            id, new EntityId<IWell>(id.Value),
            new Trajectory(
            [
                new TrajectoryStation(new Length(0.0), new Length(0.0), NoMapYet),
                new TrajectoryStation(DeepestMd(completion), DeepestMd(completion), NoMapYet),
            ]),
            completion);
    }

    /// <summary>
    /// Plugs a well and discharges its obligation (SDD-007 §6).
    ///
    /// <para>The completion stays in the network, permanently shut: registration
    /// is write-once and an element that vanished mid-tick would take its
    /// tie-ins with it (SDD-002 §6). A plugged well is a closed valve that will
    /// never open again — which is what an abandoned well IS, and it keeps the
    /// header slot it occupied, exactly as it does on a real site.</para>
    /// </summary>
    public void Abandon(EntityId<ICompletion> well, AuditId cause)
    {
        if (_wells.Find(well) is not Completion found)
            throw new InvariantFault("R1 §2.5", null,
                $"an abandonment completed against well {well.Value}, which is not open");

        found.SetChoke(ChokeSetting.Closed);
        _abandoned.Add(well);

        _obligations.Discharge(
            new EntityRef(EntityKind.Completion, well.Value), new EntityId<IOperation>(cause.Value));
    }

    /// <summary>Which wells are plugged. Held here because it is what
    /// distinguishes a shut-in well from an abandoned one, and only this layer
    /// knows an abandonment happened.
    ///
    /// <para>STATE, and it was not (R20d.9). `Abandon` sets the SAME
    /// `ChokeSetting.Closed` a normal shut-in uses — `Completion.IsShutIn` reads
    /// true for either — so this set was the only place the two differed, and it
    /// went nowhere on a save. Unreachable while nothing published the
    /// difference; it became reachable the moment `IWell.Status` made
    /// `Abandoned` a fact a player could ask for.</para>
    /// </summary>
    private readonly HashSet<EntityId<ICompletion>> _abandoned = [];

    // ------------------------------------------------------- SDD-013 §4

    public StateKey Key { get; } = new("field.abandoned");

    public int SchemaVersion => 1;

    /// <summary>Nothing has to be back before this is (SDD-013 §2b).</summary>
    public IReadOnlyList<StateKey> RestoreAfter => [];

    public void Capture(IStateWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        // ID ORDER, not insertion order: a HashSet's enumeration is hash-ordered
        // (rule D-5), and two runs of one save could plug the same wells in a
        // different byte order otherwise.
        var ordered = new List<EntityId<ICompletion>>(_abandoned);
        ordered.Sort((a, b) => a.Value.CompareTo(b.Value));

        writer.WriteInt64("count", ordered.Count);

        for (int i = 0; i < ordered.Count; i++)
            writer.WriteInt64(
                "id." + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture),
                (long)ordered[i].Value);
    }

    public void Restore(IStateReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        _abandoned.Clear();

        long count = reader.ReadInt64("count");

        for (long i = 0; i < count; i++)
            _abandoned.Add(new EntityId<ICompletion>(
                (ulong)reader.ReadInt64(
                    "id." + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture))));
    }

    /// <summary>
    /// Turns a well's valve (SDD-003 §5.1's R20.4 amendment).
    ///
    /// <para>Cannot fail: the validator has already proven the well exists and
    /// that the setting is a change (R1 §2.5).</para>
    /// </summary>
    public void SetChoke(EntityId<ICompletion> well, ChokeSetting choke)
    {
        if (_wells.Find(well) is not Completion found)
            throw new InvariantFault("R1 §2.5", null,
                $"the choke command passed validation and well {well.Value} is not open; " +
                "an applier cannot fail");

        found.SetChoke(choke);
    }

    /// <summary>
    /// Which wells drain a compartment, plugged ones excluded — what a
    /// build-up test on that compartment has to shut in and reopen (SDD-007
    /// §5's R12b.18 amendment). A plugged well's valve reads the same
    /// <c>ChokeSetting.Closed</c> a shut-in test would set and is never coming
    /// back open, so it is not "on" the compartment for this question even
    /// though its perforations still are.
    /// </summary>
    internal IReadOnlyList<EntityId<ICompletion>> WellsOn(
        EntityId<IReservoirCompartmentEntity> compartment)
    {
        var found = new List<EntityId<ICompletion>>();
        IReadOnlyList<Completion> completions = _wells.Completions;

        for (int i = 0; i < completions.Count; i++)
        {
            Completion completion = completions[i];
            if (_abandoned.Contains(completion.CompletionId)) continue;

            for (int p = 0; p < completion.Perforations.Count; p++)
                if (completion.Perforations[p].Drains == compartment)
                {
                    found.Add(completion.CompletionId);
                    break;
                }
        }

        return found;
    }

    /// <summary>Whether a well's valve is shut, plugged or not (SDD-007 §5's
    /// R12b.18 amendment) — what a build-up test's own refusal reads, since
    /// testing an already-shut-in well would either reopen it against the
    /// player's own choice or leave the test's "reopen when done" with
    /// nothing honest to restore.</summary>
    internal bool IsShutIn(EntityId<ICompletion> well) =>
        _wells.Find(well) is Completion found && found.IsShutIn;

    public int CompartmentCount => _subsurface.Count;

    public int WellCount => _wells.Count;

    /// <summary>
    /// The next completion id, issued by the module that owns the wells.
    ///
    /// <para>Not a counter on the composition: a static would be shared by every
    /// engine in a process and would make two games' ids depend on each other,
    /// which is both law L2's ban on static mutable state and the end of
    /// determinism.</para>
    /// </summary>
    public ulong NextWellId() => (ulong)_wells.Count + 1;
}

/// <summary>
/// Stage 4. Says which elements are available this tick, and for how long.
///
/// <para>THE HAZARD PASS RUNS HERE (SDD-012 §2). Everything registered ages,
/// everything rolls, and what fails is subtracted from the availability list —
/// which is the whole of what a failure IS in this engine: an unavailable
/// element is ABSENT from the network rather than present at zero rate (design
/// 04 §4). There is no broken flag for the solver to consult and no code path
/// where a failed element takes part and is then ignored.</para>
///
/// <para>THEN THE ROUTE LAW (SDD-002 §5). Subtracting an element is not enough
/// on its own: `ViewFor` drops the connections touching it, and whatever fed it
/// would go on flowing into a pipe that now ends nowhere — the solver accepts
/// that, and the mass leaves the network by the back door while stage 6 still
/// publishes the withdrawal against the reservoir. `Routed` propagates the
/// shut-in upstream until it stops, so a broken separator stops the wells
/// instead of losing their oil.</para>
///
/// <para>AND THE MONTH SPLITS AT THE FAILURE DAY. The day is drawn as an
/// integer in {0..29} precisely so the boundary lands on the /30ths grid
/// exactly (SDD-012 §2), and the two segments are the reason stage 5 iterates a
/// plan rather than solving "the tick": a non-linear solve does not average, so
/// half a month at full rate and half at nothing is not a month at half rate.</para>
/// </summary>
internal sealed class SegmentationStage(
    IFlowElementRegistry network,
    OGSim.Integrity.AssetIntegrity integrity,
    ProductionLoop loop,
    IAuditTrail audit) : ITickStage
{
    public StageId Id => StageId.Availability;

    public void Execute(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        IReadOnlyList<IFlowElement> registered = network.Registered;

        IReadOnlyList<OGSim.Integrity.FailureOutcome> failures =
            integrity.Advance(
                registered, loop.WaterCut, loop.SourFraction, Duration.FromTicks(1.0));

        // WHAT IS UP AT THE END OF THE MONTH. Built first because it is the
        // state that persists; the earlier segment is this plus whatever failed
        // partway through, which is the cheaper of the two to describe.
        var standing = new List<EntityRef>(registered.Count);
        for (int i = 0; i < registered.Count; i++)
            if (!integrity.HasFailed(registered[i].Id))
                standing.Add(FlowElementRegistry.ReferenceTo(registered[i]));

        RouteClosure closure = network.Close(standing);
        IReadOnlyList<EntityRef> after = closure.Routed;

        // WHY EACH ELEMENT WENT DOWN, recorded where the law decided it
        // (SDD-002 §5, design 09 §4.3, finding 202). A deferral used to be
        // written with `cause: null` because nothing named the element behind
        // it; these entries are what a "why?" walks. Recorded here rather than
        // in the publish loop, because that loop rebuilds the READ MODEL and a
        // side effect inside a pure projection would double-record the moment
        // anything asked twice.
        RecordOutage(closure, context.Tick);

        int day = failures.Count == 0
            ? (int)Duration.DaysPerTick
            : EarliestDay(failures);

        // ONE SEGMENT, either because nothing broke or because what broke did so
        // on day zero and the month was never anything else. A zero-day segment
        // would be a legal plan the solver spent a whole iteration on to weight
        // by nothing.
        if (day == 0 || failures.Count == 0)
        {
            context.Segments = new SegmentPlan(
            [
                new Segment(StartDay: 0, DurationDays: (int)Duration.DaysPerTick, after),
            ]);

            return;
        }

        // THE EARLIEST FAILURE OWNS THE BOUNDARY. Two failures on different days
        // would want three segments; taking the earliest is a deliberate
        // simplification and a conservative one — the field is down from the
        // first break either way, and the second cannot un-break it.
        var before = new List<EntityRef>(registered.Count);
        for (int i = 0; i < registered.Count; i++)
        {
            EntityId<IFlowElement> element = registered[i].Id;

            // UP BEFORE THE BREAK means: not already down when the month began.
            // A component that failed in an earlier tick is absent from both
            // segments; one that failed today was working until it did.
            if (!integrity.HasFailed(element) || FailedToday(failures, element))
                before.Add(FlowElementRegistry.ReferenceTo(registered[i]));
        }

        context.Segments = new SegmentPlan(
        [
            new Segment(StartDay: 0, DurationDays: day, network.Routed(before)),
            new Segment(StartDay: day, DurationDays: (int)Duration.DaysPerTick - day, after),
        ]);
    }

    /// <summary>
    /// One entry per element the route law shut in, each CITING the entry for
    /// the element that shut it (design 09 §4.3, finding 202).
    ///
    /// <para>The chain is what makes this more than a list. An outage four
    /// elements deep records four entries, and the last one's cause walks back
    /// to the first — so "why is this well shut in" is answered by following
    /// ids rather than by re-deriving the topology at read time, which is what
    /// `CauseChainLeaf` exists to do.</para>
    ///
    /// <para>The first entry in a chain cites nothing: its `Because` element is
    /// absent from the available set, which means something else — a hazard
    /// draw, an operation — took it out and audited that with its own reason.
    /// The two records meet there rather than overlapping.</para>
    /// </summary>
    private void RecordOutage(RouteClosure closure, Tick tick)
    {
        if (closure.Excluded.Count == 0) return;

        // Written in removal order, so an element's cause has always been
        // recorded before the element that names it — the entries can only chain
        // backwards, which is what makes the walk terminate.
        var idOf = new Dictionary<ulong, AuditId>();

        for (int i = 0; i < closure.Excluded.Count; i++)
        {
            RouteExclusion exclusion = closure.Excluded[i];

            AuditId? cause = idOf.TryGetValue(exclusion.Because.Value, out AuditId behind)
                ? behind
                : null;

            AuditId recorded = audit.Record(
                // PER-TICK PER-ELEMENT DETAIL, and the category is what says so
                // (design 09 §4.4, SDD-001 §5). `StateTransition` is DURABLE —
                // never pruned — and one of those per shut-in element per tick
                // is thousands of permanently retained entries in a forty-year
                // run, which grows until the process dies. It did: four host
                // crashes on a clean build before the category was the suspect.
                //
                // A route shut-in is the same KIND of fact as the deferral it
                // causes, and the retention partition already places that here.
                // The cause closure still protects any of these that a durable
                // entry depends on, which is the guarantee §4.4 actually makes.
                AuditCategory.ConstraintBinding,
                exclusion.Element,
                cause,
                new Dictionary<string, AuditValue>(StringComparer.Ordinal)
                {
                    ["element"] = new(loop.NameOf(exclusion.Element)),
                    ["shut-in-by"] = new(loop.NameOf(exclusion.Because)),
                    ["tick"] = new(tick.Value.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)),
                });

            idOf[exclusion.Element.Value] = recorded;
        }
    }

    private static int EarliestDay(IReadOnlyList<OGSim.Integrity.FailureOutcome> failures)
    {
        int day = (int)Duration.DaysPerTick;

        for (int i = 0; i < failures.Count; i++)
            if (failures[i].FailureDay < day) day = failures[i].FailureDay;

        return day;
    }

    private static bool FailedToday(
        IReadOnlyList<OGSim.Integrity.FailureOutcome> failures, EntityId<IFlowElement> element)
    {
        for (int i = 0; i < failures.Count; i++)
            if (failures[i].Component == element) return true;

        return false;
    }
}

internal sealed class SolveFlowStage(ProductionLoop loop) : ITickStage
{
    public StageId Id => StageId.SolveFlow;

    public void Execute(TickContext context) => loop.SolveFlow(context);
}

/// <summary>Stage 7. Custody, in its own slot (design 03 §6).</summary>
internal sealed class CustodyStage(ProductionLoop loop) : ITickStage
{
    public StageId Id => StageId.Custody;

    public void Execute(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Store, lift, then record. Custody is metered on the way INTO storage,
        // so what is recorded is what the meter passed — and the lifting is what
        // makes room for next month rather than a second transfer.
        loop.StoreAndExport(context.Tick, Duration.FromTicks(1.0));
        loop.RecordCustody();
    }
}

internal sealed class EconomicsStage(
    ProductionLoop loop, Bank bank, ReservesBook reserves, ReserveHistory history,
    EsgAssessment esg)
    : ITickStage
{

    public StageId Id => StageId.Economics;

    public void Execute(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // THE MONTH ENTERS THE ESG RECORD FIRST (SDD-012 §4b's R23.1 amendment),
        // because the rate charged below is priced against a record that includes
        // the month being charged for. Stage 9 ages the window afterwards.
        esg.Observe(loop.FlaredThisTick, loop.ProducedThisTick);

        // The market moves before anything is sold, so every barrel in a month
        // crosses at one price (SDD-009 §6).
        loop.AdvancePrices();
        loop.PostEconomics(context.Tick);

        // The facility is re-priced and its interest charged after the month's
        // revenue is in, so a company is judged on the cash it actually has
        // (SDD-009 §5).
        // The record a lender prices against (SDD-012 §4). Not a constant
        // any more: a company that flares its gas pays for it in the rate it
        // borrows at, for years, which is design 08 §5's slowest loop.
        bank.Settle(
            context.Tick,
            esg.Of());

        // AND WHERE THE COMPANY STOOD THIS MONTH, so a year from now there is
        // something to measure replacement against (SDD-009 §4). Recorded AFTER
        // the settle, on the same reserves the bank was just re-priced against:
        // taking them before would compare a month's additions to a base struck
        // at a different moment, which is the kind of half-tick skew that reads
        // as a real movement in the indicator.
        history.Record(
            reserves.Remaining(loop.CumulativeProduced).Proved, loop.CumulativeProduced);
    }
}
