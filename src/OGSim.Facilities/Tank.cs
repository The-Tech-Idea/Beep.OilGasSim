// R8.6 — the tank (SDD-006 §5, R8 §2.3).
//
// THE SINGLE MOST IMPORTANT COUPLING IN THE EXPORT CHAIN, and the reason a
// buffer was one of R4's five synthetic elements: the shape was proven before
// the real thing was written.
//
// A full tank accepts nothing. That constraint throttles the network through
// SDD-002 S3, the throttling raises pressure back up the line, and the pressure
// reaches the reservoir and reduces withdrawal — within the same tick, with no
// rule anywhere saying tanks affect reservoirs (FV5 / R8-V5).
//
// It is also the ONE stateful surface element, and its state is committed at
// stage 6 only. Transform stays pure: it reports what WOULD be received, and
// nothing is held until the commit.

using OGSim.Contracts;
using OGSim.Kernel;

namespace OGSim.Facilities;

/// <summary>A storage tank's datasheet.</summary>
public sealed record TankTier(
    ContentId Id,
    Mass Capacity,                 // by mass, at storage conditions
    double VapourLossRatePerTick); // boil-off / breathing losses, fraction of held

/// <summary>
/// SDD-006 §5. Inventory, ullage, and the backpressure that emerges from them.
/// </summary>
public sealed class Tank : IFlowElement
{
    private readonly TankTier _tier;
    private readonly int _materialCount;

    private MaterialInventory _held;
    private Allocation _provenance;

    public Tank(
        EntityId<IFlowElement> id,
        TankTier tier,
        int materialCount,
        MaterialInventory initialInventory,
        Allocation initialProvenance)
    {
        ArgumentNullException.ThrowIfNull(tier);

        if (tier.Capacity.Kilograms <= 0.0)
            throw new ModelFault("SDD-006 §5", null,
                $"tank tier {tier.Id.Value} declares a non-positive capacity");

        if (initialInventory.Total.Kilograms > tier.Capacity.Kilograms)
            throw new ModelFault("SDD-006 §5", null,
                $"tank {id.Value} is initialised holding more than its capacity");

        Id = id;
        _tier = tier;
        _materialCount = materialCount;
        _held = initialInventory;
        _provenance = initialProvenance;
    }

    public EntityId<IFlowElement> Id { get; }

    /// <summary>Inventory, kg. Read by the read model and by R11's cargo lifting.</summary>
    public MaterialInventory Held => _held;

    /// <summary>Whose oil this is. Blended on receipt, so a lifting can be
    /// allocated back to compartments (SDD-002 §3, FV10).</summary>
    public Allocation Provenance => _provenance;

    /// <summary>Remaining capacity, kg. Zero is <c>tank.full</c>.</summary>
    public Mass Ullage => new(Math.Max(0.0, _tier.Capacity.Kilograms - _held.Total.Kilograms));

    /// <summary>The ports by name, so a caller wiring the chain never writes a
    /// bare index.</summary>
    public static PortId Inlet { get; } = new(0);

    public static PortId Outlet { get; } = new(1);

    public IReadOnlyList<PortSpec> Ports { get; } =
    [
        new PortSpec(new PortId(0), PortDirection.Inlet, PortRole.Main),
        new PortSpec(new PortId(1), PortDirection.Outlet, PortRole.Main),
    ];

    /// <summary>
    /// A tank in the network is a SINK for the segment: what arrives is held,
    /// not passed on. The outlet exists for lifting, which is a commanded draw
    /// (R11) rather than a continuous flow.
    ///
    /// <para>PURE. Nothing is held here — <see cref="Receive"/> at stage 6 is the
    /// only thing that changes state, and the solver iterates this transform
    /// many times per segment. A tank that accumulated inside Transform would
    /// fill up two hundred times over during one solve.</para>
    /// </summary>
    public TransformResult Transform(TransformInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        Composition arriving = input.Inlets.Count > 0
            ? input.Inlets[0].MassRates
            : Composition.Zero(_materialCount);

        // Held as inventory, which for the segment's conservation check is mass
        // leaving the network — it is accounted for, not vanished, and stage 6
        // is where it lands in the tank.
        return new TransformResult(
            [],
            Sourced: Composition.Zero(_materialCount),
            FuelConsumed: Composition.Zero(_materialCount),
            Disposed: new DisposedMass(
                Composition.Zero(_materialCount),
                Composition.Zero(_materialCount),
                Discharged: arriving),
            PowerDraw: new Power(0.0));
    }

