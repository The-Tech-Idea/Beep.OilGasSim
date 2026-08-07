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
    Money OilPricePerCubicMetre,
    Money FixedOperatingCostPerTick);

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
    private readonly Pressure _wellheadBackpressure;

    // Stage 5's answer, held for stage 6. Cleared at the start of every solve so
    // a tick that produced nothing cannot commit last month's volumes.
    private readonly List<CompletionProduction> _thisTick = [];

    public ProductionLoop(
        SubsurfaceState subsurface,
        WellsState wells,
        CompanyState company,
        TickProduction production,
        IFluidPropertyModel fluid,
        IAuditTrail audit,
        FieldEconomics economics,
        Temperature reservoirTemperature,
        Pressure wellheadBackpressure)
    {
        ArgumentNullException.ThrowIfNull(subsurface);
        ArgumentNullException.ThrowIfNull(wells);
        ArgumentNullException.ThrowIfNull(company);
        ArgumentNullException.ThrowIfNull(production);
        ArgumentNullException.ThrowIfNull(fluid);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(economics);

        _subsurface = subsurface;
        _wells = wells;
        _company = company;
        _production = production;
        _fluid = fluid;
        _audit = audit;
        _economics = economics;
        _reservoirTemperature = reservoirTemperature;
        _wellheadBackpressure = wellheadBackpressure;
    }

    /// <summary>Stock-tank oil produced in the tick just solved — what the read
    /// model reports and what a test asserts on.</summary>
    public SurfaceVolume ProducedThisTick { get; private set; } = new(0.0);

    /// <summary>
    /// Stage 5. Refresh every well with the pressure its compartment is at NOW,
    /// then solve each operating point.
    ///
    /// <para>The refresh is why decline happens: without it a completion holds
    /// the pressure it was built with and produces at month one's rate
    /// forever.</para>
    /// </summary>
    public void SolveFlow()
    {
        _wells.RefreshFromReservoir(_subsurface.TruePressureOf, _reservoirTemperature);

        _thisTick.Clear();
        _thisTick.AddRange(_wells.ProduceOver(
            Duration.FromTicks(1.0),
            _wellheadBackpressure,
            _fluid.Bo(AverageReservoirPressure())));

        var total = 0.0;
        for (int i = 0; i < _thisTick.Count; i++) total += _thisTick[i].Oil.CubicMetres;
        ProducedThisTick = new SurfaceVolume(total);

        PublishWithdrawals();
    }

    /// <summary>
    /// Hands stage 5's answer to stage 6 in the shape the compartments consume:
    /// what the wells took, charged to what they took it from — which is what
    /// makes next month's solve give a smaller answer.
    /// </summary>
    private void PublishWithdrawals()
    {
        var withdrawals = new List<CompartmentWithdrawal>(_thisTick.Count);

        for (int i = 0; i < _thisTick.Count; i++)
        {
            CompletionProduction production = _thisTick[i];

            withdrawals.Add(new CompartmentWithdrawal(
                production.Compartment,
                production.Oil,
                new StandardGasVolume(0.0),
                new SurfaceVolume(0.0),
                Influx: new ReservoirVolume(0.0),
                Injected: new ReservoirVolume(0.0),
                ReservoirVolume: production.ReservoirVolume));
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
    public void PostEconomics(Tick tick)
    {
        if (ProducedThisTick.CubicMetres > 0.0)
        {
            AuditId sale = _audit.Record(
                AuditCategory.CustodyTransfer,
                subject: null,
                cause: null,
                new Dictionary<string, AuditValue>(StringComparer.Ordinal)
                {
                    ["volume-m3"] = new(ProducedThisTick.CubicMetres.ToString(
                        "R", System.Globalization.CultureInfo.InvariantCulture)),
                });

            Money revenue = Scale(_economics.OilPricePerCubicMetre, ProducedThisTick.CubicMetres);

            _company.Ledger.Post(new Movement(
                tick, Account.Cash, Account.Revenue, revenue,
                MovementCategory.Production, Asset: null, Cause: sale));
        }

        // The field costs money to run whether or not it produced — which is the
        // whole shape of the late-life decision, and it must not be conditional
        // on production or a shut-in field would be free to keep.
        AuditId operating = _audit.Record(
            AuditCategory.Financial, subject: null, cause: null,
            new Dictionary<string, AuditValue>(StringComparer.Ordinal));

        _company.Ledger.Post(new Movement(
            tick, Account.Opex, Account.Cash, _economics.FixedOperatingCostPerTick,
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
    /// One Bo for the field this tick. A per-compartment factor is the honest
    /// form and arrives with per-compartment allocation (R20c.11); using the
    /// mean pressure here is a stated simplification rather than a hidden one,
    /// and with a single compartment it is exact.
    /// </summary>
    private Pressure AverageReservoirPressure()
    {
        IReadOnlyList<Completion> completions = _wells.Completions;
        if (completions.Count == 0) return _fluid.Pb;

        var sum = 0.0;
        for (int i = 0; i < completions.Count; i++)
            sum += _subsurface.TruePressureOf(
                _wells.CompartmentOf(completions[i].CompletionId)).Pascals;

        return new Pressure(sum / completions.Count);
    }
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

    internal FieldControl(SubsurfaceState subsurface, WellsState wells)
    {
        _subsurface = subsurface;
        _wells = wells;
    }

    public EntityId<IReservoirCompartmentEntity> AddCompartment(
        GeneratedCompartment generated,
        Permeability permeability,
        Length netThickness,
        Area drainageArea,
        double rockCompressibility,
        Length gasOilContact,
        Length oilWaterContact) =>
        _subsurface.Create(
            generated, permeability, netThickness, drainageArea,
            rockCompressibility, gasOilContact, oilWaterContact);

    /// <summary>Brings a completion online against a compartment. From the next
    /// tick it is a source element in the network the solver sees.</summary>
    public EntityId<ICompletion> OpenWell(
        Completion completion, EntityId<IReservoirCompartmentEntity> drains) =>
        _wells.Open(completion, drains);

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

internal sealed class SolveFlowStage(ProductionLoop loop) : ITickStage
{
    public StageId Id => StageId.SolveFlow;

    public void Execute(TickContext context) => loop.SolveFlow();
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
