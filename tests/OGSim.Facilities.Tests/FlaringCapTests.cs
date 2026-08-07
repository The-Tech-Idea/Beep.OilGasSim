// R9-V8 — THE PHASE'S HEADLINE TEST.
//
// With flaring capped and no other gas outlet, OIL production is throttled,
// because the oil carries the gas. If this does not hold, the environmental
// system is a fine rather than a constraint and the design intent is lost
// (R9 §2.2, G3).
//
// Nothing in the engine implements this. The flare is an element with a
// capacity; SDD-002 S3 throttles the completions feeding a violated constraint
// pro-rata; the reduced rate is the well's oil as much as its gas. The whole
// behaviour is the composition of two rules written phases apart, neither of
// which mentions the other.

using OGSim.Contracts;
using OGSim.Facilities;
using OGSim.Flow;
using OGSim.Kernel;
using OGSim.Wells;

namespace OGSim.Facilities.Tests;

public class FlaringCapTests
{
    private static readonly SegmentContext WholeTick =
        new(DurationDays: 30, Temperature.FromCelsius(15.0), WeatherSeverity: 0.0);

    /// <summary>Two materials: 0 oil, 1 gas. A completion producing both.</summary>
    private const int Materials = 2;

    private static FlowConnection Edge(ulong from, int fromPort, ulong to, int toPort) =>
        new(new EntityId<IFlowElement>(from), new PortId(fromPort),
            new EntityId<IFlowElement>(to), new PortId(toPort));

    /// <summary>
    /// A completion whose stream is oil AND gas in a fixed ratio — the physical
    /// fact the whole test turns on. Associated gas is not optional: it comes up
    /// with the oil, and refusing to handle it refuses the oil.
    /// </summary>
    private sealed class AssociatedGasWell : ICompletion
    {
        private readonly double _productivity;
        private readonly double _reservoirPa;
        private readonly double _gasOilRatio;

        public AssociatedGasWell(ulong id, double productivity, double reservoirBar, double gor)
        {
            Id = new EntityId<IFlowElement>(id);
            CompletionId = new EntityId<ICompletion>(id);
            Wellbore = new EntityId<IWellbore>(id);
            _productivity = productivity;
            _reservoirPa = reservoirBar * 1e5;
            _gasOilRatio = gor;
        }

        public EntityId<IFlowElement> Id { get; }
        public EntityId<ICompletion> CompletionId { get; }
        public EntityId<IWellbore> Wellbore { get; }
        public ILiftMethod? Lift => null;
        public bool IsPressureDecoupled => false;

        public IReadOnlyList<Perforation> Perforations { get; } =
            [new Perforation(new EntityId<IReservoirCompartmentEntity>(1),
                             new Length(0.0), new Length(1.0), 0.0, false)];

        public IReadOnlyList<PortSpec> Ports { get; } =
            [new PortSpec(new PortId(0), PortDirection.Outlet, PortRole.Main)];

        public OperatingPoint SolveOperatingPoint(Pressure wellheadBackpressure)
        {
            double drawdown = _reservoirPa - wellheadBackpressure.Pascals;
            if (drawdown <= 0.0) return new Dead();

            return new Flowing(new ReservoirRate(_productivity * drawdown),
                               new Pressure(_reservoirPa));
        }

        public TransformResult Transform(TransformInput input)
        {
            double rate = input.SolvedRate?.CubicMetresPerSecond ?? 0.0;

            // Oil and its associated gas, in a fixed ratio.
            double oil = rate * 800.0;
            Composition produced = Composition.Validated([oil, oil * _gasOilRatio]);

            return new TransformResult(
                [new MaterialStream(produced, new Pressure(_reservoirPa),
                                    Temperature.FromCelsius(70.0),
                                    Allocation.FromSingle(new EntityRef(EntityKind.Compartment, 1)))],
                produced, Composition.Zero(Materials),
                new DisposedMass(Composition.Zero(Materials), Composition.Zero(Materials),
                                 Composition.Zero(Materials)),
                new Power(0.0));
        }

