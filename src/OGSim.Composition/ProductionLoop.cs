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
    Money LiftingCostPerTonne);

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
    IReadOnlyList<(ConstraintKind Kind, Mass Deferred)> Deferred)
{
    // Finding 131.
    public bool Equals(ChainElementView? other) =>
        other is not null && Element == other.Element && DisplayId == other.DisplayId
        && Throughput == other.Throughput
        && Structural.Equal(Deferred, other.Deferred);

    public override int GetHashCode() =>
        HashCode.Combine(Element, DisplayId, Throughput, Structural.HashOf(Deferred));

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

    public ChainElementView Published(Func<EntityId<IFlowElement>, string> nameOf) =>
        new(new EntityRef(EntityKind.FlowElement, Element.Value),
            nameOf(Element),
            new Mass(Throughput),
            [.. _deferred]);
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
internal sealed class ProductionLoop
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
    private readonly IAquiferModel _aquifer;
    private readonly IFlowElementRegistry _network;
    private readonly Temperature _ambient;
    private readonly Density _surfaceDensity;
    private readonly int _materialCount;

    // Which elements meter. A set, because stage 5 asks it once per element per
    // segment — and because the loop must not ask an element what it IS, only
    // whether composition told it this one is a meter.
    private readonly HashSet<EntityId<IFlowElement>> _meters = [];

    // Stage 5's answer, held for stage 6. Cleared at the start of every solve so
    // a tick that produced nothing cannot commit last month's volumes.
    private readonly Dictionary<EntityId<IReservoirCompartmentEntity>, double> _byCompartment = [];

    // The chain, rebuilt each tick in the solver's topological order.
    private readonly List<ChainElement> _chain = [];

    private readonly Func<EntityId<IFlowElement>, string> _names;

    private readonly OGSim.Facilities.Tank _tank;
    private readonly MassRate _offtake;

    private OGSim.Kernel.Composition _stored;
    private Allocation _tankProvenance;

    // What the field handled this tick, per material — what a lifting cost is
    // charged on, and the reason a watered-out field stops paying.
    private readonly double[] _handled;

    private readonly IFiscalRegime _regime;
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
        IAquiferModel aquifer,
        IFlowElementRegistry network,
        IReadOnlyList<EntityId<IFlowElement>> meteredPoints,
        Func<EntityId<IFlowElement>, string> names,
        OGSim.Facilities.Tank tank,
        MassRate offtake,
        IFiscalRegime regime,
        IReadOnlyList<int> liquidOrdinals,
        Func<bool> isAbandoned,
        FieldEconomics economics,
        Temperature reservoirTemperature,
        Temperature ambient,
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
        ArgumentNullException.ThrowIfNull(aquifer);
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(meteredPoints);
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(tank);
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
        _aquifer = aquifer;
        _network = network;
        _names = names;
        _tank = tank;
        _offtake = offtake;
        _regime = regime;
        _liquidOrdinals = liquidOrdinals;
        _isAbandoned = isAbandoned;
        _economics = economics;
        _handled = new double[materialCount];
        _reservoirTemperature = reservoirTemperature;
        _ambient = ambient;
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

    /// <summary>What crossed the custody meter this tick — the ONLY mass stage 8
    /// is allowed to price (SDD-009 §1).</summary>
    public OGSim.Kernel.Composition Delivered { get; private set; }

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

        _byCompartment.Clear();
        _chain.Clear();
        _stored = OGSim.Kernel.Composition.Zero(_materialCount);
        Array.Clear(_handled);
        double[] delivered = new double[_materialCount];

        for (int i = 0; i < plan.Segments.Count; i++)
        {
            Segment segment = plan.Segments[i];

            SolveReport report = _solver.Solve(
                new SegmentContext(segment.DurationDays, _ambient, WeatherSeverity: 0.0),
                _network.ViewFor(segment.Available));

            // DURATION-WEIGHTED (SDD-002 §9). Rates are per second and a segment
            // is a whole number of days on the /30ths grid, so the weight is
            // exact rather than nearly.
            Accumulate(report, segment.DurationDays * SecondsPerDay, delivered);
        }

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
            Flowing(report.Deferrals[i].Element)
                .Refuse(report.Deferrals[i].Kind, report.Deferrals[i].Deferred);

        // WHAT THE FIELD HANDLED, from the completions' own sourced mass: every
        // barrel that came up the hole, whether it was sold, flared or disposed
        // of. That is what a lifting cost is charged on.
        for (int i = 0; i < report.Solutions.Count; i++)
        {
            OGSim.Kernel.Composition sourced = report.Solutions[i].Converged.Sourced;

            for (int m = 0; m < _materialCount; m++)
                _handled[m] += sourced[new MaterialId(m)].KgPerSecond * seconds;
        }

        for (int i = 0; i < report.CompletionStates.Count; i++)
        {
            CompletionState state = report.CompletionStates[i];

            EntityId<IReservoirCompartmentEntity> compartment =
                _wells.CompartmentOf(new EntityId<ICompletion>(state.Completion.Value));

            _byCompartment[compartment] =
                _byCompartment.GetValueOrDefault(compartment)
                + state.Rate.CubicMetresPerSecond * seconds;
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

            // What the tank receives is exactly what the meter passed, with the
            // provenance it carries — so a lifting out of storage allocates back
            // to the compartments that filled it (SDD-002 §3, design 04 §2.2).
            _stored = onSpec;
            _tankProvenance = passed.Provenance;
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

    /// <summary>The tick's chain, as SDD-017 §2's <c>ChainElementView</c>.</summary>
    public IReadOnlyList<ChainElementView> Chain()
    {
        var rows = new List<ChainElementView>(_chain.Count);

        for (int i = 0; i < _chain.Count; i++) rows.Add(_chain[i].Published(_names));

        return rows;
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

        for (int i = 0; i < completions.Count; i++)
        {
            EntityId<IReservoirCompartmentEntity> compartment =
                _wells.CompartmentOf(completions[i].CompletionId);

            if (!seen.Add(compartment)) continue;
            if (!_byCompartment.TryGetValue(compartment, out double reservoirVolume)) continue;

            // Each compartment's OWN Bo, at its own pressure. The field-average
            // this replaced was a stated simplification with a task against it
            // (R20c.11); solving per element made the honest form free, because
            // the compartment is already in hand.
            Pressure pressure = _subsurface.TruePressureOf(compartment);
            double waterCut = _subsurface.TrueWaterCutOf(compartment, WaterViscosity, _fluid.MuOil(pressure));

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
            ReservoirVolume influx = _aquifer.InfluxDuring(pressure, Duration.FromTicks(1.0));

            withdrawals.Add(new CompartmentWithdrawal(
                compartment,
                oil,
                new StandardGasVolume(oil.CubicMetres * _fluid.Rs(pressure)),
                _fluid.Bw(pressure).Shrink(waterReservoir),
                Influx: influx,
                Injected: new ReservoirVolume(0.0),
                ReservoirVolume: new ReservoirVolume(reservoirVolume)));
        }

        _production.Set(withdrawals);
    }

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
    /// Stage 6, after the solve: what reached the tank is held, and what the
    /// export line contracts to take is lifted.
    ///
    /// <para>The two together are the buffer. A field producing below its export
    /// rate never fills the tank and never notices it; one producing above fills
    /// it, and when it is full the ullage constraint reaches back down the chain
    /// and shuts wells in (R8-V5) — which is the moment a player has to decide
    /// between more storage, more export and less production.</para>
    /// </summary>
    public void StoreAndExport(Duration tick)
    {
        _tank.Receive(_stored, _tankProvenance, tick);

        // Boil-off first, because oil that evaporated was never available to
        // lift. It is a conservation term, not a rounding: the tank reports it
        // and stage 9 will account it as fugitive emissions.
        _tank.VapourLossOver(tick);

        MaterialInventory lifted = _tank.Draw(new Mass(_offtake.KgPerSecond * tick.Seconds));

        Exported = lifted.Total;
    }

    /// <summary>What left for market this tick. What the tank could not hold
    /// stays in it, and what it could not take never left the field.</summary>
    public Mass Exported { get; private set; }

    public void RecordCustody()
    {
        _sale = null;
        if (Delivered.Total.KgPerSecond <= 0.0) return;

        _sale = _audit.Record(
            AuditCategory.CustodyTransfer,
            subject: null,
            cause: null,
            new Dictionary<string, AuditValue>(StringComparer.Ordinal)
            {
                ["mass-kg"] = new(Format(Delivered.Total.KgPerSecond)),
                ["volume-m3"] = new(Format(ProducedThisTick.CubicMetres)),
            });
    }

    private AuditId? _sale;

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

    public void PostEconomics(Tick tick)
    {
        // PRICED OFF THE METER (SDD-009 §1) — not off what the wells produced.
        // Everything the chain does between the two lives in that difference,
        // and oil that failed the spec gate is oil the company has and cannot
        // sell.
        // The month's OPEX first, because the fiscal assessment deducts it.
        Money opex = OperatingCost();

        if (_sale is AuditId sale)
        {
            Money gross = Scale(
                _economics.OilPricePerTonne, Delivered.Total.KgPerSecond / KilogramsPerTonne);

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
        AuditId operating = _audit.Record(
            AuditCategory.Financial, subject: null, cause: null,
            new Dictionary<string, AuditValue>(StringComparer.Ordinal));

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
    private Money OperatingCost()
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
        if (_isAbandoned()) return Money.Zero;

        var liquid = 0.0;
        for (int i = 0; i < _liquidOrdinals.Count; i++)
            liquid += _handled[_liquidOrdinals[i]];

        return _economics.FixedOperatingCostPerTick
             + Scale(_economics.LiftingCostPerTonne, liquid / KilogramsPerTonne);
    }

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
public sealed class FieldControl
{
    private readonly SubsurfaceState _subsurface;
    private readonly WellsState _wells;
    private readonly IFlowElementRegistry _network;
    private readonly SurfaceChain _chain;
    private readonly IObligationRegistry _obligations;
    private readonly ContentId _abandonmentTemplate;

    private int _slotsTaken;

    internal FieldControl(
        SubsurfaceState subsurface,
        WellsState wells,
        IFlowElementRegistry network,
        SurfaceChain chain,
        IObligationRegistry obligations,
        ContentId abandonmentTemplate)
    {
        _subsurface = subsurface;
        _wells = wells;
        _network = network;
        _chain = chain;
        _obligations = obligations;
        _abandonmentTemplate = abandonmentTemplate;
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
        ContentId drive) =>
        _subsurface.Create(
            generated, permeability, netThickness, drainageArea,
            rockCompressibility, gasOilContact, oilWaterContact, wettability, drive);

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
    public EntityId<ICompletion> OpenWell(
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

        EntityId<ICompletion> opened = _wells.Open(completion, drains);

        // UNCONDITIONAL, at creation (SDD-007 §6, design 02 §3.4): a well that
        // is drilled will one day be plugged whatever else happens to it, and a
        // company able to create one without the liability could walk away from
        // the cost by never recording it.
        _obligations.Register(
            new EntityRef(EntityKind.Completion, opened.Value), _abandonmentTemplate);

        _network.Connect(new FlowConnection(
            completion.Id, WellheadOutlet,
            _chain.Manifold.Id, _chain.Manifold.SlotAt(_slotsTaken)));

        _slotsTaken++;
        return opened;
    }

    /// <summary>A completion's one outlet: the wellhead.</summary>
    private static PortId WellheadOutlet { get; } = new(0);

    /// <summary>One open well by id, or null — the door a player's lever is
    /// pulled through.</summary>
    public Completion? WellNamed(EntityId<ICompletion> well) => _wells.Find(well);

    /// <summary>How many wells are still producing — abandoned ones are plugged
    /// and no longer part of the field a player is running.</summary>
    public int LiveWellCount => _wells.Count - _abandoned.Count;

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
    /// Every well and its state (SDD-017 §2's R21.5 amendment) — the list a
    /// well-level command is aimed with.
    ///
    /// <para>Walked in the order the wells were opened, so a host's list does
    /// not reshuffle between months (D-5). Production is deliberately absent
    /// here and reported as zero: what a WELL produced needs a per-completion
    /// split of the solve, and the loop totals the field — a number invented per
    /// well would be a plausible fiction, so the honest answer is the field's
    /// own total on the read model beside it.</para>
    /// </summary>
    public IReadOnlyList<WellStatusView> Wells()
    {
        IReadOnlyList<Completion> completions = _wells.Completions;
        var rows = new List<WellStatusView>(completions.Count);

        for (int i = 0; i < completions.Count; i++)
        {
            Completion well = completions[i];

            WellStatus status =
                _abandoned.Contains(well.CompletionId) ? WellStatus.Abandoned
                : well.IsShutIn ? WellStatus.ShutIn
                : WellStatus.Producing;

            rows.Add(new WellStatusView(
                new EntityRef(EntityKind.Completion, well.CompletionId.Value),
                "well-" + well.CompletionId.Value.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                status,
                new SurfaceVolume(0.0)));
        }

        return rows;
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
    /// knows an abandonment happened.</summary>
    private readonly HashSet<EntityId<ICompletion>> _abandoned = [];

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
/// <para>ONE segment covering the whole month, because nothing yet takes an
/// element away mid-tick: integrity owns no state and runs no stage (R20d.11)
/// and weather is unbuilt (R22). The plan is REAL rather than skipped — stage 5
/// iterates segments, and a stage 5 that solved "the tick" instead would have
/// nowhere to put the second segment on the day one arrives.</para>
///
/// <para>Availability is every REGISTERED element, because a failed element is
/// ABSENT from the network rather than present at zero rate (design 04 §4). The
/// day hazards land they subtract from this list and nothing else changes.</para>
/// </summary>
internal sealed class SegmentationStage(IFlowElementRegistry network) : ITickStage
{
    public StageId Id => StageId.Availability;

    public void Execute(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        IReadOnlyList<IFlowElement> registered = network.Registered;
        var available = new List<EntityRef>(registered.Count);

        for (int i = 0; i < registered.Count; i++)
            available.Add(FlowElementRegistry.ReferenceTo(registered[i]));

        // Set exactly once, by this stage: a later stage reading it before now
        // is a stage-isolation violation (I-V5), and one writing it would be a
        // second owner of the tick's shape.
        context.Segments = new SegmentPlan(
        [
            new Segment(StartDay: 0, DurationDays: (int)Duration.DaysPerTick, available),
        ]);
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
        loop.StoreAndExport(Duration.FromTicks(1.0));
        loop.RecordCustody();
    }
}

internal sealed class EconomicsStage(ProductionLoop loop) : ITickStage
{
    public StageId Id => StageId.Economics;

    public void Execute(TickContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        loop.PostEconomics(context.Tick);
    }
}