    /// <summary>
    /// SDD-006 §5's ullage constraint — and the whole of R8-V5.
    ///
    /// <para>Capacity is <c>remaining / duration</c>: the mass rate the tank can
    /// still accept for the rest of this segment. When the tank is full that is
    /// zero, S3 throttles every completion feeding it pro-rata, and the
    /// backpressure propagates to the reservoir on S4's next pass.</para>
    ///
    /// <para>Expressed as a RATE rather than as a boolean because S3 throttles
    /// pro-rata against a capacity: "full" would force the solver to choose
    /// between all and nothing, and a tank with room for half the tick's
    /// production would take none of it.</para>
    /// </summary>
    public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        double seconds = input.Segment.DurationDays * SecondsPerDay;
        if (seconds <= 0.0) return [];

        double acceptable = Ullage.Kilograms / seconds;
        double arriving = input.Inlets.Count > 0
            ? input.Inlets[0].MassRates.Total.KgPerSecond
            : 0.0;

        return [new ConstraintEvaluation(ConstraintKind.Ullage, acceptable, arriving)];
    }

    /// <summary>
    /// Stage 6. The ONLY thing that changes inventory.
    ///
    /// <para>Provenance is blended by mass, so a lifting out of a tank fed by
    /// three fields allocates back to all three in the proportion actually
    /// stored — which is what makes partner accounting on a shared tank
    /// possible at all.</para>
    /// </summary>
    public void Receive(Composition massRate, Allocation provenance, Duration duration)
    {
        double seconds = duration.Seconds;
        if (seconds <= 0.0) return;

        MaterialInventory arriving = MaterialInventory.From(massRate, duration);
        double arrivingKg = arriving.Total.Kilograms;
        if (arrivingKg <= 0.0) return;

        if (arrivingKg > Ullage.Kilograms + OverfillTolerance)
            throw new InvariantFault("SDD-006 §5", null,
                $"tank {Id.Value} was committed {Format(arrivingKg)} kg with only " +
                $"{Format(Ullage.Kilograms)} kg of ullage; the ullage constraint should " +
                "have throttled this at S3 and the commit must never exceed what the " +
                "solve allowed");

        double heldKg = _held.Total.Kilograms;

        _provenance = heldKg > 0.0
            ? Allocation.Blend([(_provenance, new Mass(heldKg)), (provenance, new Mass(arrivingKg))])
            : provenance;

        _held = _held.Plus(arriving);
    }

    /// <summary>
    /// A lifting. Composition is proportional, so what leaves looks like what is
    /// held — a tank does not fractionate.
    /// </summary>
    public MaterialInventory Draw(Mass wanted)
    {
        double heldKg = _held.Total.Kilograms;
        if (heldKg <= 0.0 || wanted.Kilograms <= 0.0) return MaterialInventory.Empty(_materialCount);

        double fraction = Math.Min(1.0, wanted.Kilograms / heldKg);

        (MaterialInventory drawn, MaterialInventory left) = _held.Split(fraction);
        _held = left;
        return drawn;
    }

    /// <summary>
    /// SDD-006 §5's boil-off. NEVER silently vanished — it is a conservation
    /// term, and the caller routes it to vapour recovery or to the emissions
    /// ledger as fugitives.
    /// </summary>
    public MaterialInventory VapourLossOver(Duration duration)
    {
        double ticks = duration.Days / Duration.DaysPerTick;
        double fraction = Math.Min(1.0, _tier.VapourLossRatePerTick * ticks);
        if (fraction <= 0.0) return MaterialInventory.Empty(_materialCount);

        (MaterialInventory lost, MaterialInventory left) = _held.Split(fraction);
        _held = left;
        return lost;
    }

    /// <summary>Rounding room on the commit check only. The ullage CONSTRAINT is
    /// exact; this guards the last ulp of a mass that passed it.</summary>
    private const double OverfillTolerance = 1e-6;

    private const double SecondsPerDay = 86_400.0;

    private static string Format(double value) =>
        value.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
}