        public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input) => [];
    }

    /// <summary>Splits the well stream into an oil leg and a gas leg.</summary>
    private sealed class TwoPhaseSplit(ulong id) : IFlowElement
    {
        public EntityId<IFlowElement> Id { get; } = new(id);

        public IReadOnlyList<PortSpec> Ports { get; } =
        [
            new PortSpec(new PortId(0), PortDirection.Inlet, PortRole.Main),
            new PortSpec(new PortId(1), PortDirection.Outlet, PortRole.Liquid),
            new PortSpec(new PortId(2), PortDirection.Outlet, PortRole.Gas),
        ];

        public TransformResult Transform(TransformInput input)
        {
            Composition inlet = input.Inlets.Count > 0
                ? input.Inlets[0].MassRates
                : Composition.Zero(Materials);

            var oil = Composition.Validated([inlet[new MaterialId(0)].KgPerSecond, 0.0]);
            var gas = Composition.Validated([0.0, inlet[new MaterialId(1)].KgPerSecond]);

            MaterialStream reference = input.Inlets.Count > 0
                ? input.Inlets[0]
                : new MaterialStream(Composition.Zero(Materials), Pressure.FromBar(20.0),
                                     input.Segment.Ambient,
                                     Allocation.FromSingle(new EntityRef(EntityKind.Compartment, 1)));

            return new TransformResult(
                [reference with { MassRates = oil }, reference with { MassRates = gas }],
                Composition.Zero(Materials), Composition.Zero(Materials),
                new DisposedMass(Composition.Zero(Materials), Composition.Zero(Materials),
                                 Composition.Zero(Materials)),
                new Power(0.0));
        }

        public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input) => [];
    }

    /// <summary>Where the oil goes. Unconstrained — so the ONLY thing that can
    /// limit this network is the flare.</summary>
    private sealed class OilSink(ulong id) : IFlowElement
    {
        public EntityId<IFlowElement> Id { get; } = new(id);

        public IReadOnlyList<PortSpec> Ports { get; } =
            [new PortSpec(new PortId(0), PortDirection.Inlet, PortRole.Main)];

        public Composition Received { get; private set; } = Composition.Zero(Materials);

        public TransformResult Transform(TransformInput input)
        {
            Received = input.Inlets.Count > 0
                ? input.Inlets[0].MassRates
                : Composition.Zero(Materials);

            return new TransformResult(
                [], Composition.Zero(Materials), Composition.Zero(Materials),
                new DisposedMass(Composition.Zero(Materials), Composition.Zero(Materials),
                                 Received),
                new Power(0.0));
        }

        public IReadOnlyList<ConstraintEvaluation> EvaluateConstraints(TransformInput input) => [];
    }

    /// <summary>
    /// Read from the REPORT, not from a fixture that recorded itself during
    /// Transform.
    ///
    /// <para>§8's attribution pass re-runs every transform at the completions'
    /// UNCAPPED targets, so an element that saved state inside Transform ends up
    /// holding the uncapped answer — and a first version of this test read a
    /// sink that did exactly that, and reported no throttling at any cap.
    /// Transform is required to be pure (SDD-002 §5); the converged values live
    /// in the report, which is where a caller is meant to look.</para>
    /// </summary>
    private static double OilRateWithFlareCap(double flareCapKgPerSecond)
    {
        var well = new AssociatedGasWell(1, productivity: 5e-9, reservoirBar: 200.0, gor: 0.5);

        var topology = new FlowTopology(
            [well, new TwoPhaseSplit(2), new OilSink(4),
             new Flare(new EntityId<IFlowElement>(3), new MassRate(flareCapKgPerSecond),
                       combustionEfficiency: 0.98, Materials)],
            [
                Edge(1, 0, 2, 0),      // well -> split
                Edge(2, 1, 4, 0),      // oil leg -> sink (unconstrained)
                Edge(2, 2, 3, 0),      // gas leg -> flare (capped)
            ]);

        var clock = new SimulationClock(new GameDate(1975, 6));
        var solver = new FlowSolver(
            SolverSettings.Pinned, new AuditTrail(clock, new AuditRetention(500)));

        SolveReport report = solver.Solve(WholeTick, topology);

        double oil = 0.0;
        foreach (ElementSolution solution in report.Solutions)
            oil += solution.Converged.Sourced[new MaterialId(0)].KgPerSecond;

        return oil;
    }

    // -------------------------------------------------------------- R9-V8

    [Fact] // R9-V8: a flaring cap throttles OIL
    public void R9V8_a_binding_flaring_cap_limits_oil_production()
    {
        // Generous cap: the gas is all flared and the oil flows freely.
        double unlimited = OilRateWithFlareCap(1_000_000.0);
        Assert.True(unlimited > 0.0, "the well should flow with no cap");

        // Tight cap: the flare cannot take the associated gas, S3 throttles the
        // completion, and the OIL comes down with it — because the oil carries
        // the gas and there is no way to produce one without the other.
        double capped = OilRateWithFlareCap(unlimited * 0.5 * 0.2);

        Assert.True(capped < unlimited,
            $"the flaring cap did not reach the oil: {capped} not below {unlimited}");
    }

    [Fact] // R9-V8: the tighter the cap, the less oil — monotonically
    public void R9V8_oil_falls_monotonically_as_the_flaring_cap_tightens()
    {
        double unlimited = OilRateWithFlareCap(1_000_000.0);
        double associatedGas = unlimited * 0.5;

        double previous = double.MaxValue;
        foreach (double fraction in new[] { 0.8, 0.5, 0.3, 0.15, 0.05 })
        {
            double oil = OilRateWithFlareCap(associatedGas * fraction);

            Assert.True(oil < previous,
                $"at cap fraction {fraction} the oil {oil} did not fall below {previous}");
            previous = oil;
        }

        // An environmental rule has become a physical production constraint. No
        // code anywhere says "flaring limits oil" — it is S3 throttling an
        // element that reported a capacity, and a well whose stream is oil and
        // gas together.
        Assert.True(previous < unlimited * 0.2);
    }

    [Fact] // R9-V8: the loss is ATTRIBUTED to gas handling, by element
    public void R9V8_the_deferred_volume_names_the_flare()
    {
        var well = new AssociatedGasWell(1, 5e-9, 200.0, gor: 0.5);

        var topology = new FlowTopology(
            [well, new TwoPhaseSplit(2), new OilSink(4),
             new Flare(new EntityId<IFlowElement>(3), new MassRate(0.5), 0.98, Materials)],
            [Edge(1, 0, 2, 0), Edge(2, 1, 4, 0), Edge(2, 2, 3, 0)]);

        var clock = new SimulationClock(new GameDate(1975, 6));
        var solver = new FlowSolver(
            SolverSettings.Pinned, new AuditTrail(clock, new AuditRetention(500)));

        SolveReport report = solver.Solve(WholeTick, topology);

        // The bottleneck report names the FLARE — so the player is told the
        // constraint is gas handling, not that their well got worse.
        (EntityId<IFlowElement> element, ConstraintKind kind, Mass deferred) =
            Assert.Single(report.Deferrals);

        Assert.Equal(3UL, element.Value);
        Assert.Equal(ConstraintKind.TotalCapacity, kind);
        Assert.True(deferred.Kilograms > 0.0);
    }

    [Fact] // R9-V11: the whole chain conserves, flare included
    public void R9V11_the_gas_chain_conserves_with_the_flare_in_it()
    {
        var well = new AssociatedGasWell(1, 5e-9, 200.0, gor: 0.5);

        var topology = new FlowTopology(
            [well, new TwoPhaseSplit(2), new OilSink(4),
             new Flare(new EntityId<IFlowElement>(3), new MassRate(1e6), 0.98, Materials)],
            [Edge(1, 0, 2, 0), Edge(2, 1, 4, 0), Edge(2, 2, 3, 0)]);

        var clock = new SimulationClock(new GameDate(1975, 6));
        var solver = new FlowSolver(
            SolverSettings.Pinned, new AuditTrail(clock, new AuditRetention(500)));

        SolveReport report = solver.Solve(WholeTick, topology);

        double sourced = 0.0, left = 0.0;
        foreach (ElementSolution solution in report.Solutions)
        {
            TransformResult r = solution.Converged;
            sourced += r.Sourced.Total.KgPerSecond;
            left += r.Disposed.Flared.Total.KgPerSecond
                  + r.Disposed.Vented.Total.KgPerSecond
                  + r.Disposed.Discharged.Total.KgPerSecond
                  + r.FuelConsumed.Total.KgPerSecond;
        }

        Assert.True(sourced > 0.0);
        Assert.Equal(sourced, left, precision: 9);
    }
}
